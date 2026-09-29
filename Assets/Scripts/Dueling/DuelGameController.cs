using System;
using System.Collections.Generic;
using System.Linq;
using DuelGenesis.Cards;
using DuelGenesis.Economy;
using DuelGenesis.Player;
using UnityEngine;

namespace DuelGenesis.Dueling
{
    /// <summary>
    /// Runs a duel: builds both decks, owns the <see cref="DuelEngine"/>, paces the CPU's turn so
    /// every play is visible on the table, pays rewards and hands control back to Genesis City.
    /// Public API (StartDuel / CloseDuel / IsActive / DuelFinished) is what the rest of the game uses.
    /// </summary>
    public class DuelGameController : MonoBehaviour
    {
        public const int HumanIndexConst = 0;
        public const int WinReward = 400;
        public const int LossReward = 200;

        public event Action<bool> DuelFinished;

        public DuelEngine Engine { get; private set; }
        public DuelBoardView Board { get; private set; }
        public int HumanIndex => HumanIndexConst;
        public int CpuIndex => 1 - HumanIndexConst;
        public int LastReward { get; private set; }
        public string CpuDeckName { get; private set; } = "Genesis CPU";

        public bool IsActive => _active;

        /// <summary>Set by a city duel table just before it starts a duel, so each table keeps its own opponent.</summary>
        public int? NextCpuSeed;
        private int _cpuSeed;
        public bool IsDuelOver => Engine != null && Engine.IsOver;

        // ---- compatibility surface used by existing systems and tests
        public int PlayerLifePoints => Engine?.Duelists[HumanIndex].LifePoints ?? 0;
        public int CpuLifePoints => Engine?.Duelists[CpuIndex].LifePoints ?? 0;
        public int TurnNumber => Engine?.TurnNumber ?? 0;
        public DuelTurnPhase Phase => Engine == null || Engine.IsOver ? DuelTurnPhase.Finished
            : Engine.TurnPlayer != HumanIndex ? DuelTurnPhase.Opponent
            : Engine.Phase == DuelPhase.Battle ? DuelTurnPhase.Battle : DuelTurnPhase.Main;
        public int PlayerDeckCount => Engine?.Duelists[HumanIndex].Deck.Count ?? 0;
        public int CpuDeckCount => Engine?.Duelists[CpuIndex].Deck.Count ?? 0;
        public int PlayerHandCount => Engine?.Duelists[HumanIndex].Hand.Count ?? 0;
        public int CpuHandCount => Engine?.Duelists[CpuIndex].Hand.Count ?? 0;
        public IReadOnlyList<DuelMonsterState> PlayerMonsters => Engine?.Duelists[HumanIndex].MonstersOnField.ToList() ?? new List<DuelMonsterState>();
        public IReadOnlyList<DuelMonsterState> CpuMonsters => Engine?.Duelists[CpuIndex].MonstersOnField.ToList() ?? new List<DuelMonsterState>();
        public IReadOnlyList<DuelBackrowState> PlayerBackrow => Engine?.Duelists[HumanIndex].AllBackrow.ToList() ?? new List<DuelBackrowState>();
        public IReadOnlyList<DuelBackrowState> CpuBackrow => Engine?.Duelists[CpuIndex].AllBackrow.ToList() ?? new List<DuelBackrowState>();

        private bool _active;
        private bool _finishHandled;
        private bool _surrendered;
        private DuelAi _ai;
        private DuelAi _autopilot;

        /// <summary>DEV: the CPU also plays the human side (F8 in a duel). Useful for watching and testing.</summary>
        public bool Autopilot => _autopilot != null;

