using System.Collections.Generic;
using System.Linq;
using DuelGenesis.Cards;
using DuelGenesis.Economy;
using DuelGenesis.Player;
using DuelGenesis.UI;
using UnityEngine;

namespace DuelGenesis.Dueling
{
    public enum DuelTurnPhase
    {
        Main,
        Battle,
        Opponent,
        Finished
    }

    public enum DuelMonsterPosition
    {
        FaceUpAttack,
        FaceUpDefense,
        FaceDownDefense
    }

    public sealed class DuelMonsterState
    {
        public CardData Card;
        public DuelMonsterPosition Position;
        public bool HasAttacked;
        public bool HasChangedPosition;
        public bool EffectUsed;
        public bool CannotAttackThisTurn;
        public bool ProtectionUsed;
        public int AttackBonus;
        public int SummonedTurn;
        public int NegatedUntilTurn;

        public bool IsFaceDown => Position == DuelMonsterPosition.FaceDownDefense;
        public bool IsAttackPosition => Position == DuelMonsterPosition.FaceUpAttack;
        public bool IsNegated(int turn) => NegatedUntilTurn >= turn;

        public DuelMonsterState(CardData card, DuelMonsterPosition position, int turn)
        {
            Card = card;
            Position = position;
            SummonedTurn = turn;
        }
    }

    public sealed class DuelBackrowState
    {
        public CardData Card;
        public bool FaceDown;
        public int SetTurn;
        public int NegatedUntilTurn;

        public bool IsNegated(int turn) => NegatedUntilTurn >= turn;

        public DuelBackrowState(CardData card, bool faceDown, int turn)
        {
            Card = card;
            FaceDown = faceDown;
            SetTurn = turn;
        }
    }

    public class DuelGameController : MonoBehaviour
    {
        private const int EndPhaseHandLimit = 5;

        private readonly List<CardData> _playerDeck = new();
        private readonly List<CardData> _cpuDeck = new();
        private readonly List<CardData> _playerHand = new();
        private readonly List<CardData> _cpuHand = new();
        private readonly List<CardData> _playerGraveyard = new();
        private readonly List<CardData> _cpuGraveyard = new();
        private readonly List<CardData> _playerBanished = new();
        private readonly List<CardData> _cpuBanished = new();
        private readonly List<DuelMonsterState> _playerMonsters = new();
        private readonly List<DuelMonsterState> _cpuMonsters = new();
        private readonly List<DuelBackrowState> _playerBackrow = new();
        private readonly List<DuelBackrowState> _cpuBackrow = new();

        private GameObject _playerObject;
        private PlayerDeck _savedDeck;
        private PlayerCollection _collection;
        private GenesisWallet _wallet;
        private ThirdPersonPlayerController _playerController;
        private ThirdPersonCamera _cameraController;

        private DuelTurnPhase _phase;
        private bool _active;
        private bool _duelOver;
        private bool _normalSummoned;
        private bool _rewardGranted;
        private bool _playerMarketUsed;
        private bool _cpuMarketUsed;
        private bool _playerActivatedSpell;
        private bool _cpuActivatedSpell;
        private bool _playerDiscardingForHandLimit;
        private int _turnNumber;
        private int _playerLP;
        private int _cpuLP;
        private string _message = string.Empty;
        private Vector2 _handScroll;

        private DuelMonsterState _pendingAttacker;
        private CardData _pendingQuickCharge;
        private DuelMonsterState _pendingArchmage;

        public event System.Action<bool> DuelFinished;

        public bool IsActive => _active;
        public bool IsDuelOver => _duelOver;
        public int PlayerLifePoints => _playerLP;
        public int CpuLifePoints => _cpuLP;
        public int TurnNumber => _turnNumber;
        public DuelTurnPhase Phase => _phase;
        public int PlayerDeckCount => _playerDeck.Count;
        public int CpuDeckCount => _cpuDeck.Count;
        public int PlayerHandCount => _playerHand.Count;
        public int CpuHandCount => _cpuHand.Count;
        public int PlayerGraveyardCount => _playerGraveyard.Count;
        public int CpuGraveyardCount => _cpuGraveyard.Count;
        public int PlayerBanishedCount => _playerBanished.Count;
        public int CpuBanishedCount => _cpuBanished.Count;
        public IReadOnlyList<DuelMonsterState> PlayerMonsters => _playerMonsters;
        public IReadOnlyList<DuelMonsterState> CpuMonsters => _cpuMonsters;
        public IReadOnlyList<DuelBackrowState> PlayerBackrow => _playerBackrow;
        public IReadOnlyList<DuelBackrowState> CpuBackrow => _cpuBackrow;

