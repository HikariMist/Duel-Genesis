using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DuelGenesis.Cards
{
    [Serializable]
    public class CollectionEntry
    {
        public string cardId;
        public int quantity;

        public CollectionEntry(string cardId, int quantity)
        {
            this.cardId = cardId;
            this.quantity = quantity;
        }
    }

    [Serializable]
    internal class CollectionSaveData
    {
        public List<CollectionEntry> cards = new();
    }

    public class PlayerCollection : MonoBehaviour
    {
        private const string SaveKey = "DUEL_GENESIS_COLLECTION_V1";

        [SerializeField] private List<CollectionEntry> cards = new();

        public event Action CollectionChanged;

        public IReadOnlyList<CollectionEntry> Entries => cards;
        public int UniqueCardCount => cards.Count;
        public int TotalCardCount => cards.Sum(entry => Mathf.Max(0, entry.quantity));

        private void Awake()
        {
            Load();
        }

        public void AddCard(CardData card, int amount = 1)
        {
            if (card == null || amount <= 0) return;

            CollectionEntry entry = cards.FirstOrDefault(e => e.cardId == card.id);
            if (entry == null)
            {
                entry = new CollectionEntry(card.id, 0);
                cards.Add(entry);
            }

            entry.quantity += amount;
            Save();
            CollectionChanged?.Invoke();

            Debug.Log($"Added {amount}x {card.cardName} to collection. Owned: {entry.quantity}");
        }

        /// <summary>Tops every card in the catalog up to <paramref name="copies"/> (Tokens excluded). Returns cards added.</summary>
        public int GrantEveryCard(int copies = 3)
        {
            int added = 0;
            foreach (CardData card in CardDatabase.All)
            {
                if (card == null || card.ResolvedFrameKind == CardFrameKind.Token) continue;
                CollectionEntry entry = cards.FirstOrDefault(e => e.cardId == card.id);
                if (entry == null)
                {
                    entry = new CollectionEntry(card.id, 0);
                    cards.Add(entry);
                }
                if (entry.quantity >= copies) continue;
                added += copies - entry.quantity;
                entry.quantity = copies;
            }
            if (added > 0)
            {
                Save();
                CollectionChanged?.Invoke();
            }
            Debug.Log($"Duel: Genesis granted {added} cards — the collection now has every card ({CardDatabase.All.Count}) at {copies}+ copies.");
            return added;
        }

        public int GetQuantity(string cardId)
        {
            CollectionEntry entry = cards.FirstOrDefault(e => e.cardId == cardId);
            return entry?.quantity ?? 0;
        }

        public int RemovePrototypeCards()
        {
            int removed = cards.RemoveAll(entry => entry == null || CardDatabase.IsPrototypeId(entry.cardId));
            if (removed > 0)
            {
                Save();
                CollectionChanged?.Invoke();
                Debug.Log($"Duel: Genesis removed {removed} prototype card entries from the saved collection.");
            }
            return removed;
        }

        public List<(CardData card, int quantity)> GetOwnedCardsSorted()
        {
            return cards
                .Where(e => e != null && e.quantity > 0)
                .Select(e => (CardDatabase.GetById(e.cardId), e.quantity))
                .Where(pair => pair.Item1 != null)
                .OrderByDescending(pair => pair.Item1.rarity)
                .ThenBy(pair => pair.Item1.cardName)
                .ToList();
        }

        public void Save()
        {
            CollectionSaveData data = new CollectionSaveData { cards = cards };
            PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(data));
            PlayerPrefs.Save();
        }

        public void Load()
        {
            if (!PlayerPrefs.HasKey(SaveKey))
            {
                cards = new List<CollectionEntry>();
                return;
            }

            string json = PlayerPrefs.GetString(SaveKey, string.Empty);
            if (string.IsNullOrWhiteSpace(json))
            {
                cards = new List<CollectionEntry>();
                return;
            }

            CollectionSaveData data = JsonUtility.FromJson<CollectionSaveData>(json);
            cards = data?.cards ?? new List<CollectionEntry>();
        }

        [ContextMenu("Clear Saved Collection")]
        public void ClearSavedCollection()
        {
            cards.Clear();
            PlayerPrefs.DeleteKey(SaveKey);
            PlayerPrefs.Save();
            CollectionChanged?.Invoke();
            Debug.Log("Duel: Genesis saved card collection cleared.");
        }
    }
}
