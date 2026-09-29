#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DuelGenesis.Cards;
using UnityEditor;
using UnityEngine;

namespace DuelGenesis.EditorTools
{
    /// <summary>
    /// Renders a contact sheet of finished card faces (every frame type, rarities, long names and
    /// long effect text) so card layout can be checked without entering Play Mode.
    /// Output: Builds/CardFacePreview.png
    /// </summary>
    public static class CardFacePreviewTool
    {
        private const string OutputPath = "Builds/CardFacePreview.png";

        [MenuItem("Duel Genesis/Cards/Render Card Face Preview Sheet")]
        public static void RenderSheet()
        {
            List<CardData> catalog = LoadCatalog();
            if (catalog.Count == 0)
            {
                EditorUtility.DisplayDialog("Card Face Preview", "Card catalog not found in StreamingAssets.", "OK");
                return;
            }

            List<CardData> samples = PickSamples(catalog);
            const int columns = 6;
            int rows = Mathf.CeilToInt((samples.Count + 1) / (float)columns);
            int w = CardFaceCompositor.FaceWidth;
            int h = CardFaceCompositor.FaceHeight;
            const int pad = 12;

            Texture2D sheet = new Texture2D(columns * (w + pad) + pad, rows * (h + pad) + pad, TextureFormat.RGBA32, false);
            Color32[] fill = Enumerable.Repeat(new Color32(40, 42, 48, 255), sheet.width * sheet.height).ToArray();
            sheet.SetPixels32(fill);

            try
            {
                for (int i = 0; i <= samples.Count; i++)
                {
                    EditorUtility.DisplayProgressBar("Card Face Preview", i < samples.Count ? samples[i].cardName : "Card back", i / (float)(samples.Count + 1));
                    Texture2D face = i < samples.Count
                        ? CardFaceCompositor.RenderPreview(samples[i])
                        : CardFaceCompositor.RenderPreview(null);
                    if (face == null) continue;

                    int col = i % columns;
                    int row = rows - 1 - i / columns;
                    sheet.SetPixels(pad + col * (w + pad), pad + row * (h + pad), w, h, face.GetPixels());
                    Object.DestroyImmediate(face);
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                CardFaceCompositor.Shutdown();
            }

            sheet.Apply();
            Directory.CreateDirectory(Path.GetDirectoryName(OutputPath) ?? "Builds");
            File.WriteAllBytes(OutputPath, sheet.EncodeToPNG());
            Object.DestroyImmediate(sheet);
            Debug.Log($"Duel: Genesis card face preview written to {Path.GetFullPath(OutputPath)} ({samples.Count} faces + back).");
            EditorUtility.RevealInFinder(OutputPath);
        }

        private static List<CardData> LoadCatalog()
        {
            string path = Path.Combine(Application.streamingAssetsPath, "duel_genesis_cards.json");
            if (!File.Exists(path)) return new List<CardData>();
            ExternalCardCatalog catalog = JsonUtility.FromJson<ExternalCardCatalog>(File.ReadAllText(path));
            return catalog?.cards?.Select(record => record.ToCardData()).ToList() ?? new List<CardData>();
        }

        private static List<CardData> PickSamples(List<CardData> catalog)
        {
            string[] named =
            {
                "Dark Magician Knight", "Buster Blader", "Dark Paladin", "Giant Rat", "Pot of Greed",
                "Mirror Force", "Heavy Storm", "Magic Cylinder", "Black Luster Soldier - Envoy of the Beginning",
                "Mystic Plasma Zone", "Axe of Despair", "Solemn Judgment", "Swords of Revealing Light",
                "Call of the Haunted", "Gyakutenno Megami", "Five-Headed Dragon"
            };

            var picks = new List<CardData>();
            foreach (string name in named)
            {
                CardData card = catalog.FirstOrDefault(c => c.cardName == name);
                if (card != null) picks.Add(card);
            }

            // Make sure every frame type and rarity appears at least once.
            foreach (CardFrameKind kind in System.Enum.GetValues(typeof(CardFrameKind)))
            {
                if (picks.Any(c => c.ResolvedFrameKind == kind)) continue;
                CardData card = catalog.FirstOrDefault(c => c.ResolvedFrameKind == kind);
                if (card != null) picks.Add(card);
            }

            CardData longest = catalog.OrderByDescending(c => (c.effectText ?? string.Empty).Length).First();
            if (!picks.Contains(longest)) picks.Add(longest);

            return picks.Take(23).ToList();
        }
    }
}
#endif
