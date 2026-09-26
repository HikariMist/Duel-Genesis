using DuelGenesis.Cards;
using DuelGenesis.Economy;
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
        private DeckBuilderUI _deckBuilder;
        private PackOpeningUI _packOpening;
        private bool _showCollection;
        private Vector2 _scroll;

        private void Update()
        {
            ResolvePlayerSystems();

            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.cKey.wasPressedThisFrame)
            {
                bool blocked = (_deckBuilder != null && _deckBuilder.IsOpen) ||
                               (_packOpening != null && _packOpening.IsOpen);
                if (!blocked)
                    _showCollection = !_showCollection;
            }

            if (_deckBuilder != null && _deckBuilder.IsOpen)
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
            if (_deckBuilder == null)
                _deckBuilder = Object.FindFirstObjectByType<DeckBuilderUI>();
            if (_packOpening == null)
                _packOpening = Object.FindFirstObjectByType<PackOpeningUI>();
        }

        private void OnGUI()
        {
            ResolvePlayerSystems();

            GUIStyle hud = new GUIStyle(GUI.skin.box)
            {
                fontSize = 16,
                alignment = TextAnchor.UpperLeft,
                padding = new RectOffset(12, 12, 10, 10)
            };

            int gc = _wallet != null ? _wallet.GenesisCredits : 0;
            int cards = _collection != null ? _collection.TotalCardCount : 0;
            int unique = _collection != null ? _collection.UniqueCardCount : 0;
            int deckCount = _deck != null ? _deck.MainDeckCount : 0;
            bool deckLegal = _deck != null && _deck.Validate(_collection, out _);

            string deckState = deckLegal ? "LEGAL" : "INCOMPLETE";
            GUI.Box(new Rect(18f, 18f, 300f, 118f),
                $"GENESIS CREDITS: {gc:N0} GC\n" +
                $"COLLECTION: {cards} cards / {unique} unique\n" +
                $"MAIN DECK: {deckCount} cards — {deckState}\n" +
                "[C] Collection   [B] Deck Builder", hud);

            if (!_showCollection || _collection == null) return;

            float width = Mathf.Min(620f, Screen.width * 0.44f);
            float height = Mathf.Min(720f, Screen.height - 80f);
            Rect panel = new Rect(Screen.width - width - 24f, 24f, width, height);
            GUI.Box(panel, string.Empty);

            GUIStyle title = new GUIStyle(GUI.skin.label)
            {
                fontSize = 26,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };

            GUIStyle row = new GUIStyle(GUI.skin.label)
            {
                fontSize = 15,
                wordWrap = true
            };

            GUI.Label(new Rect(panel.x + 16f, panel.y + 12f, width - 32f, 38f), "CARD COLLECTION", title);
            GUI.Label(new Rect(panel.x + 16f, panel.y + 48f, width - 32f, 24f), "Press C to close • Press B for Deck Builder", row);

            var owned = _collection.GetOwnedCardsSorted();
            Rect scrollRect = new Rect(panel.x + 18f, panel.y + 80f, width - 36f, height - 100f);
            Rect content = new Rect(0f, 0f, scrollRect.width - 20f, Mathf.Max(scrollRect.height, owned.Count * 76f));
            _scroll = GUI.BeginScrollView(scrollRect, _scroll, content);

            for (int i = 0; i < owned.Count; i++)
            {
                var entry = owned[i];
                float y = i * 76f;
                int inDeck = _deck != null ? _deck.GetQuantity(entry.card.id) : 0;
                GUI.Box(new Rect(0f, y, content.width, 68f), string.Empty);
                GUI.Label(new Rect(10f, y + 6f, content.width - 20f, 24f),
                    $"x{entry.quantity}  {entry.card.cardName}  [{entry.card.RarityLabel}]  • Deck x{inDeck}", row);
                GUI.Label(new Rect(10f, y + 31f, content.width - 20f, 34f),
                    $"{entry.card.kind} • {entry.card.attribute} • {entry.card.ShortStats}", row);
            }

            GUI.EndScrollView();
        }
    }
}
