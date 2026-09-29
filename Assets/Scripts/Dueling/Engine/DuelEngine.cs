using System;
using System.Collections.Generic;
using System.Linq;
using DuelGenesis.Cards;

namespace DuelGenesis.Dueling
{
    /// <summary>
    /// Pure rules engine for a two-player Yu-Gi-Oh! duel (no Unity scene code).
    ///
    /// Turn structure follows the official TCG rulebook:
    ///   Draw Phase -> Standby Phase -> Main Phase 1 -> Battle Phase -> Main Phase 2 -> End Phase
    /// The player going first skips their first draw and cannot conduct a Battle Phase on turn 1.
    /// Hand size limit is 6 (discard down in the End Phase). One Normal Summon/Set per turn,
    /// Level 5-6 need 1 Tribute, Level 7+ need 2. Battle damage follows the damage-calculation
    /// table (ATK vs ATK / ATK vs DEF); face-down Defense monsters are flipped when attacked.
    ///
    /// Whenever the engine needs a decision (tributes, targets, discards, "activate a trap?")
    /// it raises a <see cref="DuelChoice"/>. A registered <see cref="IDuelDecider"/> (the CPU)
    /// answers immediately; otherwise the choice waits in <see cref="PendingChoice"/> for the HUD.
    /// Response windows nest, so chains resolve last-in, first-out like the real game.
    /// </summary>
    public sealed class DuelEngine
    {
        public readonly DuelistState[] Duelists = new DuelistState[2];
        public readonly IDuelDecider[] Deciders = new IDuelDecider[2];
        public readonly List<DuelEvent> History = new();

        public event Action<DuelEvent> EventRaised;

        public int TurnPlayer { get; private set; }
        public int TurnNumber { get; private set; }
        public DuelPhase Phase { get; private set; }
        public BattleStep Step { get; private set; }
        public int FirstPlayer { get; private set; }
        public bool IsOver { get; private set; }
        public int Winner { get; private set; } = -1;
        public string EndReason { get; private set; } = string.Empty;
        public DuelChoice PendingChoice { get; private set; }
        public DuelMonsterState CurrentAttacker { get; private set; }
        public DuelMonsterState CurrentAttackTarget { get; private set; }

        public bool IsWaitingForChoice => PendingChoice != null;
        public bool IsBusy => PendingChoice != null || IsOver;

        private readonly Random _random;
        private int _nextUid = 1;

        public DuelEngine(int seed)
        {
            _random = new Random(seed);
        }

        public DuelistState Me(int player) => Duelists[player];

        /// <summary>"Your" for the human ("You"), otherwise "Name's".</summary>
        public string Possessive(int player)
        {
            string name = Duelists[player].Name;
            return name == "You" ? "Your" : name + "'s";
        }
        public DuelistState Opponent(int player) => Duelists[1 - player];
        public int RandomRange(int minInclusive, int maxExclusive) => _random.Next(minInclusive, maxExclusive);

        // =================================================================== setup

        public void Setup(int player, string name, IEnumerable<CardData> mainAndExtra)
        {
            DuelistState duelist = new DuelistState(player, name);
            Duelists[player] = duelist;
            foreach (CardData data in mainAndExtra)
            {
                if (data == null || DuelRules.IsToken(data)) continue;
                DuelCard card = new DuelCard(_nextUid++, data, player);
                if (card.IsExtraDeckCard)
                {
                    card.Zone = DuelZone.ExtraDeck;
                    duelist.ExtraDeck.Add(card);
                }
                else
                {
                    card.Zone = DuelZone.Deck;
                    duelist.Deck.Add(card);
                }
            }
        }

        public void StartDuel(int firstPlayer)
        {
            FirstPlayer = firstPlayer;
            foreach (DuelistState duelist in Duelists)
                Shuffle(duelist.Deck);

            Raise(DuelEventType.DuelStarted, firstPlayer, text: $"{Duelists[firstPlayer].Name} will go first.");
            for (int i = 0; i < DuelRules.OpeningHandSize; i++)
            {
                Draw(firstPlayer, 1);
                Draw(1 - firstPlayer, 1);
            }
            if (IsOver) return;
            BeginTurn(firstPlayer);
        }

        public IEnumerable<DuelCard> AllCards()
        {
            foreach (DuelistState d in Duelists)
            {
                foreach (DuelCard c in d.Deck) yield return c;
                foreach (DuelCard c in d.Hand) yield return c;
                foreach (DuelCard c in d.Graveyard) yield return c;
                foreach (DuelCard c in d.Banished) yield return c;
                foreach (DuelCard c in d.ExtraDeck) yield return c;
                foreach (DuelMonsterState m in d.MonstersOnField) yield return m.Card;
                foreach (DuelBackrowState s in d.AllBackrow) yield return s.Card;
            }
        }

        // =================================================================== turn structure

        private void BeginTurn(int player)
        {
            TurnNumber++;
            TurnPlayer = player;
            Step = BattleStep.None;
            foreach (DuelistState d in Duelists)
            {
                d.CannotAttackThisTurn = false;
                foreach (DuelMonsterState m in d.MonstersOnField)
                {
                    m.HasAttacked = false;
                    m.HasChangedPosition = false;
                    m.CannotAttackThisTurn = false;
                }
            }
            Duelists[player].NormalSummonUsed = false;

            Raise(DuelEventType.TurnStarted, player, amount: TurnNumber, text: $"Turn {TurnNumber}: {Duelists[player].Name}");

            SetPhase(DuelPhase.Draw);
            bool skipDraw = TurnNumber == 1;   // the player going first does not draw on turn 1
            if (!skipDraw && Duelists[player].SkipDraws > 0)
            {
                Duelists[player].SkipDraws--;
                skipDraw = true;
                Raise(DuelEventType.PhaseChanged, player, text: $"{Duelists[player].Name} skips the Draw Phase.");
            }
            if (!skipDraw && !Draw(player, 1))
                return;

            SetPhase(DuelPhase.Standby);
            foreach (DuelistState d in Duelists)
            foreach (DuelBackrowState s in d.AllBackrow.ToList())
            {
                if (IsOver) return;
                if (s.FaceDown || !s.Card.OnField) continue;
                CardEffects.Get(s.Card.Data)?.OnStandby(this, s, player);
            }
            if (IsOver) return;
            SetPhase(DuelPhase.Main1);
        }

        private void SetPhase(DuelPhase phase)
        {
            Phase = phase;
            Raise(DuelEventType.PhaseChanged, TurnPlayer, text: PhaseName(phase));
        }

        public static string PhaseName(DuelPhase phase) => phase switch
        {
            DuelPhase.Draw => "Draw Phase",
            DuelPhase.Standby => "Standby Phase",
            DuelPhase.Main1 => "Main Phase 1",
            DuelPhase.Battle => "Battle Phase",
            DuelPhase.Main2 => "Main Phase 2",
            DuelPhase.End => "End Phase",
            _ => phase.ToString()
        };

        public bool IsMainPhase => Phase == DuelPhase.Main1 || Phase == DuelPhase.Main2;
        public bool CanEnterBattlePhase(int player) => !IsBusy && player == TurnPlayer && Phase == DuelPhase.Main1 && TurnNumber > 1;

