using System.Collections.Generic;
using DuelGenesis.Cards;
using DuelGenesis.Economy;
using DuelGenesis.Player;
using DuelGenesis.UI;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DuelGenesis.Shops
{
    /// <summary>
    /// The card shop counter: pick one of the nine Genesis boosters (each with its own wrapper art), pay,
    /// watch the pack tear open, then flip the 9 cards one by one. Cards go into the collection the moment
    /// the pack is bought, so closing early never loses them.
    /// </summary>
    public class PackOpeningUI : MonoBehaviour
    {
        private enum Stage { Closed, Shop, Tearing, Reveal }

        private const float TearSeconds = 1.25f;

        private readonly List<CardData> _lastPack = new();
        private readonly List<float> _flippedAt = new();   // when each card was turned face-up (for the rare-pull burst)
        private Stage _stage = Stage.Closed;
        private int _revealedCount;
        private GenesisPackType _pack;
        private float _stageStart;
        private string _message = "";
        private float _messageUntil;
        private string _shopName = "Genesis Card Shop";

        private GameObject _player;
        private GenesisWallet _wallet;
        private PlayerCollection _collection;
        private ThirdPersonPlayerController _playerController;
        private ThirdPersonCamera _thirdPersonCamera;

        public bool IsOpen => _stage != Stage.Closed;

        /// <summary>Opens the pack counter for <paramref name="player"/>.</summary>
        public bool OpenShop(GameObject player, string shopName = null)
        {
            if (IsOpen || player == null || !CardDatabase.IsReady) return false;
            _player = player;
            _wallet = player.GetComponent<GenesisWallet>();
            _collection = player.GetComponent<PlayerCollection>() ?? player.AddComponent<PlayerCollection>();
            if (!string.IsNullOrEmpty(shopName)) _shopName = shopName;

            _playerController = player.GetComponent<ThirdPersonPlayerController>();
            _thirdPersonCamera = Object.FindFirstObjectByType<ThirdPersonCamera>();
            _playerController?.SetMovementEnabled(false);
            _thirdPersonCamera?.SetLookEnabled(false);
            SetStage(Stage.Shop);
            return true;
        }

        /// <summary>Kept for older callers: opens the pack counter.</summary>
        public bool OpenPack(GameObject player) => OpenShop(player);

        public void Close()
        {
            if (!IsOpen) return;
            _stage = Stage.Closed;
            _playerController?.SetMovementEnabled(true);
            _thirdPersonCamera?.SetLookEnabled(true);
            _playerController = null;
            _thirdPersonCamera = null;
            _player = null;
        }

        private void SetStage(Stage stage)
        {
            _stage = stage;
            _stageStart = Time.unscaledTime;
        }

        private void Flash(string message)
        {
            _message = message;
            _messageUntil = Time.unscaledTime + 2.5f;
        }

        private bool Buy(GenesisPackType pack)
        {
            if (_wallet == null) { Flash("No wallet on this player."); return false; }
            if (!_wallet.CanAfford(pack.price)) { Flash($"Not enough GC for the {pack.displayName} ({pack.price:N0} GC)."); return false; }

            List<CardData> cards = GenesisPacks.Roll(pack);
            if (cards == null) { Flash($"No cards in the catalog fit the {pack.displayName} yet."); return false; }
            if (!_wallet.Spend(pack.price)) return false;

            foreach (CardData card in cards) _collection.AddCard(card);
            _lastPack.Clear();
            _lastPack.AddRange(cards);
            _flippedAt.Clear();
            foreach (CardData _ in cards) _flippedAt.Add(-1f);
            _pack = pack;
            _revealedCount = 0;
            SetStage(Stage.Tearing);
            return true;
        }

        // ------------------------------------------------------------------ input

        private void Update()
        {
            if (!IsOpen) return;
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return;

            if (keyboard.escapeKey.wasPressedThisFrame)
            {
                if (_stage == Stage.Reveal && _revealedCount >= _lastPack.Count) SetStage(Stage.Shop);
                else if (_stage == Stage.Shop) Close();
                else if (_stage == Stage.Reveal) _revealedCount = _lastPack.Count;
                return;
            }

            switch (_stage)
            {
                case Stage.Shop:
                    for (int i = 0; i < GenesisPacks.All.Length && i < 9; i++)
                        if (keyboard[Key.Digit1 + i].wasPressedThisFrame) Buy(GenesisPacks.All[i]);
                    break;
                case Stage.Tearing:
                    if (Time.unscaledTime - _stageStart > TearSeconds) SetStage(Stage.Reveal);
                    break;
                case Stage.Reveal:
                    if (keyboard.rKey.wasPressedThisFrame) _revealedCount = _lastPack.Count;
                    else if ((keyboard.spaceKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame))
                    {
                        if (_revealedCount < _lastPack.Count) _revealedCount++;
                        else if (keyboard.enterKey.wasPressedThisFrame) Buy(_pack);
                    }
                    break;
            }
        }

        // ------------------------------------------------------------------ drawing

        private void OnGUI()
        {
            if (!IsOpen) return;
            GUI.depth = -50;

            float width = Mathf.Min(1240f, Screen.width - 40f);
            float height = Mathf.Min(900f, Screen.height - 40f);
            var window = new Rect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height);
            GenesisTheme.Box(new Rect(0, 0, Screen.width, Screen.height), new Color(0f, 0f, 0f, 0.55f));
            GenesisTheme.Box(window, GenesisTheme.Background);

            GUIStyle title = Style(30, FontStyle.Bold, TextAnchor.MiddleLeft, GenesisTheme.Cyan);
            GUIStyle small = Style(15, FontStyle.Normal, TextAnchor.MiddleCenter, Color.white);
            GUI.Label(new Rect(window.x + 28f, window.y + 16f, width - 300f, 44f),
                _stage == Stage.Shop ? _shopName.ToUpperInvariant() + "  ·  BOOSTER PACKS" : (_pack?.displayName ?? "").ToUpperInvariant(), title);
            if (_wallet != null)
                GUI.Label(new Rect(window.xMax - 300f, window.y + 16f, 272f, 44f), $"{_wallet.GenesisCredits:N0} GC",
                    Style(24, FontStyle.Bold, TextAnchor.MiddleRight, GenesisTheme.Gold));

            Rect body = new Rect(window.x + 24f, window.y + 70f, width - 48f, height - 130f);
            switch (_stage)
            {
                case Stage.Shop: DrawShop(body); break;
                case Stage.Tearing: DrawTear(body); break;
                case Stage.Reveal: DrawReveal(body); break;
            }

            if (Time.unscaledTime < _messageUntil)
                GUI.Label(new Rect(window.x, window.yMax - 92f, width, 30f), _message, Style(18, FontStyle.Bold, TextAnchor.MiddleCenter, GenesisTheme.Danger));

            string help = _stage switch
            {
                Stage.Shop => "Click a pack or press 1-9 to buy   •   ESC to leave the counter",
                Stage.Tearing => "Opening...",
                _ => _revealedCount < _lastPack.Count
                    ? "CLICK / SPACE flip the next card   •   R reveal all"
                    : "ENTER open another   •   ESC back to the packs   •   Cards are in your collection"
            };
            GUI.Label(new Rect(window.x, window.yMax - 52f, width, 30f), help, small);
        }

        private void DrawShop(Rect body)
        {
            GenesisPackType[] packs = GenesisPacks.All;
            const int perRow = 5;
            int rows = Mathf.CeilToInt(packs.Length / (float)perRow);
            float labelH = 54f;
            float packH = Mathf.Min((body.height - 20f) / rows - labelH - 14f, (body.width / perRow - 24f) * 1.5f);
            float packW = packH / 1.5f;

            for (int i = 0; i < packs.Length; i++)
            {
                int row = i / perRow, col = i % perRow;
                int inRow = Mathf.Min(perRow, packs.Length - row * perRow);
                float rowWidth = inRow * (packW + 24f) - 24f;
                float x = body.x + (body.width - rowWidth) * 0.5f + col * (packW + 24f);
                float y = body.y + 10f + row * (packH + labelH + 14f);
                var rect = new Rect(x, y, packW, packH);

                GenesisPackType pack = packs[i];
                bool affordable = _wallet == null || _wallet.CanAfford(pack.price);
                bool hover = rect.Contains(Event.current.mousePosition);
                Rect drawRect = hover ? Grow(rect, 8f) : rect;
                if (hover) GenesisTheme.Box(Grow(drawRect, 4f), new Color(pack.accent.r, pack.accent.g, pack.accent.b, 0.8f));
                DrawArt(drawRect, pack, affordable ? 1f : 0.45f);

                GUI.Label(new Rect(x - 12f, y + packH + 4f, packW + 24f, 24f), $"{i + 1}. {pack.displayName}",
                    Style(16, FontStyle.Bold, TextAnchor.MiddleCenter, pack.accent));
                GUI.Label(new Rect(x - 12f, y + packH + 26f, packW + 24f, 22f), $"{pack.price:N0} GC  ·  {pack.blurb}",
                    Style(12, FontStyle.Normal, TextAnchor.MiddleCenter, affordable ? Color.white : GenesisTheme.Muted));

                if (GUI.Button(rect, GUIContent.none, GUIStyle.none)) Buy(pack);
            }

            GUI.Label(new Rect(body.x, body.yMax - 22f, body.width, 22f), GenesisPacks.OddsText,
                Style(13, FontStyle.Normal, TextAnchor.MiddleCenter, GenesisTheme.Muted));
        }

        private void DrawTear(Rect body)
        {
            float t = Mathf.Clamp01((Time.unscaledTime - _stageStart) / TearSeconds);
            float h = body.height * Mathf.Lerp(0.72f, 0.9f, Mathf.SmoothStep(0f, 1f, t));
            float w = h / 1.5f;
            float shake = t > 0.35f ? Mathf.Sin(Time.unscaledTime * 70f) * 10f * (t - 0.35f) : 0f;
            var rect = new Rect(body.center.x - w * 0.5f + shake, body.center.y - h * 0.5f, w, h);
            if (_pack != null)
            {
                GenesisTheme.Box(Grow(rect, 10f + 30f * t), new Color(_pack.accent.r, _pack.accent.g, _pack.accent.b, 0.25f + 0.35f * t));
                DrawArt(rect, _pack, 1f);
            }
            if (t > 0.7f) GenesisTheme.Box(new Rect(0, 0, Screen.width, Screen.height), new Color(1f, 1f, 1f, Mathf.InverseLerp(0.7f, 1f, t)));
            if (Event.current.type == EventType.MouseDown) SetStage(Stage.Reveal);
        }

        private void DrawReveal(Rect body)
        {
            // Fade out the flash from the tear.
            float since = Time.unscaledTime - _stageStart;
            if (since < 0.35f) GenesisTheme.Box(new Rect(0, 0, Screen.width, Screen.height), new Color(1f, 1f, 1f, 1f - since / 0.35f));

            const int perRow = 5;
            float cardH = Mathf.Min((body.height - 30f) / 2f - 34f, (body.width / perRow - 20f) / 0.686f);
            float cardW = cardH * 0.686f;
            string hoverText = null;

            for (int i = 0; i < _lastPack.Count; i++)
            {
                int row = i / perRow, col = i % perRow;
                int inRow = Mathf.Min(perRow, _lastPack.Count - row * perRow);
                float rowWidth = inRow * (cardW + 20f) - 20f;
                var rect = new Rect(body.x + (body.width - rowWidth) * 0.5f + col * (cardW + 20f), body.y + 8f + row * (cardH + 40f), cardW, cardH);
                CardData card = _lastPack[i];

                if (i < _revealedCount)
                {
                    if (i < _flippedAt.Count && _flippedAt[i] < 0f) _flippedAt[i] = Time.unscaledTime;
                    float age = i < _flippedAt.Count ? Time.unscaledTime - _flippedAt[i] : 99f;
                    Rect face = ProductionCardVisualDrawer.FitCard(rect);
                    CardRarityFx.DrawRevealBurst(face, card.rarity, age);   // rays behind the card
                    if (card.rarity >= CardRarity.Rare)
                    {
                        float pulse = 0.55f + 0.35f * Mathf.Sin(Time.unscaledTime * 4f + i);
                        Color glow = GenesisTheme.RarityColor(card.rarity);
                        float spread = card.rarity >= CardRarity.UltraRare ? 9f : 6f;
                        GenesisTheme.Box(Grow(face, spread), new Color(glow.r, glow.g, glow.b, pulse));
                    }
                    // A pop of scale as a foil card lands face-up.
                    Rect drawRect = CardRarityFx.HasFoil(card.rarity) && age < 0.35f ? Grow(rect, 14f * Mathf.Sin(age / 0.35f * Mathf.PI)) : rect;
                    ProductionCardVisualDrawer.DrawCard(drawRect, card);
                    CardRarityFx.DrawCallOut(face, card.rarity, age);
                    if (card.rarity == CardRarity.SecretRare && age < 0.6f)
                    {
                        Color rainbow = CardRarityFx.Tint(card.rarity, Time.unscaledTime);
                        GenesisTheme.Box(new Rect(0, 0, Screen.width, Screen.height), new Color(rainbow.r, rainbow.g, rainbow.b, 0.35f * (1f - age / 0.6f)));
                    }
                    GUI.Label(new Rect(rect.x - 10f, rect.yMax + 4f, rect.width + 20f, 22f), card.RarityLabel,
                        Style(13, FontStyle.Bold, TextAnchor.MiddleCenter, GenesisTheme.RarityColor(card.rarity)));
                    if (rect.Contains(Event.current.mousePosition)) hoverText = $"{card.cardName}  ·  {card.kind}  ·  {card.attribute}  ·  {card.ShortStats}";
                }
                else
                {
                    ProductionCardVisualDrawer.DrawCardBack(rect);
                    if (i == _revealedCount) GenesisTheme.Box(Grow(rect, 3f), new Color(_pack.accent.r, _pack.accent.g, _pack.accent.b, 0.35f));
                }
            }

            if (hoverText != null)
                GUI.Label(new Rect(body.x, body.yMax - 24f, body.width, 24f), hoverText, Style(16, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white));

            if (Event.current.type == EventType.MouseDown && Event.current.button == 0 && _revealedCount < _lastPack.Count)
            {
                _revealedCount++;
                Event.current.Use();
            }
        }

        private static void DrawArt(Rect rect, GenesisPackType pack, float brightness)
        {
            Texture2D art = pack.Art;
            Color old = GUI.color;
            GUI.color = new Color(brightness, brightness, brightness, 1f);
            if (art != null) GUI.DrawTexture(rect, art, ScaleMode.ScaleToFit);
            else
            {
                GenesisTheme.Box(rect, pack.accent * 0.6f);
                GUI.Label(rect, pack.displayName, Style(18, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white));
            }
            GUI.color = old;
        }

        private static Rect Grow(Rect r, float by) => new Rect(r.x - by, r.y - by, r.width + by * 2f, r.height + by * 2f);

        private static GUIStyle Style(int size, FontStyle style, TextAnchor anchor, Color color) =>
            new GUIStyle(GUI.skin.label) { fontSize = size, fontStyle = style, alignment = anchor, wordWrap = true, normal = { textColor = color } };
    }
}
