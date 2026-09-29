using System;
using UnityEngine;

namespace DuelGenesis.Characters
{
    public enum GenesisGender { Female = 0, Male = 1 }

    /// <summary>Equipment slots, in the same order and numbering as DMO (EquipmentSlotTypes).</summary>
    public enum GenesisSlot
    {
        Hairstyle = 0, Shirt = 1, Pants = 2, Shoes = 3, Head = 4, Back = 5, Arm = 6, Waist = 7, Legs = 8, Neck = 9
    }

    /// <summary>One worn item: an index into that slot's list in DMOCharacterLibrary.json (-1 = nothing).</summary>
    [Serializable]
    public class GenesisEquip
    {
        public GenesisSlot slot;
        public int id = -1;
        public Color[] colors = new Color[0];   // up to 3 tint colours
        public float proportion;                 // DMO's per-item size slider

        public GenesisEquip Clone() => new GenesisEquip { slot = slot, id = id, colors = (Color[])colors.Clone(), proportion = proportion };
    }

    /// <summary>The face-shaping sliders, grouped exactly as DMO's FaceData (values are blendshape weights, 0-100).</summary>
    [Serializable]
    public class GenesisFaceData
    {
        public float[] face = new float[6];
        public float[] jaw = new float[6];
        public float[] cheeks = new float[5];
        public float[] ears = new float[3];
        public float[] nose = new float[10];
        public float[] mouth = new float[5];
        public float[] eyes1 = new float[10];
        public float[] eyes2 = new float[10];
        public float[] iris = new float[4];
        public float[] brows = new float[3];

        public GenesisFaceData Clone() => JsonUtility.FromJson<GenesisFaceData>(JsonUtility.ToJson(this));
    }

    [Serializable]
    public class GenesisFacialHair
    {
        public bool enabled;
        public int upperType = -1;   // moustache style, index into upperFacialHair (-1 = none)
        public int lowerType = -1;   // beard/goatee style, index into lowerFacialHair (-1 = none)
        public Color color = new Color(0.08f, 0.05f, 0.03f, 1f);
    }

    /// <summary>
    /// Everything that describes how a player looks. It mirrors DMO's SaveableAppearanceData field for field,
    /// so looks made in DMO map straight across, and it is plain JSON so it can be saved locally now and
    /// sent to other players when Duel Genesis goes online. Only players have one: there are no NPC looks.
    /// </summary>
    [Serializable]
    public class GenesisAppearance
    {
        public const int CurrentVersion = 1;
        public const int SlotCount = 10;

        public int version = CurrentVersion;
        public string name = "";
        public GenesisGender gender = GenesisGender.Female;
        public GenesisEquip[] equipment = NewEquipment();

        public int leftEyeType, rightEyeType, browType, eyelashType, paintType, eyelinerType;
        public bool lipstick;
        public bool heterochromia;

        public float age;          // "GU Youth Main" morph
        public int bodyType;       // 0-8, DMO body presets (see bodyMinimums/Maximums in the library)
        public float bodyWeight;   // how strongly the body type applies
        public float height;
        public float breastSize;

        public Color skinColor = new Color(0.7725f, 0.4784f, 0.3765f, 1f);
        public Color leftEyeColor = new Color(0.49f, 0.306f, 0.176f, 1f);
        public Color rightEyeColor = new Color(0.49f, 0.306f, 0.176f, 1f);
        public Color leftScleraColor = Color.white;
        public Color rightScleraColor = Color.white;
        public Color paintColor = Color.clear;
        public Color eyelinerColor = Color.clear;
        public Color lipstickColor = Color.clear;
        public Color eyebrowColor = new Color(0.08f, 0.05f, 0.03f, 1f);
        public Color eyelashColor = new Color(0.02f, 0.02f, 0.02f, 1f);

        public GenesisFaceData face = new GenesisFaceData();
        public GenesisFacialHair facialHair = new GenesisFacialHair();

        // ------------------------------------------------------------------ helpers

        public GenesisEquip Get(GenesisSlot slot) => equipment[(int)slot];

        public void Equip(GenesisSlot slot, int id, params Color[] colors)
        {
            GenesisEquip e = equipment[(int)slot];
            e.id = id;
            e.colors = colors ?? new Color[0];
        }

