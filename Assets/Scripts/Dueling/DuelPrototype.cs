using System.Collections.Generic;
using System.Linq;
using DuelGenesis.Cards;
using DuelGenesis.Economy;
using DuelGenesis.Player;
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

        private sealed class FieldMonster
        {
            public CardData Card;
            public bool HasAttacked;
            public int AttackBonus;

            public int CurrentAttack => Mathf.Max(0, Card.attack + AttackBonus);

            public FieldMonster(CardData card)
            {
                Card = card;
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
            _active = true;
            _handScroll = Vector2.zero;

            DrawPlayerCards(5);
            DrawOpponentCards(5);

            _playerController?.SetMovementEnabled(false);
            _cameraController?.SetLookEnabled(false);

            _message = "Turn 1 — Main Phase. Monsters, Spells and Trap setting are live. You go first, so there is no Battle Phase this turn.";
            Debug.Log("Duel: Genesis prototype duel started. LP 8000 vs 8000.");
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
                _message = "Genesis Market triggered: you gained 300 LP for drawing outside the Draw Phase.";
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

        private int RequiredTributes(CardData card)
        {
            if (card == null || card.kind != CardKind.Monster) return 0;
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
                   _playerField.Count >= RequiredTributes(card);
        }

        private void SummonFromHand(CardData card)
        {
            if (!CanSummon(card)) return;

            int tributes = RequiredTributes(card);
            for (int i = 0; i < tributes; i++)
            {
                FieldMonster tribute = _playerField.OrderBy(monster => monster.CurrentAttack).First();
                _playerField.Remove(tribute);
                _playerGraveyard.Add(tribute.Card);
            }

            _playerHand.Remove(card);
            _playerField.Add(new FieldMonster(card));
            _normalSummoned = true;

            _message = tributes > 0
                ? $"Tributed {tributes} monster(s) and summoned {card.cardName} in Attack Position."
                : $"Normal Summoned {card.cardName} in Attack Position.";
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

            if (_playerSpellTrap.Count >= 5)
                return false;

            if (card.id == "DG009")
                return _playerField.Count > 0;

            if (card.id == "DG010")
                return _playerGraveyard.Any(c => c.kind == CardKind.Monster);

            if (card.id == "DG016")
                return _playerDeck.Any(c => c.kind == CardKind.Monster && c.level <= 4 && c.typeLine.Contains("Spellcaster"));

            if (card.id == "DG018")
                return !HasFaceUpCard(_playerSpellTrap, "DG018");

            return false;
        }

        private void ActivatePlayerSpell(CardData card)
        {
            if (!CanActivatePlayerSpell(card)) return;

            _playerHand.Remove(card);

            if (TryNegateSpell(false, card))
            {
                _playerGraveyard.Add(card);
                _message = $"CPU's Signal Jam negated {card.cardName}.";
                return;
            }

            if (card.id == "DG009")
            {
                FieldMonster target = _playerField.OrderByDescending(monster => monster.CurrentAttack).First();
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
            _message = $"Set {card.cardName} face-down. It can activate starting on the opponent's turn.";
        }

        private bool TryNegateSpell(bool spellCasterIsPlayer, CardData spell)
        {
            List<SpellTrapCard> defenderZones = spellCasterIsPlayer ? _opponentSpellTrap : _playerSpellTrap;
            List<CardData> defenderGraveyard = spellCasterIsPlayer ? _opponentGraveyard : _playerGraveyard;

            SpellTrapCard signalJam = defenderZones.FirstOrDefault(zone =>
                zone.FaceDown && zone.Card.id == "DG012" && zone.SetTurn < _turnNumber);

            if (signalJam == null)
                return false;

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
                _message = "Battle Phase — choose ATTACK on one of your monsters, or end your turn.";
                return;
            }

            if (_phase == DuelPhase.Battle)
                EndPlayerTurn();
        }

        private void PlayerAttack(FieldMonster attacker)
        {
            if (_duelOver || _phase != DuelPhase.Battle || attacker == null || attacker.HasAttacked)
                return;

            attacker.HasAttacked = true;
            int attackValue = Mathf.Max(0, attacker.CurrentAttack - TriggerBackAlleyAmbush(false, attacker.Card.cardName));

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

            FieldMonster defender = _opponentField.OrderBy(monster => monster.CurrentAttack).First();
            int difference = attackValue - defender.CurrentAttack;

            if (difference > 0)
            {
                _opponentField.Remove(defender);
                _opponentGraveyard.Add(defender.Card);

                if (!TriggerRankedBarrier(false, difference))
                {
                    _opponentLP -= difference;
                    _message = $"{attacker.Card.cardName} destroyed {defender.Card.cardName}. CPU took {difference} battle damage.";
                }
            }
            else if (difference < 0)
            {
                _playerField.Remove(attacker);
                _playerGraveyard.Add(attacker.Card);
                int damage = -difference;

                if (!TriggerRankedBarrier(true, damage))
                {
                    _playerLP -= damage;
                    _message = $"{attacker.Card.cardName} was destroyed by {defender.Card.cardName}. You took {damage} battle damage.";
                }
            }
            else
            {
                _playerField.Remove(attacker);
                _opponentField.Remove(defender);
                _playerGraveyard.Add(attacker.Card);
                _opponentGraveyard.Add(defender.Card);
                _message = $"{attacker.Card.cardName} and {defender.Card.cardName} destroyed each other.";
            }

            CheckLifePoints();
        }

        private void EndPlayerTurn()
        {
            if (_duelOver) return;

            foreach (FieldMonster monster in _playerField)
                monster.AttackBonus = 0;

            _phase = DuelPhase.OpponentTurn;
            _turnNumber++;
            _opponentMarketUsedThisTurn = false;
            RunOpponentTurn();
        }

        private void RunOpponentTurn()
        {
            if (_duelOver) return;

            if (!DrawOpponentCards(1)) return;

            OpponentActivateOneSpell();
            if (_duelOver) return;

            OpponentNormalSummon();
            OpponentSetOneTrap();
            if (_duelOver) return;

            List<FieldMonster> attackers = new List<FieldMonster>(_opponentField);
            foreach (FieldMonster attacker in attackers)
            {
                if (_duelOver) break;
                if (_opponentField.Contains(attacker))
                    OpponentAttack(attacker);
            }

            foreach (FieldMonster monster in _opponentField)
                monster.AttackBonus = 0;

            if (_duelOver) return;

            _turnNumber++;
            foreach (FieldMonster monster in _playerField)
                monster.HasAttacked = false;

            _normalSummoned = false;
            _playerMarketUsedThisTurn = false;
            _phase = DuelPhase.Main;

            if (!DrawPlayerCards(1)) return;
            _message = $"Turn {_turnNumber} — your Draw Phase completed. Main Phase begins.";
        }

        private void OpponentActivateOneSpell()
        {
            if (_opponentSpellTrap.Count >= 5) return;

            CardData spell = _opponentHand.FirstOrDefault(card =>
                card.kind == CardKind.Spell &&
                card.id != "DG021" &&
                ((card.id == "DG009" && _opponentField.Count > 0) ||
                 (card.id == "DG010" && _opponentGraveyard.Any(c => c.kind == CardKind.Monster)) ||
                 (card.id == "DG016" && _opponentDeck.Any(c => c.kind == CardKind.Monster && c.level <= 4 && c.typeLine.Contains("Spellcaster"))) ||
                 (card.id == "DG018" && !HasFaceUpCard(_opponentSpellTrap, "DG018"))));

            if (spell == null) return;

            _opponentHand.Remove(spell);

            if (TryNegateSpell(false, spell))
            {
                _opponentGraveyard.Add(spell);
                _message = $"Your Signal Jam negated CPU's {spell.cardName}!";
                return;
            }

            if (spell.id == "DG009")
            {
                FieldMonster target = _opponentField.OrderByDescending(monster => monster.CurrentAttack).First();
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
                .Where(card => RequiredTributes(card) <= _opponentField.Count)
                .OrderByDescending(card => card.attack)
                .FirstOrDefault();

            if (choice == null) return;

            int tributes = RequiredTributes(choice);
            for (int i = 0; i < tributes; i++)
            {
                FieldMonster tribute = _opponentField.OrderBy(monster => monster.CurrentAttack).First();
                _opponentField.Remove(tribute);
                _opponentGraveyard.Add(tribute.Card);
            }

            _opponentHand.Remove(choice);
            _opponentField.Add(new FieldMonster(choice));
        }

        private void OpponentAttack(FieldMonster attacker)
        {
            if (_duelOver || attacker == null) return;

            int attackValue = Mathf.Max(0, attacker.CurrentAttack - TriggerBackAlleyAmbush(true, attacker.Card.cardName));

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

            FieldMonster defender = _playerField.OrderBy(monster => monster.CurrentAttack).First();
            int difference = attackValue - defender.CurrentAttack;

            if (difference > 0)
            {
                _playerField.Remove(defender);
                _playerGraveyard.Add(defender.Card);

                if (!TriggerRankedBarrier(true, difference))
                {
                    _playerLP -= difference;
                    _message = $"CPU's {attacker.Card.cardName} destroyed {defender.Card.cardName}. You took {difference} battle damage.";
                }
            }
            else if (difference < 0)
            {
                _opponentField.Remove(attacker);
                _opponentGraveyard.Add(attacker.Card);
                int damage = -difference;

                if (!TriggerRankedBarrier(false, damage))
                {
                    _opponentLP -= damage;
                    _message = $"CPU's {attacker.Card.cardName} was destroyed. CPU took {damage} battle damage.";
                }
            }
            else
            {
                _opponentField.Remove(attacker);
                _playerField.Remove(defender);
                _opponentGraveyard.Add(attacker.Card);
                _playerGraveyard.Add(defender.Card);
                _message = $"CPU's {attacker.Card.cardName} and your {defender.Card.cardName} destroyed each other.";
            }

            CheckLifePoints();
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

        private void OnGUI()
        {
            if (!_active) return;

            GUI.depth = -300;

            Rect screen = new Rect(12f, 12f, Screen.width - 24f, Screen.height - 24f);
            GUI.Box(screen, string.Empty);

            GUIStyle title = new GUIStyle(GUI.skin.label)
            {
                fontSize = 25,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            GUIStyle header = new GUIStyle(GUI.skin.label)
            {
                fontSize = 17,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            GUIStyle body = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                wordWrap = true,
                alignment = TextAnchor.MiddleCenter
            };
            GUIStyle small = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                wordWrap = true,
                alignment = TextAnchor.MiddleCenter
            };

            GUI.Label(new Rect(screen.x + 20f, screen.y + 6f, screen.width - 40f, 34f), "DUEL: GENESIS — TABLETOP DUEL PROTOTYPE", title);
            GUI.Label(new Rect(screen.x + 20f, screen.y + 40f, screen.width - 40f, 25f),
                $"YOU  {_playerLP:N0} LP        TURN {_turnNumber} • {PhaseLabel()}        CPU  {_opponentLP:N0} LP", header);
            GUI.Label(new Rect(screen.x + 30f, screen.y + 66f, screen.width - 60f, 36f), _message, body);

            float y = screen.y + 105f;
            DrawOpponentArea(new Rect(screen.x + 26f, y, screen.width - 52f, 104f), small);
            y += 110f;
            DrawSpellTrapArea(new Rect(screen.x + 26f, y, screen.width - 52f, 66f), _opponentSpellTrap, _opponentGraveyard.Count, true, small);
            y += 72f;
            DrawPlayerField(new Rect(screen.x + 26f, y, screen.width - 52f, 108f), small);
            y += 114f;
            DrawSpellTrapArea(new Rect(screen.x + 26f, y, screen.width - 52f, 72f), _playerSpellTrap, _playerGraveyard.Count, false, small);
            y += 78f;

            if (_duelOver)
            {
                Rect result = new Rect(screen.center.x - 260f, screen.center.y - 70f, 520f, 150f);
                GUI.Box(result, string.Empty);
                GUI.Label(new Rect(result.x + 18f, result.y + 18f, result.width - 36f, 60f), _message, header);
                if (GUI.Button(new Rect(result.center.x - 90f, result.yMax - 52f, 180f, 34f), "RETURN TO TABLE"))
                    CloseDuel();
                return;
            }

            DrawPhaseControls(new Rect(screen.x + 26f, y, screen.width - 52f, 48f), body);
            y += 54f;
            DrawHand(new Rect(screen.x + 26f, y, screen.width - 52f, Mathf.Max(130f, screen.yMax - y - 15f)), small);
        }

        private void DrawOpponentArea(Rect area, GUIStyle style)
        {
            GUI.Box(area, string.Empty);
            GUI.Label(new Rect(area.x + 8f, area.y + 3f, area.width - 16f, 20f),
                $"CPU MONSTER ZONES — Hand: {_opponentHand.Count}   Deck: {_opponentDeck.Count}", style);
            DrawFieldMonsters(_opponentField, new Rect(area.x + 10f, area.y + 25f, area.width - 20f, area.height - 30f), style, false);
        }

        private void DrawPlayerField(Rect area, GUIStyle style)
        {
            GUI.Box(area, string.Empty);
            GUI.Label(new Rect(area.x + 8f, area.y + 3f, area.width - 16f, 20f),
                $"YOUR MONSTER ZONES — Deck: {_playerDeck.Count}", style);
            DrawFieldMonsters(_playerField, new Rect(area.x + 10f, area.y + 25f, area.width - 20f, area.height - 30f), style, true);
        }

        private void DrawFieldMonsters(List<FieldMonster> monsters, Rect area, GUIStyle style, bool playerSide)
        {
            float gap = 8f;
            float zoneWidth = (area.width - gap * 4f) / 5f;

            for (int i = 0; i < 5; i++)
            {
                Rect zone = new Rect(area.x + i * (zoneWidth + gap), area.y, zoneWidth, area.height);
                GUI.Box(zone, string.Empty);

                if (i >= monsters.Count)
                {
                    GUI.Label(zone, "EMPTY\nMONSTER ZONE", style);
                    continue;
                }

                FieldMonster monster = monsters[i];
                string boost = monster.AttackBonus != 0 ? $" ({monster.AttackBonus:+#;-#;0})" : string.Empty;
                GUI.Label(new Rect(zone.x + 4f, zone.y + 3f, zone.width - 8f, zone.height - 31f),
                    $"{monster.Card.cardName}\nATK {monster.CurrentAttack}{boost} / DEF {monster.Card.defense}\nLV {monster.Card.level}", style);

                if (playerSide && _phase == DuelPhase.Battle)
                {
                    GUI.enabled = !monster.HasAttacked;
                    if (GUI.Button(new Rect(zone.x + 5f, zone.yMax - 27f, zone.width - 10f, 23f), monster.HasAttacked ? "ATTACKED" : "ATTACK"))
                        PlayerAttack(monster);
                    GUI.enabled = true;
                }
            }
        }

        private void DrawSpellTrapArea(Rect area, List<SpellTrapCard> zones, int graveyardCount, bool opponentSide, GUIStyle style)
        {
            GUI.Box(area, string.Empty);
            GUI.Label(new Rect(area.x + 8f, area.y + 2f, area.width - 16f, 18f),
                $"{(opponentSide ? "CPU" : "YOUR")} SPELL / TRAP ZONES     GY: {graveyardCount}", style);

            float gap = 8f;
            Rect zoneArea = new Rect(area.x + 10f, area.y + 21f, area.width - 20f, area.height - 25f);
            float zoneWidth = (zoneArea.width - gap * 4f) / 5f;

            for (int i = 0; i < 5; i++)
            {
                Rect zoneRect = new Rect(zoneArea.x + i * (zoneWidth + gap), zoneArea.y, zoneWidth, zoneArea.height);
                GUI.Box(zoneRect, string.Empty);

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
            GUI.Box(area, string.Empty);

            string buttonLabel = _phase == DuelPhase.Main
                ? (_turnNumber == 1 ? "END TURN" : "GO TO BATTLE PHASE")
                : "END TURN";

            GUI.Label(new Rect(area.x + 12f, area.y + 6f, area.width - 350f, 34f),
                "v0.2: Spell/Trap zones, Graveyards, Quick Charge, Recycle, Arcane Transit, Genesis Market, Ambush, Barrier and Signal Jam are live.", style);

            if (GUI.Button(new Rect(area.xMax - 320f, area.y + 8f, 190f, 30f), buttonLabel))
                AdvancePhase();

            if (GUI.Button(new Rect(area.xMax - 120f, area.y + 8f, 100f, 30f), "FORFEIT"))
                Forfeit();
        }

        private void DrawHand(Rect area, GUIStyle style)
        {
            GUI.Box(area, string.Empty);
            GUI.Label(new Rect(area.x + 8f, area.y + 3f, area.width - 16f, 20f),
                $"YOUR HAND — {_playerHand.Count} cards", style);

            Rect scrollRect = new Rect(area.x + 8f, area.y + 24f, area.width - 16f, area.height - 30f);
            const float cardWidth = 190f;
            const float cardHeight = 118f;
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
                GUI.Box(cardRect, string.Empty);

                GUI.Label(new Rect(cardRect.x + 5f, cardRect.y + 4f, cardRect.width - 10f, 68f),
                    $"{card.cardName}\n{card.kind} • {card.RarityLabel}\n{card.ShortStats}", style);

                if (card.kind == CardKind.Monster)
                {
                    int tributes = RequiredTributes(card);
                    bool canSummon = CanSummon(card);
                    GUI.enabled = canSummon;
                    string label = tributes > 0 ? $"SUMMON ({tributes} TRIBUTE)" : "NORMAL SUMMON";
                    if (GUI.Button(new Rect(cardRect.x + 7f, cardRect.yMax - 36f, cardRect.width - 14f, 28f), label))
                        SummonFromHand(card);
                    GUI.enabled = true;
                }
                else if (card.kind == CardKind.Spell)
                {
                    bool supported = IsSupportedSpell(card);
                    GUI.enabled = supported && CanActivatePlayerSpell(card);
                    string label = supported ? "ACTIVATE SPELL" : "RITUAL ENGINE LATER";
                    if (GUI.Button(new Rect(cardRect.x + 7f, cardRect.yMax - 36f, cardRect.width - 14f, 28f), label))
                        ActivatePlayerSpell(card);
                    GUI.enabled = true;
                }
                else
                {
                    GUI.enabled = CanSetTrap(card);
                    if (GUI.Button(new Rect(cardRect.x + 7f, cardRect.yMax - 36f, cardRect.width - 14f, 28f), "SET TRAP"))
                        SetTrapFromHand(card);
                    GUI.enabled = true;
                }
            }

            GUI.EndScrollView();
        }
    }
}
