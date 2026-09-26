using System.Collections.Generic;
using System.Linq;
using DuelGenesis.Cards;
using DuelGenesis.Economy;
using DuelGenesis.Player;
using DuelGenesis.UI;
using UnityEngine;

namespace DuelGenesis.Dueling
{
    public class DuelPrototype : MonoBehaviour
    {
        private enum DuelPhase
        {
            Main,
            Battle,
            OpponentTurn,
            Finished
        }

        private enum MonsterPosition
        {
            FaceUpAttack,
            FaceUpDefense,
            FaceDownDefense
        }

        private sealed class FieldMonster
        {
            public CardData Card;
            public MonsterPosition Position;
            public bool HasAttacked;
            public bool HasChangedPosition;
            public bool EffectUsed;
            public bool CannotAttackThisTurn;
            public int AttackBonus;
            public int SummonedTurn;

            public int CurrentAttack => Mathf.Max(0, Card.attack + AttackBonus);
            public bool IsFaceDown => Position == MonsterPosition.FaceDownDefense;
            public bool IsAttackPosition => Position == MonsterPosition.FaceUpAttack;

            public FieldMonster(CardData card, MonsterPosition position, int summonedTurn)
            {
                Card = card;
                Position = position;
                SummonedTurn = summonedTurn;
            }
        }

        private sealed class SpellTrapCard
        {
            public CardData Card;
            public bool FaceDown;
            public int SetTurn;

            public SpellTrapCard(CardData card, bool faceDown, int setTurn)
            {
                Card = card;
                FaceDown = faceDown;
                SetTurn = setTurn;
            }
        }

        private readonly List<CardData> _playerDeck = new();
        private readonly List<CardData> _opponentDeck = new();
        private readonly List<CardData> _playerHand = new();
        private readonly List<CardData> _opponentHand = new();
        private readonly List<CardData> _playerGraveyard = new();
        private readonly List<CardData> _opponentGraveyard = new();
        private readonly List<CardData> _playerBanished = new();
        private readonly List<CardData> _opponentBanished = new();
        private readonly List<FieldMonster> _playerField = new();
        private readonly List<FieldMonster> _opponentField = new();
        private readonly List<SpellTrapCard> _playerSpellTrap = new();
        private readonly List<SpellTrapCard> _opponentSpellTrap = new();

        private GameObject _playerObject;
        private PlayerDeck _savedDeck;
        private PlayerCollection _collection;
        private GenesisWallet _wallet;
        private ThirdPersonPlayerController _playerController;
        private ThirdPersonCamera _cameraController;

        private DuelPhase _phase;
        private bool _active;
        private bool _duelOver;
        private bool _normalSummoned;
        private bool _rewardGranted;
        private bool _playerMarketUsedThisTurn;
        private bool _opponentMarketUsedThisTurn;
        private bool _playerActivatedSpellThisTurn;
        private bool _opponentActivatedSpellThisTurn;
        private int _turnNumber;
        private int _playerLP;
        private int _opponentLP;
        private string _message = string.Empty;
        private Vector2 _handScroll;

        public bool IsActive => _active;
        public bool IsDuelOver => _duelOver;

        public bool StartDuel(GameObject player)
        {
            if (_active || player == null)
                return false;

            _playerObject = player;
            _savedDeck = player.GetComponent<PlayerDeck>();
            _collection = player.GetComponent<PlayerCollection>();
            _wallet = player.GetComponent<GenesisWallet>();
            _playerController = player.GetComponent<ThirdPersonPlayerController>();
            _cameraController = Object.FindFirstObjectByType<ThirdPersonCamera>();

            if (_savedDeck == null)
            {
                Debug.LogWarning("Cannot start duel: player deck system is missing.");
                return false;
            }

            if (!_savedDeck.Validate(_collection, out string validation))
            {
                Debug.LogWarning("Cannot start duel: " + validation);
                return false;
            }

            BuildPlayerDeck();
            BuildOpponentDeck();

            if (_playerDeck.Count < PlayerDeck.MinimumDeckSize)
            {
                Debug.LogWarning("Cannot start duel: the saved deck could not be loaded into the duel engine.");
                return false;
            }

            _playerHand.Clear();
            _opponentHand.Clear();
            _playerGraveyard.Clear();
            _opponentGraveyard.Clear();
            _playerBanished.Clear();
            _opponentBanished.Clear();
            _playerField.Clear();
            _opponentField.Clear();
            _playerSpellTrap.Clear();
            _opponentSpellTrap.Clear();

            _playerLP = 8000;
            _opponentLP = 8000;
            _turnNumber = 1;
            _phase = DuelPhase.Main;
            _normalSummoned = false;
            _duelOver = false;
            _rewardGranted = false;
            _playerMarketUsedThisTurn = false;
            _opponentMarketUsedThisTurn = false;
            _playerActivatedSpellThisTurn = false;
            _opponentActivatedSpellThisTurn = false;
            _active = true;
            _handScroll = Vector2.zero;

            DrawPlayerCards(5);
            DrawOpponentCards(5);

            _playerController?.SetMovementEnabled(false);
            _cameraController?.SetLookEnabled(false);

            _message = "Turn 1 — Main Phase. Attack/Defense positions, monster setting, Spells and Traps are live.";
            Debug.Log("Duel: Genesis duel prototype v0.4 started. LP 8000 vs 8000.");
            return true;
        }

        private void BuildPlayerDeck()
        {
            _playerDeck.Clear();
            foreach (DeckEntry entry in _savedDeck.Entries)
            {
                CardData card = CardDatabase.GetById(entry.cardId);
                if (card == null) continue;
                for (int i = 0; i < entry.quantity; i++)
                    _playerDeck.Add(card);
            }
            Shuffle(_playerDeck);
        }

        private void BuildOpponentDeck()
        {
            _opponentDeck.Clear();
            IReadOnlyList<CardData> all = CardDatabase.All;

            for (int copy = 0; copy < 2 && _opponentDeck.Count < 40; copy++)
            {
                foreach (CardData card in all)
                {
                    _opponentDeck.Add(card);
                    if (_opponentDeck.Count >= 40) break;
                }
            }

            while (_opponentDeck.Count < 40 && all.Count > 0)
                _opponentDeck.Add(all[Random.Range(0, all.Count)]);

            Shuffle(_opponentDeck);
        }

        private static void Shuffle<T>(IList<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                T temp = list[i];
                list[i] = list[j];
                list[j] = temp;
            }
        }

