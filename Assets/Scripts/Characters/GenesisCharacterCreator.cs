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

        private void RebuildSoon()
        {
            float next = Time.unscaledTime + 0.04f;
            if (_rebuildAt < 0f || _rebuildAt > next) _rebuildAt = next;
        }

        // ------------------------------------------------------------------ UI

        private void OnGUI()
        {
            if (!IsOpen) return;
            GUI.depth = -40;

            float previewW = Screen.width * 0.55f;
            float w = Screen.width - previewW;
            float x = previewW;

            // Preview side: subtle HUD framing without obscuring the character.
            GenesisTheme.Box(new Rect(0f, 0f, previewW, 72f), new Color(0.025f, 0.035f, 0.07f, 0.78f));
            GenesisTheme.Box(new Rect(previewW - 3f, 0f, 3f, Screen.height), GenesisTheme.Cyan);
            GUI.Label(new Rect(26f, 15f, previewW - 52f, 28f), "LIVE DUELIST PREVIEW", Style(18, FontStyle.Bold, Color.white));
            GUI.Label(new Rect(26f, 42f, previewW - 52f, 22f), "Turn the model • switch face/full-body view • changes preview live", Style(12, FontStyle.Normal, GenesisTheme.Muted));

            // Main creator panel.
            GenesisTheme.Box(new Rect(x, 0f, w, Screen.height), GenesisTheme.Background);
            GenesisTheme.Box(new Rect(x, 0f, w, 5f), GenesisTheme.Purple);
            GenesisTheme.Box(new Rect(x, 5f, w, 92f), GenesisTheme.PanelAlt);
            GUI.Label(new Rect(x + 24f, 16f, w - 48f, 18f), "DUEL : GENESIS", Style(12, FontStyle.Bold, GenesisTheme.Gold));
            GUI.Label(new Rect(x + 24f, 34f, w - 48f, 36f), "CHARACTER CREATOR", Style(27, FontStyle.Bold, Color.white));
            GUI.Label(new Rect(x + 24f, 67f, w - 48f, 20f), "Build the duelist you want to take into Genesis City.", Style(12, FontStyle.Normal, GenesisTheme.Muted));

            string[] tabs = { "BODY", "FACE", "COLOURS", "OUTFIT" };
            float tw = (w - 48f) / tabs.Length;
            for (int i = 0; i < tabs.Length; i++)
            {
                bool on = (int)_tab == i;
                Rect tabRect = new Rect(x + 24f + i * tw, 105f, tw - 6f, 38f);
                if (TabButton(tabRect, tabs[i], on))
                {
                    _tab = (Tab)i;
                    _scroll = Vector2.zero;
                    if (_tab == Tab.Face) _faceView = true;
                }
            }

            Rect contentCard = new Rect(x + 18f, 153f, w - 36f, Screen.height - 247f);
            GenesisTheme.Box(contentCard, GenesisTheme.Panel);
            GenesisTheme.Box(new Rect(contentCard.x, contentCard.y, 3f, contentCard.height), new Color(GenesisTheme.Purple.r, GenesisTheme.Purple.g, GenesisTheme.Purple.b, 0.75f));

            var body = new Rect(contentCard.x + 14f, contentCard.y + 12f, contentCard.width - 28f, contentCard.height - 24f);
            GUILayout.BeginArea(body);
            _scroll = GUILayout.BeginScrollView(_scroll, false, true);
            switch (_tab)
            {
                case Tab.Body: DrawBody(); break;
                case Tab.Face: DrawFace(); break;
                case Tab.Colours: DrawColours(); break;
                case Tab.Outfit: DrawOutfit(); break;
            }
            GUILayout.EndScrollView();
            GUILayout.EndArea();

            FlushLiveShapes();

            // Persistent action bar.
            float by = Screen.height - 82f;
            GenesisTheme.Box(new Rect(x, by - 8f, w, 90f), new Color(0.045f, 0.055f, 0.09f, 0.99f));
            float bw = (w - 66f) / 4f;
            if (ActionButton(new Rect(x + 24f, by, bw, 50f), "RANDOMIZE", GenesisTheme.PanelAlt)) Randomize();
            if (ActionButton(new Rect(x + 30f + bw, by, bw, 50f), _faceView ? "FULL BODY" : "FACE VIEW", GenesisTheme.Purple)) _faceView = !_faceView;
            if (ActionButton(new Rect(x + 36f + bw * 2f, by, bw, 50f), "CANCEL", GenesisTheme.Danger)) Close(save: false);
            if (ActionButton(new Rect(x + 42f + bw * 3f, by, bw, 50f), "SAVE", GenesisTheme.Green)) Close(save: true);

            float px = previewW * 0.5f;
            if (ActionButton(new Rect(px - 134f, Screen.height - 68f, 114f, 42f), "◀ TURN", GenesisTheme.PanelAlt)) _spin -= 120f * Time.unscaledDeltaTime;
            if (ActionButton(new Rect(px + 20f, Screen.height - 68f, 114f, 42f), "TURN ▶", GenesisTheme.PanelAlt)) _spin += 120f * Time.unscaledDeltaTime;
        }

        private void DrawBody()
        {
            Header("GENDER", "Choose the base figure. Your other settings remain editable.");
            GUILayout.BeginHorizontal();
            foreach (GenesisGender g in new[] { GenesisGender.Female, GenesisGender.Male })
            {
                Color old = GUI.backgroundColor;
                GUI.backgroundColor = _look.gender == g ? GenesisTheme.Purple : GenesisTheme.PanelAlt;
                if (GUILayout.Button(g.ToString().ToUpperInvariant(), Button(_look.gender == g), GUILayout.Height(42)) && _look.gender != g)
                {
                    string name = _look.name;
                    _look = GenesisAppearance.CreateDefault(g);
                    _look.name = name;
                    Rebuild();
                }
                GUI.backgroundColor = old;
            }
            GUILayout.EndHorizontal();

            Header("BODY SHAPE", "Adjust height, age and overall build.");
            _look.age = Slider("Youth", _look.age, 0f, 100f, live: true);
            _look.height = Slider("Height", _look.height, -100f, 100f, live: false);
            GUILayout.Space(6);
            GUILayout.Label("BUILD PRESET", Style(12, FontStyle.Bold, GenesisTheme.Muted));
            int sel = GUILayout.SelectionGrid(_look.bodyType, BodyTypeNames, 3, Button(false), GUILayout.Height(102));
            if (sel != _look.bodyType) { _look.bodyType = sel; if (_look.bodyWeight < 1f) _look.bodyWeight = 60f; LiveShapes(); }
            _look.bodyWeight = Slider("Build Strength", _look.bodyWeight, 0f, 100f, live: true);
            if (_look.gender == GenesisGender.Female) _look.breastSize = Slider("Bust", _look.breastSize, 0f, 100f, live: true);
        }

        private void DrawFace()
        {
            Header("FACE SCULPT", "Pick a region, then shape it with the sliders below.");
            var groups = GenesisMorphMap.FaceGroups;
            _faceGroup = GUILayout.SelectionGrid(_faceGroup, groups.Select(g => g.label.ToUpperInvariant()).ToArray(), 5, Button(false), GUILayout.Height(72));
            var group = groups[Mathf.Clamp(_faceGroup, 0, groups.Length - 1)];
            float[] values = group.values(_look.face);
            Header(group.label.ToUpperInvariant(), "Changes apply directly to the character preview.");
            for (int i = 0; i < group.morphs.Length && i < values.Length; i++)
                values[i] = Slider(Pretty(group.morphs[i]), values[i], -100f, 100f, live: true);
            GUILayout.Space(8);
            Color old = GUI.backgroundColor;
            GUI.backgroundColor = GenesisTheme.PanelAlt;
            if (GUILayout.Button("RESET " + group.label.ToUpperInvariant(), Button(false), GUILayout.Height(36)))
            {
                for (int i = 0; i < values.Length; i++) values[i] = 0f;
                LiveShapes();
            }
            GUI.backgroundColor = old;
        }

        private void DrawColours()
        {
            Header("SKIN", "Choose a preset or fine-tune the RGB sliders.");
            Swatches(SkinPresets, c => { _look.skinColor = c; RebuildSoon(); });
            _look.skinColor = Rgb(_look.skinColor);

            Header("EYES", "Eye colour updates on the live character.");
            Swatches(ColourPresets, c => { _look.leftEyeColor = _look.rightEyeColor = c; RebuildSoon(); });
            Color eye = Rgb(_look.leftEyeColor);
            _look.leftEyeColor = _look.rightEyeColor = eye;

            GenesisEquip hair = _look.Get(GenesisSlot.Hairstyle);
            if (hair != null && hair.id >= 0)
            {
                Header("HAIR", "Tint the currently equipped hairstyle.");
                if (hair.colors == null || hair.colors.Length == 0) hair.colors = new[] { ColourPresets[0] };
                Swatches(ColourPresets, c => { hair.colors[0] = c; if (hair.colors.Length > 1) hair.colors[1] = c; RebuildSoon(); });
                hair.colors[0] = Rgb(hair.colors[0]);
            }

            Header("BROWS", "Brow colour is applied to the face overlay.");
            Swatches(ColourPresets, c => { _look.eyebrowColor = c; RebuildSoon(); });
        }

        private void DrawOutfit()
        {
            GenesisCharacterAssets assets = GenesisCharacterAssets.Instance;
            GenesisCharacterLibrary library = GenesisCharacterLibrary.Instance;
            if (assets == null) { GUILayout.Label("No wardrobe imported yet.", Style(16, FontStyle.Normal, Color.white)); return; }

            Header("WARDROBE", "Cycle each slot and resize the equipped piece independently.");
            foreach (GenesisSlot slot in System.Enum.GetValues(typeof(GenesisSlot)))
            {
                List<GenesisCharacterAssets.Item> choices = assets.items.Where(i => i.slot == slot && i.prefab != null).OrderBy(i => i.id).ToList();
                if (choices.Count == 0) continue;
                GenesisEquip equip = _look.Get(slot);
                int index = choices.FindIndex(i => i.id == equip.id);
                string label = index >= 0 ? (library?.Find(slot, equip.id)?.name ?? choices[index].name) : "None";

                Color old = GUI.backgroundColor;
                GUI.backgroundColor = new Color(GenesisTheme.PanelAlt.r, GenesisTheme.PanelAlt.g, GenesisTheme.PanelAlt.b, 0.82f);
                GUILayout.BeginVertical(GUI.skin.box);
                GUI.backgroundColor = old;

                GUILayout.BeginHorizontal();
                GUILayout.Label(slot.ToString().ToUpperInvariant(), Style(14, FontStyle.Bold, GenesisTheme.Gold), GUILayout.Width(105));
                if (GUILayout.Button("◀", Button(false), GUILayout.Width(42), GUILayout.Height(34))) Step(slot, choices, index, -1);
                GUILayout.Label(label, Style(14, FontStyle.Bold, Color.white), GUILayout.Height(34), GUILayout.ExpandWidth(true));
                if (GUILayout.Button("▶", Button(false), GUILayout.Width(42), GUILayout.Height(34))) Step(slot, choices, index, +1);
                GUILayout.EndHorizontal();

                if (equip.id >= 0) equip.proportion = ItemSizeSlider(equip.proportion);
                GUILayout.EndVertical();
                GUILayout.Space(6);
            }
            GUILayout.Label($"{assets.items.Count} wardrobe pieces available", Style(12, FontStyle.Normal, GenesisTheme.Muted));
        }

        private void Step(GenesisSlot slot, List<GenesisCharacterAssets.Item> choices, int index, int dir)
        {
            bool required = slot == GenesisSlot.Hairstyle || slot == GenesisSlot.Shirt || slot == GenesisSlot.Pants;
            int count = choices.Count + (required ? 0 : 1);
            int pos = index < 0 ? choices.Count : index;
            pos = ((pos + dir) % count + count) % count;
            GenesisEquip e = _look.Get(slot);
            e.proportion = 0f;
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
            GUILayout.Space(3);
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, Style(14, FontStyle.Normal, Color.white), GUILayout.Width(154));
            Color old = GUI.backgroundColor;
            GUI.backgroundColor = GenesisTheme.Cyan;
            float v = GUILayout.HorizontalSlider(value, min, max, GUILayout.Height(24), GUILayout.ExpandWidth(true));
            GUI.backgroundColor = old;
            GUILayout.Label(Mathf.RoundToInt(v).ToString(), ValueStyle(), GUILayout.Width(48), GUILayout.Height(24));
            GUILayout.EndHorizontal();
            if (!Mathf.Approximately(v, value))
            {
                if (live) ApplyLater();
                else RebuildSoon();
            }
            GUILayout.Space(3);
            return v;
        }

        private float ItemSizeSlider(float value)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Space(105);
            GUILayout.Label("SIZE", Style(11, FontStyle.Bold, GenesisTheme.Muted), GUILayout.Width(42));
            Color old = GUI.backgroundColor;
            GUI.backgroundColor = GenesisTheme.Purple;
            float v = GUILayout.HorizontalSlider(value, -100f, 100f, GUILayout.Height(24), GUILayout.ExpandWidth(true));
            GUI.backgroundColor = old;
            int percent = Mathf.RoundToInt(Mathf.Lerp(70f, 130f, Mathf.InverseLerp(-100f, 100f, v)));
            GUILayout.Label(percent + "%", ValueStyle(), GUILayout.Width(52), GUILayout.Height(24));
            if (GUILayout.Button("RESET", Button(false), GUILayout.Width(58), GUILayout.Height(26))) v = 0f;
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
            string[] labels = { "R", "G", "B" };
            float[] values = { c.r, c.g, c.b };
            for (int i = 0; i < 3; i++)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label(labels[i], Style(11, FontStyle.Bold, GenesisTheme.Muted), GUILayout.Width(18));
                Color old = GUI.backgroundColor;
                GUI.backgroundColor = GenesisTheme.Cyan;
                values[i] = GUILayout.HorizontalSlider(values[i], 0f, 1f, GUILayout.Height(22), GUILayout.ExpandWidth(true));
                GUI.backgroundColor = old;
                GUILayout.Label(Mathf.RoundToInt(values[i] * 255f).ToString(), ValueStyle(), GUILayout.Width(44), GUILayout.Height(22));
                GUILayout.EndHorizontal();
            }
            var n = new Color(values[0], values[1], values[2], 1f);
            if (n != new Color(c.r, c.g, c.b, 1f)) RebuildSoon();
            return n;
        }

        private void Swatches(Color[] colours, System.Action<Color> pick)
        {
            GUILayout.BeginHorizontal();
            foreach (Color c in colours)
            {
                Rect r = GUILayoutUtility.GetRect(36, 36, GUILayout.Width(36), GUILayout.Height(36));
                GenesisTheme.Box(new Rect(r.x - 2f, r.y - 2f, r.width + 4f, r.height + 4f), new Color(1f, 1f, 1f, 0.15f));
                GenesisTheme.Box(r, c);
                if (GUI.Button(r, GUIContent.none, GUIStyle.none)) pick(c);
            }
            GUILayout.EndHorizontal();
            GUILayout.Space(5);
        }

        private static void Header(string text, string hint = null)
        {
            GUILayout.Space(12);
            GUILayout.Label(text, Style(16, FontStyle.Bold, GenesisTheme.Cyan));
            if (!string.IsNullOrEmpty(hint))
                GUILayout.Label(hint, Style(11, FontStyle.Normal, GenesisTheme.Muted));
            Rect line = GUILayoutUtility.GetRect(1f, 2f, GUILayout.ExpandWidth(true));
            GenesisTheme.Box(line, new Color(GenesisTheme.Cyan.r, GenesisTheme.Cyan.g, GenesisTheme.Cyan.b, 0.24f));
            GUILayout.Space(6);
        }

        private static bool TabButton(Rect rect, string label, bool active)
        {
            Color old = GUI.backgroundColor;
            GUI.backgroundColor = active ? GenesisTheme.Purple : new Color(0.16f, 0.18f, 0.25f, 1f);
            bool pressed = GUI.Button(rect, label, Button(active));
            GUI.backgroundColor = old;
            if (active) GenesisTheme.Box(new Rect(rect.x + 6f, rect.yMax - 3f, rect.width - 12f, 3f), GenesisTheme.Cyan);
            return pressed;
        }

        private static bool ActionButton(Rect rect, string label, Color color)
        {
            Color old = GUI.backgroundColor;
            GUI.backgroundColor = color;
            bool pressed = GUI.Button(rect, label, Button(false));
            GUI.backgroundColor = old;
            return pressed;
        }

        private static string Pretty(string morph)
        {
            string s = morph.Replace("BaseAnime_", "").Replace("head_bs_", "").Replace("head_cbs_", "").Replace("head_ctrl_", "")
                .Replace("facs_ctrl_", "").Replace("facs_bs_", "").Replace("Whole", " ");
            return System.Text.RegularExpressions.Regex.Replace(s, "(?<=[a-z])(?=[A-Z])", " ").Trim();
        }

        private static GUIStyle Style(int size, FontStyle style, Color colour) =>
            new GUIStyle(GUI.skin.label)
            {
                fontSize = size,
                fontStyle = style,
                wordWrap = true,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = colour }
            };

        private static GUIStyle ValueStyle() =>
            new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = GenesisTheme.Cyan }
            };

        private static GUIStyle Button(bool on)
        {
            var s = new GUIStyle(GUI.skin.button)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                padding = new RectOffset(8, 8, 4, 4)
            };
            s.normal.textColor = on ? Color.white : new Color(0.92f, 0.95f, 1f, 1f);
            s.hover.textColor = Color.white;
            s.active.textColor = Color.white;
            return s;
        }
    }
}
