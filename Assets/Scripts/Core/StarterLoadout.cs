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

            if (PlayerPrefs.GetInt(ClaimedKey, 0) == 1)
            {
                // Older saves counted Fusion monsters in the Main Deck; keep them duel-ready.
                if (!deck.Validate(collection, out _) && deck.MainDeckCount < PlayerDeck.MinimumDeckSize)
                    deck.TopUpMainDeck(collection);
                return;
            }

            if (deck.Validate(collection, out _))
            {
                MarkClaimed();
                return;
            }

            // A real starter: a Level 1-4 core, a small Tribute curve and staple Spells/Traps that work.
            var starter = new List<(CardData card, int copies)>();
            foreach (CardData card in CardDatabase.All.Where(IsEasyStarterMonster)
                         .OrderByDescending(card => Mathf.Max(card.attack, card.defense)).ThenBy(card => card.cardName).Take(13))
                starter.Add((card, 2));
            foreach (CardData card in CardDatabase.All.Where(c => IsSummonableMonster(c) && (c.level == 5 || c.level == 6))
                         .OrderByDescending(card => card.attack).Take(2))
                starter.Add((card, 1));
            foreach (CardData card in CardDatabase.All.Where(c => IsSummonableMonster(c) && c.level >= 7)
                         .OrderByDescending(card => card.attack).Take(1))
                starter.Add((card, 1));
            string[] staples =
            {
                "Pot of Greed", "Dark Hole", "Mystical Space Typhoon", "Swords of Revealing Light", "Rush Recklessly",
                "Book of Moon", "Mirror Force", "Trap Hole", "Sakuretsu Armor", "Magic Cylinder", "Negate Attack",
                "Heavy Storm", "Premature Burial", "Call of the Haunted"
            };
            foreach (string name in staples)
            {
                CardData card = CardDatabase.All.FirstOrDefault(c => c.cardName == name);
                if (card != null) starter.Add((card, 1));
            }

            foreach ((CardData card, int copies) in starter)
            {
                if (deck.MainDeckCount >= StarterDeckSize)
                    break;

                int desiredCopies = Mathf.Min(copies, StarterDeckSize - deck.MainDeckCount);
                int owned = collection.GetQuantity(card.id);
                if (owned < desiredCopies)
                    collection.AddCard(card, desiredCopies - owned);

                while (deck.GetQuantity(card.id) < desiredCopies && deck.MainDeckCount < StarterDeckSize)
                {
                    if (!deck.AddCard(card, collection))
                        break;
                }
            }

            if (deck.MainDeckCount < StarterDeckSize)
            {
                foreach (CardData card in CardDatabase.All.Where(IsEasyStarterMonster).OrderByDescending(card => card.attack))
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
            return IsSummonableMonster(card) && card.level > 0 && card.level <= 4;
        }

        private static bool IsSummonableMonster(CardData card)
        {
            return card != null && card.kind == CardKind.Monster && IsMainDeckCard(card) &&
                   DuelGenesis.Dueling.DuelRules.CanEverBeNormalSummoned(card);
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