        public void ToggleAutopilot()
        {
            if (Engine == null) return;
            if (_autopilot == null)
            {
                _autopilot = new DuelAi(HumanIndex);
                Engine.Deciders[HumanIndex] = _autopilot;
                if (Engine.PendingChoice != null && Engine.PendingChoice.Player == HumanIndex)
                {
                    DuelChoice choice = Engine.PendingChoice;
                    if (choice.Kind == DuelChoiceKind.SelectOption) Engine.SubmitOption(_autopilot.ChooseOption(Engine, choice));
                    else Engine.SubmitChoice(_autopilot.ChooseCards(Engine, choice));
                }
            }
            else
            {
                _autopilot = null;
                Engine.Deciders[HumanIndex] = null;
            }
            Debug.Log("Duel: Genesis autopilot " + (Autopilot ? "ON" : "OFF"));
        }
        private DuelHud _hud;
        private GameObject _playerObject;
        private GenesisWallet _wallet;
        private ThirdPersonPlayerController _playerController;
        private List<CardData> _playerCards;
        private List<CardData> _cpuCards;
        private float _nextCpuAction;
        private int _cpuActionsThisPhase;
        private DuelPhase _lastCpuPhase;
        private int _lastCpuTurn;

        // ============================================================== start / stop

        public bool StartDuel(GameObject player)
        {
            if (_active || player == null) return false;

            PlayerDeck savedDeck = player.GetComponent<PlayerDeck>();
            PlayerCollection collection = player.GetComponent<PlayerCollection>();
            if (savedDeck == null)
            {
                Debug.LogWarning("Cannot start duel: deck system missing.");
                return false;
            }
            if (!savedDeck.Validate(collection, out string validation))
            {
                Debug.LogWarning("Cannot start duel: " + validation);
                return false;
            }

            _playerCards = new List<CardData>();
            foreach (DeckEntry entry in savedDeck.Entries)
            {
                CardData card = CardDatabase.GetById(entry.cardId);
                if (card == null) continue;
                for (int i = 0; i < entry.quantity; i++) _playerCards.Add(card);
            }
            if (_playerCards.Count(c => !DuelRules.IsExtraDeckMonster(c)) < DuelRules.MainDeckMinimum)
            {
                Debug.LogWarning("Cannot start duel: saved Main Deck could not be loaded.");
                return false;
            }

            _playerObject = player;
            _wallet = player.GetComponent<GenesisWallet>();
            _playerController = player.GetComponent<ThirdPersonPlayerController>();

            _cpuSeed = NextCpuSeed ?? UnityEngine.Random.Range(0, int.MaxValue);
            NextCpuSeed = null;
            (List<CardData> cpuDeck, string deckName) = CpuDeckBuilder.Build(CardDatabase.All, _cpuSeed);
            _cpuCards = cpuDeck;
            CpuDeckName = deckName;

            _playerController?.SetMovementEnabled(false);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            _active = true;
            BeginEngine();
            Debug.Log($"Duel: Genesis — duel started vs {CpuDeckName} ({_cpuCards.Count} cards).");
            return true;
        }

        private void BeginEngine()
        {
            _finishHandled = false;
            _surrendered = false;
            _lastCpuTurn = -1;
            LastReward = 0;

            Engine = new DuelEngine(UnityEngine.Random.Range(0, int.MaxValue));
            string playerName = PlayerPrefs.GetString("DG_PlayerName", "You");
            Engine.Setup(HumanIndex, string.IsNullOrWhiteSpace(playerName) ? "You" : playerName, _playerCards);
            Engine.Setup(CpuIndex, CpuDeckName, _cpuCards);
            _ai = new DuelAi(CpuIndex);
            Engine.Deciders[CpuIndex] = _ai;
            if (_autopilot != null)
            {
                _autopilot = new DuelAi(HumanIndex);
                Engine.Deciders[HumanIndex] = _autopilot;
            }
            Engine.EventRaised += OnEngineEvent;

            Board = FindAnyObjectByType<DuelBoardView>();
            if (Board == null) Board = new GameObject("DG Duel Board View").AddComponent<DuelBoardView>();

            int first = UnityEngine.Random.value < 0.5f ? HumanIndex : CpuIndex;
            Engine.StartDuel(first);
            Board.BeginPresentation(Engine, HumanIndex);
            _hud = DuelHud.Create(this);
            _nextCpuAction = Time.unscaledTime + 2.4f;
        }

