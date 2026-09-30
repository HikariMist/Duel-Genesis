#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

namespace DuelGenesis.EditorTools
{
    /// <summary>Turns the skateboard OBJ (Assets/ThirdParty/Sketchfab/Skateboard, CC BY 4.0 marcos.driguez) into the
    /// Resources prefab the game spawns under the player: URP material with its colour and normal maps, 0.82 m long.</summary>
    public static class GenesisSkateboardSetup
    {
        private const string Src = "Assets/ThirdParty/Sketchfab/Skateboard";
        private const string OutDir = "Assets/Resources/DuelGenesis/Skateboard";

        [MenuItem("Duel Genesis/Production Assets/Set Up Skateboard")]
        public static void SetUp()
        {
            var importer = AssetImporter.GetAtPath(Src + "/Skateboard.obj") as ModelImporter;
            if (importer == null) { Debug.LogWarning("Duel: Genesis: " + Src + "/Skateboard.obj is missing."); return; }
            Directory.CreateDirectory(OutDir);

            string norPath = Src + "/Skateboard_tex2.png";
            var nti = AssetImporter.GetAtPath(norPath) as TextureImporter;
            if (nti != null && nti.textureType != TextureImporterType.NormalMap) { nti.textureType = TextureImporterType.NormalMap; nti.SaveAndReimport(); }

            string matPath = Src + "/Skateboard.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            if (m == null) { m = new Material(lit); AssetDatabase.CreateAsset(m, matPath); }
            m.shader = lit;
            m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Src + "/Skateboard_tex0.png"));
            m.SetColor("_BaseColor", Color.white);
            m.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(norPath));
            m.EnableKeyword("_NORMALMAP");
            m.SetFloat("_Metallic", 0.2f);
            m.SetFloat("_Smoothness", 0.35f);
            m.SetFloat("_Cull", 0f);
            m.enableInstancing = true;
            EditorUtility.SetDirty(m);

            importer.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
            importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), "SKATE_TEXTURE_0"), m);
            importer.importTangents = ModelImporterTangents.CalculateMikk;
            importer.SaveAndReimport();

            var model = AssetDatabase.LoadAssetAtPath<GameObject>(Src + "/Skateboard.obj");
            var root = new GameObject("Skateboard");
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(model, root.transform);
            foreach (var r in inst.GetComponentsInChildren<Renderer>()) r.sharedMaterial = m;
            Bounds b = GenesisWorldBuilder.RendererBounds(inst);
            float length = Mathf.Max(b.size.x, b.size.z);
            inst.transform.localScale = Vector3.one * (0.82f / Mathf.Max(0.001f, length));
            if (b.size.x > b.size.z) inst.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);   // long axis along +Z
            b = GenesisWorldBuilder.RendererBounds(inst);
            inst.transform.localPosition -= new Vector3(b.center.x, b.min.y, b.center.z);
            PrefabUtility.SaveAsPrefabAsset(root, OutDir + "/Skateboard.prefab");
            Object.DestroyImmediate(root);
            AssetDatabase.SaveAssets();
            Debug.Log($"Duel: Genesis set up the skateboard prefab ({b.size.x:0.00} x {b.size.y:0.00} x {b.size.z:0.00} m) in {OutDir}.");
        }
    }
}
#endif
