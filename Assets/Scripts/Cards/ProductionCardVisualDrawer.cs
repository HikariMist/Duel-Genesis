using UnityEngine;

namespace DuelGenesis.Cards
{
    /// <summary>
    /// IMGUI helper that draws a finished card face produced by <see cref="CardFaceCompositor"/>.
    /// The face is rendered once (outside the GUI pass) and then drawn at the real
    /// 59 x 86 mm card aspect ratio, letterboxed inside whatever rect the caller supplies.
    /// </summary>
    public static class ProductionCardVisualDrawer
    {
        public const float CardAspect = 59f / 86f;

        public static void DrawCard(Rect rect, CardData card)
        {
            if (card == null)
                return;

            Rect cardRect = FitCard(rect);
            if (CardFaceCompositor.TryGetFace(card, out Texture2D face))
            {
                GUI.DrawTexture(cardRect, face, ScaleMode.StretchToFill, true);
                return;
            }

            // Face is being composed this frame: show the correct frame colour as a placeholder.
            Texture2D frame = ProductionCardArtRegistry.LoadFrame(card.ResolvedFrameKind);
            if (frame != null)
                GUI.DrawTextureWithTexCoords(cardRect, frame, new Rect(15f / 400f, 13f / 580f, 370f / 400f, 555f / 580f), true);
            else
                GUI.Box(cardRect, card.cardName);
        }

        public static void DrawCardBack(Rect rect)
        {
            // Rendering is not allowed inside the GUI pass, so only use an already-built back here.
            Texture2D back = CardFaceCompositor.CachedCardBack ?? ProductionCardArtRegistry.LoadFrameTexture("Blank Playing Card.png");
            Rect cardRect = FitCard(rect);
            if (back != null)
                GUI.DrawTexture(cardRect, back, ScaleMode.StretchToFill, true);
            else
                GUI.Box(cardRect, GUIContent.none);
        }

        /// <summary>Largest rect with real card proportions that fits inside <paramref name="rect"/>.</summary>
        public static Rect FitCard(Rect rect)
        {
            float width = rect.width;
            float height = width / CardAspect;
            if (height > rect.height)
            {
                height = rect.height;
                width = height * CardAspect;
            }
            return new Rect(rect.x + (rect.width - width) * 0.5f, rect.y + (rect.height - height) * 0.5f, width, height);
        }
    }
}
