using System;
using System.Collections.Generic;
using System.Reflection;
using DuelGenesis.UI;
using UnityEngine;

namespace DuelGenesis.Characters
{
    /// <summary>
    /// Additive fit/dye controls for the existing character creator. This intentionally does not replace
    /// GenesisCharacterCreator: it edits the creator's live GenesisAppearance so Cancel/Save and the existing
    /// builder/persistence flow continue to work exactly as before.
    /// </summary>
    public sealed class GenesisOutfitFitDyePanel : MonoBehaviour
    {
        private static readonly FieldInfo LookField = typeof(GenesisCharacterCreator).GetField("_look", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly MethodInfo RebuildSoonMethod = typeof(GenesisCharacterCreator).GetMethod("RebuildSoon", BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly Color[] DyePresets =
        {
            new Color(0.04f, 0.04f, 0.05f, 1f),
            new Color(0.28f, 0.13f, 0.06f, 1f),
            new Color(0.78f, 0.58f, 0.30f, 1f),
            new Color(0.92f, 0.93f, 0.96f, 1f),
            new Color(0.82f, 0.08f, 0.10f, 1f),
            new Color(0.08f, 0.28f, 0.88f, 1f),
            new Color(0.06f, 0.68f, 0.76f, 1f),
            new Color(0.48f, 0.16f, 0.78f, 1f),
            new Color(0.12f, 0.62f, 0.20f, 1f),
            new Color(0.95f, 0.36f, 0.68f, 1f),
            new Color(0.92f, 0.66f, 0.12f, 1f),
            new Color(0.42f, 0.46f, 0.54f, 1f)
        };

        private GenesisCharacterCreator _creator;
        private int _selected;
        private bool _expanded = true;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (FindAnyObjectByType<GenesisOutfitFitDyePanel>() != null) return;
            var go = new GameObject("Genesis Outfit Fit + Dye Panel");
            DontDestroyOnLoad(go);
            go.AddComponent<GenesisOutfitFitDyePanel>();
        }

        private void Update()
        {
            if (_creator == null) _creator = FindAnyObjectByType<GenesisCharacterCreator>();
        }

        private void OnGUI()
        {
            if (_creator == null || !_creator.IsOpen || LookField == null) return;
            GenesisAppearance look = LookField.GetValue(_creator) as GenesisAppearance;
            if (look == null) return;

            List<GenesisEquip> worn = WornItems(look);
            if (worn.Count == 0) return;
            _selected = Mathf.Clamp(_selected, 0, worn.Count - 1);

            Matrix4x4 oldMatrix = GUI.matrix;
            float scale = Mathf.Max(0.45f, Mathf.Min(Screen.height / 1080f, Screen.width / 1920f));
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scale, scale, 1f));

            Rect collapsed = new Rect(24f, 112f, 196f, 38f);
            Color oldBg = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.26f, 0.20f, 0.62f, 1f);
            if (GUI.Button(collapsed, _expanded ? "FIT + DYE  ◀" : "FIT + DYE  ▶", ButtonStyle(13))) _expanded = !_expanded;
            GUI.backgroundColor = oldBg;

