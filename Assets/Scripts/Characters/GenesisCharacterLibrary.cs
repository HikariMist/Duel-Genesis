using System;
using UnityEngine;

namespace DuelGenesis.Characters
{
    /// <summary>
    /// Reads Resources/DuelGenesis/Characters/DMOCharacterLibrary.json: every hair, clothing and accessory item
    /// from DMO's character creator, in DMO's slot order and ID order, with its fit values. Prefab and texture
    /// references are GUIDs from the AssetRipper export; the character importer resolves them.
    /// </summary>
    [Serializable]
    public class GenesisCharacterLibrary
    {
        [Serializable] public class Fit { public float torso, thighs, groin, legs, arms, hideFeet; }
        [Serializable] public class MorphOffset { public string morph; public float value; }
        [Serializable] public class MorphSet { public string[] morphs; }
        [Serializable] public class Paint { public string textureGuid; public bool multiplySecondary; }

        [Serializable]
        public class Item
        {
            public int id;
            public string name;
            public string prefabGuid;
            public string iconGuid;
            public Fit female, male;
            public MorphOffset[] femaleMorphOffsets, maleMorphOffsets;
            public bool independent;
            public Vector3 independentBasePos, independentYouthPos, independentBaseScale, independentYouthScale;
            public int proportionType;
            public bool locked, disableSize, disableExpand, revealing;

            public Fit FitFor(GenesisGender g) => g == GenesisGender.Male ? male : female;
            public MorphOffset[] OffsetsFor(GenesisGender g) => g == GenesisGender.Male ? maleMorphOffsets : femaleMorphOffsets;
        }

        [Serializable] public class Slot { public string slot; public Item[] items; }

        public string source;
        public Slot[] slots;
        public string[] morphNames;
        public MorphSet[] upperFacialHair, lowerFacialHair;
        public string beardPrefabGuid;
        public Paint[] facePaints, eyelinerPaints;
        public string[] eyeTypeTextureGuids, eyelashTextureGuids, eyebrowTextureGuids;
        public string lipstickTextureGuid, blankTextureGuid, defaultAvatarGuid;
        public float[] femaleBodyTypeShoulderOffsets, maleBodyTypeShoulderOffsets;
        public float[] bodyMinimums, bodyMaximums;
        public float targetHeight, heightOffset;

        public Item[] ItemsIn(GenesisSlot slot) =>
            slots != null && (int)slot < slots.Length ? slots[(int)slot].items : Array.Empty<Item>();

        public Item Find(GenesisSlot slot, int id)
        {
            Item[] items = ItemsIn(slot);
            return id >= 0 && id < items.Length ? items[id] : null;
        }

        private static GenesisCharacterLibrary _instance;

        public static GenesisCharacterLibrary Instance
        {
            get
            {
                if (_instance != null) return _instance;
                TextAsset json = Resources.Load<TextAsset>("DuelGenesis/Characters/DMOCharacterLibrary");
                _instance = json != null ? JsonUtility.FromJson<GenesisCharacterLibrary>(json.text) : new GenesisCharacterLibrary();
                return _instance;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetForPlaySession() => _instance = null;
    }
}