        private bool DrawPlayerCards(int amount, bool outsideDrawPhase = false)
        {
            int drawn = 0;
            for (int i = 0; i < amount; i++)
            {
                if (_playerDeck.Count == 0)
                {
                    FinishDuel(false, "You ran out of cards to draw. Deck out!");
                    return false;
                }

                CardData card = _playerDeck[0];
                _playerDeck.RemoveAt(0);
                _playerHand.Add(card);
                drawn++;
            }

            if (outsideDrawPhase && drawn > 0 && HasFaceUpCard(_playerSpellTrap, "DG018") && !_playerMarketUsedThisTurn)
            {
                _playerLP += 300;
                _playerMarketUsedThisTurn = true;
            }

            return true;
        }

        private bool DrawOpponentCards(int amount, bool outsideDrawPhase = false)
        {
            int drawn = 0;
            for (int i = 0; i < amount; i++)
            {
                if (_opponentDeck.Count == 0)
                {
                    FinishDuel(true, "Opponent ran out of cards to draw. You win by deck out!");
                    return false;
                }

                CardData card = _opponentDeck[0];
                _opponentDeck.RemoveAt(0);
                _opponentHand.Add(card);
                drawn++;
            }

            if (outsideDrawPhase && drawn > 0 && HasFaceUpCard(_opponentSpellTrap, "DG018") && !_opponentMarketUsedThisTurn)
            {
                _opponentLP += 300;
                _opponentMarketUsedThisTurn = true;
            }

            return true;
        }

        private static bool HasFaceUpCard(List<SpellTrapCard> zones, string cardId)
        {
            return zones.Any(zone => !zone.FaceDown && zone.Card.id == cardId);
        }

        private int RequiredTributes(CardData card, bool playerSide)
        {
            if (card == null || card.kind != CardKind.Monster) return 0;

            bool activatedSpell = playerSide ? _playerActivatedSpellThisTurn : _opponentActivatedSpellThisTurn;
            if (card.id == "DG013" && activatedSpell)
                return 0;

            if (card.level >= 7) return 2;
            if (card.level >= 5) return 1;
            return 0;
        }

        private bool CanSummon(CardData card)
        {
            return card != null &&
                   card.kind == CardKind.Monster &&
                   _phase == DuelPhase.Main &&
                   !_normalSummoned &&
                   _playerField.Count < 5 &&
                   _playerField.Count >= RequiredTributes(card, true);
        }

        private void SummonFromHand(CardData card, MonsterPosition position)
        {
            if (!CanSummon(card)) return;

            int tributes = RequiredTributes(card, true);
            for (int i = 0; i < tributes; i++)
            {
                FieldMonster tribute = _playerField.OrderBy(monster => GetDisplayedAttack(monster, true)).First();
                SendMonsterToGraveyard(tribute, true);
            }

            _playerHand.Remove(card);
            FieldMonster summoned = new FieldMonster(card, position, _turnNumber);
            _playerField.Add(summoned);
            _normalSummoned = true;

            if (position == MonsterPosition.FaceDownDefense)
            {
                _message = tributes > 0
                    ? $"Tributed {tributes} monster(s) and Set a monster face-down in Defense Position."
                    : "Set a monster face-down in Defense Position.";
                return;
            }

            _message = tributes > 0
                ? $"Tributed {tributes} monster(s) and summoned {card.cardName} in Attack Position."
                : $"Normal Summoned {card.cardName} in Attack Position.";

            ResolveSummonEffects(summoned, true, tributes > 0, false);
        }

        private bool CanSpecialSummonValkyrie(CardData card)
        {
            return card != null && card.id == "DG020" && _phase == DuelPhase.Main && _playerField.Count == 0;
        }

        private void SpecialSummonValkyrie(CardData card)
        {
            if (!CanSpecialSummonValkyrie(card)) return;

            _playerHand.Remove(card);
            FieldMonster monster = new FieldMonster(card, MonsterPosition.FaceUpAttack, _turnNumber)
            {
                AttackBonus = -700
            };
            _playerField.Add(monster);
            _message = "Genesis Valkyrie Special Summoned itself. Its ATK is 2000 until the end of this turn.";
            ResolveSummonEffects(monster, true, false, true);
        }

        private void ResolveSummonEffects(FieldMonster monster, bool ownerIsPlayer, bool tributeSummon, bool specialSummon)
        {
            if (monster == null || monster.IsFaceDown) return;

            if (monster.Card.id == "DG001")
            {
                if (ownerIsPlayer)
                {
                    DrawPlayerCards(1, true);
                    if (_duelOver) return;
                    CardData bottom = _playerHand.LastOrDefault();
                    if (bottom != null)
                    {
                        _playerHand.Remove(bottom);
                        _playerDeck.Add(bottom);
                    }
                    _message = "Genesis Apprentice resolved: drew 1 card, then placed 1 card from hand on the bottom of the Deck.";
                }
                else
                {
                    DrawOpponentCards(1, true);
                    if (_duelOver) return;
                    CardData bottom = _opponentHand.LastOrDefault();
                    if (bottom != null)
                    {
                        _opponentHand.Remove(bottom);
                        _opponentDeck.Add(bottom);
                    }
                }
            }

            if (monster.Card.id == "DG024" && tributeSummon)
            {
                monster.CannotAttackThisTurn = true;
                BounceUpToTwoOpponentCards(ownerIsPlayer);
                _message = ownerIsPlayer
                    ? "Genesis Leviathan returned up to 2 opposing cards to the hand. It cannot attack this turn."
                    : "CPU's Genesis Leviathan returned up to 2 of your cards to your hand.";
            }

            if (monster.Card.id == "DG015" && specialSummon)
            {
                List<CardData> deck = ownerIsPlayer ? _playerDeck : _opponentDeck;
                if (deck.Count > 0 && deck[0].kind == CardKind.Monster)
                {
                    monster.AttackBonus += 400;
                    if (ownerIsPlayer)
                        _message = "Holo Dragon revealed a Monster Card and gained 400 ATK this turn.";
                }
            }
        }

        private void BounceUpToTwoOpponentCards(bool sourceOwnerIsPlayer)
        {
            List<FieldMonster> enemyField = sourceOwnerIsPlayer ? _opponentField : _playerField;
            List<SpellTrapCard> enemyBackrow = sourceOwnerIsPlayer ? _opponentSpellTrap : _playerSpellTrap;
            List<CardData> enemyHand = sourceOwnerIsPlayer ? _opponentHand : _playerHand;
            int returned = 0;

            while (enemyField.Count > 0 && returned < 2)
            {
                FieldMonster target = enemyField.OrderByDescending(monster => monster.Card.level).First();
                enemyField.Remove(target);
                enemyHand.Add(target.Card);
                returned++;
            }

            while (enemyBackrow.Count > 0 && returned < 2)
            {
                SpellTrapCard target = enemyBackrow[0];
                enemyBackrow.RemoveAt(0);
                enemyHand.Add(target.Card);
                returned++;
            }
        }

