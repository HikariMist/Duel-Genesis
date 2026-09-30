using System.Collections.Generic;
using System.Linq;
using DuelGenesis.Player;
using DuelGenesis.UI;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DuelGenesis.Characters
{
    /// <summary>
    /// The character creator. Press Shift+K in the city (or pick CHARACTER on the title screen): a camera turns to face
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
                if (k != null && k.kKey.wasPressedThisFrame && k.shiftKey.isPressed && !AnyOtherUiOpen()) Open();
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
            BuildStudio();
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
            if (_studio != null) Destroy(_studio);
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
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(UiScale, UiScale, 1f));   // same size on any screen
            GUI.backgroundColor = GenesisSciFiSkin.Ready ? new Color(0.35f, 0.48f, 0.85f, 1f) : new Color(0.22f, 0.26f, 0.4f, 1f);

            float previewW = VW * 0.55f;
            float w = VW - previewW;
            float x = previewW;

            GenesisTheme.Box(new Rect(0f, 0f, previewW, 72f), new Color(0.025f, 0.035f, 0.07f, 0.78f));
            GenesisTheme.Box(new Rect(previewW - 3f, 0f, 3f, VH), GenesisTheme.Cyan);
            GUI.Label(new Rect(26f, 15f, previewW - 52f, 28f), "LIVE DUELIST PREVIEW", Style(18, FontStyle.Bold, Color.white));
            GUI.Label(new Rect(26f, 42f, previewW - 52f, 22f), "Drag a control and watch the character update.", Style(12, FontStyle.Normal, GenesisTheme.Muted));
            DrawPreviewCorners(previewW);
            if (Event.current.type == EventType.Repaint)
            {
                GUI.DrawTexture(new Rect(0f, 72f, previewW - 3f, VH - 72f), Vignette(), ScaleMode.StretchToFill, true);
                Texture2D logo = Logo();
                if (logo != null) GUI.DrawTexture(new Rect(24f, VH - 150f, 130f, 121f), logo, ScaleMode.ScaleToFit, true);
            }

            GenesisTheme.Box(new Rect(x, 0f, w, VH), GenesisTheme.Background);
            GenesisSciFiSkin.Panel(new Rect(x + 6f, 4f, w - 10f, VH - 8f), "Extra_panel_glass_notches", new Color(0.3f, 0.42f, 0.75f, 0.45f), 28);
            GenesisTheme.Box(new Rect(x, 0f, w, 5f), GenesisTheme.Purple);
            GenesisTheme.Box(new Rect(x, 5f, w, 92f), GenesisTheme.PanelAlt);
            GUI.Label(new Rect(x + 24f, 16f, w - 48f, 18f), "DUEL : GENESIS", Style(12, FontStyle.Bold, GenesisTheme.Gold));
            GUI.Label(new Rect(x + 24f, 34f, w - 48f, 36f), "CHARACTER CREATOR", Style(27, FontStyle.Bold, Color.white));
            if (Logo() != null) GUI.DrawTexture(new Rect(x + w - 104f, 12f, 80f, 75f), Logo(), ScaleMode.ScaleToFit, true);
            GUI.Label(new Rect(x + 24f, 67f, w - 48f, 20f), "Create your duelist.", Style(12, FontStyle.Normal, GenesisTheme.Muted));

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

            Rect contentCard = new Rect(x + 18f, 153f, w - 36f, VH - 247f);
            if (!GenesisSciFiSkin.Panel(contentCard, "Extra_panel_glass_screws", new Color(0.16f, 0.22f, 0.45f, 0.92f), 24))
                GenesisTheme.Box(contentCard, GenesisTheme.Panel);
            GenesisTheme.Box(new Rect(contentCard.x, contentCard.y, 3f, contentCard.height), new Color(GenesisTheme.Purple.r, GenesisTheme.Purple.g, GenesisTheme.Purple.b, 0.75f));
            DrawHudOverlay(contentCard);

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

            float by = VH - 82f;
            GenesisTheme.Box(new Rect(x, by - 8f, w, 90f), new Color(0.045f, 0.055f, 0.09f, 0.99f));
            float bw = (w - 66f) / 4f;
            if (ActionButton(new Rect(x + 24f, by, bw, 50f), "RANDOMIZE", GenesisTheme.PanelAlt)) Randomize();
            if (ActionButton(new Rect(x + 30f + bw, by, bw, 50f), _faceView ? "FULL BODY" : "FACE VIEW", GenesisTheme.Purple)) _faceView = !_faceView;
            if (ActionButton(new Rect(x + 36f + bw * 2f, by, bw, 50f), "CANCEL", GenesisTheme.Danger)) Close(save: false);
            if (ActionButton(new Rect(x + 42f + bw * 3f, by, bw, 50f), "SAVE", GenesisTheme.Green)) Close(save: true);

            float px = previewW * 0.5f;
            if (ActionButton(new Rect(px - 134f, VH - 68f, 114f, 42f), "◀ TURN", GenesisTheme.PanelAlt)) _spin -= 120f * Time.unscaledDeltaTime;
            if (ActionButton(new Rect(px + 20f, VH - 68f, 114f, 42f), "TURN ▶", GenesisTheme.PanelAlt)) _spin += 120f * Time.unscaledDeltaTime;
        }

        private void DrawBody()
        {
            Header("DUELIST NAME");
            string typed = GUILayout.TextField(_look.name ?? "", 20, NameField(), GUILayout.Height(38));
            if (typed != _look.name) _look.name = typed;
            Header("GENDER");
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

            Header("BODY");
            _look.age = Slider("Youth", _look.age, 0f, 100f, live: true, "MATURE", "YOUTH");
            _look.height = Slider("Height", _look.height, -100f, 100f, live: false, "SHORT", "TALL");
            GUILayout.Space(8);
            GUILayout.Label("BUILD PRESET", Style(11, FontStyle.Bold, GenesisTheme.Muted));
            int sel = GUILayout.SelectionGrid(_look.bodyType, BodyTypeNames, 3, Button(false), GUILayout.Height(102));
            if (sel != _look.bodyType) { _look.bodyType = sel; if (_look.bodyWeight < 1f) _look.bodyWeight = 60f; LiveShapes(); }
            _look.bodyWeight = Slider("Build Strength", _look.bodyWeight, 0f, 100f, live: true, "LIGHT", "POWER");
            if (_look.gender == GenesisGender.Female) _look.breastSize = Slider("Bust", _look.breastSize, 0f, 100f, live: true, "SMALL", "LARGE");
        }

        private void DrawFace()
        {
            Header("FACE");
            var groups = GenesisMorphMap.FaceGroups;
            _faceGroup = GUILayout.SelectionGrid(_faceGroup, groups.Select(g => g.label.ToUpperInvariant()).ToArray(), 5, Button(false), GUILayout.Height(72));
            var group = groups[Mathf.Clamp(_faceGroup, 0, groups.Length - 1)];
            float[] values = group.values(_look.face);
            GUILayout.Space(10);
            GUILayout.Label(group.label.ToUpperInvariant(), Style(13, FontStyle.Bold, GenesisTheme.Gold));
            for (int i = 0; i < group.morphs.Length && i < values.Length; i++)
                values[i] = Slider(Pretty(group.morphs[i]), values[i], -100f, 100f, live: true, "−", "+");
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

            Header("WARDROBE");
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
                GUILayout.Label(slot.ToString().ToUpperInvariant(), Style(13, FontStyle.Bold, GenesisTheme.Gold), GUILayout.Width(105));
                if (GUILayout.Button("◀", Button(false), GUILayout.Width(42), GUILayout.Height(34))) Step(slot, choices, index, -1);
                GUILayout.Label(label, Style(14, FontStyle.Bold, Color.white), GUILayout.Height(34), GUILayout.ExpandWidth(true));
                if (GUILayout.Button("▶", Button(false), GUILayout.Width(42), GUILayout.Height(34))) Step(slot, choices, index, +1);
                GUILayout.EndHorizontal();

                if (equip.id >= 0) equip.proportion = ItemSizeSlider(equip.proportion);
                GUILayout.EndVertical();
                GUILayout.Space(6);
            }
            GUILayout.Label($"{assets.items.Count} wardrobe pieces", Style(11, FontStyle.Normal, GenesisTheme.Muted));
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

        // ------------------------------------------------------------------ Duel: Genesis HUD controls

        private float Slider(string label, float value, float min, float max, bool live, string low = "LOW", string high = "HIGH")
        {
            GUILayout.Space(5);
            Rect card = GUILayoutUtility.GetRect(10f, 68f, GUILayout.ExpandWidth(true));
            float v = HudSlider(card, label, value, min, max, GenesisTheme.Cyan, Mathf.RoundToInt(value).ToString(), low, high);
            if (!Mathf.Approximately(v, value))
            {
                if (live) ApplyLater();
                else RebuildSoon();
            }
            return v;
        }

        private float ItemSizeSlider(float value)
        {
            GUILayout.Space(4);
            Rect card = GUILayoutUtility.GetRect(10f, 70f, GUILayout.ExpandWidth(true));
            int currentPercent = Mathf.RoundToInt(Mathf.Lerp(70f, 130f, Mathf.InverseLerp(-100f, 100f, value)));
            float v = HudSlider(card, "ITEM SCALE", value, -100f, 100f, GenesisTheme.Purple, currentPercent + "%", "70%", "130%");

            Rect reset = new Rect(card.xMax - 126f, card.y + 7f, 52f, 20f);
            Color old = GUI.backgroundColor;
            GUI.backgroundColor = GenesisTheme.PanelAlt;
            if (GUI.Button(reset, "RESET", MiniButton())) v = 0f;
            GUI.backgroundColor = old;

            if (!Mathf.Approximately(v, value)) RebuildSoon();
            return v;
        }

        private static float HudSlider(Rect card, string label, float value, float min, float max, Color accent, string display, string low, string high)
        {
            bool hover = card.Contains(Event.current.mousePosition);
            Color panel = hover ? new Color(0.075f, 0.09f, 0.145f, 0.98f) : new Color(0.048f, 0.06f, 0.105f, 0.96f);
            if (!GenesisSciFiSkin.Panel(card, "Extra_panel_glass", hover ? new Color(0.32f, 0.42f, 0.75f, 0.95f) : new Color(0.2f, 0.27f, 0.5f, 0.9f), 16))
                GenesisTheme.Box(card, panel);
            GenesisTheme.Box(new Rect(card.x, card.y + 4f, 3f, card.height - 8f), accent);
            GenesisTheme.Box(new Rect(card.x + 3f, card.y, card.width - 3f, 1f), new Color(accent.r, accent.g, accent.b, hover ? 0.45f : 0.20f));
            GenesisTheme.Box(new Rect(card.x + 3f, card.yMax - 1f, card.width - 3f, 1f), new Color(0f, 0f, 0f, 0.34f));

            GUI.Label(new Rect(card.x + 13f, card.y + 5f, card.width - 160f, 22f), label.ToUpperInvariant(), Style(12, FontStyle.Bold, Color.white));
            DrawValueBadge(new Rect(card.xMax - 68f, card.y + 5f, 56f, 22f), display, accent);

            Rect sliderRect = new Rect(card.x + 14f, card.y + 27f, card.width - 28f, 28f);
            float result = GameSlider(sliderRect, value, min, max, accent);

            GUI.Label(new Rect(card.x + 14f, card.yMax - 17f, 90f, 14f), low, Style(9, FontStyle.Bold, GenesisTheme.Muted));
            GUI.Label(new Rect(card.xMax - 104f, card.yMax - 17f, 90f, 14f), high, RightStyle(9, FontStyle.Bold, GenesisTheme.Muted));
            return result;
        }

        private static float GameSlider(Rect rect, float value, float min, float max, Color accent)
        {
            int id = GUIUtility.GetControlID(FocusType.Passive, rect);
            Event e = Event.current;
            Rect hit = new Rect(rect.x, rect.y - 4f, rect.width, rect.height + 8f);
            float left = rect.x + 7f;
            float right = rect.xMax - 7f;

            switch (e.GetTypeForControl(id))
            {
                case EventType.MouseDown:
                    if (e.button == 0 && hit.Contains(e.mousePosition))
                    {
                        GUIUtility.hotControl = id;
                        value = Mathf.Lerp(min, max, Mathf.InverseLerp(left, right, e.mousePosition.x));
                        e.Use();
                    }
                    break;
                case EventType.MouseDrag:
                    if (GUIUtility.hotControl == id)
                    {
                        value = Mathf.Lerp(min, max, Mathf.InverseLerp(left, right, e.mousePosition.x));
                        e.Use();
                    }
                    break;
                case EventType.MouseUp:
                    if (GUIUtility.hotControl == id)
                    {
                        GUIUtility.hotControl = 0;
                        e.Use();
                    }
                    break;
            }

            value = Mathf.Clamp(value, min, max);
            bool hot = GUIUtility.hotControl == id;
            bool hover = hit.Contains(Event.current.mousePosition);
            float centerY = rect.y + 12f;
            Rect track = new Rect(left, centerY - 2f, Mathf.Max(1f, right - left), 5f);

            if (hover || hot)
                GenesisTheme.Box(new Rect(track.x - 3f, track.y - 5f, track.width + 6f, track.height + 10f), new Color(accent.r, accent.g, accent.b, hot ? 0.16f : 0.08f));

            GenesisTheme.Box(new Rect(track.x - 1f, track.y - 1f, track.width + 2f, track.height + 2f), new Color(0.01f, 0.015f, 0.035f, 1f));
            GenesisTheme.Box(track, new Color(0.14f, 0.17f, 0.24f, 1f));

            // Fine tick marks give the control a finished HUD/instrument-panel feel.
            for (int i = 0; i <= 10; i++)
            {
                float tx = Mathf.Lerp(track.xMin, track.xMax, i / 10f);
                float h = i == 5 ? 8f : (i % 5 == 0 ? 6f : 4f);
                GenesisTheme.Box(new Rect(tx, track.yMax + 2f, 1f, h), new Color(1f, 1f, 1f, i == 5 ? 0.34f : 0.13f));
            }

            float t = Mathf.InverseLerp(min, max, value);
            float knobX = Mathf.Lerp(track.xMin, track.xMax, t);
            if (min < 0f && max > 0f)
            {
                float zeroX = Mathf.Lerp(track.xMin, track.xMax, Mathf.InverseLerp(min, max, 0f));
                float fillX = Mathf.Min(zeroX, knobX);
                float fillW = Mathf.Abs(knobX - zeroX);
                if (fillW > 0.5f)
                {
                    GenesisTheme.Box(new Rect(fillX, track.y - 2f, fillW, track.height + 4f), new Color(accent.r, accent.g, accent.b, 0.18f));
                    GenesisTheme.Box(new Rect(fillX, track.y, fillW, track.height), accent);
                }
                GenesisTheme.Box(new Rect(zeroX - 1f, track.y - 5f, 2f, track.height + 10f), new Color(1f, 1f, 1f, 0.38f));
            }
            else
            {
                float fillW = Mathf.Max(0f, knobX - track.xMin);
                if (fillW > 0.5f)
                {
                    GenesisTheme.Box(new Rect(track.xMin, track.y - 2f, fillW, track.height + 4f), new Color(accent.r, accent.g, accent.b, 0.18f));
                    GenesisTheme.Box(new Rect(track.xMin, track.y, fillW, track.height), accent);
                }
            }

            float outer = hot ? 22f : hover ? 20f : 18f;
            GenesisTheme.Box(new Rect(knobX - outer * 0.5f, centerY - outer * 0.5f + 0.5f, outer, outer), new Color(accent.r, accent.g, accent.b, hot ? 0.24f : 0.12f));
            GenesisTheme.Box(new Rect(knobX - 8f, centerY - 8f + 0.5f, 16f, 16f), new Color(0.015f, 0.02f, 0.045f, 1f));
            GenesisTheme.Box(new Rect(knobX - 5f, centerY - 5f + 0.5f, 10f, 10f), accent);
            GenesisTheme.Box(new Rect(knobX - 1.5f, centerY - 1.5f + 0.5f, 3f, 3f), Color.white);
            return value;
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
            string[] labels = { "RED", "GREEN", "BLUE" };
            float[] values = { c.r, c.g, c.b };
            Color[] accents =
            {
                new Color(1f, 0.30f, 0.34f, 1f),
                new Color(0.30f, 1f, 0.48f, 1f),
                new Color(0.30f, 0.62f, 1f, 1f)
            };

            for (int i = 0; i < 3; i++)
            {
                GUILayout.Space(3);
                Rect card = GUILayoutUtility.GetRect(10f, 62f, GUILayout.ExpandWidth(true));
                values[i] = HudSlider(card, labels[i], values[i], 0f, 1f, accents[i], Mathf.RoundToInt(values[i] * 255f).ToString(), "0", "255");
            }

            var n = new Color(values[0], values[1], values[2], 1f);
            if (n != new Color(c.r, c.g, c.b, 1f)) RebuildSoon();
            return n;
        }

        private static void DrawValueBadge(Rect r, string text, Color accent)
        {
            GenesisTheme.Box(r, new Color(0.02f, 0.028f, 0.06f, 1f));
            GenesisTheme.Box(new Rect(r.x, r.y, 3f, r.height), accent);
            GenesisTheme.Box(new Rect(r.x + 3f, r.y, r.width - 3f, 1f), new Color(accent.r, accent.g, accent.b, 0.45f));
            GUI.Label(r, text, ValueStyle(accent));
        }

        private void Swatches(Color[] colours, System.Action<Color> pick)
        {
            GUILayout.BeginHorizontal();
            foreach (Color c in colours)
            {
                Rect r = GUILayoutUtility.GetRect(36, 36, GUILayout.Width(36), GUILayout.Height(36));
                bool hover = r.Contains(Event.current.mousePosition);
                if (Event.current.type == EventType.Repaint)
                {
                    Color old = GUI.color;
                    GUI.color = hover ? Color.white : new Color(1f, 1f, 1f, 0.25f);
                    GUI.DrawTexture(new Rect(r.x - 3f, r.y - 3f, r.width + 6f, r.height + 6f), Round(), ScaleMode.StretchToFill, true);
                    GUI.color = c;
                    GUI.DrawTexture(r, Round(), ScaleMode.StretchToFill, true);
                    GUI.color = old;
                }
                if (GUI.Button(r, GUIContent.none, GUIStyle.none)) pick(c);
            }
            GUILayout.EndHorizontal();
            GUILayout.Space(5);
        }

        private static void Header(string text)
        {
            GUILayout.Space(12);
            GUILayout.Label(text, Style(16, FontStyle.Bold, GenesisTheme.Cyan));
            Rect line = GUILayoutUtility.GetRect(1f, 2f, GUILayout.ExpandWidth(true));
            GenesisTheme.Box(line, new Color(GenesisTheme.Cyan.r, GenesisTheme.Cyan.g, GenesisTheme.Cyan.b, 0.24f));
            GUILayout.Space(6);
        }

        private static void DrawHudOverlay(Rect rect)
        {
            Color scan = new Color(GenesisTheme.Cyan.r, GenesisTheme.Cyan.g, GenesisTheme.Cyan.b, 0.018f);
            for (float y = rect.y + 22f; y < rect.yMax - 10f; y += 26f)
                GenesisTheme.Box(new Rect(rect.x + 8f, y, rect.width - 16f, 1f), scan);

            Color corner = new Color(GenesisTheme.Cyan.r, GenesisTheme.Cyan.g, GenesisTheme.Cyan.b, 0.40f);
            float c = 13f;
            GenesisTheme.Box(new Rect(rect.x + 7f, rect.y + 7f, c, 2f), corner);
            GenesisTheme.Box(new Rect(rect.x + 7f, rect.y + 7f, 2f, c), corner);
            GenesisTheme.Box(new Rect(rect.xMax - 7f - c, rect.y + 7f, c, 2f), corner);
            GenesisTheme.Box(new Rect(rect.xMax - 9f, rect.y + 7f, 2f, c), corner);
            GenesisTheme.Box(new Rect(rect.x + 7f, rect.yMax - 9f, c, 2f), corner);
            GenesisTheme.Box(new Rect(rect.x + 7f, rect.yMax - 7f - c, 2f, c), corner);
            GenesisTheme.Box(new Rect(rect.xMax - 7f - c, rect.yMax - 9f, c, 2f), corner);
            GenesisTheme.Box(new Rect(rect.xMax - 9f, rect.yMax - 7f - c, 2f, c), corner);
        }

        private static void DrawPreviewCorners(float previewW)
        {
            Color c = new Color(GenesisTheme.Cyan.r, GenesisTheme.Cyan.g, GenesisTheme.Cyan.b, 0.36f);
            float m = 24f, len = 26f;
            GenesisTheme.Box(new Rect(m, 92f, len, 2f), c);
            GenesisTheme.Box(new Rect(m, 92f, 2f, len), c);
            GenesisTheme.Box(new Rect(previewW - m - len, 92f, len, 2f), c);
            GenesisTheme.Box(new Rect(previewW - m - 2f, 92f, 2f, len), c);
        }

        private static bool TabButton(Rect rect, string label, bool active)
        {
            GUIStyle sci = GenesisSciFiSkin.Button(14, active ? Color.white : new Color(0.8f, 0.85f, 0.95f));
            if (sci != null)
            {
                Color was = GUI.backgroundColor;
                GUI.backgroundColor = active ? GenesisTheme.Purple : new Color(0.22f, 0.27f, 0.42f, 1f);
                bool hit = GUI.Button(rect, label, sci);
                GUI.backgroundColor = was;
                return hit;
            }
            Color old = GUI.backgroundColor;
            GUI.backgroundColor = active ? GenesisTheme.Purple : new Color(0.16f, 0.18f, 0.25f, 1f);
            bool pressed = GUI.Button(rect, label, Button(active));
            GUI.backgroundColor = old;
            if (active) GenesisTheme.Box(new Rect(rect.x + 6f, rect.yMax - 3f, rect.width - 12f, 3f), GenesisTheme.Cyan);
            return pressed;
        }

        private static bool ActionButton(Rect rect, string label, Color color)
        {
            GUIStyle sci = GenesisSciFiSkin.Button(15);
            if (sci != null)
            {
                Color was = GUI.backgroundColor;
                GUI.backgroundColor = color == GenesisTheme.PanelAlt ? new Color(0.35f, 0.4f, 0.55f, 1f) : color;
                bool hit = GUI.Button(rect, label, sci);
                GUI.backgroundColor = was;
                return hit;
            }
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

        private static GUIStyle RightStyle(int size, FontStyle style, Color colour) =>
            new GUIStyle(GUI.skin.label)
            {
                fontSize = size,
                fontStyle = style,
                wordWrap = false,
                alignment = TextAnchor.MiddleRight,
                normal = { textColor = colour }
            };

        private static GUIStyle ValueStyle(Color colour) =>
            new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = colour }
            };

        private static GUIStyle MiniButton() =>
            new GUIStyle(GUI.skin.button)
            {
                border = new RectOffset(14, 14, 14, 14),
                normal = { background = Round(), textColor = GenesisTheme.Muted },
                hover = { background = RoundBright(), textColor = Color.white },
                active = { background = RoundBright(), textColor = Color.white },
                fontSize = 9,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                padding = new RectOffset(4, 4, 2, 2)
            };

        private static GUIStyle Button(bool on)
        {
            GUIStyle sci = GenesisSciFiSkin.Button(13, on ? GenesisTheme.Cyan : Color.white);
            if (sci != null) return sci;
            var s = new GUIStyle(GUI.skin.button)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                padding = new RectOffset(8, 8, 4, 4),
                border = new RectOffset(14, 14, 14, 14)
            };
            s.normal.background = Round();
            s.hover.background = RoundBright();
            s.active.background = RoundBright();
            s.focused.background = Round();
            s.onNormal.background = RoundBright();
            s.normal.textColor = on ? GenesisTheme.Cyan : new Color(0.92f, 0.95f, 1f, 1f);
            s.onNormal.textColor = GenesisTheme.Cyan;
            s.hover.textColor = Color.white;
            s.active.textColor = Color.white;
            return s;
        }
            // ------------------------------------------------------------------ look and feel

        private GameObject _studio;

        /// <summary>The creator is laid out for 1080p and scaled to the real screen, so it keeps its size on any monitor.</summary>
        private static float UiScale => Mathf.Max(0.45f, Mathf.Min(UnityEngine.Screen.height / 1080f, UnityEngine.Screen.width / 1920f));   // fit both ways so narrow windows keep every button
        private static float VW => UnityEngine.Screen.width / UiScale;
        private static float VH => UnityEngine.Screen.height / UiScale;
        private static Texture2D _round, _roundBright, _vignette, _logo;

        /// <summary>Studio lighting on the duelist (key, fill and a coloured rim) and a spinning holo ring at their feet.</summary>
        private void BuildStudio()
        {
            if (_character == null) return;
            _studio = new GameObject("Character Creator Studio");
            Transform t = _character.transform;
            Transform owner = t.parent != null ? t.parent : t;
            _studio.transform.SetPositionAndRotation(t.position, owner.rotation);
            void Lamp(string n, Vector3 local, Color c, float intensity, float range)
            {
                var go = new GameObject(n);
                go.transform.SetParent(_studio.transform, false);
                go.transform.localPosition = local;
                var l = go.AddComponent<Light>();
                l.type = LightType.Point;
                l.color = c;
                l.intensity = intensity;
                l.range = range;
                l.shadows = LightShadows.None;
            }
            Lamp("Key", new Vector3(0.9f, 2.1f, 1.6f), new Color(1f, 0.95f, 0.9f), 6f, 6f);
            Lamp("Fill", new Vector3(-1.3f, 1.4f, 1.4f), new Color(0.75f, 0.85f, 1f), 3f, 5f);
            Lamp("Rim", new Vector3(0f, 2.2f, -1.4f), GenesisTheme.Purple, 5f, 5f);

            Shader unlit = Shader.Find("Universal Render Pipeline/Unlit");
            var ringMat = new Material(unlit != null ? unlit : Shader.Find("Unlit/Color")) { color = GenesisTheme.Cyan };
            var ring = new GameObject("Holo Ring").transform;
            ring.SetParent(_studio.transform, false);
            ring.localPosition = new Vector3(0f, 0.02f, 0f);
            for (int i = 0; i < 64; i++)
            {
                if (i % 8 == 7) continue;   // gaps make it read as a hologram
                var seg = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Destroy(seg.GetComponent<Collider>());
                seg.transform.SetParent(ring, false);
                float a = i * 360f / 64f;
                seg.transform.localRotation = Quaternion.Euler(0f, a, 0f);
                seg.transform.localPosition = Quaternion.Euler(0f, a, 0f) * Vector3.forward * 0.75f;
                seg.transform.localScale = new Vector3(0.07f, 0.01f, 0.025f);
                seg.GetComponent<Renderer>().sharedMaterial = ringMat;
            }
            ring.gameObject.AddComponent<DuelGenesis.Core.GenesisSpin>().degreesPerSecond = new Vector3(0f, 25f, 0f);
        }

        private static Texture2D Round() => _round != null ? _round : _round = RoundedTexture(new Color(1f, 1f, 1f, 1f), new Color(0.82f, 0.86f, 0.95f, 1f));
        private static Texture2D RoundBright() => _roundBright != null ? _roundBright : _roundBright = RoundedTexture(new Color(1f, 1f, 1f, 1f), new Color(1f, 1f, 1f, 1f));

        /// <summary>A white rounded rectangle (tinted by GUI.backgroundColor / GUI.color) with a soft top-to-bottom sheen.</summary>
        private static Texture2D RoundedTexture(Color top, Color bottom)
        {
            const int size = 48, radius = 12;
            var t = new Texture2D(size, size, TextureFormat.RGBA32, false) { hideFlags = HideFlags.DontSave, wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = Mathf.Max(0f, Mathf.Max(radius - x, x - (size - 1 - radius)));
                float dy = Mathf.Max(0f, Mathf.Max(radius - y, y - (size - 1 - radius)));
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.Clamp01(radius + 0.5f - d);
                Color c = Color.Lerp(bottom, top, y / (float)(size - 1));
                c.a = a;
                t.SetPixel(x, y, c);
            }
            t.Apply();
            return t;
        }

        private static Texture2D Vignette()
        {
            if (_vignette != null) return _vignette;
            const int size = 128;
            _vignette = new Texture2D(size, size, TextureFormat.RGBA32, false) { hideFlags = HideFlags.DontSave, wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = (x / (size - 1f) - 0.5f) * 2f, dy = (y / (size - 1f) - 0.45f) * 2f;
                float r = Mathf.Sqrt(dx * dx * 0.8f + dy * dy);
                _vignette.SetPixel(x, y, new Color(0.02f, 0.02f, 0.06f, Mathf.Clamp01((r - 0.55f) * 1.4f) * 0.85f));
            }
            _vignette.Apply();
            return _vignette;
        }

        private static Texture2D Logo() => _logo != null ? _logo : _logo = Resources.Load<Texture2D>("DuelGenesis/DuelGenesisLogo");

        private static GUIStyle NameField()
        {
            var s = new GUIStyle(GUI.skin.textField) { fontSize = 18, fontStyle = FontStyle.Bold, padding = new RectOffset(12, 12, 8, 8), border = new RectOffset(14, 14, 14, 14) };
            s.normal.background = s.focused.background = s.hover.background = Round();
            s.normal.textColor = s.focused.textColor = s.hover.textColor = Color.white;
            return s;
        }
    }
}
