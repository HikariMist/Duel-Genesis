using System;
using System.Collections.Generic;
using System.Linq;
using DuelGenesis.Cards;
using DuelGenesis.Dueling;
using DuelGenesis.Player;
using DuelGenesis.Shops;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace DuelGenesis.UI
{
    /// <summary>
    /// Visual deck builder: your collection as a grid of real card faces on the left, a large card
    /// inspector in the middle and your Main + Extra Deck as card thumbnails on the right.
    /// Left-click adds / removes, hover inspects, right-click on the collection adds a full playset.
    /// Open with B (or from the pause / profile menus), close with B, ESC or the X button.
    /// </summary>
    public class DeckBuilderUI : MonoBehaviour
    {
        private enum Filter { All, Monster, Spell, Trap, Extra }
        private enum Sort { Name, Attack, Level, Rarity }

        private const float CollectionCellW = 132f, CollectionCellH = 192f;
        private const float DeckCellW = 56f, DeckCellH = 82f;
        private const int FaceRequestsPerFrame = 6;   // x8 visible cells polled per frame

        private PlayerCollection _collection;
        private PlayerDeck _deck;
        private ThirdPersonPlayerController _playerController;
        private ThirdPersonCamera _cameraController;
        private PackOpeningUI _packOpening;
        private DuelGameController _duel;

        private bool _open;
        private bool _built;
        private bool _dirty = true;
        private Filter _filter = Filter.All;
        private Sort _sort = Sort.Name;
        private string _search = string.Empty;
        private string _toast;
        private float _toastUntil;

        // UI
        private Canvas _canvas;
        private RectTransform _root;
        private InputField _searchField;
        private readonly Dictionary<Filter, Button> _filterButtons = new();
        private Button _sortButton;
        private ScrollRect _collectionScroll;
        private RectTransform _collectionContent;
        private RectTransform _mainContent, _extraContent;
        private ScrollRect _mainScroll;
        private Text _collectionHeader, _mainHeader, _extraHeader, _status, _breakdown, _toastText;
        private RawImage _inspectorImage;
        private Text _inspectorTitle, _inspectorBody;
        private CardData _inspected;

        private readonly List<CardCell> _collectionCells = new();
        private readonly List<CardCell> _mainCells = new();
        private readonly List<CardCell> _extraCells = new();

        public bool IsOpen => _open;

        // ================================================================== lifecycle

        private void Update()
        {
            ResolveSystems();

            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                bool typing = _searchField != null && _searchField.isFocused;
                if (!typing && keyboard.bKey.wasPressedThisFrame)
                {
                    if (_open) Close();
                    else if ((_packOpening == null || !_packOpening.IsOpen) && (_duel == null || !_duel.IsActive)) Open();
                }
                if (_open && keyboard.escapeKey.wasPressedThisFrame) Close();
                if (_open && !typing && keyboard.f5Key.wasPressedThisFrame) AutoBuild();
            }

            if (!_open) return;
            if (_dirty) Refresh();
            StreamFaces();
            if (_toastText != null)
                _toastText.color = new Color(1f, 1f, 1f, Mathf.Clamp01((_toastUntil - Time.unscaledTime) * 2f));
        }

        private void AutoBuild()
        {
            if (_deck == null || _collection == null) return;
            int count = _deck.AutoBuild(_collection);
            int spells = _deck.Entries.Where(e => e != null).Sum(e => CardDatabase.GetById(e.cardId)?.kind == CardKind.Monster ? 0 : e.quantity);
            Toast(count >= PlayerDeck.MinimumDeckSize ? $"Auto-built a {count}-card deck ({spells} Spells/Traps)." : $"Only {count} usable cards — open more packs.");
            _dirty = true;
        }

        private void ResolveSystems()
        {
            if (_collection == null) _collection = FindFirstObjectByType<PlayerCollection>();
            if (_deck == null)
            {
                _deck = FindFirstObjectByType<PlayerDeck>();
                if (_deck != null) _deck.DeckChanged += MarkDirty;
            }
            if (_playerController == null) _playerController = FindFirstObjectByType<ThirdPersonPlayerController>();
            if (_cameraController == null) _cameraController = FindFirstObjectByType<ThirdPersonCamera>();
            if (_packOpening == null) _packOpening = FindFirstObjectByType<PackOpeningUI>();
            if (_duel == null) _duel = FindFirstObjectByType<DuelGameController>();
        }

        private void OnDestroy()
        {
            if (_deck != null) _deck.DeckChanged -= MarkDirty;
        }

        private void MarkDirty() => _dirty = true;

        public void Open()
        {
            ResolveSystems();
            if (_collection == null || _deck == null || (_duel != null && _duel.IsActive)) return;
            if (!_built) Build();

            _open = true;
            _dirty = true;
            _canvas.gameObject.SetActive(true);
            _playerController?.SetMovementEnabled(false);
            _cameraController?.SetLookEnabled(false);
            if (_collectionScroll != null) _collectionScroll.verticalNormalizedPosition = 1f;
        }

        public void Close()
        {
            if (!_open) return;
            _open = false;
            if (_canvas != null) _canvas.gameObject.SetActive(false);
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
            _playerController?.SetMovementEnabled(true);
            _cameraController?.SetLookEnabled(true);
        }

        // ================================================================== data

        private List<(CardData card, int owned)> FilteredCollection()
        {
            IEnumerable<(CardData card, int owned)> owned = _collection.GetOwnedCardsSorted();
            owned = _filter switch
            {
                Filter.Monster => owned.Where(e => e.card.kind == CardKind.Monster && !PlayerDeck.IsExtraDeckCard(e.card)),
                Filter.Spell => owned.Where(e => e.card.kind == CardKind.Spell),
                Filter.Trap => owned.Where(e => e.card.kind == CardKind.Trap),
                Filter.Extra => owned.Where(e => PlayerDeck.IsExtraDeckCard(e.card)),
                _ => owned
            };
            owned = owned.Where(e => e.card.ResolvedFrameKind != CardFrameKind.Token);
            if (!string.IsNullOrWhiteSpace(_search))
            {
                string s = _search.Trim();
                owned = owned.Where(e =>
                    e.card.cardName.IndexOf(s, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    (e.card.typeLine ?? string.Empty).IndexOf(s, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    (e.card.attribute ?? string.Empty).IndexOf(s, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    (e.card.effectText ?? string.Empty).IndexOf(s, StringComparison.OrdinalIgnoreCase) >= 0);
            }
            owned = _sort switch
            {
                Sort.Attack => owned.OrderBy(e => KindOrder(e.card)).ThenByDescending(e => e.card.attack).ThenBy(e => e.card.cardName),
                Sort.Level => owned.OrderBy(e => KindOrder(e.card)).ThenByDescending(e => e.card.level).ThenBy(e => e.card.cardName),
                Sort.Rarity => owned.OrderByDescending(e => e.card.rarity).ThenBy(e => e.card.cardName),
                _ => owned.OrderBy(e => KindOrder(e.card)).ThenBy(e => e.card.cardName)
            };
            return owned.ToList();
        }

        private static int KindOrder(CardData c) =>
            c.kind == CardKind.Monster ? (PlayerDeck.IsExtraDeckCard(c) ? 3 : 0) : c.kind == CardKind.Spell ? 1 : 2;

        private void Refresh()
        {
            _dirty = false;
            if (!_built) return;

            // ---------- collection grid
            List<(CardData card, int owned)> owned = FilteredCollection();
            EnsureCells(_collectionCells, owned.Count, _collectionContent, CollectionCellW, CollectionCellH, true);
            for (int i = 0; i < owned.Count; i++)
            {
                CardData card = owned[i].card;
                int inDeck = _deck.GetQuantity(card.id);
                bool canAdd = _deck.CanAdd(card, _collection, out _);
                _collectionCells[i].Bind(card, $"x{owned[i].owned}", inDeck > 0 ? $"{inDeck} in deck" : null, canAdd ? 1f : 0.45f);
            }
            _collectionHeader.text = $"COLLECTION  <color=#8fa3c7>{owned.Count} shown • {_collection.TotalCardCount} cards owned</color>";

            // ---------- deck (one thumbnail per copy)
            var deckCards = _deck.GetDeckCardsSorted().OrderBy(e => KindOrder(e.card)).ThenBy(e => e.card.cardName).ToList();
            List<CardData> main = new(), extra = new();
            foreach (var entry in deckCards)
                for (int n = 0; n < entry.quantity; n++)
                    (PlayerDeck.IsExtraDeckCard(entry.card) ? extra : main).Add(entry.card);

            EnsureCells(_mainCells, main.Count, _mainContent, DeckCellW, DeckCellH, false);
            for (int i = 0; i < main.Count; i++) _mainCells[i].Bind(main[i], null, null, 1f);
            EnsureCells(_extraCells, extra.Count, _extraContent, DeckCellW, DeckCellH, false);
            for (int i = 0; i < extra.Count; i++) _extraCells[i].Bind(extra[i], null, null, 1f);

            int monsters = main.Count(c => c.kind == CardKind.Monster);
            int spells = main.Count(c => c.kind == CardKind.Spell);
            int traps = main.Count(c => c.kind == CardKind.Trap);
            _mainHeader.text = $"MAIN DECK  <color=#ffd060>{main.Count}</color><color=#8fa3c7>/{PlayerDeck.MinimumDeckSize}–{PlayerDeck.MaximumDeckSize}</color>";
            _extraHeader.text = $"EXTRA DECK  <color=#c79bff>{extra.Count}</color><color=#8fa3c7>/{PlayerDeck.MaximumExtraDeckSize}</color>";
            _breakdown.text = $"<color=#e0a060>{monsters} Monsters</color>   <color=#3fd6a0>{spells} Spells</color>   <color=#e060b0>{traps} Traps</color>";

            bool legal = _deck.Validate(_collection, out string message);
            _status.text = legal ? "<color=#5cff9a>✓ DECK LEGAL — ready to duel</color>" : $"<color=#ff8080>{message}</color>";

            foreach (var pair in _filterButtons)
            {
                Image bg = pair.Value.targetGraphic as Image;
                if (bg != null) bg.color = pair.Key == _filter ? new Color(0.10f, 0.45f, 0.65f, 1f) : new Color(0.08f, 0.10f, 0.16f, 0.95f);
            }
            UiKit.SetButtonLabel(_sortButton, "SORT: " + _sort.ToString().ToUpperInvariant());
            if (_inspected == null && owned.Count > 0) Inspect(owned[0].card);
        }

        private void EnsureCells(List<CardCell> cells, int count, RectTransform parent, float w, float h, bool collection)
        {
            while (cells.Count < count)
            {
                CardCell cell = CardCell.Create(parent, w, h, collection);
                cell.Clicked += OnCellClicked;
                cell.RightClicked += OnCellRightClicked;
                cell.Hovered += Inspect;
                cells.Add(cell);
            }
            for (int i = 0; i < cells.Count; i++) cells[i].gameObject.SetActive(i < count);
        }

        // ================================================================== interaction

        private void OnCellClicked(CardCell cell)
        {
            if (cell.Card == null) return;
            if (cell.IsCollection)
            {
                if (_deck.CanAdd(cell.Card, _collection, out string reason)) _deck.AddCard(cell.Card, _collection);
                else Toast(reason);
            }
            else
            {
                _deck.RemoveCard(cell.Card);
            }
            _dirty = true;
        }

        private void OnCellRightClicked(CardCell cell)
        {
            if (cell.Card == null) return;
            if (cell.IsCollection)
            {
                int added = 0;
                while (_deck.CanAdd(cell.Card, _collection, out _) && added < PlayerDeck.MaximumCopiesPerCard) { _deck.AddCard(cell.Card, _collection); added++; }
                if (added == 0 && !_deck.CanAdd(cell.Card, _collection, out string reason)) Toast(reason);
            }
            else
            {
                while (_deck.GetQuantity(cell.Card.id) > 0) _deck.RemoveCard(cell.Card);
            }
            _dirty = true;
        }

        private void Inspect(CardData card)
        {
            if (card == null || _inspectorImage == null) return;
            _inspected = card;
            _inspectorImage.texture = CardFaceCompositor.TryGetFace(card, out Texture2D face) ? face : null;
            _inspectorImage.color = _inspectorImage.texture != null ? Color.white : new Color(0.12f, 0.14f, 0.2f, 1f);
            _inspectorTitle.text = card.cardName;
            string stats = card.kind == CardKind.Monster
                ? $"{card.attribute} • Level {card.level} • {card.typeLine}\nATK {card.attack}  /  DEF {card.defense}"
                : $"{card.kind} • {card.typeLine}";
            int owned = _collection != null ? _collection.GetQuantity(card.id) : 0;
            int inDeck = _deck != null ? _deck.GetQuantity(card.id) : 0;
            _inspectorBody.text = $"<color=#9fb4d8>{stats}</color>\n<color=#ffd060>{card.RarityLabel}</color>  •  owned {owned}  •  in deck {inDeck}\n\n{card.effectText}";
        }

        private void Toast(string message)
        {
            if (string.IsNullOrEmpty(message) || _toastText == null) return;
            _toastText.text = message;
            _toastUntil = Time.unscaledTime + 2.2f;
        }

        /// <summary>Requests card faces only for cells that are actually on screen.</summary>
        private void StreamFaces()
        {
            int requested = 0;
            Rect view = WorldRect(_collectionScroll.viewport);
            foreach (CardCell cell in _collectionCells)
            {
                if (requested >= FaceRequestsPerFrame * 8) break;
                if (!cell.gameObject.activeSelf || cell.HasFace) continue;
                if (!view.Overlaps(WorldRect((RectTransform)cell.transform))) continue;
                cell.TryLoadFace();   // queues the face; it appears once the compositor has drawn it
                requested++;
            }
            Rect mainView = WorldRect(_mainScroll.viewport);
            foreach (CardCell cell in _mainCells.Concat(_extraCells))
            {
                if (!cell.gameObject.activeSelf || cell.HasFace) continue;
                if (cell.IsInExtraRow || mainView.Overlaps(WorldRect((RectTransform)cell.transform))) cell.TryLoadFace();
            }
            if (_inspected != null && _inspectorImage.texture == null && CardFaceCompositor.TryGetFace(_inspected, out Texture2D face))
            {
                _inspectorImage.texture = face;
                _inspectorImage.color = Color.white;
            }
        }

        private static readonly Vector3[] Corners = new Vector3[4];

        private static Rect WorldRect(RectTransform rt)
        {
            rt.GetWorldCorners(Corners);
            return new Rect(Corners[0].x, Corners[0].y, Corners[2].x - Corners[0].x, Corners[2].y - Corners[0].y);
        }

        // ================================================================== build

        private void Build()
        {
            _built = true;
            UiKit.EnsureEventSystem();
            GameObject canvasGo = new GameObject("DG Deck Builder Canvas", typeof(RectTransform));
            canvasGo.transform.SetParent(transform, false);
            _canvas = canvasGo.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 500;   // above every other overlay (HUDs, menus)
            CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();
            _root = (RectTransform)canvasGo.transform;

            Image backdrop = UiKit.Fill(_root, "Backdrop", new Color(0.01f, 0.015f, 0.03f, 0.92f));
            backdrop.raycastTarget = true;
            UiKit.Stretch(backdrop.rectTransform);

            // ---------- top bar
            Text title = UiKit.Label(_root, "Title", "DECK BUILDER", 34, UiKit.TextColor, TextAnchor.MiddleLeft, FontStyle.Bold);
            UiKit.Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(36f, -18f), new Vector2(360f, 50f));
            UiKit.Glow(title, new Color(0.1f, 0.8f, 1f, 0.5f));

            _searchField = BuildSearchField(_root);
            UiKit.Place((RectTransform)_searchField.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(330f, -24f), new Vector2(330f, 40f));

            float x = 680f;
            foreach (Filter f in Enum.GetValues(typeof(Filter)))
            {
                Filter captured = f;
                Button b = UiKit.Button(_root, "Filter " + f, f.ToString().ToUpperInvariant(), DuelVisualResources.Cyan, () => { _filter = captured; _dirty = true; _collectionScroll.verticalNormalizedPosition = 1f; }, 17);
                UiKit.Place((RectTransform)b.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(x, -24f), new Vector2(104f, 40f));
                _filterButtons[f] = b;
                x += 112f;
            }
            _sortButton = UiKit.Button(_root, "Sort", "SORT: NAME", DuelVisualResources.Violet, () =>
            {
                _sort = (Sort)(((int)_sort + 1) % Enum.GetValues(typeof(Sort)).Length);
                _dirty = true;
            }, 17);
            UiKit.Place((RectTransform)_sortButton.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(x + 8f, -24f), new Vector2(170f, 40f));

            Button auto = UiKit.Button(_root, "Auto", "AUTO BUILD", DuelVisualResources.Gold, AutoBuild, 17);
            UiKit.Place((RectTransform)auto.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-236f, -24f), new Vector2(150f, 40f));
            Button clear = UiKit.Button(_root, "Clear", "CLEAR", DuelVisualResources.Magenta, () => { _deck.Clear(); _dirty = true; }, 17);
            UiKit.Place((RectTransform)clear.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-124f, -24f), new Vector2(104f, 40f));
            Button close = UiKit.Button(_root, "Close", "✕", DuelVisualResources.Violet, Close, 22);
            UiKit.Place((RectTransform)close.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-30f, -24f), new Vector2(58f, 40f));

            // ---------- collection (left)
            Image collectionPanel = UiKit.Panel(_root, "Collection Panel", UiKit.PanelColor);
            RectTransform cp = collectionPanel.rectTransform;
            cp.anchorMin = new Vector2(0f, 0f); cp.anchorMax = new Vector2(0f, 1f); cp.pivot = new Vector2(0f, 0.5f);
            cp.offsetMin = new Vector2(24f, 70f); cp.offsetMax = new Vector2(24f + 924f, -84f);
            _collectionHeader = UiKit.Label(cp, "Header", "COLLECTION", 20, UiKit.TextColor, TextAnchor.MiddleLeft, FontStyle.Bold);
            UiKit.Place(_collectionHeader.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(18f, -8f), new Vector2(880f, 34f));
            _collectionScroll = BuildScroll(cp, "Collection", new Vector4(12f, 12f, 12f, 48f), out _collectionContent);
            GridLayoutGroup grid = _collectionContent.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(CollectionCellW, CollectionCellH);
            grid.spacing = new Vector2(12f, 14f);
            grid.padding = new RectOffset(8, 8, 8, 8);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 6;
            _collectionContent.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            Text help = UiKit.Label(_root, "Help", "Click a card to add it  •  right-click adds a playset  •  click a deck card to remove it (right-click removes all)  •  hover to inspect  •  F5 auto-builds  •  B / ESC closes", 15, UiKit.MutedText, TextAnchor.MiddleLeft);
            UiKit.Place(help.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(32f, 22f), new Vector2(1300f, 30f));

            // ---------- inspector (middle)
            Image inspector = UiKit.Panel(_root, "Inspector", UiKit.PanelColor);
            RectTransform ip = inspector.rectTransform;
            ip.anchorMin = new Vector2(0f, 0f); ip.anchorMax = new Vector2(0f, 1f); ip.pivot = new Vector2(0f, 0.5f);
            ip.offsetMin = new Vector2(962f, 70f); ip.offsetMax = new Vector2(962f + 372f, -84f);
            _inspectorImage = UiKit.Rect(ip, "Card").gameObject.AddComponent<RawImage>();
            UiKit.Place(_inspectorImage.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -16f), new Vector2(330f, 330f / ProductionCardVisualDrawer.CardAspect));
            _inspectorTitle = UiKit.Label(ip, "Name", "", 22, UiKit.TextColor, TextAnchor.UpperLeft, FontStyle.Bold);
            UiKit.Place(_inspectorTitle.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(20f, -506f), new Vector2(332f, 32f));
            _inspectorBody = UiKit.Label(ip, "Body", "", 15, UiKit.TextColor, TextAnchor.UpperLeft);
            _inspectorBody.verticalOverflow = VerticalWrapMode.Truncate;
            RectTransform ib = _inspectorBody.rectTransform;
            ib.anchorMin = new Vector2(0f, 0f); ib.anchorMax = new Vector2(1f, 1f);
            ib.offsetMin = new Vector2(20f, 14f); ib.offsetMax = new Vector2(-18f, -540f);

            // ---------- deck (right)
            Image deckPanel = UiKit.Panel(_root, "Deck Panel", UiKit.PanelColor);
            RectTransform dp = deckPanel.rectTransform;
            dp.anchorMin = new Vector2(0f, 0f); dp.anchorMax = new Vector2(1f, 1f);
            dp.offsetMin = new Vector2(1348f, 70f); dp.offsetMax = new Vector2(-24f, -84f);
            _mainHeader = UiKit.Label(dp, "Main Header", "MAIN DECK", 20, UiKit.TextColor, TextAnchor.MiddleLeft, FontStyle.Bold);
            UiKit.Place(_mainHeader.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(16f, -8f), new Vector2(520f, 32f));
            _breakdown = UiKit.Label(dp, "Breakdown", "", 15, UiKit.TextColor, TextAnchor.MiddleLeft);
            UiKit.Place(_breakdown.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(16f, -38f), new Vector2(520f, 24f));

            _mainScroll = BuildScroll(dp, "Main", new Vector4(10f, 214f, 10f, 66f), out _mainContent);
            GridLayoutGroup mainGrid = _mainContent.gameObject.AddComponent<GridLayoutGroup>();
            mainGrid.cellSize = new Vector2(DeckCellW, DeckCellH);
            mainGrid.spacing = new Vector2(5f, 7f);
            mainGrid.padding = new RectOffset(6, 6, 6, 6);
            mainGrid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            mainGrid.constraintCount = 8;
            _mainContent.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            _extraHeader = UiKit.Label(dp, "Extra Header", "EXTRA DECK", 20, UiKit.TextColor, TextAnchor.MiddleLeft, FontStyle.Bold);
            UiKit.Place(_extraHeader.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(16f, 176f), new Vector2(520f, 32f));
            Image extraBack = UiKit.Fill(dp, "Extra Row", new Color(0.12f, 0.06f, 0.2f, 0.6f));
            RectTransform er = extraBack.rectTransform;
            er.anchorMin = new Vector2(0f, 0f); er.anchorMax = new Vector2(1f, 0f); er.pivot = new Vector2(0.5f, 0f);
            er.offsetMin = new Vector2(10f, 72f); er.offsetMax = new Vector2(-10f, 172f);
            _extraContent = UiKit.Rect(er, "Extra Cards");
            UiKit.Stretch(_extraContent, 6f, 6f, 6f, 6f);
            HorizontalLayoutGroup extraRow = _extraContent.gameObject.AddComponent<HorizontalLayoutGroup>();
            extraRow.spacing = -26f;   // Extra Deck fans like a hand so 15 cards fit
            extraRow.childAlignment = TextAnchor.MiddleLeft;
            extraRow.childForceExpandWidth = false;
            extraRow.childForceExpandHeight = false;
            extraRow.childControlWidth = false;
            extraRow.childControlHeight = false;

            _status = UiKit.Label(dp, "Status", "", 17, UiKit.TextColor, TextAnchor.MiddleLeft, FontStyle.Bold);
            UiKit.Place(_status.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(16f, 14f), new Vector2(530f, 48f));
            _status.horizontalOverflow = HorizontalWrapMode.Wrap;

            _toastText = UiKit.Label(_root, "Toast", "", 22, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            UiKit.Place(_toastText.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 60f), new Vector2(900f, 40f));
            UiKit.Glow(_toastText, new Color(0f, 0f, 0f, 0.8f));
            _toastText.color = Color.clear;

            _canvas.gameObject.SetActive(false);
        }

        private InputField BuildSearchField(Transform parent)
        {
            Image bg = UiKit.Panel(parent, "Search", new Color(0.06f, 0.08f, 0.13f, 0.98f));
            InputField field = bg.gameObject.AddComponent<InputField>();
            Text text = UiKit.Label(bg.transform, "Text", "", 18, UiKit.TextColor);
            text.supportRichText = false;
            UiKit.Stretch(text.rectTransform, 14f, 4f, 14f, 4f);
            Text placeholder = UiKit.Label(bg.transform, "Placeholder", "Search name, type, effect…", 18, UiKit.MutedText, TextAnchor.MiddleLeft, FontStyle.Italic);
            UiKit.Stretch(placeholder.rectTransform, 14f, 4f, 14f, 4f);
            field.textComponent = text;
            field.placeholder = placeholder;
            field.targetGraphic = bg;
            field.onValueChanged.AddListener(v => { _search = v; _dirty = true; });
            return field;
        }

        private static ScrollRect BuildScroll(RectTransform parent, string name, Vector4 insets, out RectTransform content)
        {
            RectTransform area = UiKit.Rect(parent, name + " Scroll");
            UiKit.Stretch(area, insets.x, insets.y, insets.z, insets.w);
            ScrollRect scroll = area.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.scrollSensitivity = 40f;
            scroll.movementType = ScrollRect.MovementType.Clamped;

            RectTransform viewport = UiKit.Rect(area, "Viewport");
            UiKit.Stretch(viewport, 0f, 0f, 14f, 0f);
            viewport.gameObject.AddComponent<RectMask2D>();
            Image hit = viewport.gameObject.AddComponent<Image>();
            hit.color = new Color(0f, 0f, 0f, 0.001f);

            content = UiKit.Rect(viewport, "Content");
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.offsetMin = Vector2.zero;
            content.offsetMax = Vector2.zero;

            RectTransform bar = UiKit.Rect(area, "Scrollbar");
            bar.anchorMin = new Vector2(1f, 0f); bar.anchorMax = new Vector2(1f, 1f); bar.pivot = new Vector2(1f, 0.5f);
            bar.offsetMin = new Vector2(-8f, 0f); bar.offsetMax = Vector2.zero;
            Image barBg = bar.gameObject.AddComponent<Image>();
            barBg.color = new Color(1f, 1f, 1f, 0.05f);
            Scrollbar scrollbar = bar.gameObject.AddComponent<Scrollbar>();
            scrollbar.direction = Scrollbar.Direction.BottomToTop;
            RectTransform handle = UiKit.Rect(bar, "Handle");
            UiKit.Stretch(handle);
            Image handleImage = handle.gameObject.AddComponent<Image>();
            handleImage.color = new Color(0.3f, 0.75f, 1f, 0.55f);
            scrollbar.handleRect = handle;
            scrollbar.targetGraphic = handleImage;

            scroll.viewport = viewport;
            scroll.content = content;
            scroll.verticalScrollbar = scrollbar;
            scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
            return scroll;
        }

        // ================================================================== card cell

        private sealed class CardCell : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
        {
            public CardData Card { get; private set; }
            public bool IsCollection { get; private set; }
            public bool IsInExtraRow => transform.parent != null && transform.parent.GetComponent<HorizontalLayoutGroup>() != null;
            public bool HasFace => _face.texture != null && _boundTo == Card;

            public event Action<CardCell> Clicked;
            public event Action<CardCell> RightClicked;
            public event Action<CardData> Hovered;

            private RawImage _face;
            private Text _name, _badge, _tag;
            private Image _hover;
            private CanvasGroup _group;
            private CardData _boundTo;

            public static CardCell Create(Transform parent, float w, float h, bool collection)
            {
                RectTransform rt = UiKit.Rect(parent, "Card");
                rt.sizeDelta = new Vector2(w, h);
                CardCell cell = rt.gameObject.AddComponent<CardCell>();
                cell.IsCollection = collection;
                cell._group = rt.gameObject.AddComponent<CanvasGroup>();

                Image back = rt.gameObject.AddComponent<Image>();
                back.color = new Color(0.10f, 0.12f, 0.18f, 1f);
                back.raycastTarget = true;

                cell._face = UiKit.Rect(rt, "Face").gameObject.AddComponent<RawImage>();
                cell._face.raycastTarget = false;
                UiKit.Stretch(cell._face.rectTransform);

                cell._name = UiKit.Label(rt, "Name", "", collection ? 12 : 9, UiKit.TextColor, TextAnchor.MiddleCenter, FontStyle.Bold);
                UiKit.Stretch(cell._name.rectTransform, 4f, 4f, 4f, 4f);
                cell._name.verticalOverflow = VerticalWrapMode.Overflow;

                cell._hover = UiKit.Fill(rt, "Hover", new Color(0.3f, 0.85f, 1f, 0f));
                UiKit.Stretch(cell._hover.rectTransform, -3f, -3f, -3f, -3f);
                cell._hover.transform.SetAsFirstSibling();

                if (collection)
                {
                    cell._badge = BadgeLabel(rt, "Owned", new Vector2(1f, 1f), new Vector2(-4f, -4f), new Color(0.05f, 0.07f, 0.12f, 0.9f), DuelVisualResources.Gold);
                    cell._tag = BadgeLabel(rt, "InDeck", new Vector2(0f, 0f), new Vector2(4f, 4f), new Color(0.05f, 0.35f, 0.25f, 0.92f), Color.white);
                }
                return cell;
            }

            private static Text BadgeLabel(RectTransform parent, string name, Vector2 anchor, Vector2 pos, Color bg, Color fg)
            {
                Image back = UiKit.Fill(parent, name, bg);
                UiKit.Place(back.rectTransform, anchor, anchor, pos, new Vector2(anchor.x > 0.5f ? 38f : 74f, 22f));
                Text t = UiKit.Label(back.transform, "Text", "", 13, fg, TextAnchor.MiddleCenter, FontStyle.Bold);
                UiKit.Stretch(t.rectTransform);
                return t;
            }

            public void Bind(CardData card, string badge, string tag, float alpha)
            {
                if (card != _boundTo)
                {
                    Card = card;
                    _face.texture = null;
                    _face.color = new Color(1f, 1f, 1f, 0f);
                    _name.text = card.cardName;
                }
                Card = card;
                _group.alpha = alpha;
                if (_badge != null)
                {
                    _badge.text = badge ?? string.Empty;
                    _badge.transform.parent.gameObject.SetActive(!string.IsNullOrEmpty(badge));
                }
                if (_tag != null)
                {
                    _tag.text = tag ?? string.Empty;
                    _tag.transform.parent.gameObject.SetActive(!string.IsNullOrEmpty(tag));
                }
                if (!IsCollection && _face.texture == null) TryLoadFace();
            }

            public bool TryLoadFace()
            {
                if (Card == null) return false;
                if (!CardFaceCompositor.TryGetFace(Card, out Texture2D face) || face == null) return false;
                _face.texture = face;
                _face.color = Color.white;
                _boundTo = Card;
                _name.text = string.Empty;
                return true;
            }

            public void OnPointerClick(PointerEventData e)
            {
                if (e.button == PointerEventData.InputButton.Left) Clicked?.Invoke(this);
                else if (e.button == PointerEventData.InputButton.Right) RightClicked?.Invoke(this);
            }

            public void OnPointerEnter(PointerEventData e)
            {
                _hover.color = new Color(0.3f, 0.85f, 1f, 0.9f);
                Hovered?.Invoke(Card);
            }

            public void OnPointerExit(PointerEventData e) => _hover.color = new Color(0.3f, 0.85f, 1f, 0f);
        }
    }
}