        public bool StartDuel(GameObject player)
        {
            if (_active || player == null)
                return false;

            _playerObject = player;
            _savedDeck = player.GetComponent<PlayerDeck>();
            _collection = player.GetComponent<PlayerCollection>();
            _wallet = player.GetComponent<GenesisWallet>();
            _playerController = player.GetComponent<ThirdPersonPlayerController>();
            _cameraController = UnityEngine.Object.FindFirstObjectByType<ThirdPersonCamera>();

            if (_savedDeck == null)
            {
                Debug.LogWarning("Cannot start duel: deck system missing.");
                return false;
            }

            if (!_savedDeck.Validate(_collection, out string validation))
            {
                Debug.LogWarning("Cannot start duel: " + validation);
                return false;
            }

            BuildPlayerDeck();
            BuildCpuDeck();
            if (_playerDeck.Count < PlayerDeck.MinimumDeckSize)
            {
                Debug.LogWarning("Cannot start duel: saved Main Deck could not be loaded.");
                return false;
            }

            ClearZones();
            _playerLP = 8000;
            _cpuLP = 8000;
            _turnNumber = 1;
            _phase = DuelTurnPhase.Main;
            _normalSummoned = false;
            _rewardGranted = false;
            _playerMarketUsed = false;
            _cpuMarketUsed = false;
            _playerActivatedSpell = false;
            _cpuActivatedSpell = false;
            _playerDiscardingForHandLimit = false;
            _duelOver = false;
            _active = true;
            _handScroll = Vector2.zero;
            ClearPending();

            if (!DrawPlayer(5) || !DrawCpu(5))
                return true;

            _playerController?.SetMovementEnabled(false);
            _cameraController?.SetLookEnabled(false);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            _message = "Turn 1 — Main Phase. Summon, Set, activate cards, then End Turn.";
            Debug.Log("Duel: Genesis DuelGameController started — playable duel engine v0.5.");
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

        private void BuildCpuDeck()
        {
            _cpuDeck.Clear();
            IReadOnlyList<CardData> all = CardDatabase.All;
            int index = 0;
            while (_cpuDeck.Count < 40 && all.Count > 0)
            {
                _cpuDeck.Add(all[index % all.Count]);
                index++;
            }
            Shuffle(_cpuDeck);
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

        private void ClearZones()
        {
            _playerHand.Clear();
            _cpuHand.Clear();
            _playerGraveyard.Clear();
            _cpuGraveyard.Clear();
            _playerBanished.Clear();
            _cpuBanished.Clear();
            _playerMonsters.Clear();
            _cpuMonsters.Clear();
            _playerBackrow.Clear();
            _cpuBackrow.Clear();
        }

        private bool DrawPlayer(int count, bool extraDraw = false)
        {
            int drawn = 0;
            for (int i = 0; i < count; i++)
            {
                if (_playerDeck.Count == 0)
                {
                    FinishDuel(false, "Deck out — you have no cards left to draw.");
                    return false;
                }

                CardData card = _playerDeck[0];
                _playerDeck.RemoveAt(0);
                _playerHand.Add(card);
                drawn++;
            }

            if (extraDraw && drawn > 0 && HasFaceUp(_playerBackrow, "DG018") && !_playerMarketUsed)
            {
                _playerLP += 300;
                _playerMarketUsed = true;
                _message = "Genesis Market restored 300 LP after your extra draw.";
            }
            return true;
        }

        private bool DrawCpu(int count, bool extraDraw = false)
        {
            int drawn = 0;
            for (int i = 0; i < count; i++)
            {
                if (_cpuDeck.Count == 0)
                {
                    FinishDuel(true, "CPU decked out. Victory!");
                    return false;
                }

                CardData card = _cpuDeck[0];
                _cpuDeck.RemoveAt(0);
                _cpuHand.Add(card);
                drawn++;
            }

            if (extraDraw && drawn > 0 && HasFaceUp(_cpuBackrow, "DG018") && !_cpuMarketUsed)
            {
                _cpuLP += 300;
                _cpuMarketUsed = true;
            }
            return true;
        }

        private static bool HasFaceUp(List<DuelBackrowState> zones, string id)
        {
            return zones.Any(zone => !zone.FaceDown && zone.Card.id == id);
        }

        private int RequiredTributes(CardData card, bool playerSide)
        {
            if (card == null || card.kind != CardKind.Monster) return 0;
            bool activatedSpell = playerSide ? _playerActivatedSpell : _cpuActivatedSpell;
            if (card.id == "DG013" && activatedSpell) return 0;
            if (card.level >= 7) return 2;
            if (card.level >= 5) return 1;
            return 0;
        }

        private bool CanNormalSummon(CardData card)
        {
            if (card == null || card.kind != CardKind.Monster || _phase != DuelTurnPhase.Main || _normalSummoned || _playerDiscardingForHandLimit)
                return false;
            if (_playerMonsters.Count >= 5)
                return false;
            return _playerMonsters.Count >= RequiredTributes(card, true);
        }

        private void PlayerNormalSummon(CardData card, DuelMonsterPosition position)
        {
            if (!CanNormalSummon(card)) return;

            int tributes = RequiredTributes(card, true);
            for (int i = 0; i < tributes; i++)
            {
                DuelMonsterState tribute = _playerMonsters.OrderBy(m => DisplayedAttack(m, true)).First();
                SendMonsterToGraveyard(tribute, true);
                if (_duelOver) return;
            }

            _playerHand.Remove(card);
            DuelMonsterState monster = new DuelMonsterState(card, position, _turnNumber);
            _playerMonsters.Add(monster);
            _normalSummoned = true;

            if (position == DuelMonsterPosition.FaceDownDefense)
            {
                _message = tributes > 0
                    ? $"Tributed {tributes} monster(s) and Set a monster."
                    : "Set a monster in face-down Defense Position.";
                return;
            }

            _message = tributes > 0
                ? $"Tributed {tributes} monster(s) and summoned {card.cardName}."
                : $"Normal Summoned {card.cardName}.";
            ResolveSummonEffect(monster, true, tributes > 0, false);
        }

        private bool CanSpecialValkyrie(CardData card, bool playerSide)
        {
            if (card == null || card.id != "DG020") return false;
            if (playerSide)
                return !_playerDiscardingForHandLimit && _phase == DuelTurnPhase.Main && _playerMonsters.Count == 0 && _playerMonsters.Count < 5;
            return _phase == DuelTurnPhase.Opponent && _cpuMonsters.Count == 0 && _cpuMonsters.Count < 5;
        }

        private void PlayerSpecialValkyrie(CardData card)
        {
            if (!CanSpecialValkyrie(card, true)) return;
            _playerHand.Remove(card);
            DuelMonsterState monster = new DuelMonsterState(card, DuelMonsterPosition.FaceUpAttack, _turnNumber)
            {
                AttackBonus = -700
            };
            _playerMonsters.Add(monster);
            _message = "Genesis Valkyrie Special Summoned itself at 2000 ATK this turn.";
            ResolveSummonEffect(monster, true, false, true);
        }

        private void ResolveSummonEffect(DuelMonsterState monster, bool ownerIsPlayer, bool tributeSummon, bool specialSummon)
        {
            if (monster == null || monster.IsFaceDown || monster.IsNegated(_turnNumber)) return;

            if (monster.Card.id == "DG001")
            {
                if (ownerIsPlayer)
                {
                    if (!DrawPlayer(1, true)) return;
                    CardData bottom = _playerHand.LastOrDefault();
                    if (bottom != null)
                    {
                        _playerHand.Remove(bottom);
                        _playerDeck.Add(bottom);
                    }
                    _message = "Genesis Apprentice: drew 1, then returned 1 card from hand to the bottom of the Deck.";
                }
                else
                {
                    if (!DrawCpu(1, true)) return;
                    CardData bottom = _cpuHand.LastOrDefault();
                    if (bottom != null)
                    {
                        _cpuHand.Remove(bottom);
                        _cpuDeck.Add(bottom);
                    }
                }
            }

            if (monster.Card.id == "DG024" && tributeSummon && !_duelOver)
            {
                monster.CannotAttackThisTurn = true;
                BounceTwo(ownerIsPlayer);
                _message = ownerIsPlayer
                    ? "Genesis Leviathan returned up to two opposing cards. It cannot attack this turn."
                    : "CPU's Genesis Leviathan returned up to two of your cards.";
            }

            if (monster.Card.id == "DG015" && specialSummon)
            {
                List<CardData> deck = ownerIsPlayer ? _playerDeck : _cpuDeck;
                if (deck.Count > 0 && deck[0].kind == CardKind.Monster)
                {
                    monster.AttackBonus += 400;
                    if (ownerIsPlayer)
                        _message = "Holo Dragon revealed a Monster and gained 400 ATK this turn.";
                }
            }
        }

        private void BounceTwo(bool sourceIsPlayer)
        {
            List<DuelMonsterState> enemyMonsters = sourceIsPlayer ? _cpuMonsters : _playerMonsters;
            List<DuelBackrowState> enemyBackrow = sourceIsPlayer ? _cpuBackrow : _playerBackrow;
            List<CardData> enemyHand = sourceIsPlayer ? _cpuHand : _playerHand;
            int count = 0;

            while (enemyMonsters.Count > 0 && count < 2)
            {
                DuelMonsterState target = enemyMonsters.OrderByDescending(m => m.Card.level).ThenByDescending(m => DisplayedAttack(m, !sourceIsPlayer)).First();
                enemyMonsters.Remove(target);
                enemyHand.Add(target.Card);
                count++;
            }

            while (enemyBackrow.Count > 0 && count < 2)
            {
                DuelBackrowState target = enemyBackrow[0];
                enemyBackrow.RemoveAt(0);
                enemyHand.Add(target.Card);
                count++;
            }
        }

        private int DisplayedAttack(DuelMonsterState monster, bool ownerIsPlayer)
        {
            if (monster == null) return 0;
            int attack = Mathf.Max(0, monster.Card.attack + monster.AttackBonus);
            if (!monster.IsNegated(_turnNumber) && monster.Card.id == "DG002" && ControlsSpellcaster(ownerIsPlayer))
                attack += 400;
            return Mathf.Max(0, attack);
        }

        private int BattleAttack(DuelMonsterState monster, bool ownerIsPlayer)
        {
            int attack = DisplayedAttack(monster, ownerIsPlayer);
            if (monster != null && !monster.IsNegated(_turnNumber) && monster.Card.id == "DG004")
                attack += 200;
            return Mathf.Max(0, attack);
        }

        private bool ControlsSpellcaster(bool playerSide)
        {
            List<DuelMonsterState> field = playerSide ? _playerMonsters : _cpuMonsters;
            return field.Any(m => !m.IsFaceDown && !m.IsNegated(_turnNumber) && m.Card.typeLine.Contains("Spellcaster"));
        }

        private bool CanChangePosition(DuelMonsterState monster)
        {
            return !_playerDiscardingForHandLimit && monster != null &&
                   _phase == DuelTurnPhase.Main &&
                   !monster.HasChangedPosition &&
                   !monster.HasAttacked &&
                   monster.SummonedTurn < _turnNumber;
        }

        private void ChangePosition(DuelMonsterState monster)
        {
            if (!CanChangePosition(monster)) return;

            if (monster.Position == DuelMonsterPosition.FaceDownDefense)
            {
                monster.Position = DuelMonsterPosition.FaceUpAttack;
                monster.HasChangedPosition = true;
                _message = $"Flip Summoned {monster.Card.cardName}.";
                return;
            }

            monster.Position = monster.Position == DuelMonsterPosition.FaceUpAttack
                ? DuelMonsterPosition.FaceUpDefense
                : DuelMonsterPosition.FaceUpAttack;
            monster.HasChangedPosition = true;
            _message = $"{monster.Card.cardName} changed to {(monster.IsAttackPosition ? "Attack" : "Defense")} Position.";
        }

        private bool CanUseMonsterEffect(DuelMonsterState monster, bool playerSide)
        {
            if (monster == null || monster.IsFaceDown || monster.EffectUsed || monster.IsNegated(_turnNumber))
                return false;

            if (playerSide && (_phase != DuelTurnPhase.Main || _playerDiscardingForHandLimit)) return false;
            if (!playerSide && _phase != DuelTurnPhase.Opponent) return false;

            if (monster.Card.id == "DG005")
                return playerSide ? _playerHand.Count > 0 : _cpuHand.Count > 0;
            if (monster.Card.id == "DG019")
                return (playerSide ? _playerGraveyard : _cpuGraveyard).Any(c => c.kind == CardKind.Spell) &&
                       (playerSide ? _cpuMonsters : _playerMonsters).Any(m => !m.IsFaceDown);
            if (monster.Card.id == "DG023")
                return (playerSide ? _cpuBackrow : _playerBackrow).Count > 0;
            return false;
        }

        private void PlayerUseMonsterEffect(DuelMonsterState monster)
        {
            if (!CanUseMonsterEffect(monster, true)) return;

            if (monster.Card.id == "DG023")
            {
                _pendingArchmage = monster;
                _message = "Genesis Archmage: choose an opponent Spell/Trap to banish.";
                return;
            }

            ResolveMonsterEffect(monster, true);
        }

        private void ResolveMonsterEffect(DuelMonsterState monster, bool ownerIsPlayer)
        {
            if (!CanUseMonsterEffect(monster, ownerIsPlayer)) return;
            if (TryNegateMonsterEffect(ownerIsPlayer, monster)) return;
            if (_duelOver || !ContainsMonster(monster, ownerIsPlayer)) return;

            monster.EffectUsed = true;

            if (monster.Card.id == "DG005")
            {
                List<CardData> hand = ownerIsPlayer ? _playerHand : _cpuHand;
                List<CardData> grave = ownerIsPlayer ? _playerGraveyard : _cpuGraveyard;
                CardData discard = hand[0];
                hand.RemoveAt(0);
                grave.Add(discard);
                if (ownerIsPlayer) _playerLP += 500;
                else _cpuLP += 500;
                _message = ownerIsPlayer
                    ? $"Alley Alchemist discarded {discard.cardName}; you gained 500 LP."
                    : "CPU used Alley Alchemist and gained 500 LP.";
                return;
            }

            if (monster.Card.id == "DG019")
            {
                List<CardData> grave = ownerIsPlayer ? _playerGraveyard : _cpuGraveyard;
                List<CardData> banished = ownerIsPlayer ? _playerBanished : _cpuBanished;
                CardData cost = grave.Last(c => c.kind == CardKind.Spell);
                grave.Remove(cost);
                banished.Add(cost);

                List<DuelMonsterState> enemy = ownerIsPlayer ? _cpuMonsters : _playerMonsters;
                DuelMonsterState target = enemy.Where(m => !m.IsFaceDown).OrderByDescending(m => DisplayedAttack(m, !ownerIsPlayer)).FirstOrDefault();
                if (target != null)
                    target.NegatedUntilTurn = _turnNumber;

                _message = ownerIsPlayer
                    ? $"Arcane Mainframe banished {cost.cardName}; {(target != null ? target.Card.cardName : "the target")} is negated this turn."
                    : "CPU's Arcane Mainframe negated one of your face-up monsters.";
            }
        }

        private bool ContainsMonster(DuelMonsterState monster, bool playerSide)
        {
            return (playerSide ? _playerMonsters : _cpuMonsters).Contains(monster);
        }

        private void ResolveArchmageTarget(DuelBackrowState target)
        {
            if (_pendingArchmage == null || target == null || !_cpuBackrow.Contains(target))
                return;

            DuelMonsterState source = _pendingArchmage;
            _pendingArchmage = null;

            if (!CanUseMonsterEffect(source, true))
                return;
            if (TryNegateMonsterEffect(true, source))
                return;
            if (_duelOver || !_playerMonsters.Contains(source))
                return;

            source.EffectUsed = true;
            _cpuBackrow.Remove(target);
            _cpuBanished.Add(target.Card);
            _message = $"Genesis Archmage banished {target.Card.cardName}.";
        }

        private bool TryNegateMonsterEffect(bool effectOwnerIsPlayer, DuelMonsterState source)
        {
            List<DuelBackrowState> defenderBackrow = effectOwnerIsPlayer ? _cpuBackrow : _playerBackrow;
            List<CardData> defenderGrave = effectOwnerIsPlayer ? _cpuGraveyard : _playerGraveyard;
            int defenderLP = effectOwnerIsPlayer ? _cpuLP : _playerLP;

            DuelBackrowState counter = defenderBackrow.FirstOrDefault(z =>
                z.FaceDown && z.Card.id == "DG022" && z.SetTurn < _turnNumber && !z.IsNegated(_turnNumber));

            if (counter == null || defenderLP < 800)
                return false;

            defenderBackrow.Remove(counter);
            defenderGrave.Add(counter.Card);
            if (effectOwnerIsPlayer) _cpuLP -= 800;
            else _playerLP -= 800;

            SendMonsterToGraveyard(source, effectOwnerIsPlayer);
            _message = effectOwnerIsPlayer
                ? "CPU activated Zero-Latency Counter: your effect was negated and the monster destroyed."
                : "Zero-Latency Counter negated the CPU monster effect and destroyed it.";
            CheckLifePoints();
            return true;
        }

        private bool IsSupportedSpell(CardData card)
        {
            return card != null && card.kind == CardKind.Spell &&
                   (card.id == "DG009" || card.id == "DG010" || card.id == "DG016" || card.id == "DG018");
        }

        private bool CanActivateSpell(CardData card)
        {
            if (_playerDiscardingForHandLimit || _phase != DuelTurnPhase.Main || !IsSupportedSpell(card)) return false;
            if (card.id == "DG009") return _playerMonsters.Count > 0;
            if (card.id == "DG010") return _playerGraveyard.Any(c => c.kind == CardKind.Monster);
            if (card.id == "DG016") return _playerDeck.Any(c => c.kind == CardKind.Monster && c.level <= 4 && c.typeLine.Contains("Spellcaster"));
            if (card.id == "DG018") return _playerBackrow.Count < 5 && !HasFaceUp(_playerBackrow, "DG018");
            return false;
        }

        private void PlayerActivateSpell(CardData card)
        {
            if (!CanActivateSpell(card)) return;

            if (card.id == "DG009")
            {
                _pendingQuickCharge = card;
                _message = "Quick Charge: choose one of your monsters to gain 500 ATK.";
                return;
            }

            ResolvePlayerSpell(card, null);
        }

        private void ResolveQuickChargeTarget(DuelMonsterState target)
        {
            if (_pendingQuickCharge == null || target == null || !_playerMonsters.Contains(target))
                return;
            CardData spell = _pendingQuickCharge;
            _pendingQuickCharge = null;
            ResolvePlayerSpell(spell, target);
        }

        private void ResolvePlayerSpell(CardData card, DuelMonsterState selectedTarget)
        {
            if (!CanActivateSpell(card) || !_playerHand.Contains(card)) return;

            _playerHand.Remove(card);
            _playerActivatedSpell = true;

            if (TryNegateSpell(true))
            {
                _playerGraveyard.Add(card);
                _message = $"CPU's Signal Jam negated {card.cardName}.";
                return;
            }

            if (card.id == "DG009")
            {
                DuelMonsterState target = selectedTarget;
                if (target == null || !_playerMonsters.Contains(target))
                    target = _playerMonsters.OrderByDescending(m => DisplayedAttack(m, true)).FirstOrDefault();
                if (target != null)
                    target.AttackBonus += 500;
                _playerGraveyard.Add(card);
                _message = target == null
                    ? "Quick Charge resolved without a target."
                    : $"{target.Card.cardName} gained 500 ATK until the end of the turn.";
                return;
            }

            if (card.id == "DG010")
            {
                CardData recycled = _playerGraveyard.Last(c => c.kind == CardKind.Monster);
                _playerGraveyard.Remove(recycled);
                _playerDeck.Add(recycled);
                Shuffle(_playerDeck);
                _playerGraveyard.Add(card);
                DrawPlayer(1, true);
                if (!_duelOver)
                    _message = $"Genesis Recycle returned {recycled.cardName} to the Deck and drew 1.";
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
                _message = $"Arcane Transit added {searched.cardName}, then discarded {discard.cardName}.";
                return;
            }

            if (card.id == "DG018")
            {
                _playerBackrow.Add(new DuelBackrowState(card, false, _turnNumber));
                _message = "Genesis Market is active.";
            }
        }

        private bool TryNegateSpell(bool casterIsPlayer)
        {
            List<DuelBackrowState> defenderBackrow = casterIsPlayer ? _cpuBackrow : _playerBackrow;
            List<CardData> defenderHand = casterIsPlayer ? _cpuHand : _playerHand;
            List<CardData> defenderGrave = casterIsPlayer ? _cpuGraveyard : _playerGraveyard;

            DuelBackrowState jam = defenderBackrow.FirstOrDefault(z =>
                z.FaceDown && z.Card.id == "DG012" && z.SetTurn < _turnNumber && !z.IsNegated(_turnNumber));

            if (jam == null || defenderHand.Count == 0)
                return false;

            CardData discard = defenderHand[0];
            defenderHand.RemoveAt(0);
            defenderGrave.Add(discard);
            defenderBackrow.Remove(jam);
            defenderGrave.Add(jam.Card);
            return true;
        }

        private bool CanSetTrap(CardData card)
        {
            return card != null && card.kind == CardKind.Trap && !_playerDiscardingForHandLimit && _phase == DuelTurnPhase.Main && _playerBackrow.Count < 5;
        }

        private void PlayerSetTrap(CardData card)
        {
            if (!CanSetTrap(card)) return;
            _playerHand.Remove(card);
            _playerBackrow.Add(new DuelBackrowState(card, true, _turnNumber));
            _message = $"Set {card.cardName}.";
        }

        private int TriggerAmbush(bool defendingPlayer, string attackerName)
        {
            List<DuelBackrowState> zones = defendingPlayer ? _playerBackrow : _cpuBackrow;
            List<CardData> grave = defendingPlayer ? _playerGraveyard : _cpuGraveyard;
            DuelBackrowState trap = zones.FirstOrDefault(z =>
                z.FaceDown && z.Card.id == "DG011" && z.SetTurn < _turnNumber && !z.IsNegated(_turnNumber));
            if (trap == null) return 0;

            zones.Remove(trap);
            grave.Add(trap.Card);
            _message = $"Back Alley Ambush! {attackerName} loses 700 ATK for this battle.";
            return 700;
        }

        private bool TriggerBarrier(bool defendingPlayer, int damage)
        {
            if (damage < 1500) return false;
            List<DuelBackrowState> zones = defendingPlayer ? _playerBackrow : _cpuBackrow;
            List<CardData> grave = defendingPlayer ? _playerGraveyard : _cpuGraveyard;
            DuelBackrowState trap = zones.FirstOrDefault(z =>
                z.FaceDown && z.Card.id == "DG017" && z.SetTurn < _turnNumber && !z.IsNegated(_turnNumber));
            if (trap == null) return false;

            zones.Remove(trap);
            grave.Add(trap.Card);
            _message = $"Ranked Barrier prevented {damage} battle damage.";
            return true;
        }

        private void BeginAttack(DuelMonsterState attacker)
        {
            if (!CanAttack(attacker)) return;

            if (_cpuMonsters.Count == 0)
            {
                ResolveDirectAttack(attacker, true);
                return;
            }

            _pendingAttacker = attacker;
            _message = $"{attacker.Card.cardName}: choose an attack target.";
        }

        private bool CanAttack(DuelMonsterState attacker)
        {
            return !_duelOver && !_playerDiscardingForHandLimit && _phase == DuelTurnPhase.Battle && attacker != null &&
                   _playerMonsters.Contains(attacker) && attacker.IsAttackPosition &&
                   !attacker.HasAttacked && !attacker.CannotAttackThisTurn;
        }

        private bool IsLegalPlayerAttackTarget(DuelMonsterState defender)
        {
            if (_pendingAttacker == null || defender == null || !_cpuMonsters.Contains(defender))
                return false;

            DuelMonsterState guardian = _cpuMonsters.FirstOrDefault(m =>
                !m.IsFaceDown && !m.IsNegated(_turnNumber) &&
                m.Card.id == "DG007" && m.Position == DuelMonsterPosition.FaceUpDefense);

            return guardian == null || defender == guardian;
        }

        private void ResolvePlayerAttackTarget(DuelMonsterState defender)
        {
            if (!IsLegalPlayerAttackTarget(defender)) return;
            DuelMonsterState attacker = _pendingAttacker;
            _pendingAttacker = null;
            if (!CanAttack(attacker)) return;

            attacker.HasAttacked = true;
            int attack = Mathf.Max(0, BattleAttack(attacker, true) - TriggerAmbush(false, attacker.Card.cardName));
            ResolveBattle(attacker, true, defender, attack);
        }

        private void ResolveDirectAttack(DuelMonsterState attacker, bool attackerIsPlayer)
        {
            if (attacker == null) return;
            if (attackerIsPlayer)
            {
                if (!CanAttack(attacker)) return;
                attacker.HasAttacked = true;
            }
            else
            {
                if (!_cpuMonsters.Contains(attacker) || !attacker.IsAttackPosition || attacker.HasAttacked || attacker.CannotAttackThisTurn)
                    return;
                attacker.HasAttacked = true;
            }

            int attack = Mathf.Max(0, BattleAttack(attacker, attackerIsPlayer) -
                TriggerAmbush(!attackerIsPlayer, attacker.Card.cardName));

            bool defendingPlayer = !attackerIsPlayer;
            if (!TriggerBarrier(defendingPlayer, attack))
            {
                if (defendingPlayer) _playerLP -= attack;
                else _cpuLP -= attack;
                _message = $"{attacker.Card.cardName} attacked directly for {attack} damage.";
            }
            CheckLifePoints();
        }

        private DuelMonsterState ChooseCpuTarget()
        {
            DuelMonsterState guardian = _playerMonsters.FirstOrDefault(m =>
                !m.IsFaceDown && !m.IsNegated(_turnNumber) &&
                m.Card.id == "DG007" && m.Position == DuelMonsterPosition.FaceUpDefense);
            if (guardian != null) return guardian;

            return _playerMonsters
                .OrderBy(m => m.IsFaceDown ? int.MaxValue / 2 : (m.IsAttackPosition ? DisplayedAttack(m, true) : m.Card.defense))
                .FirstOrDefault();
        }

        private void ResolveBattle(DuelMonsterState attacker, bool attackerIsPlayer, DuelMonsterState defender, int attackValue)
        {
            if (attacker == null || defender == null) return;
            bool defenderIsPlayer = !attackerIsPlayer;

            if (defender.IsFaceDown)
                defender.Position = DuelMonsterPosition.FaceUpDefense;

            int defenderValue = defender.IsAttackPosition
                ? DisplayedAttack(defender, defenderIsPlayer)
                : defender.Card.defense;
            int difference = attackValue - defenderValue;

            if (defender.IsAttackPosition)
            {
                if (difference > 0)
                {
                    bool destroyed = !TryProtect(defenderIsPlayer, defender);
                    if (destroyed)
                    {
                        SendMonsterToGraveyard(defender, defenderIsPlayer);
                        if (_duelOver) return;
                        RewardDestroy(attacker, attackerIsPlayer);
                    }

                    if (!_duelOver && !TriggerBarrier(defenderIsPlayer, difference))
                    {
                        if (defenderIsPlayer) _playerLP -= difference;
                        else _cpuLP -= difference;
                    }
                    _message = $"{attacker.Card.cardName} won the battle. {difference} battle damage.";
                }
                else if (difference < 0)
                {
                    int damage = -difference;
                    bool destroyed = !TryProtect(attackerIsPlayer, attacker);
                    if (destroyed)
                    {
                        SendMonsterToGraveyard(attacker, attackerIsPlayer);
                        if (_duelOver) return;
                        RewardDestroy(defender, defenderIsPlayer);
                    }

                    if (!_duelOver && !TriggerBarrier(attackerIsPlayer, damage))
                    {
                        if (attackerIsPlayer) _playerLP -= damage;
                        else _cpuLP -= damage;
                    }
                    _message = $"{attacker.Card.cardName} lost the battle. {damage} battle damage.";
                }
                else
                {
                    bool attackerDestroyed = !TryProtect(attackerIsPlayer, attacker);
                    bool defenderDestroyed = !TryProtect(defenderIsPlayer, defender);
                    if (attackerDestroyed)
                    {
                        SendMonsterToGraveyard(attacker, attackerIsPlayer);
                        if (_duelOver) return;
                    }
                    if (defenderDestroyed)
                    {
                        SendMonsterToGraveyard(defender, defenderIsPlayer);
                        if (_duelOver) return;
                    }
                    _message = "Equal ATK — both monsters were destroyed unless protected.";
                }
            }
            else
            {
                if (difference > 0)
                {
                    bool destroyed = !TryProtect(defenderIsPlayer, defender);
                    if (destroyed)
                    {
                        SendMonsterToGraveyard(defender, defenderIsPlayer);
                        if (_duelOver) return;
                        RewardDestroy(attacker, attackerIsPlayer);
                    }
                    _message = $"{attacker.Card.cardName} broke through {defender.Card.cardName}'s DEF.";
                }
                else if (difference < 0)
                {
                    int damage = -difference;
                    if (!TriggerBarrier(attackerIsPlayer, damage))
                    {
                        if (attackerIsPlayer) _playerLP -= damage;
                        else _cpuLP -= damage;
                    }
                    _message = $"Attack failed against {defender.Card.cardName}. {damage} battle damage.";
                }
                else
                {
                    _message = "ATK matched DEF. No destruction or damage.";
                }
            }

            CheckLifePoints();
        }

        private bool TryProtect(bool ownerIsPlayer, DuelMonsterState victim)
        {
            List<DuelMonsterState> field = ownerIsPlayer ? _playerMonsters : _cpuMonsters;
            DuelMonsterState paladin = field.FirstOrDefault(m =>
                m != victim && !m.IsFaceDown && !m.IsNegated(_turnNumber) &&
                m.Card.id == "DG014" && !m.ProtectionUsed && DisplayedAttack(m, ownerIsPlayer) >= 500);

            if (paladin == null) return false;
            paladin.AttackBonus -= 500;
            paladin.ProtectionUsed = true;
            return true;
        }

        private void RewardDestroy(DuelMonsterState source, bool ownerIsPlayer)
        {
            if (source == null || source.IsNegated(_turnNumber) || source.Card.id != "DG003")
                return;
            if (ownerIsPlayer) _playerLP += 300;
            else _cpuLP += 300;
        }

        private void SendMonsterToGraveyard(DuelMonsterState monster, bool ownerIsPlayer)
        {
            if (monster == null) return;
            List<DuelMonsterState> field = ownerIsPlayer ? _playerMonsters : _cpuMonsters;
            List<CardData> grave = ownerIsPlayer ? _playerGraveyard : _cpuGraveyard;
            if (!field.Remove(monster)) return;

            grave.Add(monster.Card);
            if (!monster.IsNegated(_turnNumber) && monster.Card.id == "DG006")
            {
                if (ownerIsPlayer) _cpuLP -= 200;
                else _playerLP -= 200;
                CheckLifePoints();
            }
        }

        private void AdvancePhase()
        {
            if (_duelOver || _playerDiscardingForHandLimit) return;
            ClearPending();

            if (_phase == DuelTurnPhase.Main)
            {
                if (_turnNumber == 1)
                {
                    EndPlayerTurn();
                    return;
                }

                _phase = DuelTurnPhase.Battle;
                _message = "Battle Phase — choose an Attack Position monster, then choose its target.";
                return;
            }

            if (_phase == DuelTurnPhase.Battle)
                EndPlayerTurn();
        }

        private void EndPlayerTurn()
        {
            if (_duelOver || _playerDiscardingForHandLimit) return;
            ClearPending();

            foreach (DuelMonsterState monster in _playerMonsters)
                monster.AttackBonus = 0;

            if (_playerHand.Count > EndPhaseHandLimit)
            {
                _playerDiscardingForHandLimit = true;
                int excess = _playerHand.Count - EndPhaseHandLimit;
                _message = $"End Phase — discard {excess} card{(excess == 1 ? string.Empty : "s")} to the Graveyard to return your hand to {EndPhaseHandLimit}.";
                return;
            }

            CompletePlayerEndTurn();
        }

        private void PlayerDiscardForHandLimit(CardData card)
        {
            if (!_playerDiscardingForHandLimit || card == null || !_playerHand.Remove(card))
                return;

            _playerGraveyard.Add(card);
            int excess = _playerHand.Count - EndPhaseHandLimit;
            if (excess > 0)
            {
                _message = $"End Phase — discarded {card.cardName}. Choose {excess} more card{(excess == 1 ? string.Empty : "s")} to discard.";
                return;
            }

            _playerDiscardingForHandLimit = false;
            _message = $"End Phase — discarded {card.cardName}. Hand returned to {EndPhaseHandLimit}.";
            CompletePlayerEndTurn();
        }

        private void CompletePlayerEndTurn()
        {
            if (_duelOver) return;

            _playerDiscardingForHandLimit = false;
            _phase = DuelTurnPhase.Opponent;
            _turnNumber++;
            _cpuMarketUsed = false;
            _cpuActivatedSpell = false;
            PrepareField(_cpuMonsters);
            RunCpuTurn();
        }

        private static void PrepareField(List<DuelMonsterState> field)
        {
            foreach (DuelMonsterState monster in field)
            {
                monster.HasAttacked = false;
                monster.HasChangedPosition = false;
                monster.EffectUsed = false;
                monster.CannotAttackThisTurn = false;
                monster.ProtectionUsed = false;
            }
        }

        private void RunCpuTurn()
        {
            if (_duelOver || !DrawCpu(1)) return;

            CpuActivateSpell();
            if (_duelOver) return;

            CpuSpecialValkyrie();
            CpuNormalSummon();
            if (_duelOver) return;

            CpuUseMonsterEffect();
            if (_duelOver) return;

            CpuSetTrap();

            List<DuelMonsterState> attackers = new List<DuelMonsterState>(_cpuMonsters);
            foreach (DuelMonsterState attacker in attackers)
            {
                if (_duelOver) break;
                if (!_cpuMonsters.Contains(attacker) || !attacker.IsAttackPosition || attacker.CannotAttackThisTurn || attacker.HasAttacked)
                    continue;

                if (_playerMonsters.Count == 0)
                {
                    ResolveDirectAttack(attacker, false);
                }
                else
                {
                    attacker.HasAttacked = true;
                    int attack = Mathf.Max(0, BattleAttack(attacker, false) - TriggerAmbush(true, attacker.Card.cardName));
                    DuelMonsterState target = ChooseCpuTarget();
                    if (target != null)
                        ResolveBattle(attacker, false, target, attack);
                }
            }

            foreach (DuelMonsterState monster in _cpuMonsters)
                monster.AttackBonus = 0;

            if (_duelOver) return;

            int cpuDiscarded = DiscardCpuToHandLimit();

            _turnNumber++;
            _normalSummoned = false;
            _playerMarketUsed = false;
            _playerActivatedSpell = false;
            PrepareField(_playerMonsters);
            _phase = DuelTurnPhase.Main;
            if (!DrawPlayer(1)) return;
            _message = cpuDiscarded > 0
                ? $"CPU discarded {cpuDiscarded} card{(cpuDiscarded == 1 ? string.Empty : "s")} at End Phase. Turn {_turnNumber} — Main Phase."
                : $"Turn {_turnNumber} — Main Phase.";
        }

        private int DiscardCpuToHandLimit()
        {
            int discarded = 0;
            while (_cpuHand.Count > EndPhaseHandLimit)
            {
                int index = _cpuHand.Count - 1;
                CardData card = _cpuHand[index];
                _cpuHand.RemoveAt(index);
                _cpuGraveyard.Add(card);
                discarded++;
            }

            return discarded;
        }

        private void CpuActivateSpell()
        {
            CardData spell = _cpuHand.FirstOrDefault(card =>
                IsSupportedSpell(card) &&
                ((card.id == "DG009" && _cpuMonsters.Count > 0) ||
                 (card.id == "DG010" && _cpuGraveyard.Any(c => c.kind == CardKind.Monster)) ||
                 (card.id == "DG016" && _cpuDeck.Any(c => c.kind == CardKind.Monster && c.level <= 4 && c.typeLine.Contains("Spellcaster"))) ||
                 (card.id == "DG018" && _cpuBackrow.Count < 5 && !HasFaceUp(_cpuBackrow, "DG018"))));

            if (spell == null) return;
            _cpuHand.Remove(spell);
            _cpuActivatedSpell = true;

            if (TryNegateSpell(false))
            {
                _cpuGraveyard.Add(spell);
                _message = $"Signal Jam negated CPU's {spell.cardName}.";
                return;
            }

            if (spell.id == "DG009")
            {
                DuelMonsterState target = _cpuMonsters.OrderByDescending(m => DisplayedAttack(m, false)).First();
                target.AttackBonus += 500;
                _cpuGraveyard.Add(spell);
                return;
            }

            if (spell.id == "DG010")
            {
                CardData recycled = _cpuGraveyard.Last(c => c.kind == CardKind.Monster);
                _cpuGraveyard.Remove(recycled);
                _cpuDeck.Add(recycled);
                Shuffle(_cpuDeck);
                _cpuGraveyard.Add(spell);
                DrawCpu(1, true);
                return;
            }

            if (spell.id == "DG016")
            {
                CardData searched = _cpuDeck.First(c => c.kind == CardKind.Monster && c.level <= 4 && c.typeLine.Contains("Spellcaster"));
                _cpuDeck.Remove(searched);
                _cpuHand.Add(searched);
                _cpuGraveyard.Add(spell);
                CardData discard = _cpuHand.FirstOrDefault(c => c != searched) ?? searched;
                _cpuHand.Remove(discard);
                _cpuGraveyard.Add(discard);
                return;
            }

            if (spell.id == "DG018")
                _cpuBackrow.Add(new DuelBackrowState(spell, false, _turnNumber));
        }

        private void CpuSpecialValkyrie()
        {
            CardData valkyrie = _cpuHand.FirstOrDefault(c => c.id == "DG020");
            if (!CanSpecialValkyrie(valkyrie, false)) return;

            _cpuHand.Remove(valkyrie);
            DuelMonsterState monster = new DuelMonsterState(valkyrie, DuelMonsterPosition.FaceUpAttack, _turnNumber)
            {
                AttackBonus = -700
            };
            _cpuMonsters.Add(monster);
            ResolveSummonEffect(monster, false, false, true);
        }

        private void CpuNormalSummon()
        {
            if (_cpuMonsters.Count >= 5) return;

            CardData choice = _cpuHand
                .Where(c => c.kind == CardKind.Monster)
                .Where(c => RequiredTributes(c, false) <= _cpuMonsters.Count)
                .OrderByDescending(c => Mathf.Max(c.attack, c.defense))
                .FirstOrDefault();

            if (choice == null) return;
            int tributes = RequiredTributes(choice, false);

            for (int i = 0; i < tributes; i++)
            {
                DuelMonsterState tribute = _cpuMonsters.OrderBy(m => DisplayedAttack(m, false)).First();
                SendMonsterToGraveyard(tribute, false);
                if (_duelOver) return;
            }

            _cpuHand.Remove(choice);
            bool defense = choice.defense >= choice.attack + 300;
            DuelMonsterState monster = new DuelMonsterState(
                choice,
                defense ? DuelMonsterPosition.FaceDownDefense : DuelMonsterPosition.FaceUpAttack,
                _turnNumber);
            _cpuMonsters.Add(monster);

            if (!defense)
                ResolveSummonEffect(monster, false, tributes > 0, false);
        }

        private void CpuUseMonsterEffect()
        {
            DuelMonsterState monster = _cpuMonsters.FirstOrDefault(m => CanUseMonsterEffect(m, false));
            if (monster == null) return;

            if (monster.Card.id == "DG023")
            {
                if (TryNegateMonsterEffect(false, monster)) return;
                if (_duelOver || !_cpuMonsters.Contains(monster)) return;
                monster.EffectUsed = true;
                DuelBackrowState target = _playerBackrow.FirstOrDefault();
                if (target != null)
                {
                    _playerBackrow.Remove(target);
                    _playerBanished.Add(target.Card);
                }
                return;
            }

            ResolveMonsterEffect(monster, false);
        }

        private void CpuSetTrap()
        {
            if (_cpuBackrow.Count >= 5) return;
            CardData trap = _cpuHand.FirstOrDefault(c => c.kind == CardKind.Trap);
            if (trap == null) return;
            _cpuHand.Remove(trap);
            _cpuBackrow.Add(new DuelBackrowState(trap, true, _turnNumber));
        }

        private void CheckLifePoints()
        {
            _playerLP = Mathf.Max(0, _playerLP);
            _cpuLP = Mathf.Max(0, _cpuLP);

            if (_playerLP <= 0)
                FinishDuel(false, "Your Life Points reached 0. Duel lost.");
            else if (_cpuLP <= 0)
                FinishDuel(true, "Opponent's Life Points reached 0. Victory!");
        }

        private void FinishDuel(bool playerWon, string result)
        {
            if (_duelOver) return;

            _duelOver = true;
            _playerDiscardingForHandLimit = false;
            _phase = DuelTurnPhase.Finished;
            ClearPending();
            _message = result;

            if (!_rewardGranted && _wallet != null)
            {
                int reward = playerWon ? 400 : 200;
                _wallet.Add(reward);
                _message += $" Reward: {reward} GC.";
                _rewardGranted = true;
            }

            DuelFinished?.Invoke(playerWon);
        }

        private void Forfeit()
        {
            if (_duelOver) return;
            _duelOver = true;
            _playerDiscardingForHandLimit = false;
            _phase = DuelTurnPhase.Finished;
            ClearPending();
            _message = "Duel forfeited. No GC reward granted.";
            _rewardGranted = true;
            DuelFinished?.Invoke(false);
        }

        public void CloseDuel()
        {
            if (!_active) return;

            _active = false;
            _duelOver = false;
            _playerDiscardingForHandLimit = false;
            ClearPending();
            _playerController?.SetMovementEnabled(false);
            _cameraController?.SetLookEnabled(true);

            _playerObject = null;
            _savedDeck = null;
            _collection = null;
            _wallet = null;
            _playerController = null;
            _cameraController = null;
        }

        private void ClearPending()
        {
            _pendingAttacker = null;
            _pendingQuickCharge = null;
            _pendingArchmage = null;
        }

        private string PhaseLabel()
        {
            if (_playerDiscardingForHandLimit)
                return "END PHASE";

            return _phase switch
            {
                DuelTurnPhase.Main => "MAIN PHASE",
                DuelTurnPhase.Battle => "BATTLE PHASE",
                DuelTurnPhase.Opponent => "CPU TURN",
                DuelTurnPhase.Finished => "DUEL COMPLETE",
                _ => _phase.ToString().ToUpperInvariant()
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
                fontSize = 24,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = GenesisTheme.Cyan }
            };
            GUIStyle header = new GUIStyle(GUI.skin.label)
            {
                fontSize = 16,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };
            GUIStyle body = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                wordWrap = true,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };

            GUI.Label(new Rect(screen.x + 20f, screen.y + 5f, screen.width - 40f, 32f), "DUEL: GENESIS — PLAYABLE DUEL ENGINE v0.5", title);
            GUI.Label(new Rect(screen.x + 20f, screen.y + 36f, screen.width - 40f, 24f),
                $"YOU {_playerLP:N0} LP     TURN {_turnNumber} • {PhaseLabel()}     CPU {_cpuLP:N0} LP", header);
            GUI.Label(new Rect(screen.x + 30f, screen.y + 61f, screen.width - 60f, 36f), _message, body);

            float y = screen.y + 100f;
            DrawMonsterArea(new Rect(screen.x + 25f, y, screen.width - 50f, 132f), false, body);
            y += 138f;
            DrawBackrowArea(new Rect(screen.x + 25f, y, screen.width - 50f, 64f), false, body);
            y += 70f;
            DrawMonsterArea(new Rect(screen.x + 25f, y, screen.width - 50f, 142f), true, body);
            y += 148f;
            DrawBackrowArea(new Rect(screen.x + 25f, y, screen.width - 50f, 68f), true, body);
            y += 74f;

            if (_duelOver)
            {
                Rect result = new Rect(screen.center.x - 270f, screen.center.y - 75f, 540f, 160f);
                GenesisTheme.Box(result, GenesisTheme.PanelAlt);
                GUI.Label(new Rect(result.x + 18f, result.y + 18f, result.width - 36f, 65f), _message, header);
                if (GenesisTheme.Button(new Rect(result.center.x - 95f, result.yMax - 55f, 190f, 36f), "RETURN TO TABLE", GenesisTheme.Purple))
                    CloseDuel();
                return;
            }

            DrawPhaseControls(new Rect(screen.x + 25f, y, screen.width - 50f, 46f), body);
            y += 52f;
            DrawHand(new Rect(screen.x + 25f, y, screen.width - 50f, Mathf.Max(142f, screen.yMax - y - 12f)), body);
        }