        public GenesisAppearance Clone() => FromJson(ToJson());

        public string ToJson(bool pretty = false) => JsonUtility.ToJson(this, pretty);

        /// <summary>Parses and repairs appearance JSON (from disk or from the network). Never returns null.</summary>
        public static GenesisAppearance FromJson(string json)
        {
            GenesisAppearance a = null;
            if (!string.IsNullOrEmpty(json))
            {
                try { a = JsonUtility.FromJson<GenesisAppearance>(json); }
                catch (Exception e) { Debug.LogWarning("Duel: Genesis could not read an appearance: " + e.Message); }
            }
            if (a == null) return CreateDefault(GenesisGender.Female);
            a.Sanitize();
            return a;
        }

        /// <summary>
        /// Clamps everything into range and fixes array sizes. Data from other players is untrusted once the
        /// game is online, so every loaded appearance goes through this.
        /// </summary>
        public void Sanitize()
        {
            version = CurrentVersion;
            name = string.IsNullOrEmpty(name) ? "" : name.Trim();
            if (name.Length > 20) name = name.Substring(0, 20);
            if (gender != GenesisGender.Female && gender != GenesisGender.Male) gender = GenesisGender.Female;

            var fixedEquip = NewEquipment();
            if (equipment != null)
            {
                foreach (GenesisEquip e in equipment)
                {
                    if (e == null || (int)e.slot < 0 || (int)e.slot >= SlotCount) continue;
                    GenesisEquip clean = e.Clone();
                    clean.id = Mathf.Max(-1, clean.id);
                    if (clean.colors == null) clean.colors = new Color[0];
                    if (clean.colors.Length > 3) Array.Resize(ref clean.colors, 3);
                    for (int i = 0; i < clean.colors.Length; i++) clean.colors[i] = Clamp01(clean.colors[i]);
                    clean.proportion = Mathf.Clamp(clean.proportion, -100f, 100f);
                    fixedEquip[(int)clean.slot] = clean;
                }
            }
            equipment = fixedEquip;

            leftEyeType = Mathf.Max(0, leftEyeType); rightEyeType = Mathf.Max(0, rightEyeType);
            browType = Mathf.Max(0, browType); eyelashType = Mathf.Max(0, eyelashType);
            paintType = Mathf.Max(0, paintType); eyelinerType = Mathf.Max(0, eyelinerType);
            age = Mathf.Clamp(age, 0f, 100f);
            bodyType = Mathf.Clamp(bodyType, 0, 8);
            bodyWeight = Mathf.Clamp(bodyWeight, -100f, 160f);
            height = Mathf.Clamp(height, -100f, 100f);
            breastSize = Mathf.Clamp(breastSize, -100f, 100f);

            skinColor = Clamp01(skinColor); leftEyeColor = Clamp01(leftEyeColor); rightEyeColor = Clamp01(rightEyeColor);
            leftScleraColor = Clamp01(leftScleraColor); rightScleraColor = Clamp01(rightScleraColor);
            paintColor = Clamp01(paintColor); eyelinerColor = Clamp01(eyelinerColor); lipstickColor = Clamp01(lipstickColor);
            eyebrowColor = Clamp01(eyebrowColor); eyelashColor = Clamp01(eyelashColor);
            if (!heterochromia) { rightEyeColor = leftEyeColor; rightEyeType = leftEyeType; }

            if (face == null) face = new GenesisFaceData();
            face.face = Fix(face.face, 6); face.jaw = Fix(face.jaw, 6); face.cheeks = Fix(face.cheeks, 5);
            face.ears = Fix(face.ears, 3); face.nose = Fix(face.nose, 10); face.mouth = Fix(face.mouth, 5);
            face.eyes1 = Fix(face.eyes1, 10); face.eyes2 = Fix(face.eyes2, 10); face.iris = Fix(face.iris, 4); face.brows = Fix(face.brows, 3);

            if (facialHair == null) facialHair = new GenesisFacialHair();
            facialHair.upperType = Mathf.Max(-1, facialHair.upperType);
            facialHair.lowerType = Mathf.Max(-1, facialHair.lowerType);
            facialHair.color = Clamp01(facialHair.color);
        }

