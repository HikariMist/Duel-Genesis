using System.Collections.Generic;
using System.Linq;
using DuelGenesis.Dueling;
using DuelGenesis.Player;
using DuelGenesis.Shops;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace DuelGenesis.UI
{
    /// <summary>
    /// Round minimap in the top-right corner: a north-up view of the city (a top-down image baked by
    /// World > Bake Minimap) scrolling under an arrow that turns with the player, markers for the card shops,
    /// the Duel Center and the garden (pinned to the rim when off-map), plus the area name and compass heading.
    /// M toggles a bigger, zoomed-out map. Hidden while menus, the deck builder, the pack shop or a duel are open.
    /// </summary>
    public sealed class GenesisMinimap : MonoBehaviour
    {
        public const string MapResource = "DuelGenesis/Map/city_map";
        public const float WorldHalf = 420f;   // the map image covers -420..420 m on X and Z

        private const float SmallSize = 250f, BigSize = 520f;
        private const float SmallView = 150f, BigView = 520f;   // metres across

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (FindAnyObjectByType<GenesisMinimap>() != null) return;
            if (Resources.Load<Texture2D>(MapResource) == null) return;   // not baked yet
            var go = new GameObject("Genesis Minimap");
            DontDestroyOnLoad(go);
            go.AddComponent<GenesisMinimap>();
        }

        private sealed class Marker { public string Name; public Vector3 World; public RectTransform Icon; public bool Pin; }

        private Canvas _canvas;
        private RectTransform _frame, _arrow, _markers;
        private RawImage _map;
        private Text _area, _heading;
        private readonly List<Marker> _pins = new List<Marker>();
        private Transform _player;
        private bool _big;
        private float _nextScan;

        private GenesisMainMenu _menu;
        private DeckBuilderUI _deckBuilder;
        private PackOpeningUI _packs;
        private DuelGameController _duel;
        private GenesisPauseMenu _pause;
        private GenesisProfilePanel _profile;
        private DuelGenesis.Characters.GenesisCharacterCreator _creator;

        private static Sprite _circle, _ring, _tri, _dot;

        private void Start() => Build();

        private void Build()
        {
            var canvasGo = new GameObject("Minimap Canvas");
            canvasGo.transform.SetParent(transform, false);
            _canvas = canvasGo.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 40;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;

            _frame = NewRect("Minimap", canvasGo.transform);
            _frame.anchorMin = _frame.anchorMax = _frame.pivot = new Vector2(1f, 1f);
            _frame.anchoredPosition = new Vector2(-28f, -28f);

            // Dark backing, the map inside a round mask, a glowing rim.
            var back = NewImage("Backing", _frame, Circle(), new Color(0.02f, 0.03f, 0.06f, 0.85f));
            Stretch(back.rectTransform, -6f);
            var maskImg = NewImage("Mask", _frame, Circle(), Color.white);
            Stretch(maskImg.rectTransform, 0f);
            maskImg.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            _map = new GameObject("Map", typeof(RectTransform)).AddComponent<RawImage>();
            _map.transform.SetParent(maskImg.transform, false);
            _map.texture = Resources.Load<Texture2D>(MapResource);
            _map.color = new Color(1f, 1f, 1f, 0.95f);
            Stretch(_map.rectTransform, 0f);
            var rim = NewImage("Rim", _frame, Ring(), new Color(0.25f, 0.9f, 1f, 0.95f));
            Stretch(rim.rectTransform, -6f);

            _markers = NewRect("Markers", _frame);
            Stretch(_markers, 0f);

            // North tick on the rim (the map is always north-up).
            var north = NewText("N", _frame, "N", 20, new Color(1f, 0.85f, 0.35f), TextAnchor.MiddleCenter);
            north.rectTransform.anchorMin = north.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            north.rectTransform.sizeDelta = new Vector2(30f, 26f);
            north.rectTransform.anchoredPosition = new Vector2(0f, 2f);

            // The player arrow.
            var arrowImg = NewImage("Player", _frame, Tri(), new Color(1f, 0.95f, 0.4f));
            _arrow = arrowImg.rectTransform;
            _arrow.anchorMin = _arrow.anchorMax = new Vector2(0.5f, 0.5f);
            _arrow.sizeDelta = new Vector2(22f, 26f);
            var arrowOutline = arrowImg.gameObject.AddComponent<Outline>();
            arrowOutline.effectColor = new Color(0f, 0f, 0f, 0.9f);
            arrowOutline.effectDistance = new Vector2(1.5f, -1.5f);

            // Area name and heading under the map.
            _area = NewText("Area", _frame, "", 20, Color.white, TextAnchor.UpperCenter);
            _area.rectTransform.anchorMin = new Vector2(0f, 0f);
            _area.rectTransform.anchorMax = new Vector2(1f, 0f);
            _area.rectTransform.pivot = new Vector2(0.5f, 1f);
            _area.rectTransform.sizeDelta = new Vector2(80f, 28f);
            _area.rectTransform.anchoredPosition = new Vector2(0f, -12f);
            _heading = NewText("Heading", _frame, "", 16, new Color(0.6f, 0.95f, 1f), TextAnchor.UpperCenter);
            _heading.rectTransform.anchorMin = new Vector2(0f, 0f);
            _heading.rectTransform.anchorMax = new Vector2(1f, 0f);
            _heading.rectTransform.pivot = new Vector2(0.5f, 1f);
            _heading.rectTransform.sizeDelta = new Vector2(80f, 22f);
            _heading.rectTransform.anchoredPosition = new Vector2(0f, -38f);

            ApplySize();
        }

        private void ApplySize()
        {
            float size = _big ? BigSize : SmallSize;
            _frame.sizeDelta = new Vector2(size, size);
        }

        private void Update()
        {
            if (_canvas == null) return;
            var k = Keyboard.current;
            if (k != null && k.mKey.wasPressedThisFrame && !Typing()) { _big = !_big; ApplySize(); }

            if (_player == null)
            {
                var pc = FindAnyObjectByType<ThirdPersonPlayerController>();
                if (pc != null) _player = pc.transform;
            }
            if (Time.unscaledTime > _nextScan) { _nextScan = Time.unscaledTime + 5f; ScanMarkers(); ResolveUi(); }

            bool show = _player != null && !Blocked();
            if (_canvas.enabled != show) _canvas.enabled = show;
            if (!show) return;

            Vector3 p = _player.position;
            float view = _big ? BigView : SmallView;
            float span = view / (WorldHalf * 2f);
            _map.uvRect = new Rect((p.x + WorldHalf) / (WorldHalf * 2f) - span * 0.5f, (p.z + WorldHalf) / (WorldHalf * 2f) - span * 0.5f, span, span);

            float yaw = _player.eulerAngles.y;
            _arrow.localRotation = Quaternion.Euler(0f, 0f, -yaw);

            // Markers: on the map where they are, pinned to the rim when out of view.
            float size = _frame.sizeDelta.x, radius = size * 0.5f;
            foreach (Marker m in _pins)
            {
                if (m.Icon == null) continue;
                Vector2 d = new Vector2(m.World.x - p.x, m.World.z - p.z) * (size / view);
                bool outside = d.magnitude > radius - 12f;
                if (outside) d = d.normalized * (radius - 12f);
                m.Icon.anchoredPosition = d;
                m.Icon.localScale = Vector3.one * (outside ? 0.8f : 1f);
            }

            _area.text = AreaName(p);
            _heading.text = Compass(yaw);
        }

        private static bool Typing() => UnityEngine.EventSystems.EventSystem.current != null &&
                                       UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject != null &&
                                       UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject.GetComponent<InputField>() != null;

        private void ResolveUi()
        {
            if (_menu == null) _menu = FindAnyObjectByType<GenesisMainMenu>();
            if (_deckBuilder == null) _deckBuilder = FindAnyObjectByType<DeckBuilderUI>();
            if (_packs == null) _packs = FindAnyObjectByType<PackOpeningUI>();
            if (_duel == null) _duel = FindAnyObjectByType<DuelGameController>();
            if (_pause == null) _pause = FindAnyObjectByType<GenesisPauseMenu>();
            if (_profile == null) _profile = FindAnyObjectByType<GenesisProfilePanel>();
            if (_creator == null) _creator = FindAnyObjectByType<DuelGenesis.Characters.GenesisCharacterCreator>();
        }

        private bool Blocked() =>
            (_menu != null && _menu.IsOpen) || (_deckBuilder != null && _deckBuilder.IsOpen) || (_packs != null && _packs.IsOpen) ||
            (_duel != null && _duel.IsActive) || (_pause != null && _pause.IsOpen) || (_profile != null && _profile.IsOpen) ||
            (_creator != null && _creator.IsOpen);

        /// <summary>One marker per shop (terminals within 35 m of each other are one shop), plus the Duel Center and the garden.</summary>
        private void ScanMarkers()
        {
            var wanted = new List<(string name, Vector3 at, Color colour, string glyph)>();
            foreach (var t in FindObjectsByType<CardShopTerminal>(FindObjectsSortMode.None))
            {
                if (wanted.Any(w => w.glyph == "$" && Vector3.Distance(w.at, t.transform.position) < 35f)) continue;
                wanted.Add((t.shopName, t.transform.position, new Color(1f, 0.8f, 0.3f), "$"));
            }
            GameObject dc = GameObject.Find("Genesis Duel Center");
            if (dc != null) wanted.Add(("Genesis Colosseum", dc.transform.position, new Color(0.3f, 0.9f, 1f), "D"));
            GameObject garden = GameObject.Find("Japanese Garden");
            if (garden != null) wanted.Add(("Japanese Garden", new Vector3(-180f, 0f, -60f), new Color(1f, 0.45f, 0.6f), "G"));
            GameObject tower = GameObject.Find("Tokyo Tower");
            if (tower != null) wanted.Add(("Tokyo Tower", tower.transform.position, new Color(1f, 0.35f, 0.25f), "T"));

            if (wanted.Count == _pins.Count && wanted.Select(w => w.name).SequenceEqual(_pins.Select(m => m.Name))) return;
            foreach (var m in _pins) if (m.Icon != null) Destroy(m.Icon.gameObject);
            _pins.Clear();
            foreach (var (name, at, colour, glyph) in wanted)
            {
                var dot = NewImage("Marker - " + name, _markers, Dot(), colour);
                dot.rectTransform.anchorMin = dot.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                dot.rectTransform.sizeDelta = new Vector2(22f, 22f);
                var outline = dot.gameObject.AddComponent<Outline>();
                outline.effectColor = new Color(0f, 0f, 0f, 0.85f);
                var g = NewText("Glyph", dot.transform, glyph, 15, new Color(0.05f, 0.05f, 0.08f), TextAnchor.MiddleCenter);
                Stretch(g.rectTransform, 0f);
                _pins.Add(new Marker { Name = name, World = at, Icon = dot.rectTransform });
            }
            _arrow.SetAsLastSibling();
        }

        private string AreaName(Vector3 p)
        {
            Marker near = _pins.Where(m => m.Name != "Japanese Garden").OrderBy(m => Vector3.Distance(m.World, p)).FirstOrDefault();
            float reach = near != null && near.Name == "Tokyo Tower" ? 36f : 26f;
            if (near != null && Vector2.Distance(new Vector2(near.World.x, near.World.z), new Vector2(p.x, p.z)) < reach) return near.Name;
            float x = p.x, z = p.z;
            if (new Vector2(x, z).magnitude < 64f) return "Genesis Plaza";
            float ex = x / 52f, ez = (z - 126f) / 56f;   // the Genesis Colosseum's oval footprint
            if (ex * ex + ez * ez < 1f || (Mathf.Abs(x) < 44f && z > 60f && z < 106f)) return "Genesis Colosseum";
            if (x > -230f && x < -130f && z > -110f && z < -12f) return "Japanese Garden";
            if (x > 55f && x < 101f && z > 7f && z < 93f) return "Genesis Card Vault";
            if (Mathf.Abs(x) > 365f || Mathf.Abs(z) > 365f) return "Forest Edge";
            if (x > 126f && x < 234f && z > -140f && z < -100f) return "Chinatown Street";
            bool park = Mathf.Abs(x) > 14f && Mathf.Abs(x) < 108f && Mathf.Abs(z) > 14f && Mathf.Abs(z) < 108f;
            string quarter = z >= 0f ? (x >= 0f ? "Neon Heights" : "Kanda Ward") : (x >= 0f ? "Harbor Row" : "Old Town");
            return park ? "Sakura Park · " + quarter : quarter;
        }

        private static string Compass(float yaw)
        {
            string[] names = { "N", "NE", "E", "SE", "S", "SW", "W", "NW" };
            int i = Mathf.RoundToInt(Mathf.Repeat(yaw, 360f) / 45f) % 8;
            return $"{names[i]}  ·  {Mathf.RoundToInt(Mathf.Repeat(yaw, 360f))}°";
        }

        // ------------------------------------------------------------------ small UI helpers and generated sprites

        private static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        private static Image NewImage(string name, Transform parent, Sprite sprite, Color colour)
        {
            var img = NewRect(name, parent).gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.color = colour;
            img.raycastTarget = false;
            return img;
        }

        private static Text NewText(string name, Transform parent, string text, int size, Color colour, TextAnchor anchor)
        {
            var t = NewRect(name, parent).gameObject.AddComponent<Text>();
            t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.text = text;
            t.fontSize = size;
            t.fontStyle = FontStyle.Bold;
            t.color = colour;
            t.alignment = anchor;
            t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            var sh = t.gameObject.AddComponent<Shadow>();
            sh.effectColor = new Color(0f, 0f, 0f, 0.9f);
            sh.effectDistance = new Vector2(1.5f, -1.5f);
            return t;
        }

        private static void Stretch(RectTransform r, float inset)
        {
            r.anchorMin = Vector2.zero;
            r.anchorMax = Vector2.one;
            r.offsetMin = new Vector2(inset, inset);
            r.offsetMax = new Vector2(-inset, -inset);
        }

        private static Sprite MakeSprite(int size, System.Func<float, float, float> alpha)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float u = (x + 0.5f) / size * 2f - 1f, v = (y + 0.5f) / size * 2f - 1f;
                px[y * size + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(alpha(u, v)) * 255f));
            }
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        private static Sprite Circle() => _circle != null ? _circle : _circle = MakeSprite(256, (u, v) => (1f - Mathf.Sqrt(u * u + v * v)) * 128f);
        private static Sprite Ring() => _ring != null ? _ring : _ring = MakeSprite(256, (u, v) => { float r = Mathf.Sqrt(u * u + v * v); return Mathf.Min((1f - r) * 128f, (r - 0.955f) * 128f); });
        private static Sprite Dot() => _dot != null ? _dot : _dot = MakeSprite(64, (u, v) => (1f - Mathf.Sqrt(u * u + v * v)) * 24f);
        private static Sprite Tri() => _tri != null ? _tri : _tri = MakeSprite(64, (u, v) =>
        {
            // An arrowhead pointing up, with a notch at the back.
            float top = 0.95f, bottom = -0.85f;
            if (v > top || v < bottom) return 0f;
            float halfWidth = (top - v) / (top - bottom) * 0.85f;
            float notch = v < -0.45f ? (-0.45f - v) / 0.4f * 0.6f : 0f;
            return Mathf.Abs(u) < halfWidth && Mathf.Abs(u) >= notch ? 1f : 0f;
        });
    }
}