        /// <summary>Main 1 -> Battle (or End on turn 1) -> Main 2 -> End -> next turn.</summary>
        public bool AdvancePhase(int player)
        {
            if (IsBusy || player != TurnPlayer) return false;
            switch (Phase)
            {
                case DuelPhase.Main1:
                    if (TurnNumber > 1) EnterBattlePhase();
                    else EnterEndPhase();
                    return true;
                case DuelPhase.Battle:
                    Step = BattleStep.End;
                    Step = BattleStep.None;
                    SetPhase(DuelPhase.Main2);
                    return true;
                case DuelPhase.Main2:
                    EnterEndPhase();
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>Skip straight to the End Phase (from Main 1, Battle or Main 2).</summary>
        public bool EndTurn(int player)
        {
            if (IsBusy || player != TurnPlayer) return false;
            if (Phase == DuelPhase.Main1 || Phase == DuelPhase.Battle || Phase == DuelPhase.Main2)
            {
                Step = BattleStep.None;
                EnterEndPhase();
                return true;
            }
            return false;
        }

        private void EnterBattlePhase()
        {
            SetPhase(DuelPhase.Battle);
            Step = BattleStep.Start;
            Step = BattleStep.Battle;
        }

        private void EnterEndPhase()
        {
            SetPhase(DuelPhase.End);
            DuelistState duelist = Duelists[TurnPlayer];
            int excess = duelist.Hand.Count - DuelRules.HandSizeLimit;
            if (excess > 0)
            {
                Ask(new DuelChoice
                {
                    Player = TurnPlayer,
                    Title = "Hand Size Limit",
                    Prompt = $"End Phase: discard {excess} card{(excess == 1 ? "" : "s")} (hand limit is {DuelRules.HandSizeLimit}).",
                    Candidates = duelist.Hand.ToList(),
                    MinCount = excess,
                    MaxCount = excess,
                    Context = "discard",
                    OnCards = picks =>
                    {
                        foreach (DuelCard card in picks)
                            Discard(card);
                        FinishEndPhase();
                    }
                });
                return;
            }
            FinishEndPhase();
        }

        private readonly List<Action> _endPhaseActions = new();

        /// <summary>Schedules something for this turn's End Phase ("destroy it during the End Phase").</summary>
        public void AtEndPhase(Action action)
        {
            if (action != null) _endPhaseActions.Add(action);
        }

        private void FinishEndPhase()
        {
            if (IsOver) return;
            int ending = TurnPlayer;

            List<Action> scheduled = _endPhaseActions.ToList();
            _endPhaseActions.Clear();
            _effectDamageRules.Clear();
            foreach (Action action in scheduled)
            {
                if (IsOver) return;
                action();
            }

            // "Until the end of this turn" effects expire.
            foreach (DuelistState d in Duelists)
            foreach (DuelMonsterState m in d.MonstersOnField.ToList())
            {
                m.TempAttack = 0;
                m.TempDefense = 0;
                if (m.ReturnControlTo >= 0)
                    SwitchControl(m, m.ReturnControlTo, false);
            }

            // Cards that count the opponent's turns (Swords of Revealing Light).
            foreach (DuelistState d in Duelists)
            foreach (DuelBackrowState s in d.AllBackrow.ToList())
            {
                if (s.FaceDown || s.TurnsRemaining <= 0 || s.Card.Controller == ending) continue;
                s.TurnsRemaining--;
                if (s.TurnsRemaining == 0)
                    SendToGraveyard(s.Card, destroyed: true, cause: s.Card);
            }

            if (IsOver) return;
            BeginTurn(1 - ending);
        }

        public void Surrender(int player)
        {
            if (IsOver) return;
            EndDuel(1 - player, $"{Duelists[player].Name} surrendered.");
        }

        // =================================================================== drawing & moving cards

        public bool Draw(int player, int count)
        {
            DuelistState d = Duelists[player];
            for (int i = 0; i < count; i++)
            {
                if (d.Deck.Count == 0)
                {
                    EndDuel(1 - player, $"{d.Name} could not draw — Deck Out.");
                    return false;
                }
                DuelCard card = d.Deck[d.Deck.Count - 1];
                MoveToList(card, DuelZone.Hand);
                Raise(DuelEventType.CardDrawn, player, card, text: $"{d.Name} drew a card.");
            }
            return true;
        }

        public void Shuffle(List<DuelCard> cards)
        {
            for (int i = cards.Count - 1; i > 0; i--)
            {
                int j = _random.Next(i + 1);
                (cards[i], cards[j]) = (cards[j], cards[i]);
            }
        }

        public void ShuffleDeck(int player) => Shuffle(Duelists[player].Deck);

        /// <summary>Test hook: move a specific card into its owner's hand.</summary>
        internal DuelCard DebugPutInHand(int player, string cardName)
        {
            DuelCard card = AllCards().FirstOrDefault(c => c.Owner == player && c.Name == cardName && c.Zone == DuelZone.Hand)
                            ?? AllCards().FirstOrDefault(c => c.Owner == player && c.Name == cardName);
            if (card != null && card.Zone != DuelZone.Hand) MoveToList(card, DuelZone.Hand);
            return card;
        }

        private List<DuelCard> OwnerList(DuelCard card, DuelZone zone)
        {
            DuelistState owner = Duelists[card.Owner];
            return zone switch
            {
                DuelZone.Deck => owner.Deck,
                DuelZone.Hand => owner.Hand,
                DuelZone.Graveyard => owner.Graveyard,
                DuelZone.Banished => owner.Banished,
                DuelZone.ExtraDeck => owner.ExtraDeck,
                _ => null
            };
        }

        /// <summary>Removes a card from wherever it currently is, running leave-the-field cleanup.</summary>
        private void Detach(DuelCard card)
        {
            switch (card.Zone)
            {
                case DuelZone.Monster:
                {
                    DuelistState controller = Duelists[card.Controller];
                    DuelMonsterState monster = controller.FindMonster(card);
                    if (monster != null)
                    {
                        controller.Monsters[monster.Slot] = null;
                        card.Zone = DuelZone.None;   // already leaving: effects must not try to move it again
                        ReleaseControlHeldBy(card);
                        if (CurrentAttacker == monster) CurrentAttacker = null;
                        if (CurrentAttackTarget == monster) CurrentAttackTarget = null;
                        // Equip Spells and linked cards leave with the monster.
                        foreach (DuelBackrowState equip in monster.Equips.ToList())
                            SendToGraveyard(equip.Card, destroyed: true, cause: card);
                        foreach (DuelistState d in Duelists)
                        foreach (DuelBackrowState s in d.SpellTrapsOnField.Where(s => s.LinkedMonster == monster).ToList())
                        {
                            s.LinkedMonster = null;
                            SendToGraveyard(s.Card, destroyed: true, cause: card);
                        }
                    }
                    break;
                }
                case DuelZone.SpellTrap:
                case DuelZone.FieldSpell:
                {
                    DuelistState controller = Duelists[card.Controller];
                    DuelBackrowState backrow = controller.FindBackrow(card);
                    if (backrow != null)
                    {
                        if (card.Zone == DuelZone.FieldSpell) controller.FieldSpell = null;
                        else controller.SpellTraps[backrow.Slot] = null;
                        card.Zone = DuelZone.None;
                        backrow.EquippedTo?.Equips.Remove(backrow);
                        backrow.EquippedTo = null;
                        if (!backrow.FaceDown)
                            CardEffects.Get(card.Data)?.OnLeaveField(this, backrow);
                    }
                    break;
                }
                default:
                    OwnerList(card, card.Zone)?.Remove(card);
                    break;
            }
            card.Slot = -1;
            card.Controller = card.Owner;
        }

        private void MoveToList(DuelCard card, DuelZone zone, bool toBottom = false)
        {
            bool wasOnField = card.OnField;
            bool wasFaceUp = card.FaceUp;
            Detach(card);
            // Defensive: a card can only ever be in one pile.
            DuelistState owner = Duelists[card.Owner];
            owner.Deck.Remove(card);
            owner.Hand.Remove(card);
            owner.Graveyard.Remove(card);
            owner.Banished.Remove(card);
            owner.ExtraDeck.Remove(card);
            card.Zone = zone;
            card.FaceUp = zone == DuelZone.Graveyard || zone == DuelZone.Banished;
            List<DuelCard> list = OwnerList(card, zone);
            if (list == null) return;
            if (toBottom) list.Insert(0, card);
            else list.Add(card);

            if (wasOnField && wasFaceUp && zone == DuelZone.Graveyard)
                CardEffects.Get(card.Data)?.OnSentToGraveyardFromField(this, card);
        }

        public DuelMonsterState PlaceMonster(DuelCard card, int controller, int slot, DuelMonsterPosition position)
        {
            DuelistState d = Duelists[controller];
            slot = d.FreeMonsterSlot(slot);
            if (slot < 0) return null;
            Detach(card);
            DuelMonsterState monster = new DuelMonsterState(card, position, slot, TurnNumber);
            d.Monsters[slot] = monster;
            card.Zone = DuelZone.Monster;
            card.Controller = controller;
            card.Slot = slot;
            card.FaceUp = position != DuelMonsterPosition.FaceDownDefense;
            return monster;
        }

        public DuelBackrowState PlaceBackrow(DuelCard card, int controller, int slot, bool faceDown)
        {
            DuelistState d = Duelists[controller];
            if (DuelRules.IsFieldSpell(card.Data))
            {
                if (d.FieldSpell != null && d.FieldSpell.Card != card)
                    SendToGraveyard(d.FieldSpell.Card, destroyed: false, cause: card);
                Detach(card);
                DuelBackrowState field = new DuelBackrowState(card, faceDown, 0, TurnNumber);
                d.FieldSpell = field;
                card.Zone = DuelZone.FieldSpell;
                card.Controller = controller;
                card.Slot = 0;
                card.FaceUp = !faceDown;
                return field;
            }

            slot = d.FreeSpellTrapSlot(slot);
            if (slot < 0) return null;
            Detach(card);
            DuelBackrowState state = new DuelBackrowState(card, faceDown, slot, TurnNumber);
            d.SpellTraps[slot] = state;
            card.Zone = DuelZone.SpellTrap;
            card.Controller = controller;
            card.Slot = slot;
            card.FaceUp = !faceDown;
            return state;
        }

        public void SendToGraveyard(DuelCard card, bool destroyed, DuelCard cause)
        {
            if (card == null || card.Zone == DuelZone.Graveyard) return;
            bool fromField = card.OnField;
            bool flippedMonster = FindMonster(card)?.WasFlipped == true;
            int lastController = card.Controller;
            MoveToList(card, DuelZone.Graveyard);
            if (flippedMonster) QueueMonsterAbility(MonsterAbilityKind.SentToGraveyardAfterFlip, card, lastController);
            if (fromField && destroyed)
                Raise(DuelEventType.Destroyed, card.Owner, card, cause, text: $"{card.Name} was destroyed.");
            else
                Raise(DuelEventType.SentToGraveyard, card.Owner, card, cause, text: $"{card.Name} was sent to the Graveyard.");
        }

        private bool _battleDestroying;

        public void Destroy(DuelCard card, DuelCard cause)
        {
            if (card == null) return;
            if (_battleDestroying && card.Zone == DuelZone.Monster)
            {
                DuelMonsterState self = FindMonster(card);
                DuelCard substitute = self != null ? CardEffects.GetMonster(card.Data)?.BattleSubstitute(this, self) : null;
                if (substitute != null && substitute.OnField)
                {
                    Raise(DuelEventType.Message, card.Controller, card, text: $"{substitute.Name} was destroyed instead of {card.Name}.");
                    card = substitute;
                }
            }
            DuelBackrowState backrow = FindBackrow(card);
            if (backrow != null && !backrow.FaceDown && cause != card &&
                CardEffects.Get(card.Data)?.ResistsDestruction(this, backrow, cause) == true)
            {
                Raise(DuelEventType.Message, card.Controller, card, text: $"{card.Name} was not destroyed.");
                return;
            }
            bool monsterByEffect = card.Zone == DuelZone.Monster && !_battleDestroying;
            SendToGraveyard(card, true, cause);
            if (monsterByEffect && card.Zone == DuelZone.Graveyard)
                foreach (var (state, effect) in FaceUpEffects().ToList())
                    effect.OnMonsterDestroyedByEffect(this, state, card);
        }

        public void Banish(DuelCard card, DuelCard cause)
        {
            if (card == null || card.Zone == DuelZone.Banished) return;
            MoveToList(card, DuelZone.Banished);
            Raise(DuelEventType.Banished, card.Owner, card, cause, text: $"{card.Name} was banished.");
        }

        public void ReturnToHand(DuelCard card, DuelCard cause)
        {
            if (card == null) return;
            if (card.IsExtraDeckCard) { MoveToList(card, DuelZone.ExtraDeck); return; }
            MoveToList(card, DuelZone.Hand);
            Raise(DuelEventType.ReturnedToHand, card.Owner, card, cause, text: $"{card.Name} returned to the hand.");
        }

        public void ReturnToDeck(DuelCard card, bool shuffle, bool bottom = false)
        {
            if (card == null) return;
            if (card.IsExtraDeckCard) { MoveToList(card, DuelZone.ExtraDeck); return; }
            MoveToList(card, DuelZone.Deck, bottom);
            if (shuffle) ShuffleDeck(card.Owner);
            Raise(DuelEventType.ReturnedToDeck, card.Owner, card, text: $"{card.Name} was returned to the Deck.");
        }

        /// <summary>Adds a card from the Deck, Graveyard or banishment to its owner's hand (searches, salvages).</summary>
        public void AddToHand(DuelCard card, DuelCard cause)
        {
            if (card == null || card.Zone == DuelZone.Hand) return;
            if (card.IsExtraDeckCard) { MoveToList(card, DuelZone.ExtraDeck); return; }
            DuelZone from = card.Zone;
            MoveToList(card, DuelZone.Hand);
            string where = from == DuelZone.Deck ? "Deck" : from == DuelZone.Graveyard ? "Graveyard" : from.ToString();
            Raise(DuelEventType.ReturnedToHand, card.Owner, card, cause, text: $"{Duelists[card.Owner].Name} added {card.Name} from the {where} to the hand.");
        }

        public void Discard(DuelCard card)
        {
            if (card == null || card.Zone != DuelZone.Hand) return;
            MoveToList(card, DuelZone.Graveyard);
            Raise(DuelEventType.Discarded, card.Owner, card, text: $"{Duelists[card.Owner].Name} discarded {card.Name}.");
            QueueMonsterAbility(MonsterAbilityKind.Discarded, card, card.Owner);
        }

        public void SwitchControl(DuelMonsterState monster, int newController, bool temporary)
        {
            if (monster == null) return;
            DuelistState target = Duelists[newController];
            int slot = target.FreeMonsterSlot();
            if (slot < 0)
            {
                if (!temporary) SendToGraveyard(monster.Card, false, null);
                return;
            }
            int previous = monster.Card.Controller;
            Duelists[previous].Monsters[monster.Slot] = null;
            target.Monsters[slot] = monster;
            monster.Slot = slot;
            monster.Card.Slot = slot;
            monster.Card.Controller = newController;
            monster.ReturnControlTo = temporary ? previous : -1;
            monster.ControlHeldBy = null;
            monster.CanAttackDirectly = false;
            monster.SummonedTurn = TurnNumber;
            Raise(DuelEventType.ControlChanged, newController, monster.Card, text: $"{target.Name} took control of {monster.Name}.");
        }

        // =================================================================== life points

        private readonly List<(DuelCard source, int player, bool reflect)> _effectDamageRules = new();

        /// <summary>Effect damage from <paramref name="source"/> to <paramref name="player"/> is negated (or reflected) this turn.</summary>
        public void ShieldEffectDamage(DuelCard source, int player, bool reflect) => _effectDamageRules.Add((source, player, reflect));

        public void DealDamage(int player, int amount, DuelCard source, bool battle)
        {
            if (amount <= 0 || IsOver) return;
            if (!battle && source != null)
            {
                int rule = _effectDamageRules.FindIndex(r => r.source == source && r.player == player);
                if (rule >= 0)
                {
                    bool reflect = _effectDamageRules[rule].reflect;
                    Raise(DuelEventType.Message, player, source, text: reflect
                        ? $"The {amount} damage from {source.Name} is sent back to {Duelists[1 - player].Name}!"
                        : $"The {amount} damage from {source.Name} was negated.");
                    if (reflect) { _effectDamageRules.RemoveAt(rule); DealDamage(1 - player, amount, null, false); }
                    return;
                }
            }
            Duelists[player].LifePoints = Math.Max(0, Duelists[player].LifePoints - amount);
            Raise(DuelEventType.Damage, player, source, amount: amount,
                text: $"{Duelists[player].Name} took {amount} {(battle ? "battle" : "effect")} damage.");
            CheckLifePoints();
        }

        public void GainLife(int player, int amount, DuelCard source)
        {
            if (amount <= 0 || IsOver) return;
            Duelists[player].LifePoints += amount;
            Raise(DuelEventType.LifeGained, player, source, amount: amount, text: $"{Duelists[player].Name} gained {amount} LP.");
        }

        public bool PayLife(int player, int amount, DuelCard source)
        {
            if (Duelists[player].LifePoints < amount) return false;
            Duelists[player].LifePoints -= amount;
            Raise(DuelEventType.LifePaid, player, source, amount: amount, text: $"{Duelists[player].Name} paid {amount} LP.");
            CheckLifePoints();
            return true;
        }

        private void CheckLifePoints()
        {
            bool p0 = Duelists[0].LifePoints <= 0;
            bool p1 = Duelists[1].LifePoints <= 0;
            if (p0 && p1) EndDuel(-1, "Both players' Life Points reached 0 — the duel is a draw.");
            else if (p0) EndDuel(1, $"{Possessive(0)} Life Points reached 0.");
            else if (p1) EndDuel(0, $"{Possessive(1)} Life Points reached 0.");
        }

        private void EndDuel(int winner, string reason)
        {
            if (IsOver) return;
            IsOver = true;
            Winner = winner;
            EndReason = reason;
            PendingChoice = null;
            CurrentAttacker = null;
            CurrentAttackTarget = null;
            Raise(DuelEventType.DuelEnded, winner, text: reason);
        }

        // =================================================================== stats

        public int GetAttack(DuelMonsterState m)
        {
            if (m == null) return 0;
            int value = m.Card.Data.attack + m.TempAttack + m.PermAttack + StatModifier(m, true);
            return Math.Max(0, value);
        }

        public int GetDefense(DuelMonsterState m)
        {
            if (m == null) return 0;
            int value = m.Card.Data.defense + m.TempDefense + m.PermDefense + StatModifier(m, false);
            return Math.Max(0, value);
        }

        private int StatModifier(DuelMonsterState m, bool attack)
        {
            if (m.IsFaceDown) return 0;
            int total = 0;
            foreach (DuelistState d in Duelists)
            foreach (DuelBackrowState s in d.AllBackrow)
            {
                if (s.FaceDown) continue;
                CardEffect effect = CardEffects.Get(s.Card.Data);
                if (effect == null) continue;
                total += attack ? effect.AttackModifier(this, s, m) : effect.DefenseModifier(this, s, m);
            }
            MonsterEffect self = CardEffects.GetMonster(m.Card.Data);
            if (self != null)
                total += attack ? self.SelfAttackModifier(this, m) : self.SelfDefenseModifier(this, m);
            if (attack)
                foreach (DuelistState d in Duelists)
                foreach (DuelMonsterState other in d.MonstersOnField)
                {
                    if (other.IsFaceDown) continue;
                    MonsterEffect aura = CardEffects.GetMonster(other.Card.Data);
                    if (aura != null) total += aura.AuraAttackModifier(this, other, m);
                }
            return total;
        }

        public DuelMonsterState FindMonster(DuelCard card)
        {
            if (card == null || card.Zone != DuelZone.Monster) return null;
            return Duelists[card.Controller].FindMonster(card);
        }

        public DuelBackrowState FindBackrow(DuelCard card)
        {
            if (card == null || (card.Zone != DuelZone.SpellTrap && card.Zone != DuelZone.FieldSpell)) return null;
            return Duelists[card.Controller].FindBackrow(card);
        }

        // =================================================================== summoning

        public bool CanNormalSummon(int player, DuelCard card, bool set)
        {
            if (IsBusy || player != TurnPlayer || !IsMainPhase) return false;
            DuelistState d = Duelists[player];
            if (d.NormalSummonUsed || card == null || card.Zone != DuelZone.Hand || card.Owner != player) return false;
            if (!DuelRules.CanEverBeNormalSummoned(card.Data)) return false;
            int tributes = TributesRequired(card);
            if (tributes == 0) return d.HasFreeMonsterZone;
            return d.MonsterCount >= tributes;
        }

        public string WhyCannotNormalSummon(int player, DuelCard card)
        {
            if (card == null || !card.IsMonster) return "Not a monster.";
            if (!DuelRules.CanEverBeNormalSummoned(card.Data))
                return card.IsExtraDeckCard ? "Extra Deck monsters must be Special Summoned." :
                    card.Data.ResolvedFrameKind == CardFrameKind.RitualMonster ? "Ritual Monsters must be Ritual Summoned." :
                    "This monster cannot be Normal Summoned or Set.";
            if (player != TurnPlayer) return "Not your turn.";
            if (!IsMainPhase) return "Only during your Main Phase.";
            if (Duelists[player].NormalSummonUsed) return "You already Normal Summoned or Set this turn.";
            int tributes = TributesRequired(card);
            if (tributes > 0 && Duelists[player].MonsterCount < tributes)
                return $"Needs {tributes} Tribute{(tributes > 1 ? "s" : "")}.";
            if (tributes == 0 && !Duelists[player].HasFreeMonsterZone) return "No free Monster Zone.";
            return string.Empty;
        }

        /// <summary>Normal Summon (face-up ATK) or Set (face-down DEF). Asks for Tributes if needed.</summary>
        public bool NormalSummon(int player, DuelCard card, bool set, int preferredSlot = -1)
        {
            if (!CanNormalSummon(player, card, set)) return false;
            DuelistState d = Duelists[player];
            int needed = TributesRequired(card);

            void Complete(List<DuelCard> tributes)
            {
                foreach (DuelCard tribute in tributes)
                {
                    SendToGraveyard(tribute, destroyed: false, cause: card);
                }
                d.NormalSummonUsed = true;
                DuelMonsterState monster = PlaceMonster(card, player, preferredSlot,
                    set ? DuelMonsterPosition.FaceDownDefense : DuelMonsterPosition.FaceUpAttack);
                if (monster == null) return;

                if (set)
                {
                    Raise(DuelEventType.MonsterSet, player, card, text: $"{d.Name} Set a monster.");
                    return;
                }

                Raise(tributes.Count > 0 ? DuelEventType.TributeSummon : DuelEventType.NormalSummon, player, card,
                    text: tributes.Count > 0
                        ? $"{d.Name} Tribute Summoned {card.Name}."
                        : $"{d.Name} Normal Summoned {card.Name}.");

                var trigger = new DuelTrigger { Kind = DuelTriggerKind.NormalSummoned, Player = player, Card = card };
                OpenResponseWindow(1 - player, trigger,
                    proceed: () => AfterSummon(MonsterAbilityKind.NormalSummoned, player, card),
                    onNegated: () => { });
            }

            if (needed == 0)
            {
                Complete(new List<DuelCard>());
                return true;
            }

            Ask(new DuelChoice
            {
                Player = player,
                Title = set ? "Tribute Set" : "Tribute Summon",
                Prompt = $"Tribute {needed} monster{(needed > 1 ? "s" : "")} to {(set ? "Set" : "Summon")} {card.Name}.",
                SourceCard = card,
                Candidates = d.MonstersOnField.Select(m => m.Card).ToList(),
                MinCount = needed,
                MaxCount = needed,
                Context = "tribute",
                OnCards = Complete,
                OnCancel = () => { }
            });
            return true;
        }

        public DuelMonsterState SpecialSummon(int player, DuelCard card, DuelMonsterPosition position, DuelCard cause, int preferredSlot = -1, bool openWindow = true)
        {
            if (card == null || !Duelists[player].HasFreeMonsterZone) return null;
            foreach (var (state, effect) in FaceUpEffects())
                if (effect.BlocksSpecialSummon(this, state, player, card))
                {
                    Raise(DuelEventType.Message, player, card, text: $"{state.Card.Name}: that monster cannot be Special Summoned.");
                    return null;
                }
            if (SpecialSummonsLocked)
            {
                Raise(DuelEventType.Message, player, card, text: "Fossil Dyna Pachycephalo: neither player can Special Summon.");
                return null;
            }
            DuelMonsterState monster = PlaceMonster(card, player, preferredSlot, position);
            if (monster == null) return null;
            monster.SpecialSummoned = true;
            Raise(DuelEventType.SpecialSummon, player, card, cause, text: $"{Duelists[player].Name} Special Summoned {card.Name}.");
            NotifySummoned(card, player, special: true);
            if (!openWindow)
            {
                QueueMonsterAbility(MonsterAbilityKind.SpecialSummoned, card, player);
                return monster;
            }
            var trigger = new DuelTrigger { Kind = DuelTriggerKind.SpecialSummoned, Player = player, Card = card };
            OpenResponseWindow(1 - player, trigger, proceed: () =>
            {
                QueueMonsterAbility(MonsterAbilityKind.SpecialSummoned, card, player);
                RunQueuedMonsterAbilities();
            }, onNegated: () => { });
            return monster;
        }

        /// <summary>A face-up Fossil Dyna Pachycephalo stops both players Special Summoning.</summary>
        public bool SpecialSummonsLocked =>
            Duelists.Any(d => d.MonstersOnField.Any(m => m.IsFaceUp && m.Name == "Fossil Dyna Pachycephalo"));

        /// <summary>Current Level (A Legendary Ocean lowers WATER monsters by 1 in the hand and on the field).</summary>
        public int LevelOf(DuelCard card)
        {
            if (card == null || !card.IsMonster) return 0;
            int level = card.Data.level;
            if (Fx.AttrIs(card.Data, "WATER") && (card.Zone == DuelZone.Hand || card.Zone == DuelZone.Monster) && FaceUpFieldCard("A Legendary Ocean") != null)
                level = Math.Max(1, level - 1);
            return level;
        }

        public int TributesRequired(DuelCard card)
        {
            if (card == null || !card.IsMonster) return 0;
            int level = LevelOf(card);
            return level >= 7 ? 2 : level >= 5 ? 1 : 0;
        }

        /// <summary>A face-up Spell/Trap with this name on either side of the field.</summary>
        public DuelBackrowState FaceUpFieldCard(string name) =>
            Duelists.SelectMany(d => d.AllBackrow).FirstOrDefault(s => !s.FaceDown && s.Card.Name == name);

        private IEnumerable<(DuelBackrowState state, CardEffect effect)> FaceUpEffects()
        {
            foreach (DuelistState d in Duelists)
            foreach (DuelBackrowState s in d.AllBackrow.ToList())
            {
                if (s.FaceDown) continue;
                CardEffect effect = CardEffects.Get(s.Card.Data);
                if (effect != null) yield return (s, effect);
            }
        }

        private void NotifySummoned(DuelCard card, int player, bool special)
        {
            DuelMonsterState m = FindMonster(card);
            if (m == null) return;
            m.LastSummonTurn = TurnNumber;
            foreach (var (state, effect) in FaceUpEffects().ToList())
                effect.OnMonsterSummoned(this, state, m, player, special);
        }

        public bool IsProtectedFromAttack(DuelMonsterState defender) =>
            defender != null && FaceUpEffects().Any(x => x.effect.ProtectsFromAttack(this, x.state, defender));

        private void BattleDamage(int player, int amount, DuelMonsterState ownMonster, DuelCard source)
        {
            if (amount <= 0) return;
            foreach (var (state, effect) in FaceUpEffects())
                if (effect.PreventsBattleDamage(this, state, player, ownMonster))
                {
                    Raise(DuelEventType.Message, player, state.Card, text: $"{state.Card.Name}: {Duelists[player].Name} takes no battle damage.");
                    return;
                }
            DealDamage(player, amount, source, battle: true);
            if (ownMonster != null && CardEffects.GetMonster(ownMonster.Card.Data)?.MirrorsBattleDamage(this, ownMonster) == true)
                DealDamage(1 - player, amount, ownMonster.Card, battle: false);
        }

        /// <summary>Equips a monster card to <paramref name="holder"/> as an Equip Card (Relinquished).</summary>
        public DuelBackrowState EquipMonsterCard(DuelCard card, DuelMonsterState holder)
        {
            int player = holder.Card.Controller;
            if (card == null || !Duelists[player].HasFreeSpellTrapZone) return null;
            DuelBackrowState s = PlaceBackrow(card, player, -1, faceDown: false);
            if (s == null) return null;
            s.EquippedTo = holder;
            holder.Equips.Add(s);
            Raise(DuelEventType.Message, player, card, text: $"{card.Name} was equipped to {holder.Name}.");
            return s;
        }

        // ------------------------------------------------------------------ monster ignition effects

        public bool CanUseMonsterEffect(int player, DuelMonsterState m)
        {
            if (IsBusy || HasQueuedMonsterAbilities || m == null || player != TurnPlayer || !IsMainPhase) return false;
            if (m.Card.Controller != player || m.IsFaceDown || m.LastIgnitionTurn == TurnNumber) return false;
            MonsterAbility ability = MonsterAbilities.Get(m.Card, MonsterAbilityKind.Ignition);
            if (ability == null) return false;
            var ctx = new EffectContext(this, player, m.Card, null);
            if (ability.Can != null && !ability.Can(ctx)) return false;
            TargetRequest request = ability.Tgt?.Invoke(ctx);
            return request == null || request.Candidates.Count >= Math.Max(1, request.Min);
        }

        public bool UseMonsterEffect(int player, DuelMonsterState m)
        {
            if (!CanUseMonsterEffect(player, m)) return false;
            m.LastIgnitionTurn = TurnNumber;
            QueueMonsterAbility(MonsterAbilityKind.Ignition, m.Card, player);
            RunQueuedMonsterAbilities();
            return true;
        }

        public bool CanFlipSummon(int player, DuelMonsterState m)
        {
            return !IsBusy && player == TurnPlayer && IsMainPhase && m != null && m.Card.Controller == player &&
                   m.IsFaceDown && m.PositionSetTurn < TurnNumber && !m.HasChangedPosition;
        }

        public bool FlipSummon(int player, DuelMonsterState m)
        {
            if (!CanFlipSummon(player, m)) return false;
            m.Position = DuelMonsterPosition.FaceUpAttack;
            m.Card.FaceUp = true;
            m.HasChangedPosition = true;
            m.WasFlipped = true;
            Raise(DuelEventType.FlipSummon, player, m.Card, text: $"{Duelists[player].Name} Flip Summoned {m.Name}.");
            QueueMonsterAbility(MonsterAbilityKind.Flip, m.Card, player);
            var trigger = new DuelTrigger { Kind = DuelTriggerKind.FlipSummoned, Player = player, Card = m.Card };
            OpenResponseWindow(1 - player, trigger, proceed: () => AfterSummon(MonsterAbilityKind.FlipSummoned, player, m.Card), onNegated: RunQueuedMonsterAbilities);
            return true;
        }

        public bool CanChangePosition(int player, DuelMonsterState m)
        {
            return !IsBusy && player == TurnPlayer && IsMainPhase && m != null && m.Card.Controller == player &&
                   m.IsFaceUp && !m.HasChangedPosition && !m.HasAttacked && !m.CannotChangePosition &&
                   m.SummonedTurn < TurnNumber && m.PositionSetTurn < TurnNumber;
        }

        public bool ChangePosition(int player, DuelMonsterState m)
        {
            if (!CanChangePosition(player, m)) return false;
            m.Position = m.IsAttackPosition ? DuelMonsterPosition.FaceUpDefense : DuelMonsterPosition.FaceUpAttack;
            m.HasChangedPosition = true;
            Raise(DuelEventType.PositionChanged, player, m.Card,
                text: $"{m.Name} changed to {(m.IsAttackPosition ? "Attack" : "Defense")} Position.");
            return true;
        }

        /// <summary>Changes position through a card effect (ignores once-per-turn rules).</summary>
        public void ForcePosition(DuelMonsterState m, DuelMonsterPosition position, bool activateFlip = true)
        {
            if (m == null || m.Position == position) return;
            bool wasFaceDown = m.IsFaceDown;
            m.Position = position;
            m.Card.FaceUp = position != DuelMonsterPosition.FaceDownDefense;
            if (position == DuelMonsterPosition.FaceDownDefense)
            {
                m.PositionSetTurn = TurnNumber;
                ReleaseControlHeldBy(m.Card);   // a Charmer turned face-down lets its monster go
            }
            else if (wasFaceDown)
            {
                m.WasFlipped = true;
                if (activateFlip) QueueMonsterAbility(MonsterAbilityKind.Flip, m.Card, m.Card.Controller);
            }
            Raise(wasFaceDown && m.Card.FaceUp ? DuelEventType.Flipped : DuelEventType.PositionChanged, m.Card.Controller, m.Card,
                text: position == DuelMonsterPosition.FaceDownDefense ? $"{m.Name} was changed to face-down Defense Position."
                    : $"{m.Name} is now in {(position == DuelMonsterPosition.FaceUpAttack ? "Attack" : "Defense")} Position.");
        }

        // =================================================================== spells & traps

        public bool CanSetSpellTrap(int player, DuelCard card)
        {
            if (IsBusy || player != TurnPlayer || !IsMainPhase || card == null || card.Zone != DuelZone.Hand) return false;
            if (card.IsMonster) return false;
            if (DuelRules.IsFieldSpell(card.Data)) return true;
            return Duelists[player].HasFreeSpellTrapZone;
        }

        public bool SetSpellTrap(int player, DuelCard card, int preferredSlot = -1)
        {
            if (!CanSetSpellTrap(player, card)) return false;
            DuelBackrowState state = PlaceBackrow(card, player, preferredSlot, faceDown: true);
            if (state == null) return false;
            Raise(DuelEventType.SpellTrapSet, player, card, text: $"{Duelists[player].Name} Set a card.");
            return true;
        }

        public bool HasImplementedEffect(DuelCard card) => card != null && CardEffects.Get(card.Data) != null;

        /// <summary>Can this card be activated right now by <paramref name="player"/> (outside a response window)?</summary>
        public bool CanActivate(int player, DuelCard card)
        {
            if (IsBusy || card == null || card.Controller != player && card.Owner != player) return false;
            CardEffect effect = CardEffects.Get(card.Data);
            if (effect == null) return false;
            EffectContext ctx = new EffectContext(this, player, card, null);

            if (card.Zone == DuelZone.Hand)
            {
                if (!card.IsSpell || player != TurnPlayer || !IsMainPhase) return false;
                if (!DuelRules.IsFieldSpell(card.Data) && !Duelists[player].HasFreeSpellTrapZone) return false;
                return effect.CanActivate(ctx) && HasTargetsIfNeeded(effect, ctx);
            }

            DuelBackrowState set = FindBackrow(card);
            if (set == null || card.Controller != player) return false;
            if (!set.FaceDown)
            {
                // Face-up continuous effects apply automatically; only cards with a usable on-field effect can be "activated".
                if (!effect.HasFaceUpEffect || player != TurnPlayer || !IsMainPhase) return false;
                ctx.Source = set;
                ctx.FromFaceUp = true;
                return effect.CanActivate(ctx) && HasTargetsIfNeeded(effect, ctx);
            }

            if (card.IsTrap || DuelRules.IsQuickPlay(card.Data))
            {
                if (set.SetTurn >= TurnNumber) return false;   // cannot activate the turn it was Set
                if (player != TurnPlayer || !IsMainPhase) return false;
            }
            else
            {
                if (player != TurnPlayer || !IsMainPhase) return false;
            }
            return effect.CanActivate(ctx) && HasTargetsIfNeeded(effect, ctx);
        }

        private static bool HasTargetsIfNeeded(CardEffect effect, EffectContext ctx)
        {
            TargetRequest request = effect.Targets(ctx);
            return request == null || request.Candidates.Count >= Math.Max(1, request.Min);
        }

        public string WhyCannotActivate(int player, DuelCard card)
        {
            if (card == null) return string.Empty;
            if (CardEffects.Get(card.Data) == null) return "This card's effect is not implemented yet.";
            if (player != TurnPlayer) return "Not your turn.";
            if (!IsMainPhase) return "Only during your Main Phase.";
            DuelBackrowState set = FindBackrow(card);
            if (set != null && set.FaceDown && (card.IsTrap || DuelRules.IsQuickPlay(card.Data)) && set.SetTurn >= TurnNumber)
                return "A Set card cannot be activated the turn it was Set.";
            if (card.Zone == DuelZone.Hand && card.IsTrap) return "Traps must be Set first.";
            if (card.Zone == DuelZone.Hand && !DuelRules.IsFieldSpell(card.Data) && !Duelists[player].HasFreeSpellTrapZone)
                return "No free Spell & Trap Zone.";
            return "Its activation requirements are not met right now.";
        }

        public bool Activate(int player, DuelCard card, int preferredSlot = -1)
        {
            if (!CanActivate(player, card)) return false;
            ActivateCard(player, card, null, null, allowCancel: true, preferredSlot);
            return true;
        }

        private void ActivateCard(int player, DuelCard card, DuelTrigger respondingTo, Action after, bool allowCancel, int preferredSlot = -1)
        {
            CardEffect effect = CardEffects.Get(card.Data);
            EffectContext ctx = new EffectContext(this, player, card, respondingTo);
            DuelBackrowState existing = FindBackrow(card);
            ctx.FromFaceUp = existing != null && !existing.FaceDown;
            if (ctx.FromFaceUp) ctx.Source = existing;
            TargetRequest request = effect.Targets(ctx);

            void Begin(List<DuelCard> targets)
            {
                ctx.Targets.AddRange(targets ?? new List<DuelCard>());

                DuelBackrowState state = FindBackrow(card);
                if (state == null)
                    state = PlaceBackrow(card, player, preferredSlot, faceDown: false);
                if (state == null) { after?.Invoke(); return; }
                state.FaceDown = false;
                card.FaceUp = true;
                state.Resolving = !effect.StaysOnField;
                ctx.Source = state;

                string what = card.IsTrap ? "Trap" : "Spell";
                Raise(DuelEventType.CardActivated, player, card, text: $"{Duelists[player].Name} activated {what} Card \"{card.Name}\".");

                effect.PayCost(ctx, () =>
                {
                    var trigger = new DuelTrigger
                    {
                        Kind = card.IsTrap ? DuelTriggerKind.TrapActivated : DuelTriggerKind.SpellActivated,
                        Player = player,
                        Card = card,
                        Depth = (respondingTo?.Depth ?? 0) + 1,
                        Targets = ctx.Targets.ToList()
                    };

                    OpenResponseWindow(1 - player, trigger,
                        proceed: () =>
                        {
                            ctx.Done = () =>
                            {
                                Raise(DuelEventType.EffectResolved, player, card, text: $"\"{card.Name}\" resolved.");
                                DuelBackrowState still = FindBackrow(card);
                                if (still != null && (!effect.StaysOnField || effect.ShouldLeaveAfterResolve(ctx)))
                                    SendToGraveyard(card, destroyed: false, cause: card);
                                else if (still != null)
                                    still.Resolving = false;
                                after?.Invoke();
                                RunQueuedMonsterAbilities();
                            };
                            effect.Resolve(ctx);
                        },
                        onNegated: () =>
                        {
                            Raise(DuelEventType.ActivationNegated, player, card, text: $"\"{card.Name}\" was negated.");
                            if (card.OnField) SendToGraveyard(card, destroyed: true, cause: null);
                            after?.Invoke();
                        });
                });
            }

            if (request == null)
            {
                Begin(null);
                return;
            }

            Ask(new DuelChoice
            {
                Player = player,
                Title = card.Name,
                Prompt = request.Prompt,
                SourceCard = card,
                Candidates = request.Candidates,
                MinCount = request.Min,
                MaxCount = request.Max,
                Context = request.Context,
                OnCards = Begin,
                OnCancel = allowCancel ? () => { } : null
            });
        }

        // =================================================================== response windows (chains)

        /// <summary>Gives <paramref name="responder"/> the chance to activate a Set card in response.
        /// Nested windows resolve in reverse order, exactly like a chain.</summary>
        private void OpenResponseWindow(int responder, DuelTrigger trigger, Action proceed, Action onNegated)
        {
            void Continue()
            {
                if (IsOver) return;
                if (trigger.Negated) onNegated?.Invoke();
                else proceed?.Invoke();
            }

            if (IsOver) return;
            if (trigger.Depth > 6) { Continue(); return; }

            List<DuelCard> candidates = ResponseCandidates(responder, trigger);
            if (candidates.Count == 0)
            {
                Continue();
                return;
            }

            Ask(new DuelChoice
            {
                Player = responder,
                Title = "Chain?",
                Prompt = DescribeTrigger(trigger),
                Candidates = candidates,
                MinCount = 0,
                MaxCount = 1,
                IsResponseWindow = true,
                Trigger = trigger,
                Context = "response:" + trigger.Kind,
                OnCards = picks =>
                {
                    if (picks == null || picks.Count == 0)
                    {
                        Continue();
                        return;
                    }
                    ActivateCard(responder, picks[0], trigger, Continue, allowCancel: false);
                }
            });
        }

        public List<DuelCard> ResponseCandidates(int responder, DuelTrigger trigger)
        {
            var result = new List<DuelCard>();
            foreach (DuelBackrowState set in Duelists[responder].SpellTrapsOnField)
            {
                if (!set.FaceDown || set.SetTurn >= TurnNumber) continue;
                if (!set.Card.IsTrap && !DuelRules.IsQuickPlay(set.Card.Data)) continue;
                CardEffect effect = CardEffects.Get(set.Card.Data);
                if (effect == null) continue;
                EffectContext ctx = new EffectContext(this, responder, set.Card, trigger);
                if (effect.CanRespond(ctx) && HasTargetsIfNeeded(effect, ctx))
                    result.Add(set.Card);
            }
            return result;
        }

        private string DescribeTrigger(DuelTrigger trigger)
        {
            string who = Duelists[trigger.Player].Name;
            string whose = Possessive(trigger.Player);
            return trigger.Kind switch
            {
                DuelTriggerKind.AttackDeclared => trigger.Defender == null
                    ? $"{whose} {trigger.Attacker?.Name} attacks directly!"
                    : $"{whose} {trigger.Attacker?.Name} attacks {(trigger.Defender.IsFaceDown ? "a face-down monster" : trigger.Defender.Name)}!",
                DuelTriggerKind.NormalSummoned => $"{who} summoned {trigger.Card?.Name}.",
                DuelTriggerKind.FlipSummoned => $"{who} Flip Summoned {trigger.Card?.Name}.",
                DuelTriggerKind.SpecialSummoned => $"{who} Special Summoned {trigger.Card?.Name}.",
                DuelTriggerKind.SpellActivated => $"{who} activated \"{trigger.Card?.Name}\".",
                DuelTriggerKind.TrapActivated => $"{who} activated \"{trigger.Card?.Name}\".",
                DuelTriggerKind.MonsterEffectActivated => $"{whose} {trigger.Card?.Name} activated its effect.",
                _ => "Activate a card?"
            };
        }

        // =================================================================== battle

        public bool OpponentCannotAttack(int attacker)
        {
            // Swords of Revealing Light etc.: any face-up card of the defender that forbids attacks.
            foreach (DuelBackrowState s in Duelists[1 - attacker].AllBackrow)
            {
                if (s.FaceDown) continue;
                if (CardEffects.Get(s.Card.Data)?.PreventsOpponentAttacks(this, s) == true) return true;
            }
            return Duelists[attacker].CannotAttackThisTurn;
        }

        public bool CanAttack(int player, DuelMonsterState m)
        {
            return !IsBusy && player == TurnPlayer && Phase == DuelPhase.Battle && Step == BattleStep.Battle &&
                   m != null && m.Card.Controller == player && m.IsAttackPosition && !m.HasAttacked &&
                   !m.CannotAttackThisTurn && !OpponentCannotAttack(player) && !IsLockedBySpell(m) && !ForbiddenToAttack(m);
        }

        /// <summary>Face-up cards on either side that stop this particular monster attacking.</summary>
        public bool ForbiddenToAttack(DuelMonsterState m)
        {
            foreach (DuelistState d in Duelists)
            foreach (DuelBackrowState s in d.AllBackrow)
            {
                if (s.FaceDown) continue;
                if (CardEffects.Get(s.Card.Data)?.ForbidsAttack(this, s, m) == true) return true;
            }
            return false;
        }

        public bool IsLockedBySpell(DuelMonsterState m)
        {
            foreach (DuelistState d in Duelists)
            foreach (DuelBackrowState s in d.SpellTrapsOnField)
                if (!s.FaceDown && s.LinkedMonster == m && CardEffects.Get(s.Card.Data)?.LocksLinkedMonster == true)
                    return true;
            return false;
        }

        public bool CanAttackDirectly(int player, DuelMonsterState m) => CanAttack(player, m) && (Duelists[1 - player].MonsterCount == 0 || m.CanAttackDirectly);

        public IEnumerable<DuelMonsterState> AttackTargets(int player, DuelMonsterState m)
        {
            if (!CanAttack(player, m)) return Enumerable.Empty<DuelMonsterState>();
            return Duelists[1 - player].MonstersOnField.Where(d => !IsProtectedFromAttack(d)).ToList();
        }

        public bool DeclareAttack(int player, DuelMonsterState attacker, DuelMonsterState defender)
        {
            if (!CanAttack(player, attacker)) return false;
            if (defender == null && Duelists[1 - player].MonsterCount > 0 && !attacker.CanAttackDirectly) return false;
            if (defender != null && defender.Card.Controller == player) return false;
            if (defender != null && IsProtectedFromAttack(defender)) return false;

            attacker.HasAttacked = true;
            CurrentAttacker = attacker;
            CurrentAttackTarget = defender;
            Raise(DuelEventType.AttackDeclared, player, attacker.Card, defender?.Card,
                text: defender == null ? $"{attacker.Name} attacks directly!" : $"{attacker.Name} attacks {(defender.IsFaceDown ? "a face-down monster" : defender.Name)}!");

            var trigger = new DuelTrigger { Kind = DuelTriggerKind.AttackDeclared, Player = player, Attacker = attacker, Defender = defender, Card = attacker.Card };
            OpenResponseWindow(1 - player, trigger,
                proceed: () => ResolveBattle(player, attacker, defender),
                onNegated: () =>
                {
                    Raise(DuelEventType.AttackNegated, player, attacker.Card, text: $"{attacker.Name}'s attack was negated.");
                    ClearAttack();
                });
            return true;
        }

        private void ClearAttack()
        {
            CurrentAttacker = null;
            CurrentAttackTarget = null;
            if (Phase == DuelPhase.Battle) Step = BattleStep.Battle;
        }

        private void ResolveBattle(int player, DuelMonsterState attacker, DuelMonsterState defender)
        {
            if (IsOver) return;
            DuelistState me = Duelists[player];
            if (me.FindMonster(attacker.Card) != attacker || !attacker.IsAttackPosition)
            {
                ClearAttack();
                return;
            }

            if (defender != null && Duelists[1 - player].FindMonster(defender.Card) != defender)
            {
                // Replay: the target left the field, so the attack is not carried out.
                attacker.HasAttacked = false;
                Raise(DuelEventType.Message, player, attacker.Card, text: "The attack target is gone — the attack was not carried out.");
                ClearAttack();
                return;
            }

            Step = BattleStep.Damage;
            int atk = GetAttack(attacker);

            if (defender == null)
            {
                Raise(DuelEventType.BattleResolved, player, attacker.Card, amount: atk, text: $"{attacker.Name} attacks directly for {atk}.");
                BattleDamage(1 - player, atk, null, attacker.Card);
                ClearAttack();
                return;
            }

            bool flippedByAttack = false;
            int defenderController = defender.Card.Controller;
            if (defender.IsFaceDown)
            {
                defender.Position = DuelMonsterPosition.FaceUpDefense;
                defender.Card.FaceUp = true;
                defender.WasFlipped = true;
                flippedByAttack = true;
                Raise(DuelEventType.Flipped, defender.Card.Controller, defender.Card, text: $"{defender.Name} was flipped face-up.");
            }

            if (defender.IsAttackPosition)
            {
                int defAtk = GetAttack(defender);
                Raise(DuelEventType.BattleResolved, player, attacker.Card, defender.Card, atk - defAtk, $"{atk} ATK vs {defAtk} ATK");
                if (atk > defAtk)
                {
                    _battleDestroying = true;
                    Destroy(defender.Card, attacker.Card);
                    _battleDestroying = false;
                    BattleDamage(1 - player, atk - defAtk, defender, attacker.Card);
                }
                else if (atk < defAtk)
                {
                    _battleDestroying = true;
                    Destroy(attacker.Card, defender.Card);
                    _battleDestroying = false;
                    BattleDamage(player, defAtk - atk, attacker, defender.Card);
                }
                else if (atk > 0)
                {
                    _battleDestroying = true;
                    Destroy(attacker.Card, defender.Card);
                    Destroy(defender.Card, attacker.Card);
                    _battleDestroying = false;
                }
            }
            else
            {
                int def = GetDefense(defender);
                Raise(DuelEventType.BattleResolved, player, attacker.Card, defender.Card, atk - def, $"{atk} ATK vs {def} DEF");
                if (atk > def)
                {
                    _battleDestroying = true;
                    Destroy(defender.Card, attacker.Card);
                    _battleDestroying = false;
                }
                else if (atk < def)
                    BattleDamage(player, def - atk, attacker, defender.Card);
            }

            // Flip effects activate after damage calculation, even if the flipped monster was destroyed —
            // unless Harpie Lady 2 (or a lone Blade Knight) destroyed it.
            bool defenderDied = defender.Card.Zone == DuelZone.Graveyard;
            bool attackerDied = attacker.Card.Zone == DuelZone.Graveyard;
            if (flippedByAttack && !(defenderDied && NegatesFlipEffects(attacker)))
                QueueMonsterAbility(MonsterAbilityKind.Flip, defender.Card, defenderController);
            if (defenderDied) QueueMonsterAbility(MonsterAbilityKind.DestroyedByBattle, defender.Card, defenderController, attacker.Card);
            if (attackerDied) QueueMonsterAbility(MonsterAbilityKind.DestroyedByBattle, attacker.Card, player, defender.Card);
            ClearAttack();
            RunQueuedMonsterAbilities();
        }

        private bool NegatesFlipEffects(DuelMonsterState attacker)
        {
            if (attacker.Card.Zone != DuelZone.Monster && attacker.Card.Zone != DuelZone.Graveyard) return false;
            if (attacker.Name == "Harpie Lady 2") return true;
            return attacker.Name == "Blade Knight" && attacker.Card.Zone == DuelZone.Monster &&
                   Duelists[attacker.Card.Controller].MonsterCount == 1;
        }

        // =================================================================== choices

        internal void Ask(DuelChoice choice)
        {
            if (IsOver) return;
            IDuelDecider decider = Deciders[choice.Player];
            if (decider != null)
            {
                if (choice.Kind == DuelChoiceKind.SelectOption)
                {
                    int option = decider.ChooseOption(this, choice);
                    choice.OnOption?.Invoke(Math.Max(0, Math.Min(option, choice.Options.Count - 1)));
                }
                else
                {
                    choice.OnCards?.Invoke(Sanitize(choice, decider.ChooseCards(this, choice)));
                }
                return;
            }
            PendingChoice = choice;
        }

        private static List<DuelCard> Sanitize(DuelChoice choice, List<DuelCard> picks)
        {
            var result = (picks ?? new List<DuelCard>()).Where(choice.Candidates.Contains).Distinct().Take(choice.MaxCount).ToList();
            foreach (DuelCard candidate in choice.Candidates)
            {
                if (result.Count >= choice.MinCount) break;
                if (!result.Contains(candidate)) result.Add(candidate);
            }
            return result;
        }

        public bool SubmitChoice(List<DuelCard> picks)
        {
            DuelChoice choice = PendingChoice;
            if (choice == null || choice.Kind != DuelChoiceKind.SelectCards) return false;
            picks ??= new List<DuelCard>();
            if (picks.Count < choice.MinCount || picks.Count > choice.MaxCount) return false;
            if (picks.Any(p => !choice.Candidates.Contains(p))) return false;
            PendingChoice = null;
            choice.OnCards?.Invoke(picks);
            RunQueuedMonsterAbilities();
            return true;
        }

        public bool SubmitOption(int option)
        {
            DuelChoice choice = PendingChoice;
            if (choice == null || choice.Kind != DuelChoiceKind.SelectOption) return false;
            PendingChoice = null;
            choice.OnOption?.Invoke(option);
            RunQueuedMonsterAbilities();
            return true;
        }

        public bool CancelChoice()
        {
            DuelChoice choice = PendingChoice;
            if (choice == null || choice.OnCancel == null) return false;
            PendingChoice = null;
            choice.OnCancel();
            RunQueuedMonsterAbilities();
            return true;
        }

        // =================================================================== monster effects (Flip, summon triggers)

        private readonly List<(MonsterAbilityKind kind, DuelCard card, int player, DuelCard other)> _monsterQueue = new();
        private readonly List<Action<Action>> _customQueue = new();

        /// <summary>Queues a triggered effect that is not printed on a monster (Field Spell triggers).
        /// <paramref name="run"/> must call the Action it is given when it has finished.</summary>
        public void QueueTriggered(Action<Action> run)
        {
            if (run != null) _customQueue.Add(run);
        }
        private bool _monsterRunning;

        /// <summary>Queues a monster's printed effect; it resolves once nothing else is waiting.</summary>
        public void QueueMonsterAbility(MonsterAbilityKind kind, DuelCard card, int player, DuelCard other = null)
        {
            if (MonsterAbilities.Get(card, kind) != null) _monsterQueue.Add((kind, card, player, other));
        }

        public bool HasQueuedMonsterAbilities => _monsterQueue.Count > 0 || _customQueue.Count > 0 || _monsterRunning;

        public void RunQueuedMonsterAbilities()
        {
            if (_monsterRunning || IsOver || PendingChoice != null) return;
            if (_monsterQueue.Count == 0 && _customQueue.Count > 0)
            {
                Action<Action> run = _customQueue[0];
                _customQueue.RemoveAt(0);
                _monsterRunning = true;
                run(() =>
                {
                    _monsterRunning = false;
                    RunQueuedMonsterAbilities();
                });
                return;
            }
            if (_monsterQueue.Count == 0) return;
            var item = _monsterQueue[0];
            _monsterQueue.RemoveAt(0);
            MonsterAbility ability = MonsterAbilities.Get(item.card, item.kind);
            _monsterRunning = true;
            var ctx = new EffectContext(this, item.player, item.card, null) { Paid = item.other };
            ctx.Done = () =>
            {
                _monsterRunning = false;
                RunQueuedMonsterAbilities();
            };

            if (ability.Can != null && !ability.Can(ctx)) { ctx.Finish(); return; }
            string label = item.kind == MonsterAbilityKind.Flip ? "FLIP effect" : "effect";
            Raise(DuelEventType.CardActivated, item.player, item.card, text: $"{item.card.Name}'s {label} activates!");

            // The opponent may respond (Divine Wrath) before the monster effect resolves.
            void Resolve()
            {
                var trigger = new DuelTrigger { Kind = DuelTriggerKind.MonsterEffectActivated, Player = item.player, Card = item.card, Targets = ctx.Targets.ToList() };
                OpenResponseWindow(1 - item.player, trigger,
                    proceed: () => ability.Do(ctx),
                    onNegated: () =>
                    {
                        Raise(DuelEventType.ActivationNegated, item.player, item.card, text: $"{item.card.Name}'s effect was negated.");
                        ctx.Finish();
                    });
            }

            TargetRequest request = ability.Tgt?.Invoke(ctx);
            if (request == null) { Resolve(); return; }
            if (request.Candidates.Count == 0)
            {
                Raise(DuelEventType.Message, item.player, item.card, text: $"{item.card.Name}: there is nothing to target.");
                ctx.Finish();
                return;
            }
            Ask(new DuelChoice
            {
                Player = item.player,
                Title = item.card.Name,
                Prompt = request.Prompt,
                SourceCard = item.card,
                Candidates = request.Candidates,
                MinCount = Math.Min(request.Min, request.Candidates.Count),
                MaxCount = Math.Min(request.Max, request.Candidates.Count),
                Context = request.Context,
                OnCards = picks =>
                {
                    ctx.Targets.AddRange(picks ?? new List<DuelCard>());
                    Resolve();
                }
            });
        }

        /// <summary>After a Normal or Flip Summon resolves: summon triggers, Mysterious Puppeteer, then anything queued.</summary>
        private void AfterSummon(MonsterAbilityKind kind, int player, DuelCard card)
        {
            NotifySummoned(card, player, special: false);
            QueueMonsterAbility(kind, card, player);
            foreach (DuelistState d in Duelists)
            foreach (DuelMonsterState m in d.MonstersOnField.Where(m => m.IsFaceUp && m.Name == "Mysterious Puppeteer" && m.Card != card).ToList())
                GainLife(d.Index, 500, m.Card);
            RunQueuedMonsterAbilities();
        }

        /// <summary>Monsters taken by a Charmer go back when the Charmer leaves the field or is turned face-down.</summary>
        private void ReleaseControlHeldBy(DuelCard holder)
        {
            foreach (DuelistState d in Duelists)
            foreach (DuelMonsterState m in d.MonstersOnField.Where(m => m.ControlHeldBy == holder).ToList())
            {
                m.ControlHeldBy = null;
                if (m.Card.Owner != m.Card.Controller) SwitchControl(m, m.Card.Owner, temporary: false);
            }
        }

        // =================================================================== events

        internal void Raise(DuelEventType type, int player, DuelCard card = null, DuelCard other = null, int amount = 0, string text = null)
        {
            var e = new DuelEvent
            {
                Type = type,
                Player = player,
                Card = card,
                Other = other,
                Amount = amount,
                Text = text ?? type.ToString(),
                Turn = TurnNumber,
                Phase = Phase
            };
            History.Add(e);
            EventRaised?.Invoke(e);
        }
    }
}
