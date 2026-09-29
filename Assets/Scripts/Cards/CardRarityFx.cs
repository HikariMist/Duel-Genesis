using UnityEngine;

namespace DuelGenesis.Cards
{
    /// <summary>
    /// Foil treatment for rare cards drawn with IMGUI (pack reveals and anywhere else that uses
    /// <see cref="ProductionCardVisualDrawer.DrawCard"/>):
    ///   Super Rare  - a silver holo shine that sweeps across the card,
    ///   Ultra Rare  - a gold shine plus twinkling sparkles,
    ///   Secret Rare - a rainbow shine in two layers plus more sparkles.
    /// Also the reveal burst (rays, ring and a "SUPER RARE!" call-out) when one is flipped in a pack.
    /// </summary>
    public static class CardRarityFx
    {
        private static Texture2D _band, _spark, _white;

        public static bool HasFoil(CardRarity rarity) => rarity >= CardRarity.SuperRare;

        /// <summary>The shine colour; Secret Rare cycles through the rainbow.</summary>
        public static Color Tint(CardRarity rarity, float time) => rarity switch
        {
            CardRarity.SuperRare => new Color(0.82f, 0.9f, 1f, 1f),
            CardRarity.UltraRare => new Color(1f, 0.84f, 0.38f, 1f),
            CardRarity.SecretRare => Color.HSVToRGB(Mathf.Repeat(time * 0.22f, 1f), 0.6f, 1f),
            _ => Color.white
        };

        public static string CallOut(CardRarity rarity) => rarity switch
        {
            CardRarity.SuperRare => "SUPER RARE!",
            CardRarity.UltraRare => "ULTRA RARE!!",
            CardRarity.SecretRare => "SECRET RARE!!!",
            _ => ""
        };

        /// <summary>Draws the moving foil over a card already drawn in <paramref name="cardRect"/>.</summary>
        public static void DrawFoil(Rect cardRect, CardRarity rarity, int seed)
        {
            if (!HasFoil(rarity) || Event.current.type != EventType.Repaint) return;
            float t = Time.unscaledTime + seed * 0.37f;
            Color old = GUI.color;

            Color c = Tint(rarity, t);
            float strength = rarity == CardRarity.SuperRare ? 0.34f : rarity == CardRarity.UltraRare ? 0.42f : 0.5f;
            float sweep = Mathf.Repeat(t * 0.32f, 1f);
            GUI.color = new Color(c.r, c.g, c.b, strength);
            GUI.DrawTextureWithTexCoords(cardRect, Band(), new Rect(-sweep, 0f, 1f, 1f), true);

            if (rarity == CardRarity.SecretRare)
            {
                Color c2 = Color.HSVToRGB(Mathf.Repeat(t * 0.22f + 0.5f, 1f), 0.65f, 1f);
                GUI.color = new Color(c2.r, c2.g, c2.b, 0.3f);
                GUI.DrawTextureWithTexCoords(cardRect, Band(), new Rect(sweep * 1.6f, 0.35f, 0.7f, 0.7f), true);
            }

            if (rarity >= CardRarity.UltraRare)
            {
                int n = rarity == CardRarity.SecretRare ? 8 : 5;
                for (int i = 0; i < n; i++)
                {
                    float px = Hash(seed * 31 + i * 7), py = Hash(seed * 17 + i * 13 + 5);
                    float twinkle = Mathf.Max(0f, Mathf.Sin(t * 2.6f + i * 2.1f));
                    float size = cardRect.width * (0.07f + 0.08f * twinkle);
                    var r = new Rect(cardRect.x + px * cardRect.width - size * 0.5f, cardRect.y + py * cardRect.height - size * 0.5f, size, size);
                    GUI.color = new Color(1f, 1f, 1f, twinkle * 0.9f);
                    GUI.DrawTexture(r, Spark(), ScaleMode.StretchToFill, true);
                }
            }
            GUI.color = old;
        }

