using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace DuelGenesis.Cards
{
    [Serializable]
    public class ExternalCardCatalog
    {
        public List<ExternalCardRecord> cards = new();
    }

    [Serializable]
    public class ExternalCardRecord
    {
        public string id;
        public string cardName;
        public string kind = "Monster";
        public string rarity = "Common";
        public string attribute = "DARK";
        public string typeLine = "Effect";
        public int level;
        public int attack;
        public int defense;
        public string effectText;
        public string frameKind = "Auto";
        public string modelResource;

        public CardData ToCardData()
        {
            if (!Enum.TryParse(kind?.Replace(" ", string.Empty), true, out CardKind parsedKind))
                parsedKind = CardKind.Monster;

            if (!Enum.TryParse(rarity?.Replace(" ", string.Empty), true, out CardRarity parsedRarity))
                parsedRarity = CardRarity.Common;

            string frameText = (frameKind ?? "Auto").Replace(" ", string.Empty).Replace("XYZ", "Xyz");
            if (!Enum.TryParse(frameText, true, out CardFrameKind parsedFrame))
                parsedFrame = CardFrameKind.Auto;

            return new CardData(
                id,
                string.IsNullOrWhiteSpace(cardName) ? id : cardName,
                parsedKind,
                parsedRarity,
                attribute ?? string.Empty,
                typeLine ?? string.Empty,
                Mathf.Max(0, level),
                Mathf.Max(0, attack),
                Mathf.Max(0, defense),
                effectText ?? string.Empty,
                parsedFrame);
        }
    }

    public static class CardModelRegistry
    {
        private static readonly Dictionary<string, string> ResourcePaths = new();
        private static readonly Dictionary<string, string> DmoCharacterNames = new();
        private static bool _manifestLoaded;

        public static void Register(string cardId, string resourcePath)
        {
            if (string.IsNullOrWhiteSpace(cardId) || string.IsNullOrWhiteSpace(resourcePath))
                return;
            ResourcePaths[cardId] = resourcePath;
        }

        public static void ClearRegistrations()
        {
            ResourcePaths.Clear();
        }

        public static GameObject LoadPrefab(string cardId)
        {
            if (string.IsNullOrWhiteSpace(cardId)) return null;

            if (ResourcePaths.TryGetValue(cardId, out string explicitPath))
            {
                GameObject explicitPrefab = Resources.Load<GameObject>(explicitPath);
                if (explicitPrefab != null) return explicitPrefab;
            }

            return Resources.Load<GameObject>($"CardModels/{cardId}");
        }

        public static GameObject LoadPrefab(CardData card)
        {
            if (card == null) return null;

            GameObject byId = LoadPrefab(card.id);
            if (byId != null) return byId;

            if (!TryResolveDmoCharacterName(card.cardName, out string characterName))
                return null;

            return Resources.Load<GameObject>($"Models/{characterName}");
        }

        public static AnimationClip[] LoadAnimationClips(CardData card)
        {
            if (card == null || !TryResolveDmoCharacterName(card.cardName, out string characterName))
                return Array.Empty<AnimationClip>();

            return Resources.LoadAll<AnimationClip>($"Animations/{characterName}");
        }

        public static bool TryResolveDmoCharacterName(string cardName, out string characterName)
        {
            EnsureDmoManifest();
            return DmoCharacterNames.TryGetValue(NormalizeName(cardName), out characterName);
        }

        public static string NormalizeName(string value)
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

        private static void EnsureDmoManifest()
        {
            if (_manifestLoaded) return;
            _manifestLoaded = true;

            TextAsset manifest = Resources.Load<TextAsset>("dmo_manifest");
            if (manifest == null) return;

            string[] lines = manifest.text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string line in lines)
            {
                string[] parts = line.Split('\t');
                if (parts.Length == 0 || string.IsNullOrWhiteSpace(parts[0]))
                    continue;

                string name = parts[0].Trim();
                string key = NormalizeName(name);
                if (!string.IsNullOrEmpty(key) && !DmoCharacterNames.ContainsKey(key))
                    DmoCharacterNames.Add(key, name);
            }
        }
    }

    public class ExternalCardCatalogLoader : MonoBehaviour
    {
        public const string CatalogFileName = "duel_genesis_cards.json";
        private const string ExcludedCardName = "Tricky Token";
        public int LoadedCardCount { get; private set; }

        private void Awake()
        {
            LoadIfPresent();
        }

        public void LoadIfPresent()
        {
            CardDatabase.Clear();
            CardModelRegistry.ClearRegistrations();
            ProductionCardArtRegistry.ClearCaches();
            LoadedCardCount = 0;

            string path = Path.Combine(Application.streamingAssetsPath, CatalogFileName);
            if (!File.Exists(path))
            {
                Debug.LogWarning($"Duel: Genesis production card catalog is missing. Build {CatalogFileName} from the DMO card library before opening packs or dueling.");
                return;
            }

            try
            {
                string json = File.ReadAllText(path);
                ExternalCardCatalog catalog = JsonUtility.FromJson<ExternalCardCatalog>(json);
                if (catalog?.cards == null || catalog.cards.Count == 0)
                {
                    Debug.LogWarning("Duel: Genesis production card catalog contains no cards. No prototype fallback will be loaded.");
                    return;
                }

                int registered = 0;
                foreach (ExternalCardRecord record in catalog.cards)
                {
                    if (record == null || string.IsNullOrWhiteSpace(record.id))
                        continue;
                    if (string.Equals(record.cardName?.Trim(), ExcludedCardName, StringComparison.OrdinalIgnoreCase))
                        continue;

                    CardData card = record.ToCardData();
                    CardDatabase.RegisterProductionCard(card);
                    CardModelRegistry.Register(record.id, record.modelResource);
                    registered++;
                }

                LoadedCardCount = registered;
                Debug.Log($"Duel: Genesis loaded {LoadedCardCount} production cards. Prototype cards and Tricky Token are disabled.");
            }
            catch (Exception exception)
            {
                CardDatabase.Clear();
                LoadedCardCount = 0;
                Debug.LogError("Failed to load Duel: Genesis production card catalog: " + exception.Message);
            }
        }
    }
}
