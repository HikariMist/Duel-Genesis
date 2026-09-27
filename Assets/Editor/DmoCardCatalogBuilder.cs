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
        private const string ExcludedCardName = "Tricky Token";
        private static string ProjectRoot => Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        private static string FaceSource => Path.Combine(ProjectRoot, "Cards", "DMO_card_art", "cards");
        private static string OutputPath => Path.Combine(Application.streamingAssetsPath, ExternalCardCatalogLoader.CatalogFileName);

        [Serializable]
        private class ApiResponse
        {
            public List<ApiCard> data = new();
        }

        [Serializable]
        private class ApiCardSet
        {
            public string set_name;
            public string set_rarity;
            public string set_rarity_code;
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
            public List<ApiCardSet> card_sets = new();
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
                    "This downloads current public card metadata once, matches it only against the card PNGs already in your DMO library, imports rarity metadata, and writes Assets/StreamingAssets/duel_genesis_cards.json.\n\nExact names are required before normalized matching, Token cards cannot steal non-Token artwork, and Tricky Token is excluded completely.\n\nContinue?",
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

                List<string> localNames = Directory
                    .GetFiles(FaceSource, "*.png", SearchOption.TopDirectoryOnly)
                    .Select(Path.GetFileNameWithoutExtension)
                    .Where(name => !string.IsNullOrWhiteSpace(name))
                    .Where(name => !IsExcluded(name))
                    .ToList();

                Dictionary<string, string> exactFaces = localNames
                    .GroupBy(name => name, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

                Dictionary<string, List<string>> normalizedFaces = localNames
                    .GroupBy(Normalize)
                    .Where(group => !string.IsNullOrWhiteSpace(group.Key))
                    .ToDictionary(group => group.Key, group => group.ToList());

                ExternalCardCatalog catalog = new ExternalCardCatalog();
                HashSet<string> matchedLocalNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (ApiCard apiCard in response.data)
                {
                    if (apiCard == null || string.IsNullOrWhiteSpace(apiCard.name) || IsExcluded(apiCard.name))
                        continue;

                    string localName = ResolveLocalFace(apiCard, exactFaces, normalizedFaces);
                    if (string.IsNullOrWhiteSpace(localName))
                        continue;

                    matchedLocalNames.Add(localName);
                    catalog.cards.Add(ToExternalRecord(apiCard, localName));
                }

                catalog.cards = catalog.cards
                    .GroupBy(card => card.id)
                    .Select(group => group.First())
                    .OrderBy(card => card.cardName, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                EditorUtility.DisplayProgressBar("Duel: Genesis", "Writing production catalog...", 0.82f);
                Directory.CreateDirectory(Application.streamingAssetsPath);
                File.WriteAllText(OutputPath, JsonUtility.ToJson(catalog, true), new UTF8Encoding(false));

                string reportPath = Path.Combine(Application.streamingAssetsPath, "dmo_card_catalog_report.txt");
                List<string> unmatched = localNames
                    .Where(name => !matchedLocalNames.Contains(name))
                    .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                string report = BuildReport(response.data.Count, localNames.Count, catalog.cards.Count, unmatched);
                File.WriteAllText(reportPath, report, new UTF8Encoding(false));
                AssetDatabase.Refresh();

                Debug.Log(report);
                EditorUtility.DisplayDialog(
                    "Duel: Genesis — Real Card Catalog Ready",
                    $"Matched {catalog.cards.Count:N0} real card records to your supplied DMO card images.\n\n" +
                    $"Unmatched image names: {unmatched.Count:N0}\n" +
                    "Tricky Token is excluded. Rarity metadata is now included.\n" +
                    "A detailed report was saved beside the catalog.",
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
            client.DefaultRequestHeaders.UserAgent.ParseAdd("Duel-Genesis-Development/0.8");
            return await client.GetStringAsync(ApiUrl);
        }

        private static string ResolveLocalFace(
            ApiCard apiCard,
            Dictionary<string, string> exactFaces,
            Dictionary<string, List<string>> normalizedFaces)
        {
            if (exactFaces.TryGetValue(apiCard.name, out string exact))
                return TokenCompatible(apiCard, exact) ? exact : null;

            string key = Normalize(apiCard.name);
            if (!normalizedFaces.TryGetValue(key, out List<string> candidates))
                return null;

            List<string> compatible = candidates.Where(name => TokenCompatible(apiCard, name)).ToList();
            return compatible.Count == 1 ? compatible[0] : null;
        }

        private static bool TokenCompatible(ApiCard apiCard, string localName)
        {
            bool apiIsToken = IsToken(apiCard.name) || IsToken(apiCard.type) || IsToken(apiCard.frameType);
            bool localIsToken = IsToken(localName);
            return apiIsToken == localIsToken;
        }

        private static bool IsToken(string value)
        {
            return !string.IsNullOrWhiteSpace(value) &&
                   value.IndexOf("Token", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsExcluded(string value)
        {
            return string.Equals(value?.Trim(), ExcludedCardName, StringComparison.OrdinalIgnoreCase);
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
                rarity = ResolveRarity(card.card_sets),
                attribute = card.attribute ?? string.Empty,
                typeLine = typeLine,
                level = Mathf.Max(0, card.level),
                attack = Mathf.Max(0, card.atk),
                defense = Mathf.Max(0, card.def),
                effectText = card.desc ?? string.Empty,
                modelResource = string.Empty
            };
        }

        private static string ResolveRarity(List<ApiCardSet> sets)
        {
            CardRarity best = CardRarity.Common;
            if (sets == null) return best.ToString();

            foreach (ApiCardSet set in sets)
            {
                string text = ((set?.set_rarity ?? string.Empty) + " " + (set?.set_rarity_code ?? string.Empty)).ToLowerInvariant();
                CardRarity rarity = CardRarity.Common;

                if (text.Contains("secret") || text.Contains("ghost") || text.Contains("starlight") || text.Contains("collector"))
                    rarity = CardRarity.SecretRare;
                else if (text.Contains("ultra") || text.Contains("ultimate"))
                    rarity = CardRarity.UltraRare;
                else if (text.Contains("super"))
                    rarity = CardRarity.SuperRare;
                else if (text.Contains("rare"))
                    rarity = CardRarity.Rare;

                if ((int)rarity > (int)best)
                    best = rarity;
            }

            return best.ToString();
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
            builder.AppendLine($"Local DMO card PNGs considered: {localCount:N0}");
            builder.AppendLine($"Matched production cards: {matchedCount:N0}");
            builder.AppendLine($"Unmatched local image names: {unmatched.Count:N0}");
            builder.AppendLine("Excluded card: Tricky Token");
            builder.AppendLine();
            builder.AppendLine("Matching rule: exact name first; normalized fallback only when unambiguous; Token/non-Token art may never cross-match.");
            builder.AppendLine("Presentation rule: local image is treated as artwork and fitted into the metadata-selected frame.");

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
