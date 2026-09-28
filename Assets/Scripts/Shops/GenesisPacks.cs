using System;
using System.Collections.Generic;
using System.Linq;
using DuelGenesis.Cards;
using UnityEngine;

namespace DuelGenesis.Shops
{
    /// <summary>One kind of booster sold at Genesis card shops, with its own wrapper art and card pool.</summary>
    public sealed class GenesisPackType
    {
        public string id;
        public string displayName;
        public string blurb;
        public int price;
        public Color accent;
        public Func<CardData, bool> filter;

        private Texture2D _art;

        /// <summary>Wrapper art from Resources/DuelGenesis/Packs/pack_{id}.</summary>
        public Texture2D Art => _art != null ? _art : _art = Resources.Load<Texture2D>("DuelGenesis/Packs/pack_" + id);

        public List<CardData> Pool() => CardDatabase.All.Where(c => c != null && !CardDatabase.IsPrototypeId(c.id) && filter(c)).ToList();

        internal void ClearArt() => _art = null;
    }

    /// <summary>
    /// The nine Duel Genesis boosters. Each has 9 cards: 6 commons, 2 rare slots and one foil slot.
    /// Rarities come from Tools/assign_rarities.py (StreamingAssets/duel_genesis_rarities.json).
    /// Roughly: a Super Rare or better in 3 packs out of 5, an Ultra Rare or better in about 1 pack in 5,
    /// and a Secret Rare in 1 pack in 25. Cards only come from the pack's own pool.
    /// </summary>
    public static class GenesisPacks
    {
        public const int CardsPerPack = 9;

        public static readonly GenesisPackType[] All =
        {
            Make("monster", "Monster Pack", "Monsters of every Attribute", 1000, new Color(1f, 0.35f, 0.25f), c => c.kind == CardKind.Monster),
            Make("spell", "Spell Pack", "Spell Cards only", 1000, new Color(0.25f, 0.95f, 0.85f), c => c.kind == CardKind.Spell),
            Make("trap", "Trap Pack", "Trap Cards only", 1000, new Color(1f, 0.3f, 0.35f), c => c.kind == CardKind.Trap),
            Make("dark", "Dark Pack", "DARK monsters", 1200, new Color(0.7f, 0.35f, 1f), Attr("DARK")),
            Make("light", "Light Pack", "LIGHT monsters", 1200, new Color(1f, 0.9f, 0.5f), Attr("LIGHT")),
            Make("earth", "Earth Pack", "EARTH monsters", 1200, new Color(0.45f, 0.9f, 0.4f), Attr("EARTH")),
            Make("fire", "Fire Pack", "FIRE monsters", 1200, new Color(1f, 0.5f, 0.15f), Attr("FIRE")),
            Make("water", "Water Pack", "WATER monsters", 1200, new Color(0.3f, 0.6f, 1f), Attr("WATER")),
            Make("wind", "Wind Pack", "WIND monsters", 1200, new Color(0.4f, 1f, 0.7f), Attr("WIND")),
        };

        public static GenesisPackType Get(string id) => All.FirstOrDefault(p => p.id == id) ?? All[0];

        private static GenesisPackType Make(string id, string name, string blurb, int price, Color accent, Func<CardData, bool> filter) =>
            new GenesisPackType { id = id, displayName = name, blurb = blurb, price = price, accent = accent, filter = filter };

        private static Func<CardData, bool> Attr(string attribute) =>
            c => c.kind == CardKind.Monster && string.Equals((c.attribute ?? "").Trim(), attribute, StringComparison.OrdinalIgnoreCase);

        /// <summary>Rolls a full pack, rarest card last. Returns null if the pack's pool is empty.</summary>
        public static List<CardData> Roll(GenesisPackType pack)
        {
            List<CardData> pool = pack.Pool();
            if (pool.Count == 0) return null;

            var byRarity = pool.GroupBy(c => c.rarity).ToDictionary(g => g.Key, g => g.ToList());
            var cards = new List<CardData>(CardsPerPack);
            for (int i = 0; i < CardsPerPack; i++)
            {
                CardRarity wanted = i < 6 ? CardRarity.Common : i < 8 ? RollRare() : RollFoil();
                cards.Add(Pick(byRarity, pool, wanted));
            }
            return cards.OrderBy(c => (int)c.rarity).ToList();
        }

        // Rare slots (x2): Rare 86%, Super 11%, Ultra 3%.
        public const float RareSlotSuper = 0.11f, RareSlotUltra = 0.03f;
        // Foil slot (x1): Rare 55%, Super 28%, Ultra 13%, Secret 4%.
        public const float FoilSuper = 0.28f, FoilUltra = 0.13f, FoilSecret = 0.04f;

        /// <summary>The odds line shown at the pack counter.</summary>
        public static string OddsText =>
            $"Each pack: 9 cards  ·  6 Common  ·  2 Rare slots  ·  1 Foil slot: Super {FoilSuper * 100:0}%  ·  Ultra {FoilUltra * 100:0}%  ·  Secret {FoilSecret * 100:0}%";

        private static CardRarity RollRare()
        {
            float r = UnityEngine.Random.value;
            return r < RareSlotUltra ? CardRarity.UltraRare : r < RareSlotUltra + RareSlotSuper ? CardRarity.SuperRare : CardRarity.Rare;
        }

        private static CardRarity RollFoil()
        {
            float r = UnityEngine.Random.value;
            if (r < FoilSecret) return CardRarity.SecretRare;
            if (r < FoilSecret + FoilUltra) return CardRarity.UltraRare;
            if (r < FoilSecret + FoilUltra + FoilSuper) return CardRarity.SuperRare;
            return CardRarity.Rare;
        }

        /// <summary>A card of the wanted rarity, or the nearest rarity below (then above) that the pool has.</summary>
        private static CardData Pick(Dictionary<CardRarity, List<CardData>> byRarity, List<CardData> pool, CardRarity wanted)
        {
            for (int r = (int)wanted; r >= 0; r--)
                if (byRarity.TryGetValue((CardRarity)r, out var list) && list.Count > 0) return list[UnityEngine.Random.Range(0, list.Count)];
            for (int r = (int)wanted + 1; r <= (int)CardRarity.SecretRare; r++)
                if (byRarity.TryGetValue((CardRarity)r, out var list) && list.Count > 0) return list[UnityEngine.Random.Range(0, list.Count)];
            return pool[UnityEngine.Random.Range(0, pool.Count)];
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetForPlaySession()
        {
            foreach (GenesisPackType p in All) p.ClearArt();
        }
    }
}
