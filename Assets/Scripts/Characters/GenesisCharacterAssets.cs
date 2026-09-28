using System;
using System.Collections.Generic;
using UnityEngine;

namespace DuelGenesis.Characters
{
    /// <summary>
    /// The imported character pieces the builder needs at runtime: the Genesis 9 base figure, the locomotion
    /// animator and every wardrobe item imported so far. Filled in by Duel Genesis > Characters > Import.
    /// Lives in Resources/DuelGenesis/Characters so builds include it.
    /// </summary>
    [CreateAssetMenu(menuName = "Duel Genesis/Character Assets")]
    public sealed class GenesisCharacterAssets : ScriptableObject
    {
        [Serializable]
        public class Item
        {
            public GenesisSlot slot;
            public int id;
            public string name;
            public GameObject prefab;
        }

        public GameObject basePrefab;
        public RuntimeAnimatorController locomotion;
        public List<Item> items = new List<Item>();

        public GameObject Find(GenesisSlot slot, int id)
        {
            foreach (Item i in items)
                if (i.slot == slot && i.id == id) return i.prefab;
            return null;
        }

        private static GenesisCharacterAssets _instance;
        public static GenesisCharacterAssets Instance =>
            _instance != null ? _instance : _instance = Resources.Load<GenesisCharacterAssets>("DuelGenesis/Characters/GenesisCharacterAssets");

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetForPlaySession() => _instance = null;
    }
}
