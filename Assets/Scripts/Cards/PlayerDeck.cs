using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DuelGenesis.Cards
{
    [Serializable]
    public class DeckEntry
    {
        public string cardId;
        public int quantity;

        public DeckEntry(string cardId, int quantity)
        {
            this.cardId = cardId;
            this.quantity = quantity;
        }
    }

    [Serializable]
    internal class DeckSaveData
    {
        public List<DeckEntry> mainDeck = new();
    }

    public class PlayerDeck : MonoBehaviour
    {
        public const int MinimumDeckSize = 40;
        public const int MaximumDeckSize = 60;
        public const int MaximumCopiesPerCard = 3;

        private const string SaveKey = "DUEL_GENESIS_MAIN_DECK_V1";

        [SerializeField] private List<DeckEntry> mainDeck = new();

        public event Action DeckChanged;

        public IReadOnlyList<DeckEntry> Entries => mainDeck;
        public int MainDeckCount => mainDeck.Where(entry => entry != null).Sum(entry => Mathf.Max(0, entry.quantity));
        public bool IsLegalSize => MainDeckCount >= MinimumDeckSize && MainDeckCount <= MaximumDeckSize;

        private void Awake()
        {
            Load();
        }

        public int GetQuantity(string cardId)
        {
            DeckEntry entry = mainDeck.FirstOrDefault(e => e != null && e.cardId == cardId);
            return entry?.quantity ?? 0;
        }

        public bool CanAdd(CardData card, PlayerCollection collection, out string reason)
        {
            reason = string.Empty;
            if (card == null || collection == null)
            {
                reason = "Missing card or collection.";
                return false;
            }

            if (MainDeckCount >= MaximumDeckSize)
            {
                reason = $"Deck is already at {MaximumDeckSize} cards.";
                return false;
            }

            int inDeck = GetQuantity(card.id);
            if (inDeck >= MaximumCopiesPerCard)
            {
                reason = $"Maximum {MaximumCopiesPerCard} copies allowed.";
                return false;
            }

            int owned = collection.GetQuantity(card.id);
            if (inDeck >= owned)
            {
                reason = "You do not own another copy of this card.";
                return false;
            }

            return true;
        }

        public bool AddCard(CardData card, PlayerCollection collection)
        {
            if (!CanAdd(card, collection, out _))
                return false;

            DeckEntry entry = mainDeck.FirstOrDefault(e => e != null && e.cardId == card.id);
            if (entry == null)
            {
                entry = new DeckEntry(card.id, 0);
                mainDeck.Add(entry);
            }

            entry.quantity++;
            SaveAndNotify();
            return true;
        }

        public bool RemoveCard(CardData card)
        {
            if (card == null) return false;

            DeckEntry entry = mainDeck.FirstOrDefault(e => e != null && e.cardId == card.id);
            if (entry == null || entry.quantity <= 0) return false;

            entry.quantity--;
            if (entry.quantity <= 0)
                mainDeck.Remove(entry);

            SaveAndNotify();
            return true;
        }

        public int RemovePrototypeCards()
        {
            int removed = mainDeck.RemoveAll(entry => entry == null || CardDatabase.IsPrototypeId(entry.cardId));
            if (removed > 0)
            {
                SaveAndNotify();
                Debug.Log($"Duel: Genesis removed {removed} prototype card entries from the saved Main Deck.");
            }
            return removed;
        }

        public void Clear()
        {
            mainDeck.Clear();
            SaveAndNotify();
        }

        public int AutoBuild(PlayerCollection collection)
        {
            if (collection == null) return 0;

            mainDeck.Clear();

            List<(CardData card, int quantity)> owned = collection.GetOwnedCardsSorted()
                .OrderBy(pair => pair.card.kind)
                .ThenByDescending(pair => pair.card.rarity)
                .ThenBy(pair => pair.card.cardName)
                .ToList();

            foreach (var entry in owned)
            {
                int copies = Mathf.Min(MaximumCopiesPerCard, entry.quantity);
                for (int i = 0; i < copies && MainDeckCount < MinimumDeckSize; i++)
                {
                    DeckEntry deckEntry = mainDeck.FirstOrDefault(e => e != null && e.cardId == entry.card.id);
                    if (deckEntry == null)
                    {
                        deckEntry = new DeckEntry(entry.card.id, 0);
                        mainDeck.Add(deckEntry);
                    }
                    deckEntry.quantity++;
                }

                if (MainDeckCount >= MinimumDeckSize)
                    break;
            }

            SaveAndNotify();
            return MainDeckCount;
        }

        public List<(CardData card, int quantity)> GetDeckCardsSorted()
        {
            return mainDeck
                .Where(e => e != null && e.quantity > 0)
                .Select(e => (CardDatabase.GetById(e.cardId), e.quantity))
                .Where(pair => pair.Item1 != null)
                .OrderBy(pair => pair.Item1.kind)
                .ThenBy(pair => pair.Item1.cardName)
                .ToList();
        }

        public bool Validate(PlayerCollection collection, out string message)
        {
            if (!CardDatabase.IsReady)
            {
                message = "Production card catalog is not loaded.";
                return false;
            }

            if (MainDeckCount < MinimumDeckSize)
            {
                message = $"Need {MinimumDeckSize - MainDeckCount} more cards.";
                return false;
            }

            if (MainDeckCount > MaximumDeckSize)
            {
                message = $"Remove {MainDeckCount - MaximumDeckSize} cards.";
                return false;
            }

            foreach (DeckEntry entry in mainDeck.Where(entry => entry != null))
            {
                CardData card = CardDatabase.GetById(entry.cardId);
                if (card == null)
                {
                    message = $"Deck contains unavailable card ID {entry.cardId}.";
                    return false;
                }

                if (CardDatabase.IsPrototypeId(entry.cardId))
                {
                    message = "Deck still contains retired prototype cards.";
                    return false;
                }

                if (entry.quantity > MaximumCopiesPerCard)
                {
                    message = $"Too many copies of {card.cardName}.";
                    return false;
                }

                if (collection != null && entry.quantity > collection.GetQuantity(entry.cardId))
                {
                    message = $"Deck uses more copies of {card.cardName} than you own.";
                    return false;
                }
            }

            message = "DECK LEGAL";
            return true;
        }

        private void SaveAndNotify()
        {
            Save();
            DeckChanged?.Invoke();
        }

        public void Save()
        {
            DeckSaveData data = new DeckSaveData { mainDeck = mainDeck };
            PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(data));
            PlayerPrefs.Save();
        }

        public void Load()
        {
            if (!PlayerPrefs.HasKey(SaveKey))
            {
                mainDeck = new List<DeckEntry>();
                return;
            }

            string json = PlayerPrefs.GetString(SaveKey, string.Empty);
            DeckSaveData data = string.IsNullOrWhiteSpace(json)
                ? null
                : JsonUtility.FromJson<DeckSaveData>(json);

            mainDeck = data?.mainDeck ?? new List<DeckEntry>();
        }
    }
}
