using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DuelGenesis.Shops
{
    /// <summary>A duel mat you can buy at the card shop; its art is printed on your half of the duel table.</summary>
    public sealed class GenesisMatType
    {
        public string id;
        public string displayName;
        public int price;
        public string blurb;
        public Color accent;
        private Texture2D _art;

        /// <summary>Resources/DuelGenesis/Mats/mat_{id}; null for the classic printed mat.</summary>
        public Texture2D Art => id == GenesisMats.ClassicId ? null : _art != null ? _art : _art = Resources.Load<Texture2D>("DuelGenesis/Mats/mat_" + id);
    }

    /// <summary>Mat catalog plus which mats this player owns and which one is equipped (saved in PlayerPrefs).</summary>
    public static class GenesisMats
    {
        public const string ClassicId = "classic";
        private const string OwnedKey = "dg.mats.owned";
        private const string SelectedKey = "dg.mats.selected";

        public static event Action Changed;

        public static readonly GenesisMatType[] All =
        {
            new() { id = ClassicId, displayName = "Genesis Classic", price = 0, blurb = "The standard printed mat", accent = new Color(0.25f, 0.8f, 1f) },
            new() { id = "blue_eyes", displayName = "Blue-Eyes Hieroglyph", price = 4000, blurb = "Blue-Eyes over an ancient tablet", accent = new Color(0.35f, 0.75f, 1f) },
            new() { id = "pharaoh", displayName = "The Pharaoh's Duel", price = 4500, blurb = "The King of Games and Dark Magician", accent = new Color(0.8f, 0.35f, 1f) },
            new() { id = "egyptian_gods", displayName = "Egyptian Gods", price = 6000, blurb = "Ra, Obelisk and Slifer", accent = new Color(1f, 0.78f, 0.3f) },
            new() { id = "dark_magician_girl", displayName = "Dark Magician Girl", price = 3500, blurb = "Her own wallpaper mat", accent = new Color(1f, 0.45f, 0.75f) },
            new() { id = "emerald_seal", displayName = "Emerald Seal", price = 2500, blurb = "A glowing seal among the stars", accent = new Color(0.3f, 1f, 0.55f) },
        };

        public static GenesisMatType Get(string id) => All.FirstOrDefault(m => m.id == id) ?? All[0];

        private static HashSet<string> OwnedSet() =>
            new HashSet<string>((PlayerPrefs.GetString(OwnedKey, ClassicId) + "," + ClassicId).Split(',').Where(s => s.Length > 0));

        public static bool Owns(string id) => id == ClassicId || OwnedSet().Contains(id);

        public static GenesisMatType Selected
        {
            get
            {
                string id = PlayerPrefs.GetString(SelectedKey, ClassicId);
                return Owns(id) ? Get(id) : All[0];
            }
        }

        public static void Grant(string id)
        {
            HashSet<string> owned = OwnedSet();
            if (!owned.Add(id)) return;
            PlayerPrefs.SetString(OwnedKey, string.Join(",", owned));
            PlayerPrefs.Save();
            Changed?.Invoke();
        }

        public static bool Equip(string id)
        {
            if (!Owns(id)) return false;
            PlayerPrefs.SetString(SelectedKey, id);
            PlayerPrefs.Save();
            Changed?.Invoke();
            return true;
        }
    }
}