        private int GetDisplayedAttack(FieldMonster monster, bool ownerIsPlayer)
        {
            if (monster == null) return 0;
            int attack = monster.CurrentAttack;

            if (monster.Card.id == "DG002" && ControlsFaceUpSpellcaster(ownerIsPlayer))
                attack += 400;

            return Mathf.Max(0, attack);
        }

        private int GetBattleAttack(FieldMonster monster, bool ownerIsPlayer)
        {
            int attack = GetDisplayedAttack(monster, ownerIsPlayer);
            if (monster != null && monster.Card.id == "DG004")
                attack += 200;
            return Mathf.Max(0, attack);
        }

        private bool ControlsFaceUpSpellcaster(bool playerSide)
        {
            List<FieldMonster> field = playerSide ? _playerField : _opponentField;
            return field.Any(monster => !monster.IsFaceDown && monster.Card.typeLine.Contains("Spellcaster"));
        }

        private bool CanChangePosition(FieldMonster monster)
        {
            return monster != null &&
                   _phase == DuelPhase.Main &&
                   !monster.HasChangedPosition &&
                   !monster.HasAttacked &&
                   monster.SummonedTurn < _turnNumber;
        }

        private void ChangePosition(FieldMonster monster)
        {
            if (!CanChangePosition(monster)) return;

            if (monster.Position == MonsterPosition.FaceDownDefense)
            {
                monster.Position = MonsterPosition.FaceUpAttack;
                monster.HasChangedPosition = true;
                _message = $"Flip Summoned {monster.Card.cardName} into Attack Position.";
                ResolveSummonEffects(monster, true, false, false);
                return;
            }

            monster.Position = monster.Position == MonsterPosition.FaceUpAttack
                ? MonsterPosition.FaceUpDefense
                : MonsterPosition.FaceUpAttack;
            monster.HasChangedPosition = true;
            _message = $"Changed {monster.Card.cardName} to {(monster.IsAttackPosition ? "Attack" : "Defense")} Position.";
        }

        private bool CanUseMonsterEffect(FieldMonster monster, bool playerSide)
        {
            if (monster == null || monster.IsFaceDown || monster.EffectUsed || _phase != DuelPhase.Main)
                return false;

            if (monster.Card.id == "DG005")
                return playerSide ? _playerHand.Count > 0 : _opponentHand.Count > 0;

            if (monster.Card.id == "DG023")
                return playerSide ? _opponentSpellTrap.Count > 0 : _playerSpellTrap.Count > 0;

            return false;
        }

        private void ActivateMonsterEffect(FieldMonster monster, bool playerSide)
        {
            if (!CanUseMonsterEffect(monster, playerSide)) return;

            if (TryNegateMonsterEffect(playerSide, monster))
                return;

            monster.EffectUsed = true;

            if (monster.Card.id == "DG005")
            {
                List<CardData> hand = playerSide ? _playerHand : _opponentHand;
                List<CardData> graveyard = playerSide ? _playerGraveyard : _opponentGraveyard;
                CardData discarded = hand[0];
                hand.RemoveAt(0);
                graveyard.Add(discarded);

                if (playerSide) _playerLP += 500;
                else _opponentLP += 500;

                _message = playerSide
                    ? $"Alley Alchemist discarded {discarded.cardName}; you gained 500 LP."
                    : "CPU used Alley Alchemist and gained 500 LP.";
                return;
            }

            if (monster.Card.id == "DG023")
            {
                List<SpellTrapCard> enemyZones = playerSide ? _opponentSpellTrap : _playerSpellTrap;
                List<CardData> enemyBanished = playerSide ? _opponentBanished : _playerBanished;
                SpellTrapCard target = enemyZones[0];
                enemyZones.RemoveAt(0);
                enemyBanished.Add(target.Card);
                _message = playerSide
                    ? $"Genesis Archmage banished {target.Card.cardName}."
                    : "CPU's Genesis Archmage banished one of your Spell/Trap cards.";
            }
        }

        private bool TryNegateMonsterEffect(bool effectOwnerIsPlayer, FieldMonster source)
        {
            List<SpellTrapCard> defenderZones = effectOwnerIsPlayer ? _opponentSpellTrap : _playerSpellTrap;
            List<CardData> defenderGraveyard = effectOwnerIsPlayer ? _opponentGraveyard : _playerGraveyard;
            int defenderLP = effectOwnerIsPlayer ? _opponentLP : _playerLP;

            SpellTrapCard counter = defenderZones.FirstOrDefault(zone =>
                zone.FaceDown && zone.Card.id == "DG022" && zone.SetTurn < _turnNumber);

            if (counter == null || defenderLP < 800)
                return false;

            defenderZones.Remove(counter);
            defenderGraveyard.Add(counter.Card);
            if (effectOwnerIsPlayer) _opponentLP -= 800;
            else _playerLP -= 800;

            SendMonsterToGraveyard(source, effectOwnerIsPlayer);
            _message = effectOwnerIsPlayer
                ? "CPU activated Zero-Latency Counter: your monster effect was negated and the monster was destroyed."
                : "Zero-Latency Counter negated the CPU monster effect and destroyed that monster.";
            CheckLifePoints();
            return true;
        }

        private bool IsSupportedSpell(CardData card)
        {
            if (card == null || card.kind != CardKind.Spell) return false;
            return card.id == "DG009" || card.id == "DG010" || card.id == "DG016" || card.id == "DG018";
        }

        private bool CanActivatePlayerSpell(CardData card)
        {
            if (_phase != DuelPhase.Main || card == null || card.kind != CardKind.Spell || !IsSupportedSpell(card))
                return false;

            if (_playerSpellTrap.Count >= 5 && card.id == "DG018")
                return false;
            if (card.id == "DG009") return _playerField.Count > 0;
            if (card.id == "DG010") return _playerGraveyard.Any(c => c.kind == CardKind.Monster);
            if (card.id == "DG016") return _playerDeck.Any(c => c.kind == CardKind.Monster && c.level <= 4 && c.typeLine.Contains("Spellcaster"));
            if (card.id == "DG018") return !HasFaceUpCard(_playerSpellTrap, "DG018");
            return false;
        }