            if (_expanded)
            {
                Rect panel = new Rect(24f, 156f, 352f, 462f);
                GenesisTheme.Box(panel, new Color(0.025f, 0.035f, 0.075f, 0.96f));
                GenesisTheme.Box(new Rect(panel.x, panel.y, panel.width, 3f), GenesisTheme.Purple);
                GenesisTheme.Box(new Rect(panel.x, panel.y, 3f, panel.height), GenesisTheme.Cyan);

                GUILayout.BeginArea(new Rect(panel.x + 14f, panel.y + 12f, panel.width - 28f, panel.height - 24f));
                GUILayout.Label("OUTFIT FIT + DYE", LabelStyle(17, FontStyle.Bold, Color.white));
                GUILayout.Label("Resize and recolor the selected worn piece. Changes update the live preview and save with your character.", LabelStyle(10, FontStyle.Normal, GenesisTheme.Muted));
                GUILayout.Space(9f);

                GUILayout.BeginHorizontal();
                if (GUILayout.Button("◀", ButtonStyle(14), GUILayout.Width(38f), GUILayout.Height(34f))) _selected = (_selected - 1 + worn.Count) % worn.Count;
                GenesisEquip equip = worn[_selected];
                GUILayout.Label(equip.slot.ToString().ToUpperInvariant(), LabelStyle(14, FontStyle.Bold, GenesisTheme.Gold), GUILayout.Height(34f), GUILayout.ExpandWidth(true));
                if (GUILayout.Button("▶", ButtonStyle(14), GUILayout.Width(38f), GUILayout.Height(34f))) _selected = (_selected + 1) % worn.Count;
                GUILayout.EndHorizontal();

                // Re-read after navigation so this frame edits the newly selected piece.
                equip = worn[_selected];
                GUILayout.Space(8f);
                int percent = Mathf.RoundToInt(Mathf.Lerp(70f, 130f, Mathf.InverseLerp(-100f, 100f, equip.proportion)));
                GUILayout.BeginHorizontal();
                GUILayout.Label("FIT / SIZE", LabelStyle(12, FontStyle.Bold, GenesisTheme.Cyan));
                GUILayout.FlexibleSpace();
                GUILayout.Label(percent + "%", LabelStyle(12, FontStyle.Bold, Color.white), GUILayout.Width(52f));
                GUILayout.EndHorizontal();

                float newScale = GUILayout.HorizontalSlider(equip.proportion, -100f, 100f, GUILayout.Height(22f));
                if (!Mathf.Approximately(newScale, equip.proportion))
                {
                    equip.proportion = newScale;
                    RequestRebuild();
                }
                GUILayout.BeginHorizontal();
                GUILayout.Label("70%", LabelStyle(9, FontStyle.Bold, GenesisTheme.Muted));
                GUILayout.FlexibleSpace();
                GUILayout.Label("130%", LabelStyle(9, FontStyle.Bold, GenesisTheme.Muted));
                GUILayout.EndHorizontal();

                EnsureThreeColours(equip);
                string[] names = { "PRIMARY", "SECONDARY", "ACCENT" };
                for (int channel = 0; channel < 3; channel++)
                {
                    GUILayout.Space(8f);
                    GUILayout.BeginHorizontal();
                    GUILayout.Label(names[channel], LabelStyle(11, FontStyle.Bold, Color.white), GUILayout.Width(86f));
                    Rect swatch = GUILayoutUtility.GetRect(26f, 20f, GUILayout.Width(26f), GUILayout.Height(20f));
                    if (Event.current.type == EventType.Repaint)
                    {
                        Color was = GUI.color;
                        GUI.color = equip.colors[channel];
                        GUI.DrawTexture(swatch, Texture2D.whiteTexture);
                        GUI.color = was;
                    }
                    GUILayout.EndHorizontal();
                    DrawPalette(equip, channel);
                }

                GUILayout.FlexibleSpace();
                GUI.backgroundColor = new Color(0.18f, 0.22f, 0.34f, 1f);
                if (GUILayout.Button("RESET THIS PIECE", ButtonStyle(11), GUILayout.Height(30f)))
                {
                    equip.proportion = 0f;
                    equip.colors = new[] { Color.white, Color.white, Color.white };
                    RequestRebuild();
                }
                GUI.backgroundColor = oldBg;
                GUILayout.EndArea();
            }

            GUI.matrix = oldMatrix;
        }

        private void DrawPalette(GenesisEquip equip, int channel)
        {
            const int perRow = 6;
            for (int start = 0; start < DyePresets.Length; start += perRow)
            {
                GUILayout.BeginHorizontal();
                for (int i = start; i < Mathf.Min(start + perRow, DyePresets.Length); i++)
                {
                    Color c = DyePresets[i];
                    Color old = GUI.backgroundColor;
                    GUI.backgroundColor = c;
                    if (GUILayout.Button(GUIContent.none, ButtonStyle(10), GUILayout.Width(42f), GUILayout.Height(24f)))
                    {
                        equip.colors[channel] = c;
                        RequestRebuild();
                    }
                    GUI.backgroundColor = old;
                }
                GUILayout.EndHorizontal();
            }
        }

        private static List<GenesisEquip> WornItems(GenesisAppearance look)
        {
            var result = new List<GenesisEquip>();
            if (look.equipment == null) return result;
            foreach (GenesisEquip equip in look.equipment)
                if (equip != null && equip.id >= 0) result.Add(equip);
            return result;
        }

        private static void EnsureThreeColours(GenesisEquip equip)
        {
            if (equip.colors != null && equip.colors.Length >= 3) return;
            Color[] old = equip.colors ?? Array.Empty<Color>();
            var next = new[] { Color.white, Color.white, Color.white };
            for (int i = 0; i < old.Length && i < next.Length; i++) next[i] = old[i];
            equip.colors = next;
        }

        private void RequestRebuild()
        {
            if (_creator == null) return;
            if (RebuildSoonMethod != null) RebuildSoonMethod.Invoke(_creator, null);
        }

        private static GUIStyle LabelStyle(int size, FontStyle fontStyle, Color color) => new GUIStyle(GUI.skin.label)
        {
            fontSize = size,
            fontStyle = fontStyle,
            wordWrap = true,
            normal = { textColor = color }
        };

        private static GUIStyle ButtonStyle(int size) => new GUIStyle(GUI.skin.button)
        {
            fontSize = size,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            padding = new RectOffset(6, 6, 4, 4),
            normal = { textColor = Color.white },
            hover = { textColor = Color.white },
            active = { textColor = Color.white }
        };
    }
}