        /// <summary>DMO's starting looks (CharacterCreationSystem FemaleData / MaleData).</summary>
        public static GenesisAppearance CreateDefault(GenesisGender gender)
        {
            var a = new GenesisAppearance { gender = gender };
            if (gender == GenesisGender.Female)
            {
                a.leftEyeColor = a.rightEyeColor = new Color(0f, 0.7333f, 0.7922f, 1f);
                a.Equip(GenesisSlot.Hairstyle, 29, new Color(0.094f, 0.047f, 0.031f, 1f), new Color(0.105f, 0.051f, 0.034f, 1f));   // Ponytail
                a.Equip(GenesisSlot.Shirt, 1, Color.white);                                                                        // TG Shirt
                a.Equip(GenesisSlot.Pants, 23, new Color(0.708f, 0.604f, 0.177f, 1f), new Color(0.651f, 0.114f, 0.417f, 1f));   // Cute Shorts
                a.Equip(GenesisSlot.Shoes, 29, Color.white, Color.black);                                                          // Heel Boots
            }
            else
            {
                a.Equip(GenesisSlot.Hairstyle, 12, new Color(0.019f, 0.019f, 0.019f, 1f));   // Male Bob
                a.Equip(GenesisSlot.Shirt, 0, new Color(0.009f, 0.009f, 0.009f, 1f));        // Basic Shirt
                a.Equip(GenesisSlot.Pants, 9, new Color(0.009f, 0.009f, 0.009f, 1f));        // Standard Pants
                a.Equip(GenesisSlot.Shoes, 0, Color.white, Color.black);                     // Gym Shoes
            }
            return a;
        }

        private static GenesisEquip[] NewEquipment()
        {
            var e = new GenesisEquip[SlotCount];
            for (int i = 0; i < SlotCount; i++) e[i] = new GenesisEquip { slot = (GenesisSlot)i };
            return e;
        }

        private static float[] Fix(float[] values, int length)
        {
            var result = new float[length];
            if (values != null)
                for (int i = 0; i < Mathf.Min(length, values.Length); i++)
                    result[i] = float.IsNaN(values[i]) ? 0f : Mathf.Clamp(values[i], -100f, 100f);
            return result;
        }

        private static Color Clamp01(Color c) =>
            new Color(Mathf.Clamp01(c.r), Mathf.Clamp01(c.g), Mathf.Clamp01(c.b), Mathf.Clamp01(c.a));
    }

    /// <summary>The local player's saved look. Stored in PlayerPrefs until accounts exist online.</summary>
    public static class GenesisAppearanceStore
    {
        private const string Key = "DG_APPEARANCE_V1";

        public static bool HasSaved => PlayerPrefs.HasKey(Key);

        public static GenesisAppearance Load() =>
            HasSaved ? GenesisAppearance.FromJson(PlayerPrefs.GetString(Key)) : GenesisAppearance.CreateDefault(GenesisGender.Male);

        public static void Save(GenesisAppearance appearance)
        {
            appearance.Sanitize();
            PlayerPrefs.SetString(Key, appearance.ToJson());
            PlayerPrefs.Save();
        }
    }

    /// <summary>
    /// Which Genesis 9 blendshape each slider drives, recovered from DMO's MorphNames list. The array order
    /// matches <see cref="GenesisFaceData"/> so face.nose[3] drives Nose[3], and so on.
    /// </summary>
    public static class GenesisMorphMap
    {
        public const string Youth = "GU Youth Main";               // DMO converts it to "GU Head" + "GU Body"
        public const string FemaleFigure = "BaseAnimeF_figure_ctrl_Character";
        public const string MaleFigure = "BaseAnimeM_figure_ctrl_Character";
        public const string BreastSize = "body_bs_BreastsLarge";

        /// <summary>Body type presets 1-8 (0 is the plain figure); bodyWeight sets how strongly they apply.</summary>
        public static readonly string[] BodyTypes =
        {
            "", "body_bs_BodyOlder", "body_bs_BodyLithe", "body_bs_BodyEmaciated", "body_bs_BodyHeavy",
            "body_bs_BodyStocky", "body_bs_BodyPortly", "body_bs_BodyPearFigure", "body_bs_BodyVoluptuous"
        };

        public static readonly string[] MuscleByGender = { "body_bs_AnimeBodyMuscularFeminine", "body_bs_AnimeBodyMuscularMasculine" };