        private void DrawMonsterArea(Rect area, bool playerSide, GUIStyle style)
        {
            List<DuelMonsterState> monsters = playerSide ? _playerMonsters : _cpuMonsters;
            Color panelColor = playerSide ? new Color(0.04f, 0.18f, 0.24f, 1f) : new Color(0.22f, 0.05f, 0.16f, 1f);
            GenesisTheme.Box(area, panelColor);

            int hand = playerSide ? _playerHand.Count : _cpuHand.Count;
            int deck = playerSide ? _playerDeck.Count : _cpuDeck.Count;
            GUI.Label(new Rect(area.x + 8f, area.y + 2f, area.width - 16f, 20f),
                $"{(playerSide ? "YOUR" : "CPU")} MONSTERS — Hand {hand} • Deck {deck}", style);

            Rect zones = new Rect(area.x + 8f, area.y + 23f, area.width - 16f, area.height - 27f);
            float gap = 6f;
            float width = (zones.width - gap * 4f) / 5f;

            for (int i = 0; i < 5; i++)
            {
                Rect zone = new Rect(zones.x + i * (width + gap), zones.y, width, zones.height);
                GenesisTheme.Box(zone, playerSide ? new Color(0.07f, 0.26f, 0.32f, 1f) : new Color(0.31f, 0.08f, 0.22f, 1f));

                if (i >= monsters.Count)
                {
                    GUI.Label(zone, "EMPTY\nMONSTER ZONE", style);
                    continue;
                }

                DuelMonsterState monster = monsters[i];
                bool hide = !playerSide && monster.IsFaceDown;
                string name = hide ? "FACE-DOWN MONSTER" : monster.Card.cardName;
                string stats = hide
                    ? "DEFENSE POSITION"
                    : $"ATK {DisplayedAttack(monster, playerSide)} / DEF {monster.Card.defense}\n{PositionText(monster)}";
                GUI.Label(new Rect(zone.x + 4f, zone.y + 2f, zone.width - 8f, zone.height - 43f), $"{name}\n{stats}", style);

                if (!playerSide)
                {
                    if (_pendingAttacker != null)
                    {
                        GUI.enabled = IsLegalPlayerAttackTarget(monster);
                        if (GenesisTheme.Button(new Rect(zone.x + 4f, zone.yMax - 29f, zone.width - 8f, 25f), "ATTACK TARGET", GenesisTheme.Danger))
                            ResolvePlayerAttackTarget(monster);
                        GUI.enabled = true;
                    }
                    continue;
                }

                if (_pendingQuickCharge != null)
                {
                    if (GenesisTheme.Button(new Rect(zone.x + 4f, zone.yMax - 29f, zone.width - 8f, 25f), "+500 ATK TARGET", GenesisTheme.Green))
                        ResolveQuickChargeTarget(monster);
                    continue;
                }

                if (_phase == DuelTurnPhase.Battle && !_playerDiscardingForHandLimit)
                {
                    GUI.enabled = CanAttack(monster);
                    string label = monster.HasAttacked ? "ATTACKED" : monster.CannotAttackThisTurn ? "NO ATTACK" : "ATTACK";
                    if (GenesisTheme.Button(new Rect(zone.x + 4f, zone.yMax - 29f, zone.width - 8f, 25f), label, GenesisTheme.Cyan))
                        BeginAttack(monster);
                    GUI.enabled = true;
                }
                else if (_phase == DuelTurnPhase.Main && !_playerDiscardingForHandLimit)
                {
                    float buttonWidth = (zone.width - 12f) * 0.5f;
                    GUI.enabled = CanChangePosition(monster);
                    string label = monster.IsFaceDown ? "FLIP" : monster.IsAttackPosition ? "TO DEF" : "TO ATK";
                    if (GenesisTheme.Button(new Rect(zone.x + 4f, zone.yMax - 29f, buttonWidth, 25f), label, GenesisTheme.Cyan))
                        ChangePosition(monster);
                    GUI.enabled = CanUseMonsterEffect(monster, true);
                    if (GenesisTheme.Button(new Rect(zone.x + 8f + buttonWidth, zone.yMax - 29f, buttonWidth, 25f), "EFFECT", GenesisTheme.Purple))
                        PlayerUseMonsterEffect(monster);
                    GUI.enabled = true;
                }
            }
        }