        private void ActivatePlayerSpell(CardData card)
        {
            if (!CanActivatePlayerSpell(card)) return;
            _playerHand.Remove(card);
            _playerActivatedSpellThisTurn = true;

            if (TryNegateSpell(true, card))
            {
                _playerGraveyard.Add(card);
                _message = $"CPU's Signal Jam negated {card.cardName}.";
                return;
            }

            if (card.id == "DG009")
            {
                FieldMonster target = _playerField.OrderByDescending(monster => GetDisplayedAttack(monster, true)).First();
                target.AttackBonus += 500;
                _playerGraveyard.Add(card);
                _message = $"Quick Charge activated! {target.Card.cardName} gained 500 ATK until the end of the turn.";
                return;
            }

            if (card.id == "DG010")
            {
                CardData recycled = _playerGraveyard.Last(c => c.kind == CardKind.Monster);
                _playerGraveyard.Remove(recycled);
                _playerDeck.Add(recycled);
                Shuffle(_playerDeck);
                _playerGraveyard.Add(card);
                DrawPlayerCards(1, true);
                _message = $"Genesis Recycle returned {recycled.cardName} to the Deck and drew 1 card.";
                return;
            }

            if (card.id == "DG016")
            {
                CardData searched = _playerDeck.First(c => c.kind == CardKind.Monster && c.level <= 4 && c.typeLine.Contains("Spellcaster"));
                _playerDeck.Remove(searched);
                _playerHand.Add(searched);
                _playerGraveyard.Add(card);

                CardData discard = _playerHand.FirstOrDefault(c => c != searched) ?? searched;
                _playerHand.Remove(discard);
                _playerGraveyard.Add(discard);
                _message = $"Arcane Transit added {searched.cardName} to your hand, then discarded {discard.cardName}.";
                return;
            }

            if (card.id == "DG018")
            {
                _playerSpellTrap.Add(new SpellTrapCard(card, false, _turnNumber));
                _message = "Genesis Market is active. Your first extra draw each turn restores 300 LP.";
            }
        }

        private bool CanSetTrap(CardData card)
        {
            return card != null && card.kind == CardKind.Trap && _phase == DuelPhase.Main && _playerSpellTrap.Count < 5;
        }

        private void SetTrapFromHand(CardData card)
        {
            if (!CanSetTrap(card)) return;
            _playerHand.Remove(card);
            _playerSpellTrap.Add(new SpellTrapCard(card, true, _turnNumber));
            _message = $"Set {card.cardName} face-down.";
        }

        private bool TryNegateSpell(bool spellCasterIsPlayer, CardData spell)
        {
            List<SpellTrapCard> defenderZones = spellCasterIsPlayer ? _opponentSpellTrap : _playerSpellTrap;
            List<CardData> defenderGraveyard = spellCasterIsPlayer ? _opponentGraveyard : _playerGraveyard;
            List<CardData> defenderHand = spellCasterIsPlayer ? _opponentHand : _playerHand;

            SpellTrapCard signalJam = defenderZones.FirstOrDefault(zone =>
                zone.FaceDown && zone.Card.id == "DG012" && zone.SetTurn < _turnNumber);

            if (signalJam == null || defenderHand.Count == 0)
                return false;

            CardData discard = defenderHand[0];
            defenderHand.RemoveAt(0);
            defenderGraveyard.Add(discard);
            defenderZones.Remove(signalJam);
            defenderGraveyard.Add(signalJam.Card);
            return true;
        }

        private int TriggerBackAlleyAmbush(bool defendingPlayer, string attackerName)
        {
            List<SpellTrapCard> zones = defendingPlayer ? _playerSpellTrap : _opponentSpellTrap;
            List<CardData> graveyard = defendingPlayer ? _playerGraveyard : _opponentGraveyard;

            SpellTrapCard trap = zones.FirstOrDefault(zone =>
                zone.FaceDown && zone.Card.id == "DG011" && zone.SetTurn < _turnNumber);

            if (trap == null)
                return 0;

            zones.Remove(trap);
            graveyard.Add(trap.Card);
            _message = $"Back Alley Ambush activated! {attackerName} loses 700 ATK for this battle.";
            return 700;
        }

        private bool TriggerRankedBarrier(bool defendingPlayer, int battleDamage)
        {
            if (battleDamage < 1500)
                return false;

            List<SpellTrapCard> zones = defendingPlayer ? _playerSpellTrap : _opponentSpellTrap;
            List<CardData> graveyard = defendingPlayer ? _playerGraveyard : _opponentGraveyard;

            SpellTrapCard trap = zones.FirstOrDefault(zone =>
                zone.FaceDown && zone.Card.id == "DG017" && zone.SetTurn < _turnNumber);

            if (trap == null)
                return false;

            zones.Remove(trap);
            graveyard.Add(trap.Card);
            _message = $"Ranked Barrier activated and prevented {battleDamage} battle damage!";
            return true;
        }

        private void AdvancePhase()
        {
            if (_duelOver) return;

            if (_phase == DuelPhase.Main)
            {
                if (_turnNumber == 1)
                {
                    EndPlayerTurn();
                    return;
                }

                _phase = DuelPhase.Battle;
                _message = "Battle Phase — only face-up Attack Position monsters can attack.";
                return;
            }

            if (_phase == DuelPhase.Battle)
                EndPlayerTurn();
        }

        private FieldMonster ChooseAttackTarget(List<FieldMonster> defenders)
        {
            FieldMonster guardian = defenders.FirstOrDefault(monster =>
                !monster.IsFaceDown && monster.Card.id == "DG007" && monster.Position == MonsterPosition.FaceUpDefense);
            if (guardian != null)
                return guardian;

            return defenders.FirstOrDefault();
        }

        private void PlayerAttack(FieldMonster attacker)
        {
            if (_duelOver || _phase != DuelPhase.Battle || attacker == null || attacker.HasAttacked ||
                !attacker.IsAttackPosition || attacker.CannotAttackThisTurn)
                return;

            attacker.HasAttacked = true;
            int attackValue = Mathf.Max(0, GetBattleAttack(attacker, true) - TriggerBackAlleyAmbush(false, attacker.Card.cardName));

            if (_opponentField.Count == 0)
            {
                int damage = attackValue;
                if (!TriggerRankedBarrier(false, damage))
                {
                    _opponentLP -= damage;
                    _message = $"{attacker.Card.cardName} attacked directly for {damage} damage!";
                }
                CheckLifePoints();
                return;
            }

            FieldMonster defender = ChooseAttackTarget(_opponentField);
            ResolveBattle(attacker, true, defender, attackValue);
        }

