using System;
using System.Collections.Generic;
using System.Linq;
using DuelGenesis.Cards;
using DuelGenesis.Dueling;
using DuelGenesis.Economy;
using DuelGenesis.Player;
using DuelGenesis.Progression;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace DuelGenesis.UI
{
    /// <summary>
    /// The title screen: slow-panning key art that cross-fades between the Duel Genesis artwork, the
    /// logo with a breathing glow, an animated menu (mouse or keyboard), a duelist card, a fan of the
    /// player's rarest cards, drifting sparks, a news ticker and a settings page.
    /// Public API (IsOpen / Open / EnterGenesis) is what the rest of the game uses.
    /// </summary>
    public class GenesisMainMenu : MonoBehaviour
    {
        private const string VolumeKey = "DG_SETTINGS_VOLUME";
        private const string QualityKey = "DG_SETTINGS_QUALITY";
        private const string VsyncKey = "DG_SETTINGS_VSYNC";
        private const string SensitivityKey = "DG_SETTINGS_SENSITIVITY";

        private ThirdPersonPlayerController _player;
        private ThirdPersonCamera _camera;
        private DuelistProfile _profile;
        private GenesisWallet _wallet;
        private PlayerCollection _collection;
        private PlayerDeck _deck;
        private DeckBuilderUI _deckBuilder;
        private DuelTableSeat _seat;

        private bool _open = true;
        private bool _built;
        private Canvas _canvas;
        private CanvasGroup _group;
        private float _fadeTarget = 1f;

        // background
        private RawImage[] _art = new RawImage[2];
        private readonly List<Texture2D> _artwork = new();
        private int _artIndex;
        private float _artStarted;
        private const float ArtSeconds = 9f;

        // header / widgets
        private RawImage _logo;
        private Shadow _logoGlow;
        private Text _duelistText, _statsText, _deckText, _ticker, _toast;
        private Image _xpFill;
        private RectTransform _tickerRect;
        private float _toastUntil;
        private readonly List<RawImage> _showcase = new();
        private readonly List<CardData> _showcaseCards = new();
        private readonly List<(RectTransform rt, float speed, float phase, float x)> _sparks = new();

        // menu
        private readonly List<MenuItem> _items = new();
        private int _selected;
        private RectTransform _settingsPanel, _controlsPanel, _showcaseRoot;
        private Text _volumeValue, _qualityValue, _vsyncValue, _sensitivityValue;

        public bool IsOpen => _open;

        // ================================================================== lifecycle

        private void Start()
        {
            ApplySavedSettings();
            Resolve();
            if (_camera != null && PlayerPrefs.HasKey(SensitivityKey)) _camera.sensitivity = PlayerPrefs.GetFloat(SensitivityKey);
            Open();
        }

        private void Resolve()
        {
            if (_player == null) _player = FindFirstObjectByType<ThirdPersonPlayerController>();
            if (_camera == null) _camera = FindFirstObjectByType<ThirdPersonCamera>();
            if (_profile == null) _profile = FindFirstObjectByType<DuelistProfile>();
            if (_wallet == null) _wallet = FindFirstObjectByType<GenesisWallet>();
            if (_collection == null) _collection = FindFirstObjectByType<PlayerCollection>();
            if (_deck == null) _deck = FindFirstObjectByType<PlayerDeck>();
            if (_deckBuilder == null) _deckBuilder = FindFirstObjectByType<DeckBuilderUI>();
            if (_seat == null) _seat = FindFirstObjectByType<DuelTableSeat>();
        }

        public void Open()
        {
            Resolve();
            if (!_built) Build();
            _open = true;
            _canvas.gameObject.SetActive(true);
            _group.alpha = 0f;
            _group.blocksRaycasts = true;
            _fadeTarget = 1f;
            ShowPage(null);
            if (_ticker != null)
            {
                _ticker.text = TickerText();
                _tickerRect.sizeDelta = new Vector2(Mathf.Max(2000f, _ticker.preferredWidth + 40f), 0f);
            }
            RefreshInfo();
            PickShowcase();
            _player?.SetMovementEnabled(false);
            _camera?.SetLookEnabled(false);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            Select(0);
        }

        public void EnterGenesis()
        {
            Resolve();
            _open = false;
            _fadeTarget = 0f;
            if (_group != null) _group.blocksRaycasts = false;
            _player?.SetMovementEnabled(true);
            _camera?.SetLookEnabled(true);
        }

        private void Update()
        {
            if (!_built) return;

            // Fade in / out.
            _group.alpha = Mathf.MoveTowards(_group.alpha, _fadeTarget, Time.unscaledDeltaTime * 2.6f);
            if (!_open && _group.alpha <= 0.001f && _canvas.gameObject.activeSelf) _canvas.gameObject.SetActive(false);
            if (!_canvas.gameObject.activeSelf) return;

            float t = Time.unscaledTime;
            AnimateBackground(t);
            AnimateWidgets(t);
            if (_open) HandleKeys();
        }

        // ================================================================== input

        private void HandleKeys()
        {
            Keyboard k = Keyboard.current;
            if (k == null) return;
            bool pageOpen = (_settingsPanel != null && _settingsPanel.gameObject.activeSelf) || (_controlsPanel != null && _controlsPanel.gameObject.activeSelf);
            if (k.escapeKey.wasPressedThisFrame && pageOpen) { ShowPage(null); return; }
            if (pageOpen) return;
            if (k.downArrowKey.wasPressedThisFrame || k.sKey.wasPressedThisFrame) Select((_selected + 1) % _items.Count);
            if (k.upArrowKey.wasPressedThisFrame || k.wKey.wasPressedThisFrame) Select((_selected + _items.Count - 1) % _items.Count);
            if (k.enterKey.wasPressedThisFrame || k.numpadEnterKey.wasPressedThisFrame || k.spaceKey.wasPressedThisFrame)
                _items[_selected].Activate();
        }

        private void Select(int index)
        {
            if (_items.Count == 0) return;
            _selected = Mathf.Clamp(index, 0, _items.Count - 1);
            for (int i = 0; i < _items.Count; i++) _items[i].Selected = i == _selected;
        }

        // ================================================================== actions

        private void QuickDuel()
        {
            Resolve();
            if (_deck == null || _collection == null) { Toast("Your duelist isn't ready yet."); return; }
            if (!_deck.Validate(_collection, out string why))
            {
                Toast("Build a legal deck first — " + why);
                OpenDeckBuilder();
                return;
            }
            if (_seat == null || _player == null) { Toast("No duel table found."); return; }
            EnterGenesis();
            _seat.Interact(_player.gameObject);
        }

        private void OpenDeckBuilder()
        {
            Resolve();
            EnterGenesis();
            _deckBuilder?.Open();
        }

        private void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void Toast(string message)
        {
            if (_toast == null) return;
            _toast.text = message;
            _toastUntil = Time.unscaledTime + 3f;
        }

        // ================================================================== settings

        private static void ApplySavedSettings()
        {
            AudioListener.volume = PlayerPrefs.GetFloat(VolumeKey, 0.8f);
            int q = PlayerPrefs.GetInt(QualityKey, -1);
            if (q >= 0 && q < QualitySettings.names.Length) QualitySettings.SetQualityLevel(q, true);
            QualitySettings.vSyncCount = PlayerPrefs.GetInt(VsyncKey, 1);
        }

        private void ChangeVolume(float delta)
        {
            AudioListener.volume = Mathf.Clamp01(Mathf.Round((AudioListener.volume + delta) * 10f) / 10f);
            PlayerPrefs.SetFloat(VolumeKey, AudioListener.volume);
            RefreshSettings();
        }

        private void ChangeQuality(int delta)
        {
            int count = QualitySettings.names.Length;
            if (count == 0) return;
            int q = (QualitySettings.GetQualityLevel() + delta + count) % count;
            QualitySettings.SetQualityLevel(q, true);
            PlayerPrefs.SetInt(QualityKey, q);
            RefreshSettings();
        }

        private void ToggleVsync()
        {
            QualitySettings.vSyncCount = QualitySettings.vSyncCount > 0 ? 0 : 1;
            PlayerPrefs.SetInt(VsyncKey, QualitySettings.vSyncCount);
            RefreshSettings();
        }

        private void ChangeSensitivity(float delta)
        {
            Resolve();
            if (_camera == null) return;
            _camera.sensitivity = Mathf.Clamp(Mathf.Round((_camera.sensitivity + delta) * 100f) / 100f, 0.03f, 0.6f);
            PlayerPrefs.SetFloat(SensitivityKey, _camera.sensitivity);
            RefreshSettings();
        }

        private void RefreshSettings()
        {
            if (_volumeValue == null) return;
            _volumeValue.text = $"{Mathf.RoundToInt(AudioListener.volume * 100f)}%";
            string[] names = QualitySettings.names;
            int q = QualitySettings.GetQualityLevel();
            _qualityValue.text = q >= 0 && q < names.Length ? names[q] : "—";
            _vsyncValue.text = QualitySettings.vSyncCount > 0 ? "ON" : "OFF";
            _sensitivityValue.text = _camera != null ? _camera.sensitivity.ToString("0.00") : "—";
        }

        private void ShowPage(RectTransform page)
        {
            if (_settingsPanel != null) _settingsPanel.gameObject.SetActive(page == _settingsPanel);
            if (_controlsPanel != null) _controlsPanel.gameObject.SetActive(page == _controlsPanel);
            if (_showcaseRoot != null) _showcaseRoot.gameObject.SetActive(page == null);
            if (page == _settingsPanel) RefreshSettings();
        }

        // ================================================================== info

        private void RefreshInfo()
        {
            if (_duelistText == null) return;
            if (_profile != null)
            {
                _duelistText.text = $"<b>{_profile.Title.ToUpperInvariant()}</b>   <color=#8FE9FF>LV {_profile.Level}</color>";
                _xpFill.fillAmount = _profile.XPToNextLevel > 0 ? Mathf.Clamp01(_profile.XP / (float)_profile.XPToNextLevel) : 1f;
                _statsText.text = $"Record  <b>{_profile.Wins}</b> W  /  <b>{_profile.Losses}</b> L        Packs opened  <b>{_profile.PacksOpened}</b>\n" +
                                  $"<color=#FFD166>{(_wallet != null ? _wallet.GenesisCredits : 0):N0} GC</color>        Collection  <b>{(_collection != null ? _collection.TotalCardCount : 0)}</b> cards";
            }
            else
            {
                _duelistText.text = "<b>NEW DUELIST</b>";
                _statsText.text = "Your journey begins in Genesis City.";
            }
            bool legal = _deck != null && _collection != null && _deck.Validate(_collection, out _);
            _deckText.text = _deck == null ? "" :
                legal ? $"<color=#63FFA0>✔ DECK READY</color>   {_deck.MainDeckCount} main / {_deck.ExtraDeckCount} extra"
                      : $"<color=#FF7A7A>✖ DECK NOT LEGAL</color>   {_deck.MainDeckCount} cards — open the Deck Builder";
        }

        private void PickShowcase()
        {
            _showcaseCards.Clear();
            if (_collection != null)
            {
                var owned = _collection.GetOwnedCardsSorted().Select(p => p.card)
                    .Where(c => c != null && c.kind == CardKind.Monster && ProductionCardArtRegistry.HasAuthoritativeFace(c)).ToList();
                // Rarest and strongest first, then a little variety each time the menu opens.
                var top = owned.OrderByDescending(c => (int)c.rarity * 10000 + c.attack).Take(12).OrderBy(_ => UnityEngine.Random.value).Take(3).ToList();
                _showcaseCards.AddRange(top);
            }
            if (_showcaseCards.Count < 3 && CardDatabase.All != null)
                _showcaseCards.AddRange(CardDatabase.All.Where(c => c.kind == CardKind.Monster && c.rarity >= CardRarity.UltraRare)
                    .OrderBy(_ => UnityEngine.Random.value).Take(3 - _showcaseCards.Count));
            for (int i = 0; i < _showcase.Count; i++)
            {
                _showcase[i].texture = null;
                _showcase[i].color = new Color(1f, 1f, 1f, 0f);
            }
        }

        // ================================================================== animation

        private void AnimateBackground(float t)
        {
            if (_artwork.Count == 0) return;
            float elapsed = t - _artStarted;
            if (elapsed > ArtSeconds && _artwork.Count > 1)
            {
                // Swap layers: the top fades out to reveal the next piece underneath.
                _artIndex = (_artIndex + 1) % _artwork.Count;
                RawImage front = _art[1];
                _art[1] = _art[0];
                _art[0] = front;
                _art[0].transform.SetAsFirstSibling();
                _art[0].texture = _artwork[(_artIndex + 1) % _artwork.Count];
                _art[1].texture = _artwork[_artIndex];
                _artStarted = t;
                elapsed = 0f;
            }
            float fade = Mathf.Clamp01((elapsed - (ArtSeconds - 1.6f)) / 1.6f);
            _art[1].color = new Color(1f, 1f, 1f, 1f - fade);
            _art[0].color = Color.white;
            KenBurns(_art[1], elapsed / ArtSeconds, _artIndex);
            KenBurns(_art[0], 0f, _artIndex + 1);
        }

        /// <summary>Crops the artwork to the screen's aspect and pans/zooms slowly across it.</summary>
        private static void KenBurns(RawImage image, float progress, int seed)
        {
            if (image.texture == null) return;
            float screenAspect = Screen.width / (float)Mathf.Max(1, Screen.height);
            float texAspect = image.texture.width / (float)image.texture.height;
            float zoom = Mathf.Lerp(1.02f, 1.14f, progress);
            float w = 1f / zoom, h = 1f / zoom;
            if (texAspect > screenAspect) w *= screenAspect / texAspect; else h *= texAspect / screenAspect;
            float dir = seed % 2 == 0 ? 1f : -1f;
            float x = Mathf.Lerp(0.5f - dir * (1f - w) * 0.5f, 0.5f + dir * (1f - w) * 0.5f, progress) - w * 0.5f;
            float y = (1f - h) * 0.5f;
            image.uvRect = new Rect(Mathf.Clamp(x, 0f, 1f - w), y, w, h);
        }

        private void AnimateWidgets(float t)
        {
            if (_logo != null)
            {
                float pulse = 0.5f + 0.5f * Mathf.Sin(t * 1.6f);
                _logo.rectTransform.localScale = Vector3.one * (1f + 0.012f * pulse);
                _logoGlow.effectColor = new Color(0.2f, 0.85f, 1f, 0.25f + 0.35f * pulse);
            }

            // Showcase: faces stream in from the compositor, then bob in a fan.
            for (int i = 0; i < _showcase.Count && i < _showcaseCards.Count; i++)
            {
                RawImage img = _showcase[i];
                if (img.texture == null && CardFaceCompositor.TryGetFace(_showcaseCards[i], out Texture2D face)) img.texture = face;
                float a = img.texture != null ? Mathf.MoveTowards(img.color.a, 1f, Time.unscaledDeltaTime * 2f) : 0f;
                img.color = new Color(1f, 1f, 1f, a);
                float angle = (i - 1) * -11f + Mathf.Sin(t * 0.7f + i) * 1.5f;
                img.rectTransform.localRotation = Quaternion.Euler(0f, 0f, angle);
                img.rectTransform.anchoredPosition = new Vector2((i - 1) * 150f, Mathf.Abs(i - 1) * -26f + Mathf.Sin(t * 1.1f + i * 1.7f) * 8f);
            }

            foreach (var (rt, speed, phase, x) in _sparks)
            {
                float y = Mathf.Repeat(phase + t * speed, 1.2f) - 0.1f;
                rt.anchorMin = rt.anchorMax = new Vector2(x + Mathf.Sin(t * 0.5f + phase * 9f) * 0.01f, y);
                Image img = rt.GetComponent<Image>();
                Color c = img.color;
                c.a = Mathf.Sin(Mathf.Clamp01(y) * Mathf.PI) * 0.8f;
                img.color = c;
            }

            if (_tickerRect != null)
            {
                float width = _ticker.preferredWidth + 200f;
                _tickerRect.anchoredPosition = new Vector2(1920f - Mathf.Repeat(t * 90f, width + 1920f), 0f);
            }

            if (_toast != null)
                _toast.color = new Color(1f, 0.85f, 0.4f, Mathf.Clamp01((_toastUntil - Time.unscaledTime) * 2f));

            foreach (MenuItem item in _items) item.Animate(Time.unscaledDeltaTime);
        }

        // ================================================================== build

        private void Build()
        {
            _built = true;
            UiKit.EnsureEventSystem();
            var canvasGo = new GameObject("DG Title Screen", typeof(RectTransform));
            canvasGo.transform.SetParent(transform, false);
            _canvas = canvasGo.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 400;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();
            _group = canvasGo.AddComponent<CanvasGroup>();
            Transform root = canvasGo.transform;

            // ---------- background art (two layers for cross-fades) + shading
            Image black = UiKit.Fill(root, "Black", new Color(0.01f, 0.012f, 0.03f, 1f));
            black.raycastTarget = true;
            UiKit.Stretch(black.rectTransform);
            for (int i = 1; i <= 3; i++)
            {
                Texture2D art = Resources.Load<Texture2D>($"DuelGenesis/Artwork/genesis_art_{i}");
                if (art != null) _artwork.Add(art);
            }
            RectTransform artRoot = UiKit.Rect(root, "Key Art");   // own container so layer swaps stay above the black fill
            UiKit.Stretch(artRoot);
            for (int i = 0; i < 2; i++)
            {
                _art[i] = UiKit.Rect(artRoot, "Key Art " + i).gameObject.AddComponent<RawImage>();
                _art[i].raycastTarget = false;
                UiKit.Stretch(_art[i].rectTransform);
            }
            if (_artwork.Count > 0)
            {
                _art[1].texture = _artwork[0];
                _art[0].texture = _artwork[Mathf.Min(1, _artwork.Count - 1)];
            }
            _artStarted = Time.unscaledTime;

            RawImage leftShade = UiKit.Rect(root, "Left Shade").gameObject.AddComponent<RawImage>();
            leftShade.texture = ShadeRamp(true, new Color(0.01f, 0.015f, 0.04f), 0.97f, 0.05f);
            leftShade.raycastTarget = false;
            UiKit.Stretch(leftShade.rectTransform);
            RawImage bottomShade = UiKit.Rect(root, "Bottom Shade").gameObject.AddComponent<RawImage>();
            bottomShade.texture = ShadeRamp(false, new Color(0.01f, 0.015f, 0.04f), 0.9f, 0f);
            bottomShade.raycastTarget = false;
            UiKit.Place(bottomShade.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(1920f, 420f));
            bottomShade.rectTransform.anchorMin = new Vector2(0f, 0f);
            bottomShade.rectTransform.anchorMax = new Vector2(1f, 0f);
            bottomShade.rectTransform.sizeDelta = new Vector2(0f, 420f);

            // ---------- sparks
            for (int i = 0; i < 46; i++)
            {
                RectTransform s = UiKit.Rect(root, "Spark");
                Image img = s.gameObject.AddComponent<Image>();
                img.sprite = DuelVisualResources.GlowSprite;
                img.raycastTarget = false;
                img.color = i % 3 == 0 ? new Color(0.85f, 0.45f, 1f, 0f) : new Color(0.3f, 0.9f, 1f, 0f);
                float size = UnityEngine.Random.Range(6f, 18f);
                s.sizeDelta = new Vector2(size, size * 1.3f);
                _sparks.Add((s, UnityEngine.Random.Range(0.02f, 0.06f), UnityEngine.Random.value, UnityEngine.Random.Range(0.02f, 0.98f)));
            }

            // ---------- logo + tagline
            _logo = UiKit.Rect(root, "Logo").gameObject.AddComponent<RawImage>();
            _logo.raycastTarget = false;
            Texture2D logoTex = DuelVisualResources.Logo ?? Resources.Load<Texture2D>("UI/DuelGenesisLogo");
            _logo.texture = logoTex;
            float logoW = 700f, logoH = logoTex != null ? logoW * logoTex.height / logoTex.width : 220f;
            UiKit.Place(_logo.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(90f, -60f), new Vector2(logoW, logoH));
            _logoGlow = UiKit.Glow(_logo, new Color(0.2f, 0.85f, 1f, 0.4f), 3f);
            if (logoTex == null)
            {
                Text fallback = UiKit.Label(root, "Logo Text", "DUEL: GENESIS", 84, new Color(0.3f, 0.9f, 1f), TextAnchor.MiddleLeft, FontStyle.Bold);
                UiKit.Place(fallback.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(96f, -80f), new Vector2(800f, 140f));
            }
            Text tagline = UiKit.Label(root, "Tagline", "GENESIS CITY  ·  THE HOLOGRAM DUEL ARENA", 22, new Color(0.75f, 0.55f, 1f), TextAnchor.MiddleLeft, FontStyle.Bold);
            UiKit.Place(tagline.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(100f, -60f - logoH - 6f), new Vector2(800f, 34f));
            UiKit.Glow(tagline, new Color(0f, 0f, 0f, 0.8f), 2f);

            // ---------- menu
            RectTransform menu = UiKit.Rect(root, "Menu");
            UiKit.Place(menu, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(96f, -60f - logoH - 70f), new Vector2(560f, 520f));
            AddItem(menu, "ENTER GENESIS CITY", "Explore, shop and find duelists", new Color(0.2f, 0.85f, 1f), EnterGenesis);
            AddItem(menu, "QUICK DUEL", "Sit straight down at the arena table", new Color(1f, 0.35f, 0.7f), QuickDuel);
            AddItem(menu, "DECK BUILDER", "Build and auto-build your deck", new Color(1f, 0.78f, 0.3f), OpenDeckBuilder);
            AddItem(menu, "SETTINGS", "Volume, graphics, camera", new Color(0.7f, 0.45f, 1f), () => ShowPage(_settingsPanel));
            AddItem(menu, "CONTROLS", "Keys and how duels work", new Color(0.4f, 0.9f, 0.6f), () => ShowPage(_controlsPanel));
            AddItem(menu, "QUIT", "", new Color(1f, 0.35f, 0.35f), QuitGame);
            for (int i = 0; i < _items.Count; i++)
                UiKit.Place(_items[i].Rect, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, -i * 78f), new Vector2(560f, 68f));

            // ---------- duelist card (top right)
            Image card = UiKit.Panel(root, "Duelist Card", new Color(0.03f, 0.05f, 0.10f, 0.82f));
            UiKit.Place(card.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-60f, -60f), new Vector2(520f, 190f));
            Text header = UiKit.Label(card.transform, "Header", "DUELIST", 14, new Color(0.55f, 0.7f, 0.9f), TextAnchor.UpperLeft, FontStyle.Bold);
            UiKit.Place(header.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(22f, -14f), new Vector2(300f, 20f));
            _duelistText = UiKit.Label(card.transform, "Name", "", 28, Color.white, TextAnchor.MiddleLeft);
            UiKit.Place(_duelistText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(22f, -36f), new Vector2(480f, 38f));
            Image xpBack = UiKit.Fill(card.transform, "XP Back", new Color(1f, 1f, 1f, 0.08f));
            UiKit.Place(xpBack.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(22f, -80f), new Vector2(476f, 6f));
            _xpFill = UiKit.Fill(xpBack.transform, "XP", new Color(0.3f, 0.9f, 1f, 0.95f));
            _xpFill.sprite = DuelVisualResources.RoundedSprite;
            _xpFill.type = Image.Type.Filled;
            _xpFill.fillMethod = Image.FillMethod.Horizontal;
            UiKit.Stretch(_xpFill.rectTransform);
            _statsText = UiKit.Label(card.transform, "Stats", "", 17, UiKit.TextColor, TextAnchor.UpperLeft);
            UiKit.Place(_statsText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(22f, -96f), new Vector2(480f, 52f));
            _deckText = UiKit.Label(card.transform, "Deck", "", 16, UiKit.TextColor, TextAnchor.MiddleLeft, FontStyle.Bold);
            UiKit.Place(_deckText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(22f, -150f), new Vector2(480f, 28f));

            // ---------- showcase fan (bottom right)
            _showcaseRoot = UiKit.Rect(root, "Showcase");
            UiKit.Place(_showcaseRoot, new Vector2(1f, 0f), new Vector2(0.5f, 0.5f), new Vector2(-330f, 330f), new Vector2(10f, 10f));
            for (int i = 0; i < 3; i++)
            {
                RawImage img = UiKit.Rect(_showcaseRoot, "Card " + i).gameObject.AddComponent<RawImage>();
                img.raycastTarget = false;
                img.rectTransform.sizeDelta = new Vector2(220f, 220f / ProductionCardVisualDrawer.CardAspect);
                UiKit.Glow(img, new Color(0f, 0f, 0f, 0.6f), 6f);
                _showcase.Add(img);
            }
            _showcase[1].transform.SetAsLastSibling();

            // ---------- pages
            _settingsPanel = BuildSettings(root);
            _controlsPanel = BuildControls(root);

            // ---------- ticker + toast + version
            Image tickerBar = UiKit.Fill(root, "Ticker Bar", new Color(0.02f, 0.03f, 0.07f, 0.85f));
            tickerBar.rectTransform.anchorMin = new Vector2(0f, 0f);
            tickerBar.rectTransform.anchorMax = new Vector2(1f, 0f);
            tickerBar.rectTransform.pivot = new Vector2(0.5f, 0f);
            tickerBar.rectTransform.sizeDelta = new Vector2(0f, 42f);
            tickerBar.gameObject.AddComponent<RectMask2D>();
            _ticker = UiKit.Label(tickerBar.transform, "Ticker", TickerText(), 18, new Color(0.75f, 0.9f, 1f), TextAnchor.MiddleLeft);
            _ticker.horizontalOverflow = HorizontalWrapMode.Overflow;
            _tickerRect = _ticker.rectTransform;
            _tickerRect.anchorMin = new Vector2(0f, 0f);
            _tickerRect.anchorMax = new Vector2(0f, 1f);
            _tickerRect.pivot = new Vector2(0f, 0.5f);
            _tickerRect.sizeDelta = new Vector2(Mathf.Max(2000f, _ticker.preferredWidth + 40f), 0f);

            Text version = UiKit.Label(root, "Version", "v0.8  ·  Genesis City", 15, new Color(1f, 1f, 1f, 0.45f), TextAnchor.MiddleRight);
            UiKit.Place(version.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-24f, 50f), new Vector2(400f, 24f));
            _toast = UiKit.Label(root, "Toast", "", 22, new Color(1f, 0.85f, 0.4f, 0f), TextAnchor.MiddleLeft, FontStyle.Bold);
            UiKit.Place(_toast.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(100f, 70f), new Vector2(1100f, 34f));
            UiKit.Glow(_toast, new Color(0f, 0f, 0f, 0.9f), 2f);
        }

        private string TickerText()
        {
            int working = 0, total = 0;
            if (CardDatabase.All != null)
                foreach (CardData c in CardDatabase.All)
                {
                    if (c.kind == CardKind.Monster) continue;
                    total++;
                    if (CardEffects.Get(c) != null) working++;
                }
            string effects = total > 0 ? $"{working} of {total} Spell & Trap cards now resolve in duels" : "Hundreds of Spell & Trap cards now resolve in duels";
            string[] news =
            {
                "NEW: " + effects,
                "Every monster rises as a hologram on the arena table",
                "Walk up to a duel table and press E to challenge a duelist",
                "Press B anywhere in the city to open the Deck Builder — F5 auto-builds a legal deck",
                "Booster terminals at the card shop: 1,000 GC a pack",
                "Look up: the Duel Genesis hologram never sleeps",
            };
            return string.Join("        ◆        ", news);
        }

        private void AddItem(RectTransform parent, string title, string subtitle, Color accent, Action action)
        {
            int index = _items.Count;
            var item = MenuItem.Create(parent, title, subtitle, accent, action, () => Select(index));
            _items.Add(item);
        }

        private RectTransform BuildSettings(Transform root)
        {
            Image panel = UiKit.Panel(root, "Settings", new Color(0.03f, 0.05f, 0.10f, 0.9f));
            UiKit.Place(panel.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-80f, -40f), new Vector2(640f, 470f));
            Text title = UiKit.Label(panel.transform, "Title", "SETTINGS", 30, Color.white, TextAnchor.MiddleLeft, FontStyle.Bold);
            UiKit.Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(30f, -20f), new Vector2(400f, 44f));
            _volumeValue = Row(panel.transform, 0, "Master volume", () => ChangeVolume(-0.1f), () => ChangeVolume(0.1f));
            _qualityValue = Row(panel.transform, 1, "Graphics quality", () => ChangeQuality(-1), () => ChangeQuality(1));
            _vsyncValue = Row(panel.transform, 2, "V-Sync", ToggleVsync, ToggleVsync);
            _sensitivityValue = Row(panel.transform, 3, "Camera sensitivity", () => ChangeSensitivity(-0.02f), () => ChangeSensitivity(0.02f));
            Button back = UiKit.Button(panel.transform, "Back", "BACK", DuelVisualResources.Cyan, () => ShowPage(null), 18);
            UiKit.Place((RectTransform)back.transform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-30f, 24f), new Vector2(160f, 46f));
            panel.gameObject.SetActive(false);
            return panel.rectTransform;
        }

        private Text Row(Transform panel, int index, string label, Action less, Action more)
        {
            float y = -96f - index * 76f;
            Text name = UiKit.Label(panel, label, label, 20, UiKit.TextColor, TextAnchor.MiddleLeft);
            UiKit.Place(name.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(32f, y), new Vector2(260f, 48f));
            Button minus = UiKit.Button(panel, label + " -", "◀", DuelVisualResources.Violet, less, 20);
            UiKit.Place((RectTransform)minus.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(300f, y), new Vector2(52f, 48f));
            Text value = UiKit.Label(panel, label + " Value", "", 20, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            UiKit.Place(value.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(358f, y), new Vector2(180f, 48f));
            Button plus = UiKit.Button(panel, label + " +", "▶", DuelVisualResources.Violet, more, 20);
            UiKit.Place((RectTransform)plus.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(544f, y), new Vector2(52f, 48f));
            return value;
        }

        private RectTransform BuildControls(Transform root)
        {
            Image panel = UiKit.Panel(root, "Controls", new Color(0.03f, 0.05f, 0.10f, 0.9f));
            UiKit.Place(panel.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-80f, -40f), new Vector2(640f, 520f));
            Text title = UiKit.Label(panel.transform, "Title", "CONTROLS", 30, Color.white, TextAnchor.MiddleLeft, FontStyle.Bold);
            UiKit.Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(30f, -20f), new Vector2(400f, 44f));
            Text body = UiKit.Label(panel.transform, "Body",
                "<color=#8FE9FF><b>CITY</b></color>\n" +
                "WASD  move        Mouse  look        Space  jump\n" +
                "E  interact / sit at a duel table\n" +
                "B  deck builder        C  collection        P  profile\n" +
                "Esc  pause\n\n" +
                "<color=#FF8FD0><b>DUELS</b></color>\n" +
                "Click a card in your hand, then a glowing zone to Summon or Set it.\n" +
                "Click Set Spells/Traps to activate them; chains prompt you automatically.\n" +
                "BATTLE → pick an attacker, then a target.        V  table view        H  holograms\n" +
                "F8  let the CPU play your side (autopilot)", 18, UiKit.TextColor, TextAnchor.UpperLeft);
            body.lineSpacing = 1.15f;
            UiKit.Place(body.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(32f, -80f), new Vector2(580f, 360f));
            Button back = UiKit.Button(panel.transform, "Back", "BACK", DuelVisualResources.Cyan, () => ShowPage(null), 18);
            UiKit.Place((RectTransform)back.transform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-30f, 24f), new Vector2(160f, 46f));
            panel.gameObject.SetActive(false);
            return panel.rectTransform;
        }

        /// <summary>Horizontal (left→right) or vertical (bottom→top) alpha ramp in one colour.</summary>
        private static Texture2D ShadeRamp(bool horizontal, Color color, float from, float to)
        {
            const int n = 256;
            var t = new Texture2D(horizontal ? n : 1, horizontal ? 1 : n, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp, name = "DG Title Gradient"
            };
            for (int i = 0; i < n; i++)
            {
                float k = i / (n - 1f);
                float a = Mathf.Lerp(from, to, Mathf.SmoothStep(0f, 1f, horizontal ? Mathf.Clamp01(k / 0.62f) : k));
                var c = new Color(color.r, color.g, color.b, a);
                if (horizontal) t.SetPixel(i, 0, c); else t.SetPixel(0, i, c);
            }
            t.Apply();
            return t;
        }

        // ================================================================== menu item

        /// <summary>One animated title-menu entry: accent bar, title, subtitle; slides and glows when selected.</summary>
        private sealed class MenuItem : MonoBehaviour, IPointerEnterHandler, IPointerClickHandler
        {
            public RectTransform Rect;
            public bool Selected;
            private Action _action, _hover;
            private Image _bar, _back;
            private Text _title, _subtitle;
            private RectTransform _content;
            private Color _accent;
            private float _t;

            public static MenuItem Create(Transform parent, string title, string subtitle, Color accent, Action action, Action hover)
            {
                RectTransform rt = UiKit.Rect(parent, "Item " + title);
                Image back = rt.gameObject.AddComponent<Image>();
                back.sprite = DuelVisualResources.RoundedSprite;
                back.type = Image.Type.Sliced;
                back.color = new Color(accent.r, accent.g, accent.b, 0f);
                MenuItem item = rt.gameObject.AddComponent<MenuItem>();
                item.Rect = rt;
                item._back = back;
                item._accent = accent;
                item._action = action;
                item._hover = hover;
                item._content = UiKit.Rect(rt, "Content");
                UiKit.Stretch(item._content);
                item._bar = UiKit.Fill(item._content, "Bar", accent);
                UiKit.Place(item._bar.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0f), new Vector2(5f, 40f));
                item._title = UiKit.Label(item._content, "Title", title, 32, Color.white, TextAnchor.MiddleLeft, FontStyle.Bold);
                UiKit.Place(item._title.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(22f, -4f), new Vector2(520f, 40f));
                UiKit.Glow(item._title, new Color(0f, 0f, 0f, 0.85f), 2f);
                item._subtitle = UiKit.Label(item._content, "Subtitle", subtitle, 15, new Color(0.7f, 0.78f, 0.9f), TextAnchor.MiddleLeft);
                UiKit.Place(item._subtitle.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -42f), new Vector2(520f, 22f));
                return item;
            }

            public void Activate() => _action?.Invoke();
            public void OnPointerEnter(PointerEventData eventData) => _hover?.Invoke();
            public void OnPointerClick(PointerEventData eventData) { _hover?.Invoke(); Activate(); }

            public void Animate(float dt)
            {
                _t = Mathf.MoveTowards(_t, Selected ? 1f : 0f, dt * 6f);
                float e = Mathf.SmoothStep(0f, 1f, _t);
                _content.anchoredPosition = new Vector2(18f * e, 0f);
                _bar.rectTransform.sizeDelta = new Vector2(5f + 5f * e, 34f + 16f * e);
                _back.color = new Color(_accent.r * 0.35f, _accent.g * 0.35f, _accent.b * 0.45f, 0.55f * e);
                _title.color = Color.Lerp(new Color(0.86f, 0.9f, 1f), Color.white, e);
                _title.fontSize = Mathf.RoundToInt(Mathf.Lerp(30f, 34f, e));
                _subtitle.color = new Color(0.7f, 0.78f, 0.9f, 0.55f + 0.45f * e);
            }
        }
    }
}
