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
            // Monster Type packs.
            Make("dragon", "Dragon Pack", "Dragon monsters", 1200, new Color(0.85f, 0.2f, 0.2f), Types("Dragon")),
            Make("spellcaster", "Spellcaster Pack", "Spellcaster monsters", 1200, new Color(0.75f, 0.45f, 1f), Types("Spellcaster")),
            Make("warrior", "Warrior Pack", "Warrior monsters", 1200, new Color(0.95f, 0.65f, 0.25f), Types("Warrior")),
            Make("fiend", "Fiend Pack", "Fiend monsters", 1200, new Color(0.6f, 0.15f, 0.45f), Types("Fiend")),
            Make("machine", "Machine Pack", "Machine monsters", 1200, new Color(0.6f, 0.7f, 0.8f), Types("Machine")),
            Make("fairy", "Fairy Pack", "Fairy monsters", 1200, new Color(1f, 0.85f, 0.95f), Types("Fairy")),
            Make("zombie", "Zombie Pack", "Zombie monsters", 1200, new Color(0.45f, 0.75f, 0.45f), Types("Zombie")),
            Make("beast", "Beast Pack", "Beast, Beast-Warrior and Winged Beast", 1200, new Color(0.8f, 0.55f, 0.3f), Types("Beast", "Beast-Warrior", "Winged Beast")),
            Make("thunder", "Thunder Pack", "Thunder monsters", 1200, new Color(1f, 0.95f, 0.3f), Types("Thunder")),
            Make("insect", "Insect Pack", "Insect monsters", 1200, new Color(0.55f, 0.85f, 0.25f), Types("Insect")),
        };

        // ------------------------------------------------------------------ which shop sells what

        /// <summary>The main shop (the Card Vault rotunda): carries every pack, on a daily rotation.</summary>
        public const string MainShopName = "Genesis Card Shop";
        /// <summary>How many packs the main shop has on its shelves at once.</summary>
        public const int MainShopShelf = 10;

        /// <summary>The 4 lineups every other shop is split into (5 packs each, every pack in exactly one lineup).</summary>
        public static readonly string[][] Lineups =
        {
            new[] { "monster", "dark", "dragon", "fiend", "zombie" },
            new[] { "spell", "light", "spellcaster", "fairy", "thunder" },
            new[] { "trap", "earth", "warrior", "beast", "insect" },
            new[] { "fire", "water", "wind", "machine", "monster" },
        };

        public static readonly string[] LineupNames = { "Shadow Lineup", "Arcane Lineup", "Wild Lineup", "Elemental Lineup" };

        public static bool IsMainShop(string shopName) => string.Equals(shopName, MainShopName, StringComparison.OrdinalIgnoreCase);

        private static readonly Dictionary<string, int> LineupCache = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>Lineup (0-3) a shop carries. All shops in the world (except the main one) are sorted by
        /// name and dealt the 4 lineups in turn, so each lineup is carried by an even share of shops.</summary>
        public static int LineupFor(string shopName)
        {
            shopName ??= string.Empty;
            if (LineupCache.TryGetValue(shopName, out int cached)) return cached;
            LineupCache.Clear();
            List<string> names = UnityEngine.Object.FindObjectsByType<CardShopTerminal>(FindObjectsSortMode.None)
                .Select(t => t.shopName ?? string.Empty).Where(n => !IsMainShop(n))
                .Append(shopName).Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToList();
            for (int i = 0; i < names.Count; i++) LineupCache[names[i]] = i % Lineups.Length;
            return LineupCache[shopName];
        }

        /// <summary>Packs on sale at the named shop.</summary>
        public static GenesisPackType[] ForShop(string shopName)
        {
            if (IsMainShop(shopName))
            {
                // Every pack, MainShopShelf at a time, moving on by half a shelf each real-world day.
                int day = (int)(DateTime.Now.Date - new DateTime(2026, 1, 1)).TotalDays;
                int start = (day * (MainShopShelf / 2)) % All.Length;
                return Enumerable.Range(0, Math.Min(MainShopShelf, All.Length)).Select(i => All[(start + i) % All.Length]).ToArray();
            }
            return Lineups[LineupFor(shopName)].Select(Get).Distinct().ToArray();
        }

        public static string ShopBlurb(string shopName) => IsMainShop(shopName)
            ? "Every pack in the game, on daily rotation"
            : LineupNames[LineupFor(shopName)];

        public static GenesisPackType Get(string id) => All.FirstOrDefault(p => p.id == id) ?? All[0];

        private static GenesisPackType Make(string id, string name, string blurb, int price, Color accent, Func<CardData, bool> filter) =>
            new GenesisPackType { id = id, displayName = name, blurb = blurb, price = price, accent = accent, filter = filter };

        /// <summary>Monsters whose Type (the part of the type line before " /") is one of <paramref name="types"/>.</summary>
        private static Func<CardData, bool> Types(params string[] types) =>
            c => c.kind == CardKind.Monster && types.Any(t => string.Equals(((c.typeLine ?? "").Split('/')[0]).Trim(), t, StringComparison.OrdinalIgnoreCase));

        private static Func<CardData, bool> Attr(string attribute) =>
            c => c.kind == CardKind.Monster && string.Equals((c.attribute ?? "").Trim(), attribute, StringComparison.OrdinalIgnoreCase);

        /// <summary>Rolls a full pack, rarest card last. Returns null if the pack's pool is empty.</summary>
        public static List<CardData> Roll(GenesisPackType pack)
        {
            List<CardData> fullPool = pack.Pool();
            if (fullPool.Count == 0) return null;
            // The Legendary cards never come from normal slots...
            List<CardData> pool = fullPool.Where(c => !IsLegendary(c)).ToList();
            List<CardData> legends = fullPool.Where(IsLegendary).ToList();
            if (pool.Count == 0) pool = fullPool;

            var byRarity = pool.GroupBy(c => c.rarity).ToDictionary(g => g.Key, g => g.ToList());
            var cards = new List<CardData>(CardsPerPack);
            for (int i = 0; i < CardsPerPack; i++)
            {
                CardRarity wanted = i < 6 ? CardRarity.Common : i < 8 ? RollRare() : RollFoil();
                cards.Add(Pick(byRarity, pool, wanted));
            }
            // ...only from a 1-in-10,000 "Legendary pull" that replaces the foil card.
            if (legends.Count > 0 && UnityEngine.Random.value < LegendaryChance)
                cards[cards.Count - 1] = legends[UnityEngine.Random.Range(0, legends.Count)];
            return cards.OrderBy(c => IsLegendary(c) ? 99 : (int)c.rarity).ToList();
        }

        public const float LegendaryChance = CardDatabase.LegendaryChance;
        public static bool IsLegendary(CardData c) => CardDatabase.IsLegendary(c);

        // Rare slots (x2): Rare 86%, Super 11%, Ultra 3%.
        public const float RareSlotSuper = 0.11f, RareSlotUltra = 0.03f;
        // Foil slot (x1): Rare 55%, Super 28%, Ultra 13%, Secret 4%.
        public const float FoilSuper = 0.28f, FoilUltra = 0.13f, FoilSecret = 0.04f;

        /// <summary>The odds line shown at the pack counter.</summary>
        public static string OddsText =>
            $"Each pack: 9 cards  ·  6 Common  ·  2 Rare slots  ·  1 Foil slot: Super {FoilSuper * 100:0}%  ·  Ultra {FoilUltra * 100:0}%  ·  Secret {FoilSecret * 100:0}%  ·  Legendary 1 in 10,000";

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
            LineupCache.Clear();
        }
    }
}