        public static readonly string[] Face = { "head_bs_FaceYoung", "head_bs_FaceSquare", "head_bs_FaceOlder", "head_bs_FaceHeart", "head_bs_FaceSize", "head_bs_CraniumHeight" };
        public static readonly string[] Jaw = { "BaseAnime_head_bs_JawAngular", "head_bs_JawWholeHeight", "head_bs_JawWholeWidth", "head_bs_JawWholeSize", "BaseAnime_head_bs_ChinPoint", "head_bs_ChinHeight" };
        public static readonly string[] Cheeks = { "head_bs_CheektoJawSize", "head_bs_CheekSlack", "head_bs_CheekSize", "head_bs_CheekSink", "head_bs_CheekboneThin" };
        public static readonly string[] Ears = { "head_bs_EarSizeUpper", "head_bs_EarSizeLower", "head_bs_EarHeight" };
        public static readonly string[] Nose =
        {
            "BaseAnime_head_bs_NoseSmall", "head_bs_NoseBaseHeight", "head_bs_NoseWholeWidth", "head_bs_NoseWholeSize", "head_bs_NoseWholeDepth",
            "head_bs_NoseTipHeight", "head_bs_NoseTipDepth", "head_bs_NoseBridgeSlope", "head_bs_NoseBridgeDefinition", "head_bs_NosePhiltrumWidth"
        };
        public static readonly string[] Mouth = { "head_bs_MouthSize", "facs_ctrl_MouthWiden", "head_bs_LipThin", "head_bs_MouthHeight", "facs_bs_MouthLipsCat" };
        public static readonly string[] Eyes1 =
        {
            "head_bs_EyeWholeSize", "head_bs_EyeWholeWidth", "head_bs_EyeWholeHeight", "head_bs_EyeWholeAngle", "head_bs_EyeInnerHeightSize",
            "BaseAnime_head_bs_EyeHeightSize", "BaseAnime_head_bs_EyeWidthSize", "BaseAnime_head_cbs_EyeUpperCurveHeight",
            "BaseAnime_head_cbs_EyeLowerCurveHeight", "head_bs_EyeSwoopUp"
        };
        public static readonly string[] Eyes2 =
        {
            "head_bs_EyeOuterArcDown", "head_bs_EyeUpperPeak", "head_bs_EyeUpperCurveBalance", "head_bs_EyeSocketPuffyUpper", "head_bs_EyeSocketPuffyLower",
            "head_bs_EyeSocketInnerDepth", "head_bs_EyelidFoldToward", "head_bs_EyelidAngleLowerOuter", "head_bs_EyelidCreaseUpperHeight",
            "head_bs_EyelidCreaseLowerHeight"
        };
        public static readonly string[] Iris = { "head_ctrl_ToonIrisDilate", "head_ctrl_ToonIrisStretch", "head_bs_EyePupilSlit", "facs_bs_EyePupilsDilate" };
        public static readonly string[] Brows = { "head_bs_BrowWholeWidth", "head_bs_BrowWholeSize", "head_bs_BrowWholeHeight" };

        /// <summary>Clothing fit shapes set from each item's per-gender fit values (ClothingHelper).</summary>
        public const string ScaleTorso = "Scale Torso", ScaleThighs = "Scale Thighs", ScaleGroin = "Scale Groin",
                            ScaleLegs = "Scale Legs", ScaleArms = "Scale Arms", HideFeet = "Hide Feet", HideThighs = "Hide Thighs";

        /// <summary>Pairs each face slider group with its blendshape names (for the creator UI and the builder).</summary>
        public static (string label, string[] morphs, Func<GenesisFaceData, float[]> values)[] FaceGroups =>
            new (string, string[], Func<GenesisFaceData, float[]>)[]
            {
                ("Face", Face, f => f.face), ("Jaw", Jaw, f => f.jaw), ("Cheeks", Cheeks, f => f.cheeks), ("Ears", Ears, f => f.ears),
                ("Nose", Nose, f => f.nose), ("Mouth", Mouth, f => f.mouth), ("Eyes", Eyes1, f => f.eyes1), ("Eye Shape", Eyes2, f => f.eyes2),
                ("Iris", Iris, f => f.iris), ("Brows", Brows, f => f.brows)
            };
    }
}
