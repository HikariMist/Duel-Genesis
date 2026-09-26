using System;
using System.Collections.Generic;
using System.IO;
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
        public string modelResource;

        public CardData ToCardData()
        {
            if (!Enum.TryParse(kind?.Replace(" ", string.Empty), true, out CardKind parsedKind))
                parsedKind = CardKind.Monster;

            if (!Enum.TryParse(rarity?.Replace(" ", string.Empty), true, out CardRarity parsedRarity))
                parsedRarity = CardRarity.Common;

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
                effectText ?? string.Empty);
        }
    }

    public static class CardModelRegistry
    {
        private static readonly Dictionary<string, string> ResourcePaths = new();

        public static void Register(string cardId, string resourcePath)
        {
            if (string.IsNullOrWhiteSpace(cardId) || string.IsNullOrWhiteSpace(resourcePath))
                return;
            ResourcePaths[cardId] = resourcePath;
        }

        public static GameObject LoadPrefab(string cardId)
        {
            if (string.IsNullOrWhiteSpace(cardId)) return null;

            if (ResourcePaths.TryGetValue(cardId, out string explicitPath))
            {
                GameObject explicitPrefab = Resources.Load<GameObject>(explicitPath);
                if (explicitPrefab != null) return explicitPrefab;
            }

            // Convention fallback: Assets/Resources/CardModels/<CARD_ID>.prefab
            return Resources.Load<GameObject>($"CardModels/{cardId}");
        }
    }

    public class ExternalCardCatalogLoader : MonoBehaviour
    {
        public const string CatalogFileName = "duel_genesis_cards.json";
        public int LoadedCardCount { get; private set; }

        private void Awake()
        {
            LoadIfPresent();
        }

        public void LoadIfPresent()
        {
            string path = Path.Combine(Application.streamingAssetsPath, CatalogFileName);
            if (!File.Exists(path))
            {
                LoadedCardCount = 0;
                return;
            }

            try
            {
                string json = File.ReadAllText(path);
                ExternalCardCatalog catalog = JsonUtility.FromJson<ExternalCardCatalog>(json);
                if (catalog?.cards == null)
                {
                    Debug.LogWarning("Duel: Genesis external card catalog exists but contains no cards.");
                    return;
                }

                int registered = 0;
                foreach (ExternalCardRecord record in catalog.cards)
                {
                    if (record == null || string.IsNullOrWhiteSpace(record.id))
                        continue;

                    CardDatabase.RegisterOrReplace(record.ToCardData());
                    CardModelRegistry.Register(record.id, record.modelResource);
                    registered++;
                }

                LoadedCardCount = registered;
                Debug.Log($"Duel: Genesis loaded {LoadedCardCount} external card records from {CatalogFileName}.");
            }
            catch (Exception exception)
            {
                LoadedCardCount = 0;
                Debug.LogError("Failed to load Duel: Genesis external card catalog: " + exception.Message);
            }
        }
    }
}
