using System;
using System.Linq;
using DuelGenesis.Cards;
using DuelGenesis.Player;
using DuelGenesis.Shops;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DuelGenesis.UI
{
    public class DeckBuilderUI : MonoBehaviour
    {
        private PlayerCollection _collection;
        private PlayerDeck _deck;
        private ThirdPersonPlayerController _playerController;
        private ThirdPersonCamera _cameraController;
        private PackOpeningUI _packOpening;

        private bool _open;
        private Vector2 _collectionScroll;
        private Vector2 _deckScroll;
        private string _search = string.Empty;
        private string _status = "Build a 40–60 card Main Deck. Maximum 3 copies of each card.";

        public bool IsOpen => _open;

        private void Update()
        {
            ResolveSystems();

            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return;

            if (keyboard.bKey.wasPressedThisFrame)
            {
                if (_open)
                    Close();
                else if (_packOpening == null || !_packOpening.IsOpen)
                    Open();
            }

            if (_open && keyboard.escapeKey.wasPressedThisFrame)
                Close();
        }

        private void ResolveSystems()
        {
            if (_collection == null)
                _collection = UnityEngine.Object.FindFirstObjectByType<PlayerCollection>();

            if (_deck == null)
                _deck = UnityEngine.Object.FindFirstObjectByType<PlayerDeck>();

            if (_playerController == null)
                _playerController = UnityEngine.Object.FindFirstObjectByType<ThirdPersonPlayerController>();

            if (_cameraController == null)
                _cameraController = UnityEngine.Object.FindFirstObjectByType<ThirdPersonCamera>();

            if (_packOpening == null)
                _packOpening = UnityEngine.Object.FindFirstObjectByType<PackOpeningUI>();
        }

        public void Open()
        {
            ResolveSystems();
            if (_collection == null || _deck == null) return;

            _open = true;
            _collectionScroll = Vector2.zero;
            _deckScroll = Vector2.zero;
            _playerController?.SetMovementEnabled(false);
            _cameraController?.SetLookEnabled(false);
            UpdateValidationStatus();
        }

        public void Close()
        {
            if (!_open) return;

            _open = false;
            _playerController?.SetMovementEnabled(true);
            _cameraController?.SetLookEnabled(true);
        }

        private void UpdateValidationStatus()
        {
            if (_deck == null)
            {
                _status = "Deck system unavailable.";
                return;
            }

            bool legal = _deck.Validate(_collection, out string message);
            _status = legal ? "✓ DECK LEGAL — Ready for dueling." : message;
        }

        private void OnGUI()
        {
            if (!_open || _collection == null || _deck == null) return;

            GUI.depth = -100;

            Rect full = new Rect(20f, 20f, Screen.width - 40f, Screen.height - 40f);
            GUI.Box(full, string.Empty);

            GUIStyle title = new GUIStyle(GUI.skin.label)
            {
                fontSize = 28,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };

            GUIStyle header = new GUIStyle(GUI.skin.label)
            {
                fontSize = 19,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft
            };

            GUIStyle row = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                wordWrap = true
            };

            GUIStyle statusStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 16,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };

            GUI.Label(new Rect(full.x + 20f, full.y + 12f, full.width - 40f, 38f), "DUEL: GENESIS — DECK BUILDER", title);

            float top = full.y + 58f;
            float toolbarHeight = 42f;
            float halfWidth = (full.width - 54f) * 0.5f;

            GUI.Label(new Rect(full.x + 18f, top, 170f, 28f), "Search Collection:", row);
            _search = GUI.TextField(new Rect(full.x + 160f, top + 2f, 250f, 28f), _search ?? string.Empty);

            if (GUI.Button(new Rect(full.xMax - 330f, top, 135f, 30f), "AUTO BUILD"))
            {
                int count = _deck.AutoBuild(_collection);
                _status = count >= PlayerDeck.MinimumDeckSize
                    ? $"Auto-built {count}-card deck."
                    : $"Only {count} usable owned cards. Open more packs to reach {PlayerDeck.MinimumDeckSize}.";
                UpdateValidationStatus();
            }

            if (GUI.Button(new Rect(full.xMax - 185f, top, 95f, 30f), "CLEAR"))
            {
                _deck.Clear();
                UpdateValidationStatus();
            }

            if (GUI.Button(new Rect(full.xMax - 80f, top, 55f, 30f), "X"))
                Close();

            float columnsTop = top + toolbarHeight;
            float columnsBottom = full.yMax - 70f;
            float columnsHeight = columnsBottom - columnsTop;
            Rect collectionPanel = new Rect(full.x + 16f, columnsTop, halfWidth, columnsHeight);
            Rect deckPanel = new Rect(collectionPanel.xMax + 22f, columnsTop, halfWidth, columnsHeight);

            DrawCollectionPanel(collectionPanel, header, row);
            DrawDeckPanel(deckPanel, header, row);

            UpdateValidationStatus();
            GUI.Label(new Rect(full.x + 20f, full.yMax - 58f, full.width - 40f, 28f),
                $"MAIN DECK: {_deck.MainDeckCount}/{PlayerDeck.MinimumDeckSize} minimum — {_status}", statusStyle);
            GUI.Label(new Rect(full.x + 20f, full.yMax - 32f, full.width - 40f, 22f),
                "B or ESC — Close   •   Click ADD / REMOVE to edit   •   Deck saves automatically", row);
        }

        private void DrawCollectionPanel(Rect panel, GUIStyle header, GUIStyle row)
        {
            GUI.Box(panel, string.Empty);
            GUI.Label(new Rect(panel.x + 12f, panel.y + 8f, panel.width - 24f, 30f),
                $"YOUR COLLECTION — {_collection.TotalCardCount} cards", header);

            var owned = _collection.GetOwnedCardsSorted();
            if (!string.IsNullOrWhiteSpace(_search))
            {
                owned = owned
                    .Where(entry =>
                        entry.card.cardName.IndexOf(_search, StringComparison.OrdinalIgnoreCase) >= 0 ||
                        entry.card.kind.ToString().IndexOf(_search, StringComparison.OrdinalIgnoreCase) >= 0 ||
                        entry.card.attribute.IndexOf(_search, StringComparison.OrdinalIgnoreCase) >= 0)
                    .ToList();
            }

            Rect scrollRect = new Rect(panel.x + 10f, panel.y + 44f, panel.width - 20f, panel.height - 54f);
            Rect content = new Rect(0f, 0f, scrollRect.width - 20f, Mathf.Max(scrollRect.height, owned.Count * 74f));
            _collectionScroll = GUI.BeginScrollView(scrollRect, _collectionScroll, content);

            for (int i = 0; i < owned.Count; i++)
            {
                var entry = owned[i];
                CardData card = entry.card;
                int inDeck = _deck.GetQuantity(card.id);
                float y = i * 74f;

                GUI.Box(new Rect(0f, y, content.width, 66f), string.Empty);
                GUI.Label(new Rect(8f, y + 5f, content.width - 95f, 22f),
                    $"{card.cardName} [{card.RarityLabel}]", row);
                GUI.Label(new Rect(8f, y + 27f, content.width - 95f, 34f),
                    $"Owned {entry.quantity} • In Deck {inDeck} • {card.kind} • {card.ShortStats}", row);

                bool canAdd = _deck.CanAdd(card, _collection, out _);
                GUI.enabled = canAdd;
                if (GUI.Button(new Rect(content.width - 78f, y + 17f, 66f, 32f), "ADD"))
                {
                    _deck.AddCard(card, _collection);
                    UpdateValidationStatus();
                }
                GUI.enabled = true;
            }

            GUI.EndScrollView();
        }

        private void DrawDeckPanel(Rect panel, GUIStyle header, GUIStyle row)
        {
            GUI.Box(panel, string.Empty);
            GUI.Label(new Rect(panel.x + 12f, panel.y + 8f, panel.width - 24f, 30f),
                $"MAIN DECK — {_deck.MainDeckCount}/{PlayerDeck.MaximumDeckSize}", header);

            var deckCards = _deck.GetDeckCardsSorted();
            Rect scrollRect = new Rect(panel.x + 10f, panel.y + 44f, panel.width - 20f, panel.height - 54f);
            Rect content = new Rect(0f, 0f, scrollRect.width - 20f, Mathf.Max(scrollRect.height, deckCards.Count * 74f));
            _deckScroll = GUI.BeginScrollView(scrollRect, _deckScroll, content);

            for (int i = 0; i < deckCards.Count; i++)
            {
                var entry = deckCards[i];
                CardData card = entry.card;
                float y = i * 74f;

                GUI.Box(new Rect(0f, y, content.width, 66f), string.Empty);
                GUI.Label(new Rect(8f, y + 5f, content.width - 108f, 22f),
                    $"x{entry.quantity}  {card.cardName}", row);
                GUI.Label(new Rect(8f, y + 27f, content.width - 108f, 34f),
                    $"{card.kind} • {card.attribute} • {card.ShortStats}", row);

                if (GUI.Button(new Rect(content.width - 92f, y + 17f, 80f, 32f), "REMOVE"))
                {
                    _deck.RemoveCard(card);
                    UpdateValidationStatus();
                }
            }

            GUI.EndScrollView();
        }
    }
}
