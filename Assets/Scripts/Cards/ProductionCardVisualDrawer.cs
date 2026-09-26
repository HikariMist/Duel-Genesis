using System.Text;
using UnityEngine;

namespace DuelGenesis.Cards
{
    /// <summary>
    /// Draws a readable Yu-Gi-Oh-style production card preview from the supplied DMO art,
    /// the metadata-selected frame, and the real card metadata.
    /// </summary>
    public static class ProductionCardVisualDrawer
    {
        public static void DrawCard(Rect rect, CardData card)
        {
            if (card == null)
                return;

            Texture2D source = ProductionCardArtRegistry.LoadFace(card.cardName);
            Texture2D frame = ProductionCardArtRegistry.LoadFrame(card.ResolvedFrameKind);

            bool sourceLooksLikeCompleteCard = source != null &&
                                               source.height > 0 &&
                                               (float)source.width / source.height < 0.82f;

            if (sourceLooksLikeCompleteCard)
            {
                GUI.DrawTexture(rect, source, ScaleMode.ScaleToFit, true);
            }
            else
            {
                if (frame != null)
                    GUI.DrawTexture(rect, frame, ScaleMode.StretchToFill, true);
                else
                    GUI.Box(rect, GUIContent.none);

                if (source != null)
                {
                    Rect artworkRect = new Rect(
                        rect.x + rect.width * 0.105f,
                        rect.y + rect.height * 0.225f,
                        rect.width * 0.79f,
                        rect.height * 0.445f);
                    GUI.DrawTexture(artworkRect, source, ScaleMode.ScaleAndCrop, true);
                }
            }

            DrawMetadata(rect, card);
        }

        private static void DrawMetadata(Rect rect, CardData card)
        {
            float scale = Mathf.Max(0.55f, rect.width / 120f);
            int nameSize = Mathf.Clamp(Mathf.RoundToInt(9f * scale), 7, 18);
            int levelSize = Mathf.Clamp(Mathf.RoundToInt(8f * scale), 7, 17);
            int statSize = Mathf.Clamp(Mathf.RoundToInt(7f * scale), 6, 15);

            Color titleColor = card.ResolvedFrameKind == CardFrameKind.XyzMonster ||
                               card.ResolvedFrameKind == CardFrameKind.Spell ||
                               card.ResolvedFrameKind == CardFrameKind.Trap
                ? Color.white
                : new Color(0.08f, 0.06f, 0.04f, 1f);

            GUIStyle nameStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = nameSize,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                clipping = TextClipping.Clip,
                normal = { textColor = titleColor }
            };

            GUIStyle levelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = levelSize,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleRight,
                clipping = TextClipping.Clip,
                normal = { textColor = new Color(1f, 0.72f, 0.08f, 1f) }
            };

            GUIStyle statStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = statSize,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleRight,
                clipping = TextClipping.Clip,
                normal = { textColor = new Color(0.08f, 0.06f, 0.04f, 1f) }
            };

            Rect nameRect = new Rect(
                rect.x + rect.width * 0.10f,
                rect.y + rect.height * 0.052f,
                rect.width * 0.80f,
                rect.height * 0.095f);

            // A light shadow makes the card name readable on both supplied full-card images and blank frames.
            GUIStyle shadowStyle = new GUIStyle(nameStyle);
            shadowStyle.normal.textColor = new Color(0f, 0f, 0f, 0.45f);
            GUI.Label(new Rect(nameRect.x + 1f, nameRect.y + 1f, nameRect.width, nameRect.height), card.cardName, shadowStyle);
            GUI.Label(nameRect, card.cardName, nameStyle);

            if (card.kind == CardKind.Monster && card.level > 0)
            {
                string stars = BuildStars(card.level);
                Rect levelRect = new Rect(
                    rect.x + rect.width * 0.12f,
                    rect.y + rect.height * 0.145f,
                    rect.width * 0.76f,
                    rect.height * 0.075f);
                GUI.Label(levelRect, stars, levelStyle);
            }

            if (card.kind == CardKind.Monster)
            {
                Rect statsRect = new Rect(
                    rect.x + rect.width * 0.28f,
                    rect.y + rect.height * 0.892f,
                    rect.width * 0.62f,
                    rect.height * 0.06f);
                GUI.Label(statsRect, $"ATK {card.attack}  DEF {card.defense}", statStyle);
            }
        }

        private static string BuildStars(int level)
        {
            int count = Mathf.Clamp(level, 1, 13);
            StringBuilder builder = new StringBuilder(count * 2);
            for (int i = 0; i < count; i++)
                builder.Append('★');
            return builder.ToString();
        }
    }
}
