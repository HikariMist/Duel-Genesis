using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace DuelGenesis.Cards
{
    /// <summary>
    /// Builds complete, print-accurate Yu-Gi-Oh card faces from the DMO frame templates,
    /// the square DMO artwork and the real card metadata.
    ///
    /// Every face is laid out off-screen once with uGUI, captured into a mipmapped,
    /// compressed texture and cached. The same texture is used by the 3D duel table,
    /// the hand, pack opening and the card inspector, so a card looks identical everywhere.
    ///
    /// Geometry was measured from the supplied 400x580 frame PNGs:
    ///   card body   x 15..385, y 12..567   (the rest is a baked drop shadow and is cropped)
    ///   art window  x 50..351, y 107..408  (exact square: the 512x512 art fits with no crop)
    ///   name bar    x 22..378, y 22..68
    ///   text box    x 22..375, y 429..550
    /// </summary>
    public sealed class CardFaceCompositor : MonoBehaviour
    {
        // Output resolution. Ratio 0.685 matches a real 59 x 86 mm card; both are multiples of 4
        // so the capture can be DXT-compressed.
        public const int FaceWidth = 400;
        public const int FaceHeight = 584;

        private const int CaptureLayer = 31;
        private const int MaxFacesPerFrame = 6;

        // Frame template pixel geometry (top-left origin).
        private const float TemplateW = 400f;
        private const float TemplateH = 580f;
        private const float BodyX0 = 15f, BodyX1 = 385f, BodyY0 = 12f, BodyY1 = 567f;

        private static CardFaceCompositor _instance;
        private static readonly Dictionary<string, Texture2D> Faces = new();
        private static readonly Queue<CardData> Pending = new();
        private static readonly HashSet<string> PendingKeys = new();

        private Camera _camera;
        private RenderTexture _target;
        private Canvas _canvas;
        private RawImage _frame;
        private RawImage _art;
        private Image _sheen;
        private Text _name;
        private Text _nameShadow;
        private Image _attributeDisc;
        private Text _attributeGlyph;
        private RectTransform _starRow;
        private readonly List<Image> _stars = new();
        private Text _spellTrapLine;
        private Text _propertyIcon;
        private Text _closingBracket;
        private Text _typeLine;
        private Text _effect;
        private Image _statRule;
        private Text _stats;
        private Text _passcode;
        private RectTransform _faceRoot;
        private RectTransform _backRoot;
        private static Texture2D _cardBack;
        private Font _uiFont;
        private Font _glyphFont;
        private Sprite _discSprite;
        private Sprite _levelStarSprite;
        private Sprite _rankStarSprite;
        private Sprite _sheenSprite;

        public static int CachedFaceCount => Faces.Count;
        public static Texture2D CachedCardBack => _cardBack;

        /// <summary>Classic swirl card back at real card proportions (rendered once).</summary>
        public static Texture2D GetCardBack()
        {
            if (_cardBack != null) return _cardBack;
            CardFaceCompositor compositor = EnsureInstance();
            if (compositor == null) return null;
            _cardBack = compositor.Capture(null, "DG Card Back");
            return _cardBack;
        }

        // ------------------------------------------------------------------ public API

        /// <summary>Returns the finished face now, rendering it immediately if needed.
        /// Do not call from OnGUI (rendering inside the GUI pass is unsafe); use TryGetFace there.</summary>
        public static Texture2D GetFace(CardData card)
        {
            if (card == null) return null;
            string key = Key(card);
            if (Faces.TryGetValue(key, out Texture2D cached) && cached != null)
                return cached;

            CardFaceCompositor compositor = EnsureInstance();
            return compositor != null ? compositor.Render(card) : null;
        }

        /// <summary>Non-blocking lookup: returns true with the face if it is ready, otherwise
        /// queues it for rendering at the end of this frame.</summary>
        public static bool TryGetFace(CardData card, out Texture2D face)
        {
            face = null;
            if (card == null) return false;
            string key = Key(card);
            if (Faces.TryGetValue(key, out face) && face != null)
                return true;

            EnsureInstance();
            if (PendingKeys.Add(key))
                Pending.Enqueue(card);
            return false;
        }

        /// <summary>Queue a batch of faces (e.g. both decks at duel start).</summary>
        public static void Prewarm(IEnumerable<CardData> cards)
        {
            if (cards == null) return;
            foreach (CardData card in cards)
                TryGetFace(card, out _);
        }

        /// <summary>Renders an uncompressed, readable, uncached face (editor preview / export).</summary>
        public static Texture2D RenderPreview(CardData card)
        {
            CardFaceCompositor compositor = EnsureInstance();
            return compositor != null ? compositor.Capture(card, "DG Card Preview", false) : null;
        }

        /// <summary>Destroys the off-screen rig (used by editor tools outside Play Mode).</summary>
        public static void Shutdown()
        {
            ClearCache();
            if (_cardBack != null) DestroyImmediate(_cardBack);
            _cardBack = null;
            if (_instance != null) DestroyImmediate(_instance.gameObject);
            _instance = null;
        }

        public static void ClearCache()
        {
            foreach (Texture2D texture in Faces.Values)
            {
                if (texture == null) continue;
                if (Application.isPlaying) Destroy(texture); else DestroyImmediate(texture);
            }
            Faces.Clear();
            Pending.Clear();
            PendingKeys.Clear();
        }

        // ------------------------------------------------------------------ lifecycle

        private static string Key(CardData card) => string.IsNullOrEmpty(card.id) ? card.cardName : card.id;

        private static CardFaceCompositor EnsureInstance()
        {
            if (_instance != null) return _instance;
            GameObject host = new GameObject("DG Card Face Compositor");
            if (Application.isPlaying)
                DontDestroyOnLoad(host);
            host.hideFlags = HideFlags.DontSave;
            _instance = host.AddComponent<CardFaceCompositor>();
            _instance.Build();
            return _instance;
        }

        private void LateUpdate()
        {
            int budget = MaxFacesPerFrame;
            while (budget-- > 0 && Pending.Count > 0)
            {
                CardData card = Pending.Dequeue();
                PendingKeys.Remove(Key(card));
                if (!Faces.ContainsKey(Key(card)))
                    Render(card);
            }
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
            if (_target != null) _target.Release();
        }

        // ------------------------------------------------------------------ rendering

        private Texture2D Render(CardData card)
        {
            Texture2D face = Capture(card, "DG Card Face - " + card.cardName);
            if (face != null)
                Faces[Key(card)] = face;
            return face;
        }

        /// <summary>Lays out either a card face (card != null) or the card back and captures it.</summary>
        private Texture2D Capture(CardData card, string textureName, bool compress = true)
        {
            try
            {
                _faceRoot.gameObject.SetActive(card != null);
                _backRoot.gameObject.SetActive(card == null);
                if (card != null)
                    Populate(card);
                Canvas.ForceUpdateCanvases();

                RenderTexture previousActive = RenderTexture.active;
                _camera.targetTexture = _target;

                var request = new RenderPipeline.StandardRequest { destination = _target };
                if (RenderPipeline.SupportsRenderRequest(_camera, request))
                    RenderPipeline.SubmitRenderRequest(_camera, request);
                else
                    _camera.Render();

                RenderTexture.active = _target;
                Texture2D face = new Texture2D(FaceWidth, FaceHeight, TextureFormat.RGBA32, true)
                {
                    name = textureName,
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Trilinear,
                    anisoLevel = 8
                };
                face.ReadPixels(new Rect(0, 0, FaceWidth, FaceHeight), 0, 0, false);
                face.Apply(true);
                if (compress)
                    face.Compress(true);
                RenderTexture.active = previousActive;
                return face;
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Card face compositor could not render '{textureName}': {exception.Message}");
                return null;
            }
        }

        private void Populate(CardData card)
        {
            CardFrameKind frameKind = card.ResolvedFrameKind;
            bool isMonster = card.kind == CardKind.Monster;
            bool darkFrame = frameKind == CardFrameKind.XyzMonster;
            bool whiteName = frameKind == CardFrameKind.XyzMonster || frameKind == CardFrameKind.Spell || frameKind == CardFrameKind.Trap;

            // Frame, cropped to the card body so the baked drop shadow never shows.
            Texture2D frame = ProductionCardArtRegistry.LoadFrame(frameKind);
            _frame.texture = frame;
            _frame.color = frame != null ? Color.white : FallbackFrameColor(frameKind);
            _frame.uvRect = new Rect(
                BodyX0 / TemplateW,
                (TemplateH - BodyY1) / TemplateH,
                (BodyX1 - BodyX0) / TemplateW,
                (BodyY1 - BodyY0) / TemplateH);

            // Artwork: square source into the square window, drawn beneath the frame bevel.
            Texture2D art = ProductionCardArtRegistry.LoadFace(card.cardName);
            _art.texture = art;
            _art.color = art != null ? Color.white : new Color(0.10f, 0.11f, 0.16f, 1f);
            _art.uvRect = art != null && art.width != art.height
                ? CenteredSquareUv(art.width, art.height)
                : new Rect(0f, 0f, 1f, 1f);

            bool foil = card.rarity >= CardRarity.SuperRare;
            _sheen.enabled = foil;
            _sheen.color = card.rarity == CardRarity.SecretRare
                ? new Color(0.85f, 0.75f, 1f, 0.30f)
                : new Color(1f, 1f, 1f, 0.22f);

            // Name, with the real rarity treatment (Rare = silver, Ultra/Secret = gold foil).
            _name.text = card.cardName;
            _nameShadow.text = card.cardName;
            _name.color = NameColor(card.rarity, whiteName);
            _nameShadow.enabled = card.rarity >= CardRarity.Rare || whiteName;

            // Attribute (monsters) or SPELL / TRAP disc.
            string attributeKey = isMonster ? (card.attribute ?? string.Empty).ToUpperInvariant()
                : card.kind == CardKind.Spell ? "SPELL" : "TRAP";
            _attributeDisc.color = AttributeColor(attributeKey);
            _attributeGlyph.text = AttributeGlyph(attributeKey);
            _attributeDisc.enabled = !string.IsNullOrEmpty(attributeKey) || !isMonster;

            // Level (right-aligned, red stars) or Rank (left-aligned, black stars).
            bool rank = frameKind == CardFrameKind.XyzMonster;
            int starCount = isMonster ? Mathf.Clamp(card.level, 0, 12) : 0;
            LayoutStars(starCount, rank);

            // Spell / Trap line: "[Spell Card]" + property icon.
            _spellTrapLine.enabled = !isMonster;
            string icon = isMonster ? string.Empty : PropertyIcon(card.typeLine);
            bool hasIcon = !string.IsNullOrEmpty(icon);
            _propertyIcon.enabled = hasIcon;
            _closingBracket.enabled = hasIcon;
            if (!isMonster)
            {
                string label = card.kind == CardKind.Spell ? "Spell Card" : "Trap Card";
                _propertyIcon.text = icon;
                _spellTrapLine.text = hasIcon ? "[" + label : "[" + label + "]";
                SetFrameRect(_spellTrapLine.rectTransform, 40f, 71f, hasIcon ? 338f : 368f, 99f);
            }

            // Text box.
            Color ink = darkFrame ? Color.white : new Color(0.07f, 0.06f, 0.05f, 1f);
            _typeLine.enabled = isMonster;
            _statRule.enabled = isMonster;
            _stats.enabled = isMonster;
            _typeLine.color = ink;
            _effect.color = ink;
            _stats.color = ink;
            _statRule.color = new Color(ink.r, ink.g, ink.b, 0.85f);

            string effectText = CleanEffectText(card.effectText);
            bool normalMonster = frameKind == CardFrameKind.NormalMonster;
            _effect.fontStyle = normalMonster ? FontStyle.Italic : FontStyle.Normal;
            _effect.text = effectText;

            RectTransform effectRect = _effect.rectTransform;
            if (isMonster)
            {
                _typeLine.text = "[" + FormatTypeLine(card.typeLine) + "]";
                _stats.text = $"ATK/{FormatStat(card.attack)}  DEF/{FormatStat(card.defense)}";
                SetFrameRect(effectRect, 30f, 449f, 368f, 527f);
            }
            else
            {
                SetFrameRect(effectRect, 30f, 434f, 368f, 546f);
            }
            _effect.resizeTextMaxSize = isMonster ? 14 : 17;

            _passcode.text = string.IsNullOrEmpty(card.id) || card.id.StartsWith("DG") ? string.Empty : card.id.PadLeft(8, '0');
            _passcode.color = darkFrame ? new Color(1f, 1f, 1f, 0.9f) : new Color(0f, 0f, 0f, 0.85f);
        }

        private void LayoutStars(int count, bool rank)
        {
            const float size = 25f;      // frame pixels
            const float step = 26.5f;
            const float top = 73f;
            for (int i = 0; i < _stars.Count; i++)
            {
                Image star = _stars[i];
                bool on = i < count;
                star.enabled = on;
                if (!on) continue;

                star.sprite = rank ? _rankStarSprite : _levelStarSprite;
                float x0 = rank
                    ? 30f + i * step
                    : 364f - (i + 1) * step + (step - size);
                SetFrameRect(star.rectTransform, x0, top, x0 + size, top + size);
            }
        }

        // ------------------------------------------------------------------ one-time scene build

        private void Build()
        {
            _uiFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _glyphFont = CreateOsFont(new[] { "Yu Gothic UI", "Yu Gothic", "Meiryo", "MS Gothic", "Segoe UI Symbol", "Arial Unicode MS", "Arial" }, 64) ?? _uiFont;
            _discSprite = MakeSprite(ProceduralArt.Disc(96), "Attribute Disc");
            _levelStarSprite = MakeSprite(ProceduralArt.LevelStar(64, false), "Level Star");
            _rankStarSprite = MakeSprite(ProceduralArt.LevelStar(64, true), "Rank Star");
            _sheenSprite = MakeSprite(ProceduralArt.FoilSheen(128), "Foil Sheen");

            _target = new RenderTexture(FaceWidth, FaceHeight, 24, RenderTextureFormat.ARGB32)
            {
                name = "DG Card Face Capture",
                antiAliasing = 1,
                useMipMap = false
            };
            _target.Create();

            GameObject cameraObject = new GameObject("Card Face Camera");
            cameraObject.transform.SetParent(transform, false);
            cameraObject.transform.position = new Vector3(0f, -5000f, 0f);
            _camera = cameraObject.AddComponent<Camera>();
            _camera.enabled = false;
            _camera.orthographic = true;
            _camera.orthographicSize = FaceHeight * 0.5f;
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = new Color(0f, 0f, 0f, 0f);
            _camera.cullingMask = 1 << CaptureLayer;
            _camera.nearClipPlane = 0.1f;
            _camera.farClipPlane = 10f;
            _camera.allowHDR = false;
            _camera.allowMSAA = true;
            _camera.targetTexture = _target;

            GameObject canvasObject = new GameObject("Card Face Canvas");
            canvasObject.layer = CaptureLayer;
            canvasObject.transform.SetParent(transform, false);
            _canvas = canvasObject.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceCamera;
            _canvas.worldCamera = _camera;
            _canvas.planeDistance = 1f;
            _canvas.pixelPerfect = false;
            RectTransform canvasRoot = (RectTransform)canvasObject.transform;
            _faceRoot = NewGroup(canvasRoot, "Face");
            _backRoot = NewGroup(canvasRoot, "Back");
            BuildBack(_backRoot);
            RectTransform root = _faceRoot;

            _art = NewRawImage(root, "Artwork");
            SetFrameRect(_art.rectTransform, 49f, 106f, 352f, 409f);

            _sheen = NewImage(root, "Foil Sheen", _sheenSprite);
            SetFrameRect(_sheen.rectTransform, 50f, 107f, 351f, 408f);

            _frame = NewRawImage(root, "Frame");
            Stretch(_frame.rectTransform);

            _nameShadow = NewText(root, "Name Shadow", 32, FontStyle.Bold, TextAnchor.MiddleLeft);
            _nameShadow.color = new Color(0f, 0f, 0f, 0.55f);
            SetFrameRect(_nameShadow.rectTransform, 31f, 25f, 335f, 66f);
            _name = NewText(root, "Name", 32, FontStyle.Bold, TextAnchor.MiddleLeft);
            SetFrameRect(_name.rectTransform, 30f, 24f, 334f, 65f);
            _name.resizeTextMinSize = 12;
            _nameShadow.resizeTextMinSize = 12;

            _attributeDisc = NewImage(root, "Attribute", _discSprite);
            SetFrameRect(_attributeDisc.rectTransform, 336f, 24f, 376f, 64f);
            _attributeGlyph = NewText(_attributeDisc.rectTransform, "Glyph", 26, FontStyle.Bold, TextAnchor.MiddleCenter);
            _attributeGlyph.font = _glyphFont;
            _attributeGlyph.color = Color.white;
            _attributeGlyph.resizeTextMinSize = 8;
            Stretch(_attributeGlyph.rectTransform, 4f);

            GameObject starRow = new GameObject("Stars", typeof(RectTransform));
            starRow.layer = CaptureLayer;
            _starRow = (RectTransform)starRow.transform;
            _starRow.SetParent(root, false);
            Stretch(_starRow);
            for (int i = 0; i < 12; i++)
                _stars.Add(NewImage(_starRow, "Star " + (i + 1), _levelStarSprite));

            Color ink = new Color(0.04f, 0.035f, 0.03f, 1f);
            _spellTrapLine = NewText(root, "Spell Trap Line", 24, FontStyle.Bold, TextAnchor.MiddleRight);
            _spellTrapLine.color = ink;
            _spellTrapLine.resizeTextForBestFit = false;
            _spellTrapLine.horizontalOverflow = HorizontalWrapMode.Overflow;

            _propertyIcon = NewText(root, "Property Icon", 22, FontStyle.Bold, TextAnchor.MiddleCenter);
            _propertyIcon.font = _glyphFont;
            _propertyIcon.color = ink;
            _propertyIcon.resizeTextForBestFit = false;
            _propertyIcon.horizontalOverflow = HorizontalWrapMode.Overflow;
            SetFrameRect(_propertyIcon.rectTransform, 339f, 71f, 360f, 99f);

            _closingBracket = NewText(root, "Closing Bracket", 24, FontStyle.Bold, TextAnchor.MiddleRight);
            _closingBracket.color = ink;
            _closingBracket.text = "]";
            _closingBracket.resizeTextForBestFit = false;
            _closingBracket.horizontalOverflow = HorizontalWrapMode.Overflow;
            SetFrameRect(_closingBracket.rectTransform, 356f, 71f, 368f, 99f);

            _typeLine = NewText(root, "Type Line", 15, FontStyle.Bold, TextAnchor.MiddleLeft);
            _typeLine.resizeTextMinSize = 9;
            SetFrameRect(_typeLine.rectTransform, 30f, 432f, 368f, 449f);

            _effect = NewText(root, "Effect", 14, FontStyle.Normal, TextAnchor.UpperLeft);
            _effect.horizontalOverflow = HorizontalWrapMode.Wrap;
            _effect.verticalOverflow = VerticalWrapMode.Truncate;
            _effect.resizeTextMinSize = 6;
            _effect.resizeTextMaxSize = 14;
            _effect.lineSpacing = 0.92f;

            _statRule = NewImage(root, "Stat Rule", null);
            SetFrameRect(_statRule.rectTransform, 30f, 529f, 368f, 530.5f);

            _stats = NewText(root, "Stats", 15, FontStyle.Bold, TextAnchor.MiddleRight);
            SetFrameRect(_stats.rectTransform, 120f, 531f, 368f, 548f);

            _passcode = NewText(root, "Passcode", 11, FontStyle.Normal, TextAnchor.MiddleLeft);
            _passcode.resizeTextForBestFit = false;
            SetFrameRect(_passcode.rectTransform, 24f, 551f, 160f, 565f);
        }

        private void BuildBack(RectTransform root)
        {
            Texture2D swirl = ProductionCardArtRegistry.LoadFrameTexture("Blank Playing Card.png");

            RawImage border = NewRawImage(root, "Back Border");
            border.color = new Color(0.16f, 0.09f, 0.04f, 1f);
            Stretch(border.rectTransform);

            RawImage field = NewRawImage(root, "Back Swirl");
            field.texture = swirl;
            field.color = swirl != null ? Color.white : new Color(0.33f, 0.19f, 0.07f, 1f);
            field.uvRect = new Rect(0.045f, 0.03f, 0.91f, 0.94f);
            Stretch(field.rectTransform, 9f);

            Image oval = NewImage(root, "Back Oval", MakeSprite(ProceduralArt.Oval(160, 256), "Back Oval"));
            oval.preserveAspect = false;
            oval.color = Color.white;
            oval.rectTransform.anchorMin = new Vector2(0.24f, 0.25f);
            oval.rectTransform.anchorMax = new Vector2(0.76f, 0.75f);
            oval.rectTransform.offsetMin = Vector2.zero;
            oval.rectTransform.offsetMax = Vector2.zero;

            Text title = NewText(oval.rectTransform, "Back Title", 40, FontStyle.Bold, TextAnchor.MiddleCenter);
            title.text = "DUEL\nGENESIS";
            title.color = new Color(1f, 0.80f, 0.32f, 1f);
            title.lineSpacing = 0.9f;
            Stretch(title.rectTransform, 18f);
            Outline outline = title.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.25f, 0.08f, 0f, 1f);
            outline.effectDistance = new Vector2(2f, -2f);
        }

        private static RectTransform NewGroup(Transform parent, string name)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.layer = CaptureLayer;
            go.transform.SetParent(parent, false);
            RectTransform rect = (RectTransform)go.transform;
            Stretch(rect);
            return rect;
        }

        // ------------------------------------------------------------------ helpers

        /// <summary>Positions a rect using frame-template pixel coordinates (top-left origin),
        /// converted into the cropped card-body space of the output texture.</summary>
        private static void SetFrameRect(RectTransform rect, float x0, float y0, float x1, float y1)
        {
            float bodyW = BodyX1 - BodyX0;
            float bodyH = BodyY1 - BodyY0;
            rect.anchorMin = new Vector2((x0 - BodyX0) / bodyW, 1f - (y1 - BodyY0) / bodyH);
            rect.anchorMax = new Vector2((x1 - BodyX0) / bodyW, 1f - (y0 - BodyY0) / bodyH);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.pivot = new Vector2(0.5f, 0.5f);
        }

        private static void Stretch(RectTransform rect, float inset = 0f)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(inset, inset);
            rect.offsetMax = new Vector2(-inset, -inset);
        }

        private RawImage NewRawImage(Transform parent, string name)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.layer = CaptureLayer;
            go.transform.SetParent(parent, false);
            RawImage image = go.AddComponent<RawImage>();
            image.raycastTarget = false;
            return image;
        }

        private Image NewImage(Transform parent, string name, Sprite sprite)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.layer = CaptureLayer;
            go.transform.SetParent(parent, false);
            Image image = go.AddComponent<Image>();
            image.sprite = sprite;
            image.preserveAspect = sprite != null;
            image.raycastTarget = false;
            return image;
        }

        private Text NewText(Transform parent, string name, int size, FontStyle style, TextAnchor anchor)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.layer = CaptureLayer;
            go.transform.SetParent(parent, false);
            Text text = go.AddComponent<Text>();
            text.font = _uiFont;
            text.fontSize = size;
            text.fontStyle = style;
            text.alignment = anchor;
            text.raycastTarget = false;
            text.supportRichText = false;
            text.resizeTextForBestFit = true;
            text.resizeTextMaxSize = size;
            text.resizeTextMinSize = Mathf.Max(6, size / 2);
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            return text;
        }

        private static Font CreateOsFont(string[] names, int size)
        {
            try
            {
                string[] installed = Font.GetOSInstalledFontNames();
                var available = new HashSet<string>(installed ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);
                var chosen = new List<string>();
                foreach (string name in names)
                    if (available.Contains(name)) chosen.Add(name);
                return chosen.Count == 0 ? null : Font.CreateDynamicFontFromOSFont(chosen.ToArray(), size);
            }
            catch
            {
                return null;
            }
        }

        private static Sprite MakeSprite(Texture2D texture, string name)
        {
            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
            sprite.name = name;
            return sprite;
        }

        private static Rect CenteredSquareUv(int width, int height)
        {
            if (width > height)
            {
                float w = (float)height / width;
                return new Rect((1f - w) * 0.5f, 0f, w, 1f);
            }
            float h = (float)width / height;
            return new Rect(0f, (1f - h) * 0.5f, 1f, h);
        }

        private static Color NameColor(CardRarity rarity, bool whiteBase)
        {
            return rarity switch
            {
                CardRarity.UltraRare => new Color(1f, 0.83f, 0.30f, 1f),
                CardRarity.SecretRare => new Color(1f, 0.90f, 0.55f, 1f),
                CardRarity.Rare => new Color(0.86f, 0.88f, 0.92f, 1f),
                _ => whiteBase ? Color.white : new Color(0.06f, 0.05f, 0.04f, 1f)
            };
        }

        private static Color FallbackFrameColor(CardFrameKind kind) => kind switch
        {
            CardFrameKind.Spell => new Color(0.10f, 0.55f, 0.50f),
            CardFrameKind.Trap => new Color(0.70f, 0.18f, 0.45f),
            CardFrameKind.NormalMonster => new Color(0.80f, 0.62f, 0.28f),
            CardFrameKind.FusionMonster => new Color(0.52f, 0.30f, 0.62f),
            CardFrameKind.RitualMonster => new Color(0.36f, 0.52f, 0.80f),
            _ => new Color(0.78f, 0.45f, 0.20f)
        };

        private static Color AttributeColor(string key) => key switch
        {
            "DARK" => new Color(0.42f, 0.16f, 0.52f),
            "LIGHT" => new Color(0.86f, 0.72f, 0.16f),
            "EARTH" => new Color(0.46f, 0.32f, 0.20f),
            "WATER" => new Color(0.12f, 0.45f, 0.82f),
            "FIRE" => new Color(0.86f, 0.20f, 0.10f),
            "WIND" => new Color(0.20f, 0.62f, 0.28f),
            "DIVINE" => new Color(0.78f, 0.62f, 0.20f),
            "SPELL" => new Color(0.05f, 0.55f, 0.47f),
            "TRAP" => new Color(0.72f, 0.12f, 0.45f),
            _ => new Color(0.35f, 0.35f, 0.38f)
        };

        private string AttributeGlyph(string key)
        {
            bool cjk = _glyphFont != _uiFont;
            return key switch
            {
                "DARK" => cjk ? "闇" : "D",
                "LIGHT" => cjk ? "光" : "L",
                "EARTH" => cjk ? "地" : "E",
                "WATER" => cjk ? "水" : "W",
                "FIRE" => cjk ? "炎" : "F",
                "WIND" => cjk ? "風" : "W",
                "DIVINE" => cjk ? "神" : "G",
                "SPELL" => cjk ? "魔" : "S",
                "TRAP" => cjk ? "罠" : "T",
                _ => string.Empty
            };
        }

        private static string PropertyIcon(string typeLine) => (typeLine ?? string.Empty) switch
        {
            "Continuous" => "∞",
            "Equip" => "✚",
            "Quick-Play" => "⚡",
            "Field" => "✥",
            "Ritual" => "✺",
            "Counter" => "↻",
            _ => string.Empty
        };

        private static string FormatTypeLine(string typeLine)
        {
            if (string.IsNullOrWhiteSpace(typeLine)) return "Monster";
            return typeLine.Replace(" / ", "/").Replace("/", " / ");
        }

        private static string FormatStat(int value) => value < 0 ? "?" : value.ToString();

        private static string CleanEffectText(string text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            return text.Replace("\r\n", "\n").Replace('\r', '\n').Trim();
        }
    }

    /// <summary>Small procedural textures used on card faces and the duel mat.</summary>
    public static class ProceduralArt
    {
        public static Texture2D Disc(int size)
        {
            Texture2D texture = NewTexture(size, "Disc");
            float r = size * 0.5f - 1f;
            Vector2 c = new Vector2(size * 0.5f, size * 0.5f);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), c);
                float alpha = Mathf.Clamp01(r - d + 0.5f);
                // Bright rim + soft inner highlight so the disc reads as an embossed medallion.
                float rim = Mathf.Clamp01(1f - Mathf.Abs(d - (r - 3f)) / 2.5f);
                float shade = Mathf.Lerp(0.78f, 1f, Mathf.Clamp01((y - x * 0.3f) / size));
                Color col = new Color(shade, shade, shade, alpha);
                col = Color.Lerp(col, new Color(1f, 1f, 1f, alpha), rim * 0.8f);
                texture.SetPixel(x, y, col);
            }
            texture.Apply();
            return texture;
        }

        /// <summary>Black oval medallion with a bronze rim, as on the classic card back.</summary>
        public static Texture2D Oval(int width, int height)
        {
            Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = "DG Procedural - Oval",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            float rx = width * 0.5f - 1f, ry = height * 0.5f - 1f;
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                float nx = (x + 0.5f - width * 0.5f) / rx;
                float ny = (y + 0.5f - height * 0.5f) / ry;
                float d = Mathf.Sqrt(nx * nx + ny * ny);
                float pixel = 1f / Mathf.Min(rx, ry);
                float alpha = Mathf.Clamp01((1f - d) / pixel + 0.5f);
                float rim = Mathf.Clamp01(1f - Mathf.Abs(d - 0.955f) / 0.035f);
                float glow = Mathf.Clamp01(1f - d) * 0.18f;
                Color inner = new Color(0.05f + glow, 0.04f + glow * 0.6f, 0.03f, alpha);
                Color col = Color.Lerp(inner, new Color(0.62f, 0.40f, 0.14f, alpha), rim);
                texture.SetPixel(x, y, col);
            }
            texture.Apply();
            return texture;
        }

        public static Texture2D LevelStar(int size, bool rank)
        {
            Texture2D texture = NewTexture(size, rank ? "Rank Star" : "Level Star");
            Vector2 c = new Vector2(size * 0.5f, size * 0.5f);
            float outer = size * 0.5f - 1f;
            Vector2[] star = StarPolygon(c, outer * 0.62f, outer * 0.27f);
            Color disc = rank ? new Color(0.08f, 0.08f, 0.10f) : new Color(0.86f, 0.22f, 0.08f);
            Color rim = rank ? new Color(0.55f, 0.55f, 0.58f) : new Color(1f, 0.80f, 0.35f);
            Color glyph = rank ? new Color(0.92f, 0.82f, 0.30f) : new Color(1f, 0.93f, 0.40f);

            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                Vector2 p = new Vector2(x + 0.5f, y + 0.5f);
                float d = Vector2.Distance(p, c);
                if (d > outer + 0.5f) { texture.SetPixel(x, y, Color.clear); continue; }
                float edge = Mathf.Clamp01(outer - d + 0.5f);
                Color col = d > outer - 3f ? rim : disc;
                if (PointInPolygon(p, star)) col = glyph;
                col.a = edge;
                texture.SetPixel(x, y, col);
            }
            texture.Apply();
            return texture;
        }

        public static Texture2D FoilSheen(int size)
        {
            Texture2D texture = NewTexture(size, "Foil Sheen");
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float t = (x + y) / (2f * size);
                float band = Mathf.Pow(Mathf.Clamp01(1f - Mathf.Abs(t - 0.38f) * 5f), 2f) +
                             0.6f * Mathf.Pow(Mathf.Clamp01(1f - Mathf.Abs(t - 0.62f) * 7f), 2f);
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(band)));
            }
            texture.Apply();
            return texture;
        }

        private static Texture2D NewTexture(int size, string name)
        {
            return new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "DG Procedural - " + name,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
        }

        private static Vector2[] StarPolygon(Vector2 c, float outer, float inner)
        {
            Vector2[] points = new Vector2[10];
            for (int i = 0; i < 10; i++)
            {
                float angle = Mathf.PI * 0.5f + i * Mathf.PI / 5f;
                float radius = i % 2 == 0 ? outer : inner;
                points[i] = c + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            }
            return points;
        }

        private static bool PointInPolygon(Vector2 p, Vector2[] poly)
        {
            bool inside = false;
            for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
            {
                if ((poly[i].y > p.y) != (poly[j].y > p.y) &&
                    p.x < (poly[j].x - poly[i].x) * (p.y - poly[i].y) / (poly[j].y - poly[i].y) + poly[i].x)
                    inside = !inside;
            }
            return inside;
        }
    }
}