        private string PositionText(DuelMonsterState monster)
        {
            if (monster.IsFaceDown) return "SET / DEFENSE";
            return monster.IsAttackPosition ? "ATTACK" : "DEFENSE";
        }

        private void DrawBackrowArea(Rect area, bool playerSide, GUIStyle style)
        {
            List<DuelBackrowState> zones = playerSide ? _playerBackrow : _cpuBackrow;
            int grave = playerSide ? _playerGraveyard.Count : _cpuGraveyard.Count;
            int banished = playerSide ? _playerBanished.Count : _cpuBanished.Count;

            GenesisTheme.Box(area, playerSide ? new Color(0.04f, 0.24f, 0.26f, 1f) : new Color(0.25f, 0.06f, 0.30f, 1f));
            GUI.Label(new Rect(area.x + 8f, area.y + 1f, area.width - 16f, 18f),
                $"{(playerSide ? "YOUR" : "CPU")} S/T — GY {grave} • BANISHED {banished}", style);

            Rect zoneArea = new Rect(area.x + 8f, area.y + 20f, area.width - 16f, area.height - 23f);
            float gap = 6f;
            float width = (zoneArea.width - gap * 4f) / 5f;

            for (int i = 0; i < 5; i++)
            {
                Rect rect = new Rect(zoneArea.x + i * (width + gap), zoneArea.y, width, zoneArea.height);
                GenesisTheme.Box(rect, playerSide ? new Color(0.06f, 0.34f, 0.36f, 1f) : new Color(0.30f, 0.08f, 0.38f, 1f));

                if (i >= zones.Count)
                {
                    GUI.Label(rect, "EMPTY S/T", style);
                    continue;
                }

                DuelBackrowState zone = zones[i];
                bool hidden = !playerSide && zone.FaceDown;
                GUI.Label(new Rect(rect.x + 3f, rect.y + 1f, rect.width - 6f, rect.height - 2f),
                    hidden ? "FACE-DOWN CARD" : zone.Card.cardName, style);

                if (!playerSide && _pendingArchmage != null)
                {
                    if (GenesisTheme.Button(new Rect(rect.x + 3f, rect.yMax - 24f, rect.width - 6f, 21f), "BANISH", GenesisTheme.Purple))
                        ResolveArchmageTarget(zone);
                }
            }
        }

