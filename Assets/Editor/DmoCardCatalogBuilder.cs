#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using DuelGenesis.Cards;
using UnityEditor;
using UnityEngine;

namespace DuelGenesis.EditorTools
{
    public static class DmoCardCatalogBuilder
    {
        private const string ApiUrl = "https://db.ygoprodeck.com/api/v7/cardinfo.php";
        private static string ProjectRoot => Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        private static string FaceSource => Path.Combine(ProjectRoot, "Cards", "DMO_card_art", "cards");
        private static string OutputPath => Path.Combine(Application.streamingAssetsPath, ExternalCardCatalogLoader.CatalogFileName);

        [Serializable]
        private class ApiResponse
        {
            public List<ApiCard> data = new();
        }

        [Serializable]
        private class ApiCard
        {
            public long id;
            public string name;
            public string type;
            public string frameType;
            public string desc;
            public int atk;
            public int def;
            public int level;
            public string race;
            public string attribute;
        }

        [MenuItem("Duel Genesis/Production Assets/Build Real Card Catalog from DMO Art")]
        public static async void BuildRealCardCatalog()
        {
            if (!Directory.Exists(FaceSource))
            {
                EditorUtility.DisplayDialog(
                    "Duel: Genesis — Card Catalog",
                    "The DMO card-art folder was not found at Cards/DMO_card_art/cards.",
                    "OK");
                return;
            }

            if (!EditorUtility.DisplayDialog(
                    "Duel: Genesis — Build Real Card Catalog",
                    "This downloads current public card metadata once, matches it only against the card PNGs already in your DMO library, and writes Assets/StreamingAssets/duel_genesis_cards.json.\n\nThe supplied DMO PNG remains the authoritative visual, so its printed Normal / Effect / Fusion / Ritual / Synchro / Xyz / Spell / Trap frame is never replaced by a guessed frame.\n\nContinue?",
                    "Build Catalog",
                    "Cancel"))
                return;

            try
            {
                EditorUtility.DisplayProgressBar("Duel: Genesis", "Downloading card metadata...", 0.10f);
                string json = await DownloadCardDatabase();

                EditorUtility.DisplayProgressBar("Duel: Genesis", "Matching metadata to DMO card images...", 0.45f);
                ApiResponse response = JsonUtility.FromJson<ApiResponse>(json);
                if (response?.data == null || response.data.Count == 0)
                    throw new InvalidOperationException("The card metadata service returned no card records.");

                Dictionary<string, string> localFaces = Directory
                    .GetFiles(FaceSource, "*.png", SearchOption.TopDirectoryOnly)
                    .Select(path => Path.GetFileNameWithoutExtension(path))
                    .GroupBy(Normalize)
                    .Where(group => !string.IsNullOrWhiteSpace(group.Key))
                    .ToDictionary(group => group.Key, group => group.First());

                ExternalCardCatalog catalog = new ExternalCardCatalog();
                HashSet<string> matchedFaceKeys = new HashSet<string>();

                foreach (ApiCard apiCard in response.data)
                {
                    if (apiCard == null || string.IsNullOrWhiteSpace(apiCard.name))
                        continue;

                    string key = Normalize(apiCard.name);
                    if (!localFaces.TryGetValue(key, out string localName))
                        continue;

                    matchedFaceKeys.Add(key);
                    catalog.cards.Add(ToExternalRecord(apiCard, localName));
                }

                catalog.cards = catalog.cards
                    .OrderBy(card => card.cardName, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                EditorUtility.DisplayProgressBar("Duel: Genesis", "Writing production catalog...", 0.82f);
                Directory.CreateDirectory(Application.streamingAssetsPath);
                File.WriteAllText(OutputPath, JsonUtility.ToJson(catalog, true), new UTF8Encoding(false));

                string reportPath = Path.Combine(Application.streamingAssetsPath, "dmo_card_catalog_report.txt");
                List<string> unmatched = localFaces
                    .Where(pair => !matchedFaceKeys.Contains(pair.Key))
                    .Select(pair => pair.Value)
                    .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                string report = BuildReport(response.data.Count, localFaces.Count, catalog.cards.Count, unmatched);
                File.WriteAllText(reportPath, report, new UTF8Encoding(false));
                AssetDatabase.Refresh();

                Debug.Log(report);
                EditorUtility.DisplayDialog(
                    "Duel: Genesis — Real Card Catalog Ready",
                    $"Matched {catalog.cards.Count:N0} real card records to your supplied DMO card images.\n\n" +
                    $"Unmatched image names: {unmatched.Count:N0}\n" +
                    "A detailed report was saved beside the catalog.\n\n" +
                    "The complete card PNG is always used first, so card-frame appearance stays exactly as supplied.",
                    "OK");
            }
            catch (Exception exception)
            {
                Debug.LogError("Duel: Genesis real card catalog build failed: " + exception);
                EditorUtility.DisplayDialog(
                    "Duel: Genesis — Card Catalog Failed",
                    exception.Message,
                    "OK");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        private static async Task<string> DownloadCardDatabase()
        {
            using HttpClient client = new HttpClient();
            client.Timeout = TimeSpan.FromMinutes(2);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("Duel-Genesis-Development/0.7");
            return await client.GetStringAsync(ApiUrl);
        }

        private static ExternalCardRecord ToExternalRecord(ApiCard card, string localName)
        {
            string kind = ResolveKind(card.type);
            string frame = ResolveFrameKind(card.frameType, kind, card.type);
            string typeLine = BuildTypeLine(card);

            return new ExternalCardRecord
            {
                id = card.id > 0 ? card.id.ToString() : "DMO_" + Normalize(localName).ToUpperInvariant(),
                cardName = localName,
                kind = kind,
                frameKind = frame,
                rarity = "Common",
                attribute = card.attribute ?? string.Empty,
                typeLine = typeLine,
                level = Mathf.Max(0, card.level),
                attack = Mathf.Max(0, card.atk),
                defense = Mathf.Max(0, card.def),
                effectText = card.desc ?? string.Empty,
                modelResource = string.Empty
            };
        }

        private static string ResolveKind(string type)
        {
            if (!string.IsNullOrWhiteSpace(type) && type.IndexOf("Spell Card", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Spell";
            if (!string.IsNullOrWhiteSpace(type) && type.IndexOf("Trap Card", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Trap";
            return "Monster";
        }

        private static string ResolveFrameKind(string frameType, string kind, string cardType)
        {
            string frame = (frameType ?? string.Empty).Trim().ToLowerInvariant();
            if (frame.Contains("token")) return "Token";
            if (frame.Contains("fusion")) return "FusionMonster";
            if (frame.Contains("ritual")) return "RitualMonster";
            if (frame.Contains("synchro")) return "SynchroMonster";
            if (frame.Contains("xyz")) return "XyzMonster";
            if (frame == "normal" || frame.Contains("normal_pendulum")) return "NormalMonster";
            if (frame == "spell" || kind == "Spell") return "Spell";
            if (frame == "trap" || kind == "Trap") return "Trap";

            if (!string.IsNullOrWhiteSpace(cardType) &&
                cardType.IndexOf("Normal Monster", StringComparison.OrdinalIgnoreCase) >= 0)
                return "NormalMonster";

            return kind == "Monster" ? "EffectMonster" : kind;
        }

        private static string BuildTypeLine(ApiCard card)
        {
            if (ResolveKind(card.type) != "Monster")
                return string.IsNullOrWhiteSpace(card.race) ? (card.type ?? string.Empty) : card.race;

            string monsterType = (card.type ?? "Monster").Replace(" Monster", string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(card.race))
                return monsterType;
            if (string.IsNullOrWhiteSpace(monsterType))
                return card.race;
            return card.race + " / " + monsterType;
        }

        private static string BuildReport(int apiCount, int localCount, int matchedCount, List<string> unmatched)
        {
            StringBuilder builder = new StringBuilder();
            builder.AppendLine("DUEL: GENESIS — DMO CARD CATALOG REPORT");
            builder.AppendLine($"Metadata records downloaded: {apiCount:N0}");
            builder.AppendLine($"Local DMO card PNGs: {localCount:N0}");
            builder.AppendLine($"Matched production cards: {matchedCount:N0}");
            builder.AppendLine($"Unmatched local image names: {unmatched.Count:N0}");
            builder.AppendLine();
            builder.AppendLine("Frame rule: the complete supplied PNG is authoritative. frameKind is metadata/fallback only.");

            if (unmatched.Count > 0)
            {
                builder.AppendLine();
                builder.AppendLine("UNMATCHED IMAGE NAMES");
                foreach (string name in unmatched)
                    builder.AppendLine(name);
            }

            return builder.ToString();
        }

        private static string Normalize(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;
            StringBuilder builder = new StringBuilder(value.Length);
            foreach (char c in value)
            {
                if (char.IsLetterOrDigit(c))
                    builder.Append(char.ToLowerInvariant(c));
            }
            return builder.ToString();
        }
    }
}
#endif
