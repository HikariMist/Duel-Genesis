using DuelGenesis.Cards;
using DuelGenesis.Core;
using DuelGenesis.Dueling;
using DuelGenesis.Economy;
using DuelGenesis.Progression;
using DuelGenesis.Shops;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DuelGenesis.UI
{
    public class GenesisHUD : MonoBehaviour
    {
        private GenesisWallet _wallet;
        private PlayerCollection _collection;
        private PlayerDeck _deck;
        private DuelistProfile _profile;
        private DeckBuilderUI _deckBuilder;
        private PackOpeningUI _packOpening;
        private DuelGameController _duel;
        private GenesisMainMenu _mainMenu;
        private GenesisProfilePanel _profilePanel;
        private bool _showCollection;
        private Vector2 _scroll;

        private void Update()
        {
            ResolvePlayerSystems();

            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.cKey.wasPressedThisFrame)
            {
                bool blocked = (_mainMenu != null && _mainMenu.IsOpen) ||
                               (_profilePanel != null && _profilePanel.IsOpen) ||
                               (_deckBuilder != null && _deckBuilder.IsOpen) ||
                               (_packOpening != null && _packOpening.IsOpen) ||
                               (_duel != null && _duel.IsActive);
                if (!blocked)
                    _showCollection = !_showCollection;
            }

#if UNITY_EDITOR
            if (keyboard != null && keyboard.f9Key.wasPressedThisFrame && _wallet != null &&
                (_mainMenu == null || !_mainMenu.IsOpen))
            {
                _wallet.Add(50000);
                Debug.Log("DEV TEST: Added 50,000 GC.");
            }
            if (keyboard != null && keyboard.f10Key.wasPressedThisFrame && _collection != null &&
                (_mainMenu == null || !_mainMenu.IsOpen))
                _collection.GrantEveryCard(3);
#endif

            if ((_mainMenu != null && _mainMenu.IsOpen) ||
                (_profilePanel != null && _profilePanel.IsOpen) ||
                (_deckBuilder != null && _deckBuilder.IsOpen) ||
                (_duel != null && _duel.IsActive))
                _showCollection = false;
        }

        private void ResolvePlayerSystems()
        {
            if (_wallet == null)
                _wallet = Object.FindFirstObjectByType<GenesisWallet>();
            if (_collection == null)
                _collection = Object.FindFirstObjectByType<PlayerCollection>();
            if (_deck == null)
                _deck = Object.FindFirstObjectByType<PlayerDeck>();
            if (_profile == null)
                _profile = Object.FindFirstObjectByType<DuelistProfile>();
            if (_deckBuilder == null)
                _deckBuilder = Object.FindFirstObjectByType<DeckBuilderUI>();
            if (_packOpening == null)
                _packOpening = Object.FindFirstObjectByType<PackOpeningUI>();
            if (_duel == null)
                _duel = Object.FindFirstObjectByType<DuelGameController>();
            if (_mainMenu == null)
                _mainMenu = Object.FindFirstObjectByType<GenesisMainMenu>();
            if (_profilePanel == null)
                _profilePanel = Object.FindFirstObjectByType<GenesisProfilePanel>();
        }

        private void OnGUI()
        {
            ResolvePlayerSystems();
            if ((_mainMenu != null && _mainMenu.IsOpen) ||
                (_profilePanel != null && _profilePanel.IsOpen) ||
                (_deckBuilder != null && _deckBuilder.IsOpen) ||
                (_duel != null && _duel.IsActive))
                return;

            GUIStyle hud = new GUIStyle(GUI.skin.label)
            {
                fontSize = 16,
                alignment = TextAnchor.UpperLeft,
                padding = new RectOffset(12, 12, 10, 10),
                normal = { textColor = Color.white }
            };

            int gc = _wallet != null ? _wallet.GenesisCredits : 0;
            int cards = _collection != null ? _collection.TotalCardCount : 0;
            int unique = _collection != null ? _collection.UniqueCardCount : 0;
            int deckCount = _deck != null ? _deck.MainDeckCount : 0;
            bool deckLegal = _deck != null && _deck.Validate(_collection, out _);

            string deckState = deckLegal ? "LEGAL" : "INCOMPLETE";
            string systemState = GenesisRuntimeDiagnostics.LastPassed ? "PASS" : GenesisRuntimeDiagnostics.LastReport;
            string profileLine = _profile == null
                ? "DUELIST: Loading..."
                : $"DUELIST: Lv.{_profile.Level} {_profile.Title}  XP {_profile.XP}/{_profile.XPToNextLevel}  W/L {_profile.Wins}/{_profile.Losses}";

#if UNITY_EDITOR
            const float hudHeight = 204f;
            string devLine = "\nDEV: [F9] +50,000 GC  [F10] every card x3";
#else
            const float hudHeight = 180f;
            string devLine = string.Empty;
#endif

            Rect hudRect = new Rect(18f, 18f, 430f, hudHeight);
            GenesisTheme.Box(hudRect, GenesisTheme.Background);
            GUI.Label(hudRect,
                $"DUEL: GENESIS\n" +
                profileLine + "\n" +
                $"GENESIS CREDITS: {gc:N0} GC\n" +
                $"COLLECTION: {cards} cards / {unique} unique\n" +
                $"MAIN DECK: {deckCount} cards — {deckState}\n" +
                $"SYSTEM CHECK: {systemState}\n" +
                "[C] Collection   [B] Deck Builder   [P] Profile   [F1] Help" + devLine, hud);

            if (!_showCollection || _collection == null) return;

            float width = Mathf.Min(650f, Screen.width * 0.46f);
            float height = Mathf.Min(740f, Screen.height - 80f);
            Rect panel = new Rect(Screen.width - width - 24f, 24f, width, height);
            GenesisTheme.Box(panel, GenesisTheme.Panel);

            GUIStyle title = new GUIStyle(GUI.skin.label)
            {
                fontSize = 26,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = GenesisTheme.Cyan }
            };

            GUIStyle row = new GUIStyle(GUI.skin.label)
            {
                fontSize = 15,
                wordWrap = true,
                normal = { textColor = Color.white }
            };

            GUI.Label(new Rect(panel.x + 16f, panel.y + 12f, width - 32f, 38f), "CARD COLLECTION", title);
            GUI.Label(new Rect(panel.x + 16f, panel.y + 48f, width - 32f, 24f), "Press C to close • Press B for Deck Builder", row);

            var owned = _collection.GetOwnedCardsSorted();
            Rect scrollRect = new Rect(panel.x + 18f, panel.y + 80f, width - 36f, height - 100f);
            Rect content = new Rect(0f, 0f, scrollRect.width - 20f, Mathf.Max(scrollRect.height, owned.Count * 82f));
            _scroll = GUI.BeginScrollView(scrollRect, _scroll, content);

            for (int i = 0; i < owned.Count; i++)
            {
                var entry = owned[i];
                float y = i * 82f;
                int inDeck = _deck != null ? _deck.GetQuantity(entry.card.id) : 0;

                Rect cardRect = new Rect(0f, y, content.width, 74f);
                GenesisTheme.Box(cardRect, GenesisTheme.CardColor(entry.card));

                Color oldContent = GUI.contentColor;
                GUI.contentColor = GenesisTheme.RarityColor(entry.card.rarity);
                GUI.Label(new Rect(10f, y + 6f, content.width - 20f, 24f),
                    $"x{entry.quantity}  {entry.card.cardName}  [{entry.card.RarityLabel}]  • Deck x{inDeck}", row);
                GUI.contentColor = Color.white;
                GUI.Label(new Rect(10f, y + 31f, content.width - 20f, 38f),
                    $"{entry.card.kind} • {entry.card.attribute} • {entry.card.ShortStats}", row);
                GUI.contentColor = oldContent;
            }

            GUI.EndScrollView();
        }
    }
}
