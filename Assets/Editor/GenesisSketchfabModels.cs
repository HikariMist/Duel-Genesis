#if UNITY_EDITOR
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace DuelGenesis.EditorTools
{
    /// <summary>
    /// Sets up the Sketchfab models (converted to OBJ by Tools/glb_to_obj.py) in Assets/ThirdParty/Sketchfab:
    /// builds URP Lit materials from each model's *_materials.json (base colour/texture, emission, alpha,
    /// two-sided) and remaps the OBJ's materials to them. Credits: Assets/ThirdParty/Sketchfab/CREDITS.txt.
    /// </summary>
    public static class GenesisSketchfabModels
    {
        public const string Root = "Assets/ThirdParty/Sketchfab";
        public static readonly string[] Models = { "DragonGateInn", "SciFiBar", "PotOfGreed", "MagicalHats" };

        [System.Serializable] private class MatDef { public string name; public float[] color; public string baseTex; public float[] emissive; public string emissiveTex; public string alpha; public float cutoff; public bool doubleSided; public float smoothness; public float metallic; }
        [System.Serializable] private class MatFile { public MatDef[] materials; }

        [MenuItem("Duel Genesis/Production Assets/Set Up Sketchfab Models (card shop)")]
        public static void SetUpAll()
        {
            foreach (string m in Models) SetUp(m);
            AssetDatabase.SaveAssets();
        }

        [MenuItem("Duel Genesis/DEV/Log Card Shop Inn Parts")]
        public static void LogInnParts()
        {
            GameObject inn = GameObject.Find("Dragon Gate Inn (by daydev, CC BY 4.0)");
            if (inn == null) { Debug.LogWarning("no inn"); return; }
            var sb = new System.Text.StringBuilder();
            foreach (Renderer r in inn.GetComponentsInChildren<Renderer>(true))
                sb.AppendLine($"{r.name} | active {r.gameObject.activeInHierarchy} | mats {string.Join(",", r.sharedMaterials.Select(m => m != null ? m.name : "null"))} | min {r.bounds.min} max {r.bounds.max}");
            File.WriteAllText("Logs/DG-InnParts.txt", sb.ToString());
            Debug.Log("Duel: Genesis wrote Logs/DG-InnParts.txt");
        }

        public static GameObject Load(string model) => AssetDatabase.LoadAssetAtPath<GameObject>($"{Root}/{model}/{model}.obj");

        /// <summary>A material asset file name Unity accepts (no leading dot, no path characters).</summary>
        private static string SafeName(string name)
        {
            foreach (char c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
            name = name.TrimStart('.', ' ');
            return string.IsNullOrEmpty(name) ? "Material" : name;
        }

        public static void SetUp(string model)
        {
            string folder = $"{Root}/{model}";
            string json = Path.Combine(Directory.GetParent(Application.dataPath).FullName, folder, model + "_materials.json");
            if (!File.Exists(json)) { Debug.LogWarning("Duel: Genesis: missing " + json); return; }
            MatFile file = JsonUtility.FromJson<MatFile>(File.ReadAllText(json));
            string matFolder = folder + "/Materials";
            if (!AssetDatabase.IsValidFolder(matFolder)) AssetDatabase.CreateFolder(folder, "Materials");

            var importer = AssetImporter.GetAtPath($"{folder}/{model}.obj") as ModelImporter;
            if (importer == null) { Debug.LogWarning($"Duel: Genesis: {model}.obj is not imported yet."); return; }
            importer.globalScale = 1f;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
            importer.isReadable = false;
            importer.indexFormat = ModelImporterIndexFormat.Auto;

            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            int made = 0;
            foreach (MatDef d in file.materials)
            {
                string path = $"{matFolder}/{SafeName(d.name)}.mat";
                var m = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (m == null) { m = new Material(lit); AssetDatabase.CreateAsset(m, path); }
                m.shader = lit;
                Color c = d.color != null && d.color.Length >= 3 ? new Color(d.color[0], d.color[1], d.color[2], d.color.Length > 3 ? d.color[3] : 1f) : Color.white;
                Texture2D baseTex = string.IsNullOrEmpty(d.baseTex) ? null : AssetDatabase.LoadAssetAtPath<Texture2D>($"{folder}/{d.baseTex}");
                m.SetColor("_BaseColor", c);
                m.SetTexture("_BaseMap", baseTex);
                m.SetFloat("_Smoothness", Mathf.Clamp(d.smoothness, 0f, 0.9f));
                m.SetFloat("_Metallic", d.metallic);
                m.SetFloat("_Cull", d.doubleSided ? 0f : 2f);
                m.doubleSidedGI = d.doubleSided;

                // Alpha: cut-out for leaves and decals (cheaper and sorts correctly).
                bool clip = d.alpha == "MASK" || d.alpha == "BLEND";
                m.SetFloat("_AlphaClip", clip ? 1f : 0f);
                m.SetFloat("_Cutoff", d.alpha == "MASK" ? d.cutoff : 0.4f);
                if (clip) m.EnableKeyword("_ALPHATEST_ON"); else m.DisableKeyword("_ALPHATEST_ON");

                // Emission: neon signs and lights.
                Texture2D emTex = string.IsNullOrEmpty(d.emissiveTex) ? null : AssetDatabase.LoadAssetAtPath<Texture2D>($"{folder}/{d.emissiveTex}");
                Color e = d.emissive != null && d.emissive.Length >= 3 ? new Color(d.emissive[0], d.emissive[1], d.emissive[2]) : Color.black;
                bool emits = emTex != null || e.maxColorComponent > 0.001f;
                if (emits)
                {
                    float boost = emTex != null ? 2.5f : Mathf.Max(3f, 1f / Mathf.Max(0.05f, e.maxColorComponent));
                    m.EnableKeyword("_EMISSION");
                    m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                    m.SetTexture("_EmissionMap", emTex);
                    m.SetColor("_EmissionColor", (emTex != null ? Color.white : e) * boost);
                }
                else
                {
                    m.DisableKeyword("_EMISSION");
                    m.SetColor("_EmissionColor", Color.black);
                }
                m.enableInstancing = true;
                EditorUtility.SetDirty(m);
                importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), d.name), m);
                made++;
            }
            importer.SaveAndReimport();
            Debug.Log($"Duel: Genesis set up {model}: {made} URP materials (credits in {Root}/CREDITS.txt).");
        }
    }
}
#endif
