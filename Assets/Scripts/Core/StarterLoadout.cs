using System.Collections.Generic;
using System.Linq;
using DuelGenesis.Cards;
using UnityEngine;

namespace DuelGenesis.Core
{
    public class StarterLoadout : MonoBehaviour
    {
        private const string ClaimedKey = "DUEL_GENESIS_STARTER_V2_PRODUCTION";
        public const int StarterDeckSize = 40;

        private void Awake()
        {
            RemoveRetiredPrototypeCards();
        }

        private void Start()
        {
            GrantIfNeeded();
        }

        public void GrantIfNeeded()
        {
            PlayerCollection collection = Object.FindFirstObjectByType<PlayerCollection>();
            PlayerDeck deck = Object.FindFirstObjectByType<PlayerDeck>();
            if (collection == null || deck == null)
                return;

            collection.RemovePrototypeCards();
            deck.RemovePrototypeCards();

            if (!CardDatabase.IsReady)
            {
                Debug.LogWarning("Duel: Genesis production starter was not granted because the real card catalog is not loaded yet.");
                return;
            }

            if (deck.Validate(collection, out _))
            {
                MarkClaimed();
                return;
            }

            List<CardData> starterPool = CardDatabase.All
                .Where(IsEasyStarterMonster)
                .OrderBy(card => card.cardName)
                .Take(20)
                .ToList();

            if (starterPool.Count < 20)
            {
                foreach (CardData card in CardDatabase.All.Where(IsMainDeckCard).OrderBy(card => card.cardName))
                {
                    if (starterPool.Contains(card)) continue;
                    starterPool.Add(card);
                    if (starterPool.Count >= 20) break;
                }
            }

            foreach (CardData card in starterPool)
            {
                if (deck.MainDeckCount >= StarterDeckSize)
                    break;

                int desiredCopies = Mathf.Min(2, StarterDeckSize - deck.MainDeckCount);
                int owned = collection.GetQuantity(card.id);
                if (owned < desiredCopies)
                    collection.AddCard(card, desiredCopies - owned);

                while (deck.GetQuantity(card.id) < desiredCopies && deck.MainDeckCount < StarterDeckSize)
                {
                    if (!deck.AddCard(card, collection))
                        break;
                }
            }

            // If the catalog has fewer than 20 suitable unique cards, fill the
            // remainder without ever exceeding the three-copy deck rule.
            if (deck.MainDeckCount < StarterDeckSize)
            {
                foreach (CardData card in CardDatabase.All.Where(IsMainDeckCard).OrderBy(card => card.cardName))
                {
                    while (deck.GetQuantity(card.id) < PlayerDeck.MaximumCopiesPerCard && deck.MainDeckCount < StarterDeckSize)
                    {
                        if (collection.GetQuantity(card.id) <= deck.GetQuantity(card.id))
                            collection.AddCard(card, 1);
                        if (!deck.AddCard(card, collection))
                            break;
                    }

                    if (deck.MainDeckCount >= StarterDeckSize)
                        break;
                }
            }

            if (deck.MainDeckCount >= StarterDeckSize)
            {
                MarkClaimed();
                Debug.Log($"Duel: Genesis production starter granted — {deck.MainDeckCount} real cards in the Main Deck. Prototype cards were removed.");
            }
            else
            {
                Debug.LogWarning($"Duel: Genesis could only build a {deck.MainDeckCount}-card production starter. Rebuild the real card catalog and reload the scene.");
            }
        }

        private static bool IsEasyStarterMonster(CardData card)
        {
            return card != null &&
                   card.kind == CardKind.Monster &&
                   card.level > 0 && card.level <= 4 &&
                   IsMainDeckCard(card);
        }

        private static bool IsMainDeckCard(CardData card)
        {
            if (card == null || CardDatabase.IsPrototypeId(card.id))
                return false;

            CardFrameKind frame = card.ResolvedFrameKind;
            return frame != CardFrameKind.FusionMonster &&
                   frame != CardFrameKind.SynchroMonster &&
                   frame != CardFrameKind.XyzMonster &&
                   frame != CardFrameKind.Token;
        }

        private static void RemoveRetiredPrototypeCards()
        {
            PlayerCollection collection = Object.FindFirstObjectByType<PlayerCollection>();
            PlayerDeck deck = Object.FindFirstObjectByType<PlayerDeck>();
            collection?.RemovePrototypeCards();
            deck?.RemovePrototypeCards();
        }

        private static void MarkClaimed()
        {
            PlayerPrefs.SetInt(ClaimedKey, 1);
            PlayerPrefs.Save();
        }

#if UNITY_EDITOR
        public static void ClearClaimFlagForTesting()
        {
            PlayerPrefs.DeleteKey(ClaimedKey);
            PlayerPrefs.DeleteKey("DUEL_GENESIS_STARTER_V1");
            PlayerPrefs.Save();
        }
#endif
    }
}