        private void TearDownEngine()
        {
            if (Engine != null) Engine.EventRaised -= OnEngineEvent;
            if (_hud != null) Destroy(_hud.gameObject);
            _hud = null;
            if (Board != null) Board.EndPresentation();
            Engine = null;
        }

        public void Rematch()
        {
            if (!_active) return;
            TearDownEngine();
            (List<CardData> cpuDeck, string deckName) = CpuDeckBuilder.Build(CardDatabase.All, _cpuSeed);   // same opponent again
            _cpuCards = cpuDeck;
            CpuDeckName = deckName;
            BeginEngine();
        }

        public void Surrender()
        {
            if (Engine == null || Engine.IsOver) return;
            _surrendered = true;
            Engine.Surrender(HumanIndex);
        }

        public void CloseDuel()
        {
            if (!_active) return;
            TearDownEngine();
            _active = false;
            _playerController?.SetMovementEnabled(false);   // DuelTableSeat re-enables after returning the player
            _playerObject = null;
            _wallet = null;
            _playerController = null;
        }

        // ============================================================== CPU pacing

        private void Update()
        {
            if (!_active || Engine == null || Engine.IsOver) return;
            bool cpuTurn = Engine.TurnPlayer == CpuIndex;
            if ((!cpuTurn && _autopilot == null) || Engine.IsBusy) return;
            DuelAi actor = cpuTurn ? _ai : _autopilot;
            if (Board != null && !Board.IsSettled) { _nextCpuAction = Mathf.Max(_nextCpuAction, Time.unscaledTime + 0.25f); return; }
            if (Time.unscaledTime < _nextCpuAction) return;

            if (Engine.TurnNumber != _lastCpuTurn)
            {
                _lastCpuTurn = Engine.TurnNumber;
                actor.OnTurnStart();
                _lastCpuPhase = Engine.Phase;
                _cpuActionsThisPhase = 0;
            }
            if (Engine.Phase != _lastCpuPhase)
            {
                _lastCpuPhase = Engine.Phase;
                _cpuActionsThisPhase = 0;
            }

            bool acted = _cpuActionsThisPhase < 25 && actor.TakeAction(Engine);
            if (acted)
            {
                _cpuActionsThisPhase++;
                _nextCpuAction = Time.unscaledTime + (Engine.Phase == DuelPhase.Battle ? 1.2f : 0.9f);
                return;
            }

            Engine.AdvancePhase(Engine.TurnPlayer);
            _nextCpuAction = Time.unscaledTime + 0.7f;
        }

        // ============================================================== results

        private void OnEngineEvent(DuelEvent e)
        {
            if (e.Type != DuelEventType.DuelEnded || _finishHandled) return;
            _finishHandled = true;

            bool playerWon = Engine != null && Engine.Winner == HumanIndex;
            LastReward = _surrendered ? 0 : playerWon ? WinReward : LossReward;
            if (LastReward > 0 && _wallet != null) _wallet.Add(LastReward);
            Debug.Log($"Duel: Genesis — duel over. {e.Text} Reward {LastReward} GC.");
            DuelFinished?.Invoke(playerWon);
        }
    }

    /// <summary>
    /// Builds a legal, themed 40-card CPU deck from the real card catalog: a monster line-up
    /// around one Type or Attribute, a real Tribute curve, and implemented Spells/Traps only.
    /// </summary>
    public static class CpuDeckBuilder
    {
        private static readonly string[] Themes =
        {
            "Dragon", "Spellcaster", "Warrior", "Fiend", "Machine", "Zombie", "Beast", "Winged Beast", "Aqua", "Insect",
            "DARK", "LIGHT", "EARTH", "WATER", "FIRE", "WIND"
        };