        private void ResolveBattle(FieldMonster attacker, bool attackerIsPlayer, FieldMonster defender, int attackValue)
        {
            if (defender == null) return;

            if (defender.IsFaceDown)
            {
                defender.Position = MonsterPosition.FaceUpDefense;
                _message = $"{defender.Card.cardName} was flipped face-up in Defense Position.";
            }

            bool defenderIsPlayer = !attackerIsPlayer;
            int defenderValue = defender.IsAttackPosition
                ? GetDisplayedAttack(defender, defenderIsPlayer)
                : defender.Card.defense;

            int difference = attackValue - defenderValue;

            if (defender.IsAttackPosition)
            {
                if (difference > 0)
                {
                    if (!TryProtectWithChromePaladin(defenderIsPlayer, defender))
                    {
                        SendMonsterToGraveyard(defender, defenderIsPlayer);
                        RewardBattleDestroy(attacker, attackerIsPlayer);
                    }

                    if (!TriggerRankedBarrier(defenderIsPlayer, difference))
                    {
                        if (defenderIsPlayer) _playerLP -= difference;
                        else _opponentLP -= difference;
                    }
                    _message = $"{attacker.Card.cardName} battled {defender.Card.cardName}. {difference} battle damage was dealt.";
                }
                else if (difference < 0)
                {
                    int damage = -difference;
                    if (!TryProtectWithChromePaladin(attackerIsPlayer, attacker))
                        SendMonsterToGraveyard(attacker, attackerIsPlayer);
                    RewardBattleDestroy(defender, defenderIsPlayer);

                    if (!TriggerRankedBarrier(attackerIsPlayer, damage))
                    {
                        if (attackerIsPlayer) _playerLP -= damage;
                        else _opponentLP -= damage;
                    }
                    _message = $"{attacker.Card.cardName} lost the battle and its controller took {damage} damage.";
                }
                else
                {
                    bool attackerProtected = TryProtectWithChromePaladin(attackerIsPlayer, attacker);
                    bool defenderProtected = TryProtectWithChromePaladin(defenderIsPlayer, defender);
                    if (!attackerProtected) SendMonsterToGraveyard(attacker, attackerIsPlayer);
                    if (!defenderProtected) SendMonsterToGraveyard(defender, defenderIsPlayer);
                    _message = "Equal ATK — both Attack Position monsters were destroyed unless protected.";
                }
            }
            else
            {
                if (difference > 0)
                {
                    if (!TryProtectWithChromePaladin(defenderIsPlayer, defender))
                    {
                        SendMonsterToGraveyard(defender, defenderIsPlayer);
                        RewardBattleDestroy(attacker, attackerIsPlayer);
                    }
                    _message = $"{attacker.Card.cardName} broke through {defender.Card.cardName}'s DEF. No LP damage was dealt.";
                }
                else if (difference < 0)
                {
                    int damage = -difference;
                    if (!TriggerRankedBarrier(attackerIsPlayer, damage))
                    {
                        if (attackerIsPlayer) _playerLP -= damage;
                        else _opponentLP -= damage;
                    }
                    _message = $"The attack failed against {defender.Card.cardName}'s DEF. The attacker took {damage} battle damage.";
                }
                else
                {
                    _message = "ATK matched DEF. No monster was destroyed and no LP damage was dealt.";
                }
            }

            CheckLifePoints();
        }

        private bool TryProtectWithChromePaladin(bool ownerIsPlayer, FieldMonster victim)
        {
            List<FieldMonster> field = ownerIsPlayer ? _playerField : _opponentField;
            FieldMonster paladin = field.FirstOrDefault(monster =>
                monster != victim && !monster.IsFaceDown && monster.Card.id == "DG014" && GetDisplayedAttack(monster, ownerIsPlayer) >= 500);

            if (paladin == null)
                return false;

            paladin.AttackBonus -= 500;
            return true;
        }

        private void RewardBattleDestroy(FieldMonster attacker, bool ownerIsPlayer)
        {
            if (attacker == null || attacker.Card.id != "DG003")
                return;

            if (ownerIsPlayer) _playerLP += 300;
            else _opponentLP += 300;
        }

        private void SendMonsterToGraveyard(FieldMonster monster, bool ownerIsPlayer)
        {
            if (monster == null) return;

            List<FieldMonster> field = ownerIsPlayer ? _playerField : _opponentField;
            List<CardData> graveyard = ownerIsPlayer ? _playerGraveyard : _opponentGraveyard;
            if (!field.Remove(monster)) return;

            graveyard.Add(monster.Card);

            if (monster.Card.id == "DG006")
            {
                if (ownerIsPlayer) _opponentLP -= 200;
                else _playerLP -= 200;
            }
        }

        private void EndPlayerTurn()
        {
            if (_duelOver) return;

            foreach (FieldMonster monster in _playerField)
                monster.AttackBonus = 0;

            _phase = DuelPhase.OpponentTurn;
            _turnNumber++;
            _opponentMarketUsedThisTurn = false;
            _opponentActivatedSpellThisTurn = false;
            PrepareFieldForTurn(_opponentField);
            RunOpponentTurn();
        }

        private static void PrepareFieldForTurn(List<FieldMonster> field)
        {
            foreach (FieldMonster monster in field)
            {
                monster.HasAttacked = false;
                monster.HasChangedPosition = false;
                monster.EffectUsed = false;
                monster.CannotAttackThisTurn = false;
            }
        }

        private void RunOpponentTurn()
        {
            if (_duelOver) return;
            if (!DrawOpponentCards(1)) return;

            OpponentActivateOneSpell();
            if (_duelOver) return;

            OpponentSpecialSummonValkyrie();
            OpponentNormalSummon();
            OpponentUseMonsterEffect();
            OpponentSetOneTrap();
            if (_duelOver) return;

            List<FieldMonster> attackers = new List<FieldMonster>(_opponentField);
            foreach (FieldMonster attacker in attackers)
            {
                if (_duelOver) break;
                if (_opponentField.Contains(attacker) && attacker.IsAttackPosition && !attacker.CannotAttackThisTurn)
                    OpponentAttack(attacker);
            }

            foreach (FieldMonster monster in _opponentField)
                monster.AttackBonus = 0;

            if (_duelOver) return;

            _turnNumber++;
            _normalSummoned = false;
            _playerMarketUsedThisTurn = false;
            _playerActivatedSpellThisTurn = false;
            PrepareFieldForTurn(_playerField);
            _phase = DuelPhase.Main;

            if (!DrawPlayerCards(1)) return;
            _message = $"Turn {_turnNumber} — Draw complete. Main Phase begins.";
        }

