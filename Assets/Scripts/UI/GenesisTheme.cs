using DuelGenesis.Cards;
using UnityEngine;

namespace DuelGenesis.UI
{
    public static class GenesisTheme
    {
        public static readonly Color Background = new Color(0.035f, 0.045f, 0.075f, 0.96f);
        public static readonly Color Panel = new Color(0.07f, 0.09f, 0.14f, 0.96f);
        public static readonly Color PanelAlt = new Color(0.10f, 0.08f, 0.16f, 0.96f);
        public static readonly Color Cyan = new Color(0.10f, 0.86f, 1.00f, 1f);
        public static readonly Color Purple = new Color(0.72f, 0.28f, 1.00f, 1f);
        public static readonly Color Magenta = new Color(1.00f, 0.20f, 0.65f, 1f);
        public static readonly Color Green = new Color(0.22f, 1.00f, 0.55f, 1f);
        public static readonly Color Gold = new Color(1.00f, 0.76f, 0.20f, 1f);
        public static readonly Color Danger = new Color(1.00f, 0.30f, 0.30f, 1f);
        public static readonly Color Muted = new Color(0.58f, 0.66f, 0.76f, 1f);

        public static void Box(Rect rect, Color color)
        {
            Color old = GUI.backgroundColor;
            GUI.backgroundColor = color;
            GUI.Box(rect, string.Empty);
            GUI.backgroundColor = old;
        }

        public static bool Button(Rect rect, string label, Color color)
        {
            Color old = GUI.backgroundColor;
            GUI.backgroundColor = color;
            bool pressed = GUI.Button(rect, label);
            GUI.backgroundColor = old;
            return pressed;
        }

        public static Color CardColor(CardData card)
        {
            if (card == null) return Panel;

            Color baseColor = card.kind switch
            {
                CardKind.Monster => new Color(0.20f, 0.28f, 0.38f, 1f),
                CardKind.Spell => new Color(0.10f, 0.40f, 0.36f, 1f),
                CardKind.Trap => new Color(0.42f, 0.16f, 0.42f, 1f),
                _ => Panel
            };

            float boost = card.rarity switch
            {
                CardRarity.Rare => 0.08f,
                CardRarity.SuperRare => 0.16f,
                CardRarity.UltraRare => 0.26f,
                _ => 0f
            };

            return new Color(
                Mathf.Clamp01(baseColor.r + boost),
                Mathf.Clamp01(baseColor.g + boost),
                Mathf.Clamp01(baseColor.b + boost),
                1f);
        }

        public static Color RarityColor(CardRarity rarity)
        {
            return rarity switch
            {
                CardRarity.Common => new Color(0.82f, 0.86f, 0.90f, 1f),
                CardRarity.Rare => Cyan,
                CardRarity.SuperRare => Purple,
                CardRarity.UltraRare => Gold,
                CardRarity.SecretRare => Color.HSVToRGB(Mathf.Repeat(Time.unscaledTime * 0.22f, 1f), 0.6f, 1f),
                _ => Color.white
            };
        }
    }
}