        private void DrawPhaseControls(Rect area, GUIStyle style)
        {
            GenesisTheme.Box(area, GenesisTheme.PanelAlt);

            if (_playerDiscardingForHandLimit)
            {
                int excess = Mathf.Max(0, _playerHand.Count - EndPhaseHandLimit);
                GUI.Label(new Rect(area.x + 12f, area.y + 5f, area.width - 150f, 34f),
                    $"END PHASE — choose {excess} card{(excess == 1 ? string.Empty : "s")} from your hand to discard.", style);
                if (GenesisTheme.Button(new Rect(area.xMax - 120f, area.y + 7f, 100f, 30f), "FORFEIT", GenesisTheme.Danger))
                    Forfeit();
                return;
            }

            if (_pendingAttacker != null || _pendingQuickCharge != null || _pendingArchmage != null)
            {
                GUI.Label(new Rect(area.x + 12f, area.y + 5f, area.width - 150f, 34f), "TARGET SELECTION ACTIVE", style);
                if (GenesisTheme.Button(new Rect(area.xMax - 130f, area.y + 7f, 110f, 30f), "CANCEL", GenesisTheme.Danger))
                    ClearPending();
                return;
            }

            string label = _phase == DuelTurnPhase.Main
                ? (_turnNumber == 1 ? "END TURN" : "BATTLE PHASE")
                : "END TURN";

            GUI.Label(new Rect(area.x + 12f, area.y + 5f, area.width - 350f, 34f),
                "Manual battle targeting • ATK/DEF/Set • effects • reactive traps • GY/Banish • CPU", style);

            if (GenesisTheme.Button(new Rect(area.xMax - 320f, area.y + 7f, 190f, 30f), label, GenesisTheme.Cyan))
                AdvancePhase();
            if (GenesisTheme.Button(new Rect(area.xMax - 120f, area.y + 7f, 100f, 30f), "FORFEIT", GenesisTheme.Danger))
                Forfeit();
        }

