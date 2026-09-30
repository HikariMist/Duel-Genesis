#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

namespace DuelGenesis.EditorTools
{
    /// <summary>
    /// Pot of Greed (Graveyart) and Magical Hats (Anthony Yanez), both CC BY 4.0, from Assets/ThirdParty/Sketchfab.
    /// Builds their URP materials (colour, normal and glow maps) and the Resources prefabs the game loads:
    ///   CardModels/55144522 (Pot of Greed) and CardModels/81210420 (Magical Hats) for the duel animations,
    ///   DuelGenesis/Models/MagicalHat (one hat, to cover a monster) and DuelGenesis/Pets/PotOfGreed (the pet).
    /// </summary>
    public static class GenesisCardModelsSetup
    {
        private const string Root = GenesisSketchfabModels.Root;

        [MenuItem("Duel Genesis/Production Assets/Set Up Pot of Greed + Magical Hats Models")]
        public static void SetUp()
        {
            GenesisSketchfabModels.SetUp("PotOfGreed");
            GenesisSketchfabModels.SetUp("MagicalHats");

            // Normal and glow maps the OBJ route doesn't carry.
            Normal("PotOfGreed", "blinn1SG_0", "PotOfGreed_tex2.png");
            Normal("PotOfGreed", "blinn2SG_1", "PotOfGreed_tex5.png");
            Normal("MagicalHats", "01_-_Default_0", "MagicalHats_tex1.png");
            Normal("MagicalHats", "material_1", "MagicalHats_tex3.png");

            // The single hat shares the Magical Hats materials.
            var hatImporter = AssetImporter.GetAtPath($"{Root}/MagicalHats/MagicalHat.obj") as ModelImporter;
            if (hatImporter != null)
            {
                hatImporter.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
                foreach (string m in new[] { "Magical_Hats_3", "02_-_Default_4" })
                {
                    var mat = AssetDatabase.LoadAssetAtPath<Material>($"{Root}/MagicalHats/Materials/{m}.mat");
                    if (mat != null) hatImporter.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), m), mat);
                }
                hatImporter.SaveAndReimport();
            }

            int made = 0;
            made += Prefab($"{Root}/PotOfGreed/PotOfGreed.obj", "Assets/Resources/CardModels/55144522.prefab", "Pot of Greed", 1f);
            made += Prefab($"{Root}/MagicalHats/MagicalHats.obj", "Assets/Resources/CardModels/81210420.prefab", "Magical Hats", 1f);
            made += Prefab($"{Root}/MagicalHats/MagicalHat.obj", "Assets/Resources/DuelGenesis/Models/MagicalHat.prefab", "Magical Hat", 1f);
            made += Prefab($"{Root}/PotOfGreed/PotOfGreed.obj", "Assets/Resources/DuelGenesis/Pets/PotOfGreed.prefab", "Pot of Greed Pet", 0.62f);
            AssetDatabase.SaveAssets();
            Debug.Log($"Duel: Genesis set up the Pot of Greed and Magical Hats models: {made} prefabs (credits in {Root}/CREDITS.txt).");
        }

        private static void Normal(string model, string matName, string tex)
        {
            string texPath = $"{Root}/{model}/{tex}";
            var ti = AssetImporter.GetAtPath(texPath) as TextureImporter;
            if (ti == null) return;
            if (ti.textureType != TextureImporterType.NormalMap) { ti.textureType = TextureImporterType.NormalMap; ti.SaveAndReimport(); }
            var m = AssetDatabase.LoadAssetAtPath<Material>($"{Root}/{model}/Materials/{matName}.mat");
            if (m == null) return;
            m.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(texPath));
            m.EnableKeyword("_NORMALMAP");
            EditorUtility.SetDirty(m);
        }

        /// <summary>Saves a prefab whose model stands on y = 0, centred, facing +Z. height 1 keeps the source size normalised to 1 m.</summary>
        private static int Prefab(string modelPath, string prefabPath, string name, float height)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            if (model == null) { Debug.LogWarning("Duel: Genesis: missing " + modelPath); return 0; }
            Directory.CreateDirectory(Path.GetDirectoryName(prefabPath));
            var root = new GameObject(name);
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(model, root.transform);
            inst.name = "Model";
            foreach (var c in inst.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(c);
            Bounds b = GenesisWorldBuilder.RendererBounds(inst);
            if (b.size.y > 0.0001f) inst.transform.localScale *= height / b.size.y;
            b = GenesisWorldBuilder.RendererBounds(inst);
            inst.transform.localPosition -= new Vector3(b.center.x, b.min.y, b.center.z);
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            Object.DestroyImmediate(root);
            return 1;
        }
    }
}
#endif
