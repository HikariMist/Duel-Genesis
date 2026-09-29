using System.Collections.Generic;
using UnityEngine;

namespace DuelGenesis.UI
{
    /// <summary>
    /// Kenney "UI Pack - Sci-Fi" (CC0) pieces for IMGUI screens, loaded from Resources/DuelGenesis/UI/SciFi
    /// (Duel Genesis > Downloads). Files are named Colour_piece (Blue_, Green_, Grey_, Red_, Yellow_, Extra_).
    /// Every call falls back to nothing when the pack isn't downloaded, so screens keep their plain look.
    /// </summary>
    public static class GenesisSciFiSkin
    {
        private static readonly Dictionary<string, Texture2D> Textures = new Dictionary<string, Texture2D>();
        private static readonly Dictionary<string, GUIStyle> Panels = new Dictionary<string, GUIStyle>();

        public static bool Ready => Get("Extra_button_rectangle_depth") != null;

        public static Texture2D Get(string name)
        {
            if (!Textures.TryGetValue(name, out Texture2D t) || t == null)
                Textures[name] = t = Resources.Load<Texture2D>("DuelGenesis/UI/SciFi/" + name);
            return t;
        }

        /// <summary>Draws a nine-sliced piece stretched over <paramref name="r"/>, tinted.</summary>
        public static bool Panel(Rect r, string name, Color tint, int border = 20)
        {
            if (Event.current.type != EventType.Repaint) return Get(name) != null;
            GUIStyle s = PanelStyle(name, border);
            if (s == null) return false;
            Color old = GUI.color;
            GUI.color = tint;
            s.Draw(r, false, false, false, false);
            GUI.color = old;
            return true;
        }

        private static GUIStyle PanelStyle(string name, int border)
        {
            string key = name + border;
            if (Panels.TryGetValue(key, out GUIStyle s)) return s;
            Texture2D t = Get(name);
            if (t == null) return null;
            s = new GUIStyle { border = new RectOffset(border, border, border, border), normal = { background = t } };
            Panels[key] = s;
            return s;
        }

        /// <summary>The pack's plain button (tint it with GUI.backgroundColor).</summary>
        public static GUIStyle Button(int fontSize = 14, Color? text = null) =>
            Make(Get("Extra_button_rectangle_depth"), Get("Extra_button_rectangle"), fontSize, text);

        /// <summary>A coloured header-blade tab (Blue, Green, Grey, Red, Yellow).</summary>
        public static GUIStyle Tab(string colour, int fontSize = 13, Color? text = null) =>
            Make(Get(colour + "_button_square_header_blade_rectangle"), null, fontSize, text);

        private static GUIStyle Make(Texture2D t, Texture2D pressed, int fontSize, Color? text)
        {
            if (t == null) return null;
            Texture2D flat = pressed ?? t;
            var s = new GUIStyle(GUI.skin.button)
            {
                fontSize = fontSize,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                border = new RectOffset(14, 14, 14, 18),
                padding = new RectOffset(10, 10, 4, 8),
            };
            s.normal.background = s.focused.background = t;
            s.hover.background = t;
            s.active.background = s.onActive.background = flat;   // pressed: loses its depth
            s.onNormal.background = s.onHover.background = t;
            Color c = text ?? Color.white;
            s.normal.textColor = s.hover.textColor = s.active.textColor = s.focused.textColor = c;
            s.onNormal.textColor = s.onHover.textColor = c;
            return s;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            Textures.Clear();
            Panels.Clear();
        }
    }
}