        private void DrawHand(Rect area, GUIStyle style)
        {
            GenesisTheme.Box(area, GenesisTheme.Panel);
            string handTitle = _playerDiscardingForHandLimit
                ? $"YOUR HAND — {_playerHand.Count} cards • DISCARD {_playerHand.Count - EndPhaseHandLimit}"
                : $"YOUR HAND — {_playerHand.Count} cards";
            GUI.Label(new Rect(area.x + 8f, area.y + 2f, area.width - 16f, 20f), handTitle, style);

            if (_pendingAttacker != null || _pendingQuickCharge != null || _pendingArchmage != null)
            {
                GUI.Label(new Rect(area.x + 20f, area.y + 38f, area.width - 40f, 70f),
                    "Finish choosing a target above, or press CANCEL.", style);
                return;
            }

            Rect scroll = new Rect(area.x + 8f, area.y + 23f, area.width - 16f, area.height - 27f);
            const float cardWidth = 205f;
            const float cardHeight = 146f;
            const float gap = 7f;
            int perRow = Mathf.Max(1, Mathf.FloorToInt((scroll.width - 20f) / (cardWidth + gap)));
            int rows = Mathf.CeilToInt(_playerHand.Count / (float)perRow);
            Rect content = new Rect(0f, 0f, scroll.width - 20f, Mathf.Max(scroll.height, rows * (cardHeight + gap)));
            _handScroll = GUI.BeginScrollView(scroll, _handScroll, content);

            List<CardData> snapshot = new List<CardData>(_playerHand);
            for (int i = 0; i < snapshot.Count; i++)
            {
                CardData card = snapshot[i];
                int row = i / perRow;
                int column = i % perRow;
                Rect rect = new Rect(column * (cardWidth + gap), row * (cardHeight + gap), cardWidth, cardHeight);
                GenesisTheme.Box(rect, GenesisTheme.CardColor(card));

                Color old = GUI.contentColor;
                GUI.contentColor = GenesisTheme.RarityColor(card.rarity);
                GUI.Label(new Rect(rect.x + 5f, rect.y + 3f, rect.width - 10f, 70f),
                    $"{card.cardName}\n{card.kind} • {card.RarityLabel}\n{card.ShortStats}", style);
                GUI.contentColor = old;

                if (_playerDiscardingForHandLimit)
                {
                    if (GenesisTheme.Button(new Rect(rect.x + 6f, rect.yMax - 36f, rect.width - 12f, 29f), "DISCARD", GenesisTheme.Danger))
                        PlayerDiscardForHandLimit(card);
                    continue;
                }

                if (card.kind == CardKind.Monster)
                {
                    int tributes = RequiredTributes(card, true);
                    GUI.enabled = CanNormalSummon(card);
                    float half = (rect.width - 20f) * 0.5f;
                    if (GenesisTheme.Button(new Rect(rect.x + 6f, rect.yMax - 62f, half, 26f),
                            tributes > 0 ? $"SUMMON ({tributes})" : "SUMMON", GenesisTheme.Cyan))
                        PlayerNormalSummon(card, DuelMonsterPosition.FaceUpAttack);
                    if (GenesisTheme.Button(new Rect(rect.x + 12f + half, rect.yMax - 62f, half, 26f), "SET DEF", GenesisTheme.Purple))
                        PlayerNormalSummon(card, DuelMonsterPosition.FaceDownDefense);
                    GUI.enabled = true;

                    if (card.id == "DG020")
                    {
                        GUI.enabled = CanSpecialValkyrie(card, true);
                        if (GenesisTheme.Button(new Rect(rect.x + 6f, rect.yMax - 31f, rect.width - 12f, 24f), "SPECIAL SUMMON", GenesisTheme.Gold))
                            PlayerSpecialValkyrie(card);
                        GUI.enabled = true;
                    }
                }
                else if (card.kind == CardKind.Spell)
                {
                    if (card.id == "DG021")
                    {
                        GUI.enabled = false;
                        GenesisTheme.Button(new Rect(rect.x + 6f, rect.yMax - 33f, rect.width - 12f, 26f), "NO RITUAL TARGET YET", GenesisTheme.Muted);
                        GUI.enabled = true;
                    }
                    else
                    {
                        GUI.enabled = CanActivateSpell(card);
                        if (GenesisTheme.Button(new Rect(rect.x + 6f, rect.yMax - 33f, rect.width - 12f, 26f), "ACTIVATE SPELL", GenesisTheme.Green))
                            PlayerActivateSpell(card);
                        GUI.enabled = true;
                    }
                }
                else
                {
                    GUI.enabled = CanSetTrap(card);
                    if (GenesisTheme.Button(new Rect(rect.x + 6f, rect.yMax - 33f, rect.width - 12f, 26f), "SET TRAP", GenesisTheme.Purple))
                        PlayerSetTrap(card);
                    GUI.enabled = true;
                }
            }

            GUI.EndScrollView();
        }
    }
}