        /// <summary>
        /// The moment a Super Rare or better is flipped: light rays, an expanding ring and the call-out.
        /// <paramref name="age"/> is seconds since the flip; nothing is drawn after 1.4 s.
        /// </summary>
        public static void DrawRevealBurst(Rect cardRect, CardRarity rarity, float age)
        {
            if (!HasFoil(rarity) || age > 1.4f || Event.current.type != EventType.Repaint) return;
            float t = Time.unscaledTime;
            Color c = Tint(rarity, t);
            float fade = 1f - age / 1.4f;
            Color old = GUI.color;
            Matrix4x4 oldMatrix = GUI.matrix;
            Vector2 centre = cardRect.center;

            // Rays spinning out from behind the card.
            int rays = rarity == CardRarity.SecretRare ? 16 : rarity == CardRarity.UltraRare ? 12 : 8;
            float length = cardRect.height * (0.55f + age * 0.9f);
            for (int i = 0; i < rays; i++)
            {
                GUI.matrix = oldMatrix;
                GUIUtility.RotateAroundPivot(i * 360f / rays + t * 40f, centre);
                GUI.color = new Color(c.r, c.g, c.b, 0.55f * fade);
                GUI.DrawTexture(new Rect(centre.x - 3f, centre.y - length, 6f, length), White());
            }
            GUI.matrix = oldMatrix;

            // Expanding ring.
            float grow = 10f + age * 70f;
            GUI.color = new Color(c.r, c.g, c.b, 0.45f * fade);
            var ring = new Rect(cardRect.x - grow, cardRect.y - grow, cardRect.width + grow * 2f, cardRect.height + grow * 2f);
            DrawFrame(ring, 5f);
            GUI.color = old;
        }

        /// <summary>The call-out above the card ("SUPER RARE!"), popping in then settling.</summary>
        public static void DrawCallOut(Rect cardRect, CardRarity rarity, float age)
        {
            if (!HasFoil(rarity) || age > 2.2f) return;
            float pop = age < 0.25f ? Mathf.Lerp(0.4f, 1.25f, age / 0.25f) : Mathf.Lerp(1.25f, 1f, Mathf.Clamp01((age - 0.25f) / 0.3f));
            float alpha = age < 1.7f ? 1f : 1f - (age - 1.7f) / 0.5f;
            Color c = Tint(rarity, Time.unscaledTime);
            var style = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.RoundToInt(22f * pop),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(c.r, c.g, c.b, alpha) }
            };
            var shadow = new GUIStyle(style) { normal = { textColor = new Color(0f, 0f, 0f, 0.8f * alpha) } };
            var r = new Rect(cardRect.x - 60f, cardRect.y - 40f, cardRect.width + 120f, 34f);
            GUI.Label(new Rect(r.x + 2f, r.y + 2f, r.width, r.height), CallOut(rarity), shadow);
            GUI.Label(r, CallOut(rarity), style);
        }

        private static void DrawFrame(Rect r, float thickness)
        {
            GUI.DrawTexture(new Rect(r.x, r.y, r.width, thickness), White());
            GUI.DrawTexture(new Rect(r.x, r.yMax - thickness, r.width, thickness), White());
            GUI.DrawTexture(new Rect(r.x, r.y, thickness, r.height), White());
            GUI.DrawTexture(new Rect(r.xMax - thickness, r.y, thickness, r.height), White());
        }

        private static float Hash(int n)
        {
            n = (n << 13) ^ n;
            return ((n * (n * n * 15731 + 789221) + 1376312589) & 0x7fffffff) / 2147483647f;
        }

        private static Texture2D White()
        {
            if (_white != null) return _white;
            _white = new Texture2D(1, 1) { hideFlags = HideFlags.DontSave };
            _white.SetPixel(0, 0, Color.white);
            _white.Apply();
            return _white;
        }

        /// <summary>A diagonal light band (plus a fainter echo), tiling so it can sweep.</summary>
        private static Texture2D Band()
        {
            if (_band != null) return _band;
            const int size = 128;
            _band = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat, hideFlags = HideFlags.DontSave };
            var px = new Color[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Mathf.Repeat((x + y * 0.6f) / size, 1f);
                float main = Mathf.Exp(-Mathf.Pow((d - 0.5f) / 0.06f, 2f));
                float echo = 0.45f * Mathf.Exp(-Mathf.Pow((d - 0.66f) / 0.025f, 2f));
                px[y * size + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(main + echo));
            }
            _band.SetPixels(px);
            _band.Apply();
            return _band;
        }

        /// <summary>A soft four-point star.</summary>
        private static Texture2D Spark()
        {
            if (_spark != null) return _spark;
            const int size = 32;
            _spark = new Texture2D(size, size, TextureFormat.RGBA32, false) { hideFlags = HideFlags.DontSave };
            var px = new Color[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float fx = (x + 0.5f) / size - 0.5f, fy = (y + 0.5f) / size - 0.5f;
                float r = Mathf.Sqrt(fx * fx + fy * fy);
                float cross = 0.012f / (Mathf.Abs(fx * fy) + 0.012f);
                float a = Mathf.Clamp01(cross * (1f - r * 2f)) + Mathf.Clamp01(1f - r * 7f);
                px[y * size + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(a));
            }
            _spark.SetPixels(px);
            _spark.Apply();
            return _spark;
        }
    }
}
