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

            public FieldMonster(CardData card)
            {
                Card = card;
            }
        }

        private readonly List<CardData> _playerDeck = new();
        private readonly List<CardData> _opponentDeck = new();
        private readonly List<CardData> _playerHand = new();
        private readonly List<CardData> _opponentHand = new();
        private readonly List<FieldMonster> _playerField = new();
        private readonly List<FieldMonster> _opponentField = new();

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
            _playerField.Clear();
            _opponentField.Clear();

            _playerLP = 8000;
            _opponentLP = 8000;
            _turnNumber = 1;
            _phase = DuelPhase.Main;
            _normalSummoned = false;
            _duelOver = false;
            _rewardGranted = false;
            _active = true;
            _handScroll = Vector2.zero;

            DrawPlayerCards(5);
            DrawOpponentCards(5);

            _playerController?.SetMovementEnabled(false);
            _cameraController?.SetLookEnabled(false);

            _message = "Turn 1 — Main Phase. You go first, so there is no Battle Phase this turn.";
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

        private bool DrawPlayerCards(int amount)
        {
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
            }

            return true;
        }

        private bool DrawOpponentCards(int amount)
        {
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
            }

            return true;
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
                FieldMonster tribute = _playerField.OrderBy(monster => monster.Card.attack).First();
                _playerField.Remove(tribute);
            }

            _playerHand.Remove(card);
            _playerField.Add(new FieldMonster(card));
            _normalSummoned = true;

            _message = tributes > 0
                ? $"Tributed {tributes} monster(s) and summoned {card.cardName} in Attack Position."
                : $"Normal Summoned {card.cardName} in Attack Position.";
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

            if (_opponentField.Count == 0)
            {
                _opponentLP -= attacker.Card.attack;
                _message = $"{attacker.Card.cardName} attacked directly for {attacker.Card.attack} damage!";
                CheckLifePoints();
                return;
            }

            FieldMonster defender = _opponentField.OrderBy(monster => monster.Card.attack).First();
            int difference = attacker.Card.attack - defender.Card.attack;

            if (difference > 0)
            {
                _opponentField.Remove(defender);
                _opponentLP -= difference;
                _message = $"{attacker.Card.cardName} destroyed {defender.Card.cardName}. Opponent took {difference} battle damage.";
            }
            else if (difference < 0)
            {
                _playerField.Remove(attacker);
                _playerLP -= -difference;
                _message = $"{attacker.Card.cardName} was destroyed by {defender.Card.cardName}. You took {-difference} battle damage.";
            }
            else
            {
                _playerField.Remove(attacker);
                _opponentField.Remove(defender);
                _message = $"{attacker.Card.cardName} and {defender.Card.cardName} destroyed each other.";
            }

            CheckLifePoints();
        }

        private void EndPlayerTurn()
        {
            if (_duelOver) return;

            _phase = DuelPhase.OpponentTurn;
            _turnNumber++;
            RunOpponentTurn();
        }

        private void RunOpponentTurn()
        {
            if (_duelOver) return;

            if (!DrawOpponentCards(1)) return;

            OpponentNormalSummon();
            if (_duelOver) return;

            List<FieldMonster> attackers = new List<FieldMonster>(_opponentField);
            foreach (FieldMonster attacker in attackers)
            {
                if (_duelOver) break;
                if (_opponentField.Contains(attacker))
                    OpponentAttack(attacker);
            }

            if (_duelOver) return;

            _turnNumber++;
            foreach (FieldMonster monster in _playerField)
                monster.HasAttacked = false;

            _normalSummoned = false;
            _phase = DuelPhase.Main;

            if (!DrawPlayerCards(1)) return;
            _message = $"Turn {_turnNumber} — your Draw Phase completed. Main Phase begins.";
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
                FieldMonster tribute = _opponentField.OrderBy(monster => monster.Card.attack).First();
                _opponentField.Remove(tribute);
            }

            _opponentHand.Remove(choice);
            _opponentField.Add(new FieldMonster(choice));
        }

        private void OpponentAttack(FieldMonster attacker)
        {
            if (_duelOver || attacker == null) return;

            if (_playerField.Count == 0)
            {
                _playerLP -= attacker.Card.attack;
                CheckLifePoints();
                return;
            }

            FieldMonster defender = _playerField.OrderBy(monster => monster.Card.attack).First();
            int difference = attacker.Card.attack - defender.Card.attack;

            if (difference > 0)
            {
                _playerField.Remove(defender);
                _playerLP -= difference;
            }
            else if (difference < 0)
            {
                _opponentField.Remove(attacker);
                _opponentLP -= -difference;
            }
            else
            {
                _opponentField.Remove(attacker);
                _playerField.Remove(defender);
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
                fontSize = 26,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            GUIStyle header = new GUIStyle(GUI.skin.label)
            {
                fontSize = 18,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            GUIStyle body = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                wordWrap = true,
                alignment = TextAnchor.MiddleCenter
            };
            GUIStyle small = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                wordWrap = true,
                alignment = TextAnchor.MiddleCenter
            };

            GUI.Label(new Rect(screen.x + 20f, screen.y + 8f, screen.width - 40f, 36f), "DUEL: GENESIS — TABLETOP DUEL PROTOTYPE", title);
            GUI.Label(new Rect(screen.x + 20f, screen.y + 44f, screen.width - 40f, 26f),
                $"YOU  {_playerLP:N0} LP        TURN {_turnNumber} • {PhaseLabel()}        CPU  {_opponentLP:N0} LP", header);
            GUI.Label(new Rect(screen.x + 30f, screen.y + 72f, screen.width - 60f, 38f), _message, body);

            DrawOpponentArea(new Rect(screen.x + 26f, screen.y + 115f, screen.width - 52f, 135f), small);
            DrawPlayerField(new Rect(screen.x + 26f, screen.y + 260f, screen.width - 52f, 145f), small);

            if (_duelOver)
            {
                Rect result = new Rect(screen.center.x - 260f, screen.center.y - 70f, 520f, 150f);
                GUI.Box(result, string.Empty);
                GUI.Label(new Rect(result.x + 18f, result.y + 18f, result.width - 36f, 60f), _message, header);
                if (GUI.Button(new Rect(result.center.x - 90f, result.yMax - 52f, 180f, 34f), "RETURN TO TABLE"))
                    CloseDuel();
                return;
            }

            DrawPhaseControls(new Rect(screen.x + 26f, screen.y + 412f, screen.width - 52f, 52f), body);
            DrawHand(new Rect(screen.x + 26f, screen.y + 472f, screen.width - 52f, screen.height - 495f), small);
        }

        private void DrawOpponentArea(Rect area, GUIStyle style)
        {
            GUI.Box(area, string.Empty);
            GUI.Label(new Rect(area.x + 8f, area.y + 4f, area.width - 16f, 22f),
                $"CPU FIELD — Hand: {_opponentHand.Count}   Deck: {_opponentDeck.Count}", style);
            DrawFieldMonsters(_opponentField, new Rect(area.x + 10f, area.y + 30f, area.width - 20f, area.height - 36f), style, false);
        }

        private void DrawPlayerField(Rect area, GUIStyle style)
        {
            GUI.Box(area, string.Empty);
            GUI.Label(new Rect(area.x + 8f, area.y + 4f, area.width - 16f, 22f),
                $"YOUR MONSTER ZONES — Deck: {_playerDeck.Count}", style);
            DrawFieldMonsters(_playerField, new Rect(area.x + 10f, area.y + 30f, area.width - 20f, area.height - 36f), style, true);
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
                GUI.Label(new Rect(zone.x + 4f, zone.y + 4f, zone.width - 8f, zone.height - 36f),
                    $"{monster.Card.cardName}\nATK {monster.Card.attack} / DEF {monster.Card.defense}\nLV {monster.Card.level}", style);

                if (playerSide && _phase == DuelPhase.Battle)
                {
                    GUI.enabled = !monster.HasAttacked;
                    if (GUI.Button(new Rect(zone.x + 5f, zone.yMax - 30f, zone.width - 10f, 25f), monster.HasAttacked ? "ATTACKED" : "ATTACK"))
                        PlayerAttack(monster);
                    GUI.enabled = true;
                }
            }
        }

        private void DrawPhaseControls(Rect area, GUIStyle style)
        {
            GUI.Box(area, string.Empty);

            string buttonLabel = _phase == DuelPhase.Main
                ? (_turnNumber == 1 ? "END TURN" : "GO TO BATTLE PHASE")
                : "END TURN";

            GUI.Label(new Rect(area.x + 12f, area.y + 8f, area.width - 350f, 32f),
                "v0.1 rules: Monster summoning, tributes, battle damage, direct attacks, LP and deck-out. Spell/Trap effects are next.", style);

            if (GUI.Button(new Rect(area.xMax - 320f, area.y + 9f, 190f, 32f), buttonLabel))
                AdvancePhase();

            if (GUI.Button(new Rect(area.xMax - 120f, area.y + 9f, 100f, 32f), "FORFEIT"))
                Forfeit();
        }

        private void DrawHand(Rect area, GUIStyle style)
        {
            GUI.Box(area, string.Empty);
            GUI.Label(new Rect(area.x + 8f, area.y + 4f, area.width - 16f, 22f),
                $"YOUR HAND — {_playerHand.Count} cards", style);

            Rect scrollRect = new Rect(area.x + 8f, area.y + 28f, area.width - 16f, area.height - 36f);
            const float cardWidth = 180f;
            const float cardHeight = 122f;
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

                GUI.Label(new Rect(cardRect.x + 5f, cardRect.y + 5f, cardRect.width - 10f, 70f),
                    $"{card.cardName}\n{card.kind} • {card.RarityLabel}\n{card.ShortStats}", style);

                if (card.kind == CardKind.Monster)
                {
                    int tributes = RequiredTributes(card);
                    bool canSummon = CanSummon(card);
                    GUI.enabled = canSummon;
                    string label = tributes > 0 ? $"SUMMON ({tributes} TRIBUTE)" : "NORMAL SUMMON";
                    if (GUI.Button(new Rect(cardRect.x + 7f, cardRect.yMax - 38f, cardRect.width - 14f, 30f), label))
                        SummonFromHand(card);
                    GUI.enabled = true;
                }
                else
                {
                    GUI.enabled = false;
                    GUI.Button(new Rect(cardRect.x + 7f, cardRect.yMax - 38f, cardRect.width - 14f, 30f), "EFFECT ENGINE NEXT");
                    GUI.enabled = true;
                }
            }

            GUI.EndScrollView();
        }
    }
}
