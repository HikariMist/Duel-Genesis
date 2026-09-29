using System.Collections.Generic;
using System.Linq;
using DuelGenesis.Player;
using DuelGenesis.UI;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DuelGenesis.Characters
{
    /// <summary>
    /// The character creator. Press K in the city (or pick CHARACTER on the title screen): a camera turns to face
    /// your character on the left while the right panel edits body, face, colours and outfit live. Save keeps the
    /// look (PlayerPrefs for now, network-ready JSON); Cancel puts the old look back.
    /// </summary>
    public sealed class GenesisCharacterCreator : MonoBehaviour
    {
        private enum Tab { Body, Face, Colours, Outfit }

        private static readonly string[] BodyTypeNames = { "Default", "Older", "Lithe", "Slim", "Heavy", "Stocky", "Portly", "Pear", "Curvy" };
        private static readonly Color[] SkinPresets =
        {
            new Color(0.98f, 0.84f, 0.74f), new Color(0.93f, 0.72f, 0.6f), new Color(0.7725f, 0.4784f, 0.3765f),
            new Color(0.62f, 0.4f, 0.28f), new Color(0.45f, 0.29f, 0.2f), new Color(0.3f, 0.19f, 0.13f)
        };
        private static readonly Color[] ColourPresets =
        {
            new Color(0.05f, 0.04f, 0.03f), new Color(0.35f, 0.2f, 0.1f), new Color(0.85f, 0.7f, 0.4f), new Color(0.9f, 0.9f, 0.92f),
            new Color(0.8f, 0.1f, 0.1f), new Color(0.1f, 0.35f, 0.9f), new Color(0.1f, 0.7f, 0.75f), new Color(0.5f, 0.2f, 0.8f),
            new Color(0.2f, 0.7f, 0.2f), new Color(1f, 0.5f, 0.8f)
        };

        public bool IsOpen { get; private set; }

        private GenesisAppearance _look, _before;
        private GenesisPlayerCharacter _character;
        private Camera _camera;
        private Tab _tab;
        private int _faceGroup;
        private Vector2 _scroll;
        private float _rebuildAt = -1f;
        private bool _faceView;
        private float _spin;
        private Quaternion _avatarRest;
        private ThirdPersonPlayerController _controller;
        private ThirdPersonCamera _thirdPerson;

        private void Update()
        {
            Keyboard k = Keyboard.current;
            if (!IsOpen)
            {
                if (k != null && k.kKey.wasPressedThisFrame && !AnyOtherUiOpen()) Open();
                return;
            }
            if (k != null && k.escapeKey.wasPressedThisFrame) { Close(save: false); return; }
            if (_rebuildAt > 0f && Time.unscaledTime >= _rebuildAt) { _rebuildAt = -1f; Rebuild(); }
            PlaceCamera();
        }

        private static bool AnyOtherUiOpen()
        {
            var packs = FindAnyObjectByType<DuelGenesis.Shops.PackOpeningUI>();
            return packs != null && packs.IsOpen;
        }

        public void Open()
        {
            _character = FindAnyObjectByType<GenesisPlayerCharacter>();
            if (_character == null) { Debug.LogWarning("Duel: Genesis: no player character to edit."); return; }
            _before = GenesisAppearanceStore.Load();
            _look = _before.Clone();
            _controller = FindAnyObjectByType<ThirdPersonPlayerController>();
            _thirdPerson = FindAnyObjectByType<ThirdPersonCamera>();
            _controller?.SetMovementEnabled(false);
            _thirdPerson?.SetLookEnabled(false);
            _avatarRest = _character.transform.localRotation;
            _spin = 0f;

            var go = new GameObject("Character Creator Camera");
            _camera = go.AddComponent<Camera>();
            _camera.depth = 50;
            _camera.fieldOfView = 30f;
            _camera.nearClipPlane = 0.05f;
            _camera.rect = new Rect(0f, 0f, 0.55f, 1f);
            IsOpen = true;
            PlaceCamera();
        }

        private void Close(bool save)
        {
            if (!IsOpen) return;
            IsOpen = false;
            _rebuildAt = -1f;
            _liveDirty = false;
            if (save)
            {
                GenesisAppearanceStore.Save(_look);
                if (_character != null) _character.Rebuild(_look);
            }
            else if (_character != null) _character.Rebuild(_before);
            if (_character != null) _character.transform.localRotation = _avatarRest;
            if (_camera != null) Destroy(_camera.gameObject);
            _controller?.SetMovementEnabled(true);
            _thirdPerson?.SetLookEnabled(true);
        }

        private void PlaceCamera()
        {
            if (_camera == null || _character == null) return;
            Transform t = _character.transform;
            t.localRotation = _avatarRest * Quaternion.Euler(0f, _spin, 0f);
            Transform owner = t.parent != null ? t.parent : t;
            float baseHeight = _look.gender == GenesisGender.Male ? 1.74f : 1.64f;
            float heightScale = 1f + _look.height * 0.0012f;
            float h = baseHeight * heightScale;
            Vector3 target = t.position + Vector3.up * (_faceView ? h - 0.12f : h * 0.55f);
            float dist = (_faceView ? 0.9f : 4.2f) * heightScale;
            _camera.transform.position = target + owner.forward * dist;
            _camera.transform.rotation = Quaternion.LookRotation(target - _camera.transform.position, Vector3.up);
        }

        private void Rebuild()
        {
            if (_character != null) _character.Rebuild(_look);
        }

        private void LiveShapes()
        {
            if (_character != null && _character.Current != null) GenesisCharacterBuilder.ApplyShapes(_character.Current, _look);
        }

        private void RebuildSoon() => _rebuildAt = Time.unscaledTime + 0.12f;

        // ------------------------------------------------------------------ UI

        private void OnGUI()
        {
            if (!IsOpen) return;
            GUI.depth = -40;
            float w = Screen.width * 0.45f, x = Screen.width - w;
            var panel = new Rect(x, 0f, w, Screen.height);
            GenesisTheme.Box(panel, GenesisTheme.Background);
            GUI.Label(new Rect(x + 24f, 16f, w - 48f, 40f), "CHARACTER CREATOR", Style(28, FontStyle.Bold, GenesisTheme.Cyan));

            string[] tabs = { "BODY", "FACE", "COLOURS", "OUTFIT" };
            float tw = (w - 48f) / tabs.Length;
            for (int i = 0; i < tabs.Length; i++)
            {
                bool on = (int)_tab == i;
                if (GUI.Button(new Rect(x + 24f + i * tw, 64f, tw - 6f, 36f), tabs[i], Button(on)))
                {
                    _tab = (Tab)i;
                    _scroll = Vector2.zero;
                    if (_tab == Tab.Face) _faceView = true;
                }
            }

            var body = new Rect(x + 24f, 112f, w - 48f, Screen.height - 200f);
            GUILayout.BeginArea(body);
            _scroll = GUILayout.BeginScrollView(_scroll);
            switch (_tab)
            {
                case Tab.Body: DrawBody(); break;
                case Tab.Face: DrawFace(); break;
                case Tab.Colours: DrawColours(); break;
                case Tab.Outfit: DrawOutfit(); break;
            }
            GUILayout.EndScrollView();
            GUILayout.EndArea();

            // Apply body/face morph sliders immediately after IMGUI has written their new values.
            FlushLiveShapes();

            float by = Screen.height - 76f, bw = (w - 60f) / 4f;
            if (GUI.Button(new Rect(x + 24f, by, bw, 48f), "RANDOM", Button(false))) Randomize();
            if (GUI.Button(new Rect(x + 30f + bw, by, bw, 48f), _faceView ? "FULL BODY" : "FACE VIEW", Button(false))) _faceView = !_faceView;
            if (GUI.Button(new Rect(x + 36f + bw * 2f, by, bw, 48f), "CANCEL", Button(false))) Close(save: false);
            if (GUI.Button(new Rect(x + 42f + bw * 3f, by, bw, 48f), "SAVE", Button(true))) Close(save: true);

            // Turn the character with the arrows under the preview.
            float px = Screen.width * 0.55f * 0.5f;
            if (GUI.RepeatButton(new Rect(px - 130f, Screen.height - 70f, 110f, 44f), "◀ TURN", Button(false))) _spin -= 120f * Time.unscaledDeltaTime;
            if (GUI.RepeatButton(new Rect(px + 20f, Screen.height - 70f, 110f, 44f), "TURN ▶", Button(false))) _spin += 120f * Time.unscaledDeltaTime;
        }

        private void DrawBody()
        {
            Header("GENDER");
            GUILayout.BeginHorizontal();
            foreach (GenesisGender g in new[] { GenesisGender.Female, GenesisGender.Male })
                if (GUILayout.Button(g.ToString().ToUpperInvariant(), Button(_look.gender == g), GUILayout.Height(40)) && _look.gender != g)
                {
                    string name = _look.name;
                    _look = GenesisAppearance.CreateDefault(g);
                    _look.name = name;
                    Rebuild();
                }
            GUILayout.EndHorizontal();

            Header("BODY");
            _look.age = Slider("Youth", _look.age, 0f, 100f, live: true);
            _look.height = Slider("Height", _look.height, -100f, 100f, live: false);
            GUILayout.Label("Build", Style(15, FontStyle.Bold, Color.white));
            int sel = GUILayout.SelectionGrid(_look.bodyType, BodyTypeNames, 3, Button(false));
            if (sel != _look.bodyType) { _look.bodyType = sel; if (_look.bodyWeight < 1f) _look.bodyWeight = 60f; LiveShapes(); }
            _look.bodyWeight = Slider("Build strength", _look.bodyWeight, 0f, 100f, live: true);
            if (_look.gender == GenesisGender.Female) _look.breastSize = Slider("Bust", _look.breastSize, 0f, 100f, live: true);
        }

        private void DrawFace()
        {
            var groups = GenesisMorphMap.FaceGroups;
            _faceGroup = GUILayout.SelectionGrid(_faceGroup, groups.Select(g => g.label.ToUpperInvariant()).ToArray(), 5, Button(false));
            var group = groups[Mathf.Clamp(_faceGroup, 0, groups.Length - 1)];
            float[] values = group.values(_look.face);
            Header(group.label.ToUpperInvariant());
            for (int i = 0; i < group.morphs.Length && i < values.Length; i++)
                values[i] = Slider(Pretty(group.morphs[i]), values[i], -100f, 100f, live: true);
            if (GUILayout.Button("RESET " + group.label.ToUpperInvariant(), Button(false), GUILayout.Height(34)))
            {
                for (int i = 0; i < values.Length; i++) values[i] = 0f;
                LiveShapes();
            }
        }

        private void DrawColours()
        {
            Header("SKIN");
            Swatches(SkinPresets, c => { _look.skinColor = c; RebuildSoon(); });
            _look.skinColor = Rgb(_look.skinColor);
            Header("EYES");
            Swatches(ColourPresets, c => { _look.leftEyeColor = _look.rightEyeColor = c; RebuildSoon(); });
            Color eye = Rgb(_look.leftEyeColor);
            _look.leftEyeColor = _look.rightEyeColor = eye;
            GenesisEquip hair = _look.Get(GenesisSlot.Hairstyle);
            if (hair != null && hair.id >= 0)
            {
                Header("HAIR");
                if (hair.colors == null || hair.colors.Length == 0) hair.colors = new[] { ColourPresets[0] };
                Swatches(ColourPresets, c => { hair.colors[0] = c; if (hair.colors.Length > 1) hair.colors[1] = c; RebuildSoon(); });
                hair.colors[0] = Rgb(hair.colors[0]);
            }
            Header("BROWS");
            Swatches(ColourPresets, c => { _look.eyebrowColor = c; RebuildSoon(); });
        }

        private void DrawOutfit()
        {
            GenesisCharacterAssets assets = GenesisCharacterAssets.Instance;
            GenesisCharacterLibrary library = GenesisCharacterLibrary.Instance;
            if (assets == null) { GUILayout.Label("No wardrobe imported yet.", Style(16, FontStyle.Normal, Color.white)); return; }
            foreach (GenesisSlot slot in System.Enum.GetValues(typeof(GenesisSlot)))
            {
                List<GenesisCharacterAssets.Item> choices = assets.items.Where(i => i.slot == slot && i.prefab != null).OrderBy(i => i.id).ToList();
                if (choices.Count == 0) continue;
                GenesisEquip equip = _look.Get(slot);
                int index = choices.FindIndex(i => i.id == equip.id);   // -1 = none
                string label = index >= 0 ? (library?.Find(slot, equip.id)?.name ?? choices[index].name) : "None";
                GUILayout.BeginHorizontal();
                GUILayout.Label(slot.ToString().ToUpperInvariant(), Style(15, FontStyle.Bold, GenesisTheme.Gold), GUILayout.Width(110));
                if (GUILayout.Button("◀", Button(false), GUILayout.Width(44), GUILayout.Height(34))) Step(slot, choices, index, -1);
                GUILayout.Label(label, Style(15, FontStyle.Normal, Color.white), GUILayout.Height(34), GUILayout.ExpandWidth(true));
                if (GUILayout.Button("▶", Button(false), GUILayout.Width(44), GUILayout.Height(34))) Step(slot, choices, index, +1);
                GUILayout.EndHorizontal();

                if (equip.id >= 0)
                    equip.proportion = ItemSizeSlider(equip.proportion);
                GUILayout.Space(6);
            }
            GUILayout.Space(8);
            GUILayout.Label($"{assets.items.Count} items in the wardrobe.", Style(13, FontStyle.Normal, GenesisTheme.Muted));
        }

        private void Step(GenesisSlot slot, List<GenesisCharacterAssets.Item> choices, int index, int dir)
        {
            bool required = slot == GenesisSlot.Hairstyle || slot == GenesisSlot.Shirt || slot == GenesisSlot.Pants;
            int count = choices.Count + (required ? 0 : 1);   // optional slots can also be empty
            int pos = index < 0 ? choices.Count : index;
            pos = ((pos + dir) % count + count) % count;
            GenesisEquip e = _look.Get(slot);
            e.proportion = 0f;   // each newly selected item starts at its authored 100% size
            if (pos >= choices.Count) e.id = -1;
            else
            {
                e.id = choices[pos].id;
                if (e.colors == null || e.colors.Length == 0) e.colors = new[] { Color.white };
            }
            Rebuild();
        }

        private void Randomize()
        {
            var r = new System.Random();
            float R(float a, float b) => a + (float)r.NextDouble() * (b - a);
            _look.skinColor = SkinPresets[r.Next(SkinPresets.Length)];
            _look.leftEyeColor = _look.rightEyeColor = ColourPresets[r.Next(ColourPresets.Length)];
            _look.age = R(0f, 60f);
            _look.height = R(-40f, 40f);
            _look.bodyType = r.Next(BodyTypeNames.Length);
            _look.bodyWeight = R(0f, 70f);
            foreach (var g in GenesisMorphMap.FaceGroups)
            {
                float[] v = g.values(_look.face);
                for (int i = 0; i < v.Length; i++) v[i] = R(-35f, 35f);
            }
            GenesisCharacterAssets assets = GenesisCharacterAssets.Instance;
            if (assets != null)
                foreach (GenesisSlot slot in new[] { GenesisSlot.Hairstyle, GenesisSlot.Shirt, GenesisSlot.Pants, GenesisSlot.Shoes })
                {
                    var choices = assets.items.Where(i => i.slot == slot && i.prefab != null).ToList();
                    if (choices.Count > 0)
                    {
                        var e = _look.Get(slot);
                        e.id = choices[r.Next(choices.Count)].id;
                        e.colors = new[] { ColourPresets[r.Next(ColourPresets.Length)] };
                        e.proportion = 0f;
                    }
                }
            Rebuild();
        }

        // ------------------------------------------------------------------ widgets

        private float Slider(string label, float value, float min, float max, bool live)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, Style(14, FontStyle.Normal, Color.white), GUILayout.Width(170));
            float v = GUILayout.HorizontalSlider(value, min, max, GUILayout.Height(22), GUILayout.ExpandWidth(true));
            GUILayout.Label(Mathf.RoundToInt(v).ToString(), Style(13, FontStyle.Normal, GenesisTheme.Muted), GUILayout.Width(40));
            GUILayout.EndHorizontal();
            if (!Mathf.Approximately(v, value))
            {
                if (live) ApplyLater();
                else RebuildSoon();
            }
            return v;
        }

        private float ItemSizeSlider(float value)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Space(110);
            GUILayout.Label("Size", Style(13, FontStyle.Normal, Color.white), GUILayout.Width(48));
            float v = GUILayout.HorizontalSlider(value, -100f, 100f, GUILayout.Height(22), GUILayout.ExpandWidth(true));
            int percent = Mathf.RoundToInt(Mathf.Lerp(75f, 125f, Mathf.InverseLerp(-100f, 100f, v)));
            GUILayout.Label(percent + "%", Style(13, FontStyle.Normal, GenesisTheme.Muted), GUILayout.Width(46));
            if (GUILayout.Button("RESET", Button(false), GUILayout.Width(58), GUILayout.Height(24))) v = 0f;
            GUILayout.EndHorizontal();
            if (!Mathf.Approximately(v, value)) RebuildSoon();
            return v;
        }

        private bool _liveDirty;
        private void ApplyLater() => _liveDirty = true;

        private void FlushLiveShapes()
        {
            if (!_liveDirty) return;
            _liveDirty = false;
            LiveShapes();
        }

        private void LateUpdate() => FlushLiveShapes();

        private Color Rgb(Color c)
        {
            GUILayout.BeginHorizontal();
            float r = GUILayout.HorizontalSlider(c.r, 0f, 1f), g = GUILayout.HorizontalSlider(c.g, 0f, 1f), b = GUILayout.HorizontalSlider(c.b, 0f, 1f);
            GUILayout.EndHorizontal();
            var n = new Color(r, g, b, 1f);
            if (n != new Color(c.r, c.g, c.b, 1f)) RebuildSoon();
            return n;
        }

        private void Swatches(Color[] colours, System.Action<Color> pick)
        {
            GUILayout.BeginHorizontal();
            foreach (Color c in colours)
            {
                Rect r = GUILayoutUtility.GetRect(34, 34, GUILayout.Width(34), GUILayout.Height(34));
                GenesisTheme.Box(r, c);
                if (GUI.Button(r, GUIContent.none, GUIStyle.none)) pick(c);
            }
            GUILayout.EndHorizontal();
        }

        private static void Header(string text)
        {
            GUILayout.Space(10);
            GUILayout.Label(text, Style(17, FontStyle.Bold, GenesisTheme.Cyan));
        }

        private static string Pretty(string morph)
        {
            string s = morph.Replace("BaseAnime_", "").Replace("head_bs_", "").Replace("head_cbs_", "").Replace("head_ctrl_", "")
                .Replace("facs_ctrl_", "").Replace("facs_bs_", "").Replace("Whole", " ");
            return System.Text.RegularExpressions.Regex.Replace(s, "(?<=[a-z])(?=[A-Z])", " ").Trim();
        }

        private static GUIStyle Style(int size, FontStyle style, Color colour) =>
            new GUIStyle(GUI.skin.label) { fontSize = size, fontStyle = style, wordWrap = true, normal = { textColor = colour } };

        private static GUIStyle Button(bool on)
        {
            var s = new GUIStyle(GUI.skin.button) { fontSize = 14, fontStyle = FontStyle.Bold };
            s.normal.textColor = on ? GenesisTheme.Cyan : Color.white;
            return s;
        }
    }
}
