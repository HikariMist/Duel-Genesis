using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DuelGenesis.Cards
{
    public static class CardDatabase
    {
        private static readonly List<CardData> Cards = new();

        public static IReadOnlyList<CardData> All => Cards;
        public static bool IsReady => Cards.Count > 0;
        public static bool HasProductionCards => Cards.Count > 0;
        public static int ProductionCardCount => Cards.Count;

        public static CardData GetById(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return null;
            return Cards.FirstOrDefault(c => c.id == id);
        }

        public static void Clear()
        {
            Cards.Clear();
        }

        public static void RegisterOrReplace(CardData card)
        {
            if (card == null || string.IsNullOrWhiteSpace(card.id))
                return;

            int index = Cards.FindIndex(existing => existing.id == card.id);
            if (index >= 0)
                Cards[index] = card;
            else
                Cards.Add(card);
        }

        public static void RegisterProductionCard(CardData card)
        {
            RegisterOrReplace(card);
        }

        public static int RegisterOrReplace(IEnumerable<CardData> cards)
        {
            if (cards == null) return 0;
            int count = 0;
            foreach (CardData card in cards)
            {
                if (card == null || string.IsNullOrWhiteSpace(card.id))
                    continue;
                RegisterOrReplace(card);
                count++;
            }
            return count;
        }

        /// <summary>Chance per pack (or random pull) of a Legendary card.</summary>
        public const float LegendaryChance = 1f / 10000f;

        /// <summary>The rarest cards in the game: only ever pulled at 1 in 10,000.</summary>
        public static readonly string[] LegendaryNames =
        {
            "Black Luster Soldier", "Black Luster Soldier - Envoy of the Beginning", "Blue-Eyes White Dragon", "Chaos Sorcerer"
        };

        public static bool IsLegendary(CardData c) => c != null && LegendaryNames.Contains(c.cardName);

        public static CardData GetRandomCard(bool guaranteedRareOrBetter = false)
        {
            if (Cards.Count == 0)
                return null;

            CardRarity rarity = RollRarity(guaranteedRareOrBetter);
            List<CardData> legends = Cards.Where(IsLegendary).ToList();
            if (legends.Count > 0 && Random.value < LegendaryChance)
                return legends[Random.Range(0, legends.Count)];
            List<CardData> pool = Cards.Where(c => c.rarity == rarity && !IsLegendary(c)).ToList();

            // Some production catalogs do not contain rarity data yet. In that
            // case draw from the real catalog instead of inventing fallback cards.
            if (pool.Count == 0)
                pool = Cards.Where(c => !IsLegendary(c)).ToList();

            return pool[Random.Range(0, pool.Count)];
        }

        public static bool IsPrototypeId(string id)
        {
            if (string.IsNullOrWhiteSpace(id) || id.Length != 5 ||
                !id.StartsWith("DG", System.StringComparison.OrdinalIgnoreCase))
                return false;

            if (!int.TryParse(id.Substring(2), out int number))
                return false;

            return number >= 1 && number <= 24;
        }

        private static CardRarity RollRarity(bool guaranteedRareOrBetter)
        {
            float roll = Random.value * 100f;

            if (guaranteedRareOrBetter)
            {
                if (roll < 3f) return CardRarity.UltraRare;
                if (roll < 15f) return CardRarity.SuperRare;
                return CardRarity.Rare;
            }

            if (roll < 1.5f) return CardRarity.UltraRare;
            if (roll < 8f) return CardRarity.SuperRare;
            if (roll < 30f) return CardRarity.Rare;
            return CardRarity.Common;
        }
    }
}