        private void OpponentActivateOneSpell()
        {
            CardData spell = _opponentHand.FirstOrDefault(card =>
                card.kind == CardKind.Spell &&
                card.id != "DG021" &&
                ((card.id == "DG009" && _opponentField.Count > 0) ||
                 (card.id == "DG010" && _opponentGraveyard.Any(c => c.kind == CardKind.Monster)) ||
                 (card.id == "DG016" && _opponentDeck.Any(c => c.kind == CardKind.Monster && c.level <= 4 && c.typeLine.Contains("Spellcaster"))) ||
                 (card.id == "DG018" && _opponentSpellTrap.Count < 5 && !HasFaceUpCard(_opponentSpellTrap, "DG018"))));

            if (spell == null) return;
            _opponentHand.Remove(spell);
            _opponentActivatedSpellThisTurn = true;

            if (TryNegateSpell(false, spell))
            {
                _opponentGraveyard.Add(spell);
                _message = $"Your Signal Jam negated CPU's {spell.cardName}!";
                return;
            }

            if (spell.id == "DG009")
            {
                FieldMonster target = _opponentField.OrderByDescending(monster => GetDisplayedAttack(monster, false)).First();
                target.AttackBonus += 500;
                _opponentGraveyard.Add(spell);
                return;
            }

            if (spell.id == "DG010")
            {
                CardData recycled = _opponentGraveyard.Last(c => c.kind == CardKind.Monster);
                _opponentGraveyard.Remove(recycled);
                _opponentDeck.Add(recycled);
                Shuffle(_opponentDeck);
                _opponentGraveyard.Add(spell);
                DrawOpponentCards(1, true);
                return;
            }

            if (spell.id == "DG016")
            {
                CardData searched = _opponentDeck.First(c => c.kind == CardKind.Monster && c.level <= 4 && c.typeLine.Contains("Spellcaster"));
                _opponentDeck.Remove(searched);
                _opponentHand.Add(searched);
                _opponentGraveyard.Add(spell);
                CardData discard = _opponentHand.FirstOrDefault(c => c != searched) ?? searched;
                _opponentHand.Remove(discard);
                _opponentGraveyard.Add(discard);
                return;
            }

            if (spell.id == "DG018")
                _opponentSpellTrap.Add(new SpellTrapCard(spell, false, _turnNumber));
        }

        private void OpponentSpecialSummonValkyrie()
        {
            if (_opponentField.Count != 0) return;
            CardData valkyrie = _opponentHand.FirstOrDefault(card => card.id == "DG020");
            if (valkyrie == null) return;

            _opponentHand.Remove(valkyrie);
            FieldMonster monster = new FieldMonster(valkyrie, MonsterPosition.FaceUpAttack, _turnNumber)
            {
                AttackBonus = -700
            };
            _opponentField.Add(monster);
            ResolveSummonEffects(monster, false, false, true);
        }

        private void OpponentSetOneTrap()
        {
            if (_opponentSpellTrap.Count >= 5) return;
            CardData trap = _opponentHand.FirstOrDefault(card => card.kind == CardKind.Trap);
            if (trap == null) return;

            _opponentHand.Remove(trap);
            _opponentSpellTrap.Add(new SpellTrapCard(trap, true, _turnNumber));
        }

        private void OpponentNormalSummon()
        {
            if (_opponentField.Count >= 5) return;

            CardData choice = _opponentHand
                .Where(card => card.kind == CardKind.Monster)
                .Where(card => RequiredTributes(card, false) <= _opponentField.Count)
                .OrderByDescending(card => Mathf.Max(card.attack, card.defense))
                .FirstOrDefault();

            if (choice == null) return;

            int tributes = RequiredTributes(choice, false);
            for (int i = 0; i < tributes; i++)
            {
                FieldMonster tribute = _opponentField.OrderBy(monster => GetDisplayedAttack(monster, false)).First();
                SendMonsterToGraveyard(tribute, false);
            }

            _opponentHand.Remove(choice);
            bool preferDefense = choice.defense >= choice.attack + 300;
            MonsterPosition position = preferDefense ? MonsterPosition.FaceDownDefense : MonsterPosition.FaceUpAttack;
            FieldMonster summoned = new FieldMonster(choice, position, _turnNumber);
            _opponentField.Add(summoned);

            if (!preferDefense)
                ResolveSummonEffects(summoned, false, tributes > 0, false);
        }

        private void OpponentUseMonsterEffect()
        {
            FieldMonster effectMonster = _opponentField.FirstOrDefault(monster => CanUseMonsterEffect(monster, false));
            if (effectMonster != null)
                ActivateMonsterEffect(effectMonster, false);
        }

        private void OpponentAttack(FieldMonster attacker)
        {
            if (_duelOver || attacker == null || !attacker.IsAttackPosition) return;

            attacker.HasAttacked = true;
            int attackValue = Mathf.Max(0, GetBattleAttack(attacker, false) - TriggerBackAlleyAmbush(true, attacker.Card.cardName));

            if (_playerField.Count == 0)
            {
                int damage = attackValue;
                if (!TriggerRankedBarrier(true, damage))
                {
                    _playerLP -= damage;
                    _message = $"CPU's {attacker.Card.cardName} attacked directly for {damage} damage.";
                }
                CheckLifePoints();
                return;
            }

            FieldMonster defender = ChooseAttackTarget(_playerField);
            ResolveBattle(attacker, false, defender, attackValue);
        }

        private void CheckLifePoints()
        {
            _playerLP = Mathf.Max(0, _playerLP);
            _opponentLP = Mathf.Max(0, _opponentLP);

            if (_playerLP <= 0)
                FinishDuel(false, "Your Life Points reached 0. Duel lost.");
            else if (_opponentLP <= 0)
                FinishDuel(true, "Opponent's Life Points reached 0. Victory!");
        }

        private void FinishDuel(bool playerWon, string result)
        {
            if (_duelOver) return;

            _duelOver = true;
            _phase = DuelPhase.Finished;
            _message = result;

            if (!_rewardGranted && _wallet != null)
            {
                int reward = playerWon ? 400 : 200;
                _wallet.Add(reward);
                _message += $"  Reward: {reward} GC.";
                _rewardGranted = true;
            }
        }

        private void Forfeit()
        {
            if (_duelOver) return;
            _duelOver = true;
            _phase = DuelPhase.Finished;
            _message = "Duel forfeited. No GC reward granted.";
            _rewardGranted = true;
        }

        public void CloseDuel()
        {
            if (!_active) return;

            _active = false;
            _duelOver = false;
            _playerController?.SetMovementEnabled(false);
            _cameraController?.SetLookEnabled(true);

            _playerObject = null;
            _savedDeck = null;
            _collection = null;
            _wallet = null;
            _playerController = null;
            _cameraController = null;
        }

        private string PhaseLabel()
        {
            return _phase switch
            {
                DuelPhase.Main => "MAIN PHASE",
                DuelPhase.Battle => "BATTLE PHASE",
                DuelPhase.OpponentTurn => "OPPONENT TURN",
                DuelPhase.Finished => "DUEL COMPLETE",
                _ => _phase.ToString().ToUpperInvariant()
            };
        }

        private string PositionLabel(FieldMonster monster, bool hideFaceDown)
        {
            if (monster == null) return string.Empty;
            if (monster.IsFaceDown && hideFaceDown) return "FACE-DOWN DEFENSE";

            return monster.Position switch
            {
                MonsterPosition.FaceUpAttack => "ATTACK",
                MonsterPosition.FaceUpDefense => "DEFENSE",
                MonsterPosition.FaceDownDefense => "SET / DEFENSE",
                _ => string.Empty
            };
        }

