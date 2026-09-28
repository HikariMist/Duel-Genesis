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
    /// The nine Duel Genesis boosters. Each has 9 cards: 6 commons, 2 rare-or-better slots and one foil
    /// slot with a real shot at Super, Ultra and Secret rares. Cards only come from the pack's own pool.
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

        private static CardRarity RollRare()
        {
            float r = UnityEngine.Random.value;
            return r < 0.80f ? CardRarity.Rare : r < 0.95f ? CardRarity.SuperRare : CardRarity.UltraRare;
        }

        private static CardRarity RollFoil()
        {
            float r = UnityEngine.Random.value;
            return r < 0.45f ? CardRarity.Rare : r < 0.75f ? CardRarity.SuperRare : r < 0.95f ? CardRarity.UltraRare : CardRarity.SecretRare;
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
