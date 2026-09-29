using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace DuelGenesis.Dueling
{
    /// <summary>Tiny helpers for building uGUI in code with a consistent Duel Genesis style.</summary>
    internal static class UiKit
    {
        public static readonly Color PanelColor = new Color(0.03f, 0.05f, 0.10f, 0.86f);
        public static readonly Color PanelBorder = new Color(0.35f, 0.55f, 0.95f, 0.35f);
        public static readonly Color TextColor = new Color(0.92f, 0.95f, 1f);
        public static readonly Color MutedText = new Color(0.62f, 0.68f, 0.80f);

        private static Font _font;
        public static Font Font => _font != null ? _font : _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        public static void EnsureEventSystem()
        {
            EventSystem current = EventSystem.current;
            if (current != null && current.isActiveAndEnabled && current.GetComponent<BaseInputModule>() != null) return;
            EventSystem existing = UnityEngine.Object.FindAnyObjectByType<EventSystem>();
            if (existing != null && existing.isActiveAndEnabled && existing.GetComponent<BaseInputModule>() != null) return;
            GameObject go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
            InputSystemUIInputModule module = go.AddComponent<InputSystemUIInputModule>();
            module.AssignDefaultActions();
        }

        public static RectTransform Rect(Transform parent, string name)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        /// <summary>Anchor at a normalized point with a pivot, positioned in pixels (1920x1080 reference).</summary>
        public static RectTransform Place(RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
            return rt;
        }

        public static RectTransform Stretch(RectTransform rt, float left = 0f, float bottom = 0f, float right = 0f, float top = 0f)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
            return rt;
        }

        public static Image Panel(Transform parent, string name, Color color, bool border = true)
        {
            RectTransform rt = Rect(parent, name);
            Image image = rt.gameObject.AddComponent<Image>();
            image.sprite = DuelVisualResources.RoundedSprite;
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = 1.6f;
            image.color = color;
            if (border)
            {
                Image outline = Rect(rt, "Border").gameObject.AddComponent<Image>();
                outline.sprite = DuelVisualResources.RoundedOutlineSprite;
                outline.type = Image.Type.Sliced;
                outline.pixelsPerUnitMultiplier = 1.6f;
                outline.color = PanelBorder;
                outline.raycastTarget = false;
                Stretch(outline.rectTransform);
            }
            return image;
        }

        public static Image Fill(Transform parent, string name, Color color)
        {
            Image image = Rect(parent, name).gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        public static Text Label(Transform parent, string name, string text, int size, Color color,
            TextAnchor align = TextAnchor.MiddleLeft, FontStyle style = FontStyle.Normal)
        {
            Text label = Rect(parent, name).gameObject.AddComponent<Text>();
            label.font = Font;
            label.text = text;
            label.fontSize = size;
            label.color = color;
            label.alignment = align;
            label.fontStyle = style;
            label.raycastTarget = false;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            label.supportRichText = true;
            return label;
        }

        public static Shadow Glow(Graphic graphic, Color color, float distance = 2f)
        {
            Shadow shadow = graphic.gameObject.AddComponent<Shadow>();
            shadow.effectColor = color;
            shadow.effectDistance = new Vector2(distance, -distance);
            return shadow;
        }

        public static Button Button(Transform parent, string name, string label, Color accent, Action onClick, int fontSize = 20)
        {
            Image background = Panel(parent, name, new Color(accent.r * 0.25f, accent.g * 0.25f, accent.b * 0.35f, 0.95f));
            Button button = background.gameObject.AddComponent<Button>();
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.35f, 1.35f, 1.5f);
            colors.pressedColor = new Color(0.8f, 0.8f, 0.9f);
            colors.disabledColor = new Color(0.45f, 0.45f, 0.5f, 0.6f);
            colors.colorMultiplier = 1.4f;
            colors.fadeDuration = 0.08f;
            button.colors = colors;
            button.targetGraphic = background;
            if (onClick != null) button.onClick.AddListener(() => onClick());

            Transform border = background.transform.Find("Border");
            if (border != null) border.GetComponent<Image>().color = new Color(accent.r, accent.g, accent.b, 0.8f);

            Text text = Label(background.transform, "Label", label, fontSize, TextColor, TextAnchor.MiddleCenter, FontStyle.Bold);
            Stretch(text.rectTransform, 8f, 2f, 8f, 2f);
            return button;
        }

        public static void SetButtonLabel(Button button, string label)
        {
            Text text = button.GetComponentInChildren<Text>();
            if (text != null) text.text = label;
        }
    }
}