        private void OnGUI()
        {
            if (!_active) return;

            GUI.depth = -300;

            Rect screen = new Rect(12f, 12f, Screen.width - 24f, Screen.height - 24f);
            GenesisTheme.Box(screen, GenesisTheme.Background);

            GUIStyle title = new GUIStyle(GUI.skin.label)
            {
                fontSize = 25,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = GenesisTheme.Cyan }
            };
            GUIStyle header = new GUIStyle(GUI.skin.label)
            {
                fontSize = 17,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };
            GUIStyle body = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                wordWrap = true,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };
            GUIStyle small = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                wordWrap = true,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };

            GUI.Label(new Rect(screen.x + 20f, screen.y + 6f, screen.width - 40f, 34f), "DUEL: GENESIS — NEON TABLETOP DUEL", title);
            GUI.Label(new Rect(screen.x + 20f, screen.y + 40f, screen.width - 40f, 25f),
                $"YOU  {_playerLP:N0} LP        TURN {_turnNumber} • {PhaseLabel()}        CPU  {_opponentLP:N0} LP", header);
            GUI.Label(new Rect(screen.x + 30f, screen.y + 66f, screen.width - 60f, 38f), _message, body);

            float y = screen.y + 105f;
            DrawMonsterArea(new Rect(screen.x + 26f, y, screen.width - 52f, 138f), _opponentField, false, small);
            y += 144f;
            DrawSpellTrapArea(new Rect(screen.x + 26f, y, screen.width - 52f, 68f), _opponentSpellTrap, _opponentGraveyard.Count, _opponentBanished.Count, true, small);
            y += 74f;
            DrawMonsterArea(new Rect(screen.x + 26f, y, screen.width - 52f, 150f), _playerField, true, small);
            y += 156f;
            DrawSpellTrapArea(new Rect(screen.x + 26f, y, screen.width - 52f, 72f), _playerSpellTrap, _playerGraveyard.Count, _playerBanished.Count, false, small);
            y += 78f;

            if (_duelOver)
            {
                Rect result = new Rect(screen.center.x - 270f, screen.center.y - 75f, 540f, 160f);
                GenesisTheme.Box(result, GenesisTheme.PanelAlt);
                GUI.Label(new Rect(result.x + 18f, result.y + 18f, result.width - 36f, 65f), _message, header);
                if (GenesisTheme.Button(new Rect(result.center.x - 95f, result.yMax - 55f, 190f, 36f), "RETURN TO TABLE", GenesisTheme.Purple))
                    CloseDuel();
                return;
            }

            DrawPhaseControls(new Rect(screen.x + 26f, y, screen.width - 52f, 48f), body);
            y += 54f;
            DrawHand(new Rect(screen.x + 26f, y, screen.width - 52f, Mathf.Max(145f, screen.yMax - y - 15f)), small);
        }

        private void DrawMonsterArea(Rect area, List<FieldMonster> monsters, bool playerSide, GUIStyle style)
        {
            GenesisTheme.Box(area, playerSide ? new Color(0.04f, 0.18f, 0.24f, 1f) : new Color(0.22f, 0.05f, 0.16f, 1f));
            GUI.Label(new Rect(area.x + 8f, area.y + 3f, area.width - 16f, 20f),
                $"{(playerSide ? "YOUR" : "CPU")} MONSTER ZONES — Hand: {(playerSide ? _playerHand.Count : _opponentHand.Count)}   Deck: {(playerSide ? _playerDeck.Count : _opponentDeck.Count)}", style);

            Rect zonesArea = new Rect(area.x + 10f, area.y + 25f, area.width - 20f, area.height - 30f);
            float gap = 8f;
            float zoneWidth = (zonesArea.width - gap * 4f) / 5f;

            for (int i = 0; i < 5; i++)
            {
                Rect zone = new Rect(zonesArea.x + i * (zoneWidth + gap), zonesArea.y, zoneWidth, zonesArea.height);
                GenesisTheme.Box(zone, playerSide ? new Color(0.07f, 0.26f, 0.32f, 1f) : new Color(0.31f, 0.08f, 0.22f, 1f));

                if (i >= monsters.Count)
                {
                    GUI.Label(zone, "EMPTY\nMONSTER ZONE", style);
                    continue;
                }

                FieldMonster monster = monsters[i];
                bool hideIdentity = !playerSide && monster.IsFaceDown;
                string name = hideIdentity ? "FACE-DOWN MONSTER" : monster.Card.cardName;
                string stats = hideIdentity
                    ? "DEFENSE POSITION"
                    : $"ATK {GetDisplayedAttack(monster, playerSide)} / DEF {monster.Card.defense}\n{PositionLabel(monster, false)}";

                GUI.Label(new Rect(zone.x + 4f, zone.y + 3f, zone.width - 8f, zone.height - 50f),
                    $"{name}\n{stats}", style);

                if (!playerSide) continue;

                if (_phase == DuelPhase.Battle)
                {
                    bool canAttack = monster.IsAttackPosition && !monster.HasAttacked && !monster.CannotAttackThisTurn;
                    GUI.enabled = canAttack;
                    string attackLabel = monster.HasAttacked ? "ATTACKED" : monster.CannotAttackThisTurn ? "NO ATTACK" : "ATTACK";
                    if (GenesisTheme.Button(new Rect(zone.x + 5f, zone.yMax - 29f, zone.width - 10f, 25f), attackLabel, GenesisTheme.Cyan))
                        PlayerAttack(monster);
                    GUI.enabled = true;
                }
                else if (_phase == DuelPhase.Main)
                {
                    bool canChange = CanChangePosition(monster);
                    GUI.enabled = canChange;
                    string changeLabel = monster.IsFaceDown ? "FLIP SUMMON" : monster.IsAttackPosition ? "TO DEF" : "TO ATK";
                    if (GenesisTheme.Button(new Rect(zone.x + 4f, zone.yMax - 48f, zone.width - 8f, 21f), changeLabel, GenesisTheme.Purple))
                        ChangePosition(monster);
                    GUI.enabled = true;

                    bool canEffect = CanUseMonsterEffect(monster, true);
                    if (canEffect)
                    {
                        GUI.enabled = canEffect;
                        if (GenesisTheme.Button(new Rect(zone.x + 4f, zone.yMax - 24f, zone.width - 8f, 20f), "EFFECT", GenesisTheme.Gold))
                            ActivateMonsterEffect(monster, true);
                        GUI.enabled = true;
                    }
                }
            }
        }

        private void DrawSpellTrapArea(Rect area, List<SpellTrapCard> zones, int graveyardCount, int banishedCount, bool opponentSide, GUIStyle style)
        {
            GenesisTheme.Box(area, opponentSide ? new Color(0.18f, 0.05f, 0.24f, 1f) : new Color(0.03f, 0.22f, 0.24f, 1f));
            GUI.Label(new Rect(area.x + 8f, area.y + 2f, area.width - 16f, 18f),
                $"{(opponentSide ? "CPU" : "YOUR")} SPELL / TRAP ZONES     GY: {graveyardCount}   BANISHED: {banishedCount}", style);

            float gap = 8f;
            Rect zoneArea = new Rect(area.x + 10f, area.y + 21f, area.width - 20f, area.height - 25f);
            float zoneWidth = (zoneArea.width - gap * 4f) / 5f;

            for (int i = 0; i < 5; i++)
            {
                Rect zoneRect = new Rect(zoneArea.x + i * (zoneWidth + gap), zoneArea.y, zoneWidth, zoneArea.height);
                GenesisTheme.Box(zoneRect, opponentSide ? new Color(0.30f, 0.08f, 0.38f, 1f) : new Color(0.06f, 0.34f, 0.36f, 1f));

                if (i >= zones.Count)
                {
                    GUI.Label(zoneRect, "EMPTY S/T ZONE", style);
                    continue;
                }

                SpellTrapCard zone = zones[i];
                string text;
                if (opponentSide && zone.FaceDown)
                    text = "FACE-DOWN CARD";
                else if (zone.FaceDown)
                    text = $"{zone.Card.cardName}\nSET TRAP";
                else
                    text = $"{zone.Card.cardName}\nFACE-UP SPELL";

                GUI.Label(zoneRect, text, style);
            }
        }

        private void DrawPhaseControls(Rect area, GUIStyle style)
        {
            GenesisTheme.Box(area, GenesisTheme.PanelAlt);

            string buttonLabel = _phase == DuelPhase.Main
                ? (_turnNumber == 1 ? "END TURN" : "GO TO BATTLE PHASE")
                : "END TURN";

            GUI.Label(new Rect(area.x + 12f, area.y + 6f, area.width - 350f, 34f),
                "v0.4: positions, setting, DEF battles, several monster effects, backrow, GY/banish, CPU logic and neon UI are active.", style);

            if (GenesisTheme.Button(new Rect(area.xMax - 320f, area.y + 8f, 190f, 30f), buttonLabel, GenesisTheme.Cyan))
                AdvancePhase();

            if (GenesisTheme.Button(new Rect(area.xMax - 120f, area.y + 8f, 100f, 30f), "FORFEIT", GenesisTheme.Danger))
                Forfeit();
        }

        private void DrawHand(Rect area, GUIStyle style)
        {
            GenesisTheme.Box(area, GenesisTheme.Panel);
            GUI.Label(new Rect(area.x + 8f, area.y + 3f, area.width - 16f, 20f),
                $"YOUR HAND — {_playerHand.Count} cards", style);

            Rect scrollRect = new Rect(area.x + 8f, area.y + 24f, area.width - 16f, area.height - 30f);
            const float cardWidth = 205f;
            const float cardHeight = 148f;
            const float gap = 8f;
            int perRow = Mathf.Max(1, Mathf.FloorToInt((scrollRect.width - 20f) / (cardWidth + gap)));
            int rows = Mathf.CeilToInt(_playerHand.Count / (float)perRow);
            Rect content = new Rect(0f, 0f, scrollRect.width - 20f, Mathf.Max(scrollRect.height, rows * (cardHeight + gap)));
            _handScroll = GUI.BeginScrollView(scrollRect, _handScroll, content);

            List<CardData> snapshot = new List<CardData>(_playerHand);
            for (int i = 0; i < snapshot.Count; i++)
            {
                CardData card = snapshot[i];
                int row = i / perRow;
                int column = i % perRow;
                Rect cardRect = new Rect(column * (cardWidth + gap), row * (cardHeight + gap), cardWidth, cardHeight);
                GenesisTheme.Box(cardRect, GenesisTheme.CardColor(card));

                Color oldContent = GUI.contentColor;
                GUI.contentColor = GenesisTheme.RarityColor(card.rarity);
                GUI.Label(new Rect(cardRect.x + 5f, cardRect.y + 4f, cardRect.width - 10f, 72f),
                    $"{card.cardName}\n{card.kind} • {card.RarityLabel}\n{card.ShortStats}", style);
                GUI.contentColor = oldContent;

                if (card.kind == CardKind.Monster)
                {
                    int tributes = RequiredTributes(card, true);
                    bool canSummon = CanSummon(card);
                    GUI.enabled = canSummon;
                    string summonLabel = tributes > 0 ? $"SUMMON ({tributes})" : "SUMMON";
                    if (GenesisTheme.Button(new Rect(cardRect.x + 7f, cardRect.yMax - 64f, (cardRect.width - 21f) * 0.5f, 27f), summonLabel, GenesisTheme.Cyan))
                        SummonFromHand(card, MonsterPosition.FaceUpAttack);
                    if (GenesisTheme.Button(new Rect(cardRect.x + 14f + (cardRect.width - 21f) * 0.5f, cardRect.yMax - 64f, (cardRect.width - 21f) * 0.5f, 27f), "SET DEF", GenesisTheme.Purple))
                        SummonFromHand(card, MonsterPosition.FaceDownDefense);
                    GUI.enabled = true;

                    if (card.id == "DG020")
                    {
                        bool canSpecial = CanSpecialSummonValkyrie(card);
                        GUI.enabled = canSpecial;
                        if (GenesisTheme.Button(new Rect(cardRect.x + 7f, cardRect.yMax - 32f, cardRect.width - 14f, 25f), "SPECIAL SUMMON", GenesisTheme.Gold))
                            SpecialSummonValkyrie(card);
                        GUI.enabled = true;
                    }
                }
                else if (card.kind == CardKind.Spell)
                {
                    bool supported = IsSupportedSpell(card);
                    GUI.enabled = supported && CanActivatePlayerSpell(card);
                    string label = supported ? "ACTIVATE SPELL" : "RITUAL / ADVANCED EFFECT LATER";
                    if (GenesisTheme.Button(new Rect(cardRect.x + 7f, cardRect.yMax - 36f, cardRect.width - 14f, 28f), label, GenesisTheme.Green))
                        ActivatePlayerSpell(card);
                    GUI.enabled = true;
                }
                else
                {
                    GUI.enabled = CanSetTrap(card);
                    if (GenesisTheme.Button(new Rect(cardRect.x + 7f, cardRect.yMax - 36f, cardRect.width - 14f, 28f), "SET TRAP", GenesisTheme.Purple))
                        SetTrapFromHand(card);
                    GUI.enabled = true;
                }
            }

            GUI.EndScrollView();
        }
    }
}