        public static (List<CardData> deck, string name) Build(IReadOnlyList<CardData> catalog, int seed)
        {
            System.Random random = new System.Random(seed);
            var summonable = catalog.Where(c => c.kind == CardKind.Monster && DuelRules.CanEverBeNormalSummoned(c)).ToList();

            string theme = Themes[random.Next(Themes.Length)];
            bool Matches(CardData c) => (c.typeLine ?? "").IndexOf(theme, StringComparison.OrdinalIgnoreCase) >= 0 ||
                                        string.Equals(c.attribute, theme, StringComparison.OrdinalIgnoreCase);

            var themed = summonable.Where(Matches).ToList();
            if (themed.Count < 10)
            {
                theme = "Genesis";
                themed = summonable;
            }

            var deck = new List<CardData>();
            void AddCopies(CardData card, int copies)
            {
                if (card == null) return;
                int already = deck.Count(c => c == card);
                int target = Math.Min(DuelRules.MaxCopies, already + copies);
                for (int i = already; i < target; i++) deck.Add(card);
            }

            IEnumerable<CardData> Pick(IEnumerable<CardData> pool, int count, Func<CardData, int> score) =>
                pool.OrderByDescending(c => score(c) + random.Next(0, 700)).Take(count).ToList();

            // Monster curve: ~16 low-level beaters, 4 Level 5-6, 2 Level 7+.
            var low = themed.Where(c => c.level <= 4).ToList();
            var mid = themed.Where(c => c.level == 5 || c.level == 6).ToList();
            var high = themed.Where(c => c.level >= 7).ToList();
            if (low.Count < 7) low = low.Concat(summonable.Where(c => c.level <= 4)).Distinct().ToList();
            if (mid.Count < 2) mid = mid.Concat(summonable.Where(c => c.level == 5 || c.level == 6)).Distinct().ToList();
            if (high.Count < 1) high = high.Concat(summonable.Where(c => c.level >= 7)).Distinct().ToList();

            foreach (CardData c in Pick(low, 7, c => Math.Max(c.attack, c.defense))) AddCopies(c, 2);
            foreach (CardData c in Pick(mid, 3, c => c.attack)) AddCopies(c, 1);
            foreach (CardData c in Pick(high, 2, c => c.attack)) AddCopies(c, 1);
            int monsterGuard = 60;
            while (deck.Count < 22 && low.Count > 0 && monsterGuard-- > 0) AddCopies(low[random.Next(low.Count)], 1);

            // Spells and Traps with implemented effects.
            var spells = catalog.Where(c => c.kind == CardKind.Spell && CardEffects.Get(c) != null).ToList();
            var traps = catalog.Where(c => c.kind == CardKind.Trap && CardEffects.Get(c) != null).ToList();
            string[] staples =
            {
                "Pot of Greed", "Dark Hole", "Mystical Space Typhoon", "Mirror Force", "Trap Hole", "Sakuretsu Armor",
                "Magic Cylinder", "Swords of Revealing Light", "Graceful Charity", "Premature Burial", "Call of the Haunted"
            };
            foreach (string name in staples)
            {
                if (deck.Count >= 32) break;
                if (random.NextDouble() < 0.75) AddCopies(catalog.FirstOrDefault(c => c.cardName == name), 1);
            }
            var themedSupport = spells.Where(c => DuelRules.IsEquip(c) || DuelRules.IsFieldSpell(c))
                .Where(c => (c.effectText ?? "").IndexOf(theme, StringComparison.OrdinalIgnoreCase) >= 0).ToList();
            foreach (CardData c in themedSupport.OrderBy(_ => random.Next()).Take(2)) AddCopies(c, 1);

            int guard = 300;
            while (deck.Count < DuelRules.MainDeckMinimum && guard-- > 0)
            {
                bool trap = random.NextDouble() < 0.5 && traps.Count > 0;
                List<CardData> pool = trap ? traps : spells.Count > 0 ? spells : low;
                AddCopies(pool[random.Next(pool.Count)], 1);
            }
            guard = 300;
            while (deck.Count < DuelRules.MainDeckMinimum && summonable.Count > 0 && guard-- > 0)
                AddCopies(summonable[random.Next(summonable.Count)], 1);

            string deckName = theme == "Genesis" ? "Genesis Duelist" : $"{theme} Duelist";
            return (deck, deckName);
        }
    }
}
