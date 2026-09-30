#if UNITY_EDITOR
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DuelGenesis.EditorTools
{
    /// <summary>
    /// Sets up four Sketchfab models (all CC BY 4.0, credits in Assets/ThirdParty/Sketchfab/CREDITS.txt) as URP prefabs:
    ///   - "Cherry Blossom Trees" by Jagobo: three sakura, each with two LODs, alpha-clipped two-sided blossom cards,
    ///   - "HGU_Chinatown" by s_sugie: seven Chinatown shop-houses split from the original street row (fronts face +Z),
    ///   - "Country shop" by suvorovtim: a small roadside store on its own forecourt slab (street side +Z),
    ///   - "Tokyo Tower/Japanese Radio Tower" by zebadiahsantos (its earth plinth removed so the legs stand on the ground).
    /// Prefabs go to Assets/ThirdParty/Sketchfab/{Model}/Prefabs; the World menu items place them in the city.
    /// </summary>
    public static class GenesisLandmarkModels
    {
        private const string Root = GenesisSketchfabModels.Root;
        public const string CherryFolder = Root + "/CherryBlossom";
        public const string ChinatownFolder = Root + "/Chinatown";
        public const string CountryShopPrefab = Root + "/CountryShop/Prefabs/Country Shop.prefab";
        public const string TokyoTowerPrefab = Root + "/TokyoTower/Prefabs/Tokyo Tower.prefab";
        public static readonly string[] CherryIds = { "A", "B", "C" };
        public const int ChinatownHouses = 7;

        /// <summary>Source units per metre: the trees and houses were exported at roughly 1 unit = 10 cm / 3 cm.</summary>
        private const float CherryScale = 0.1f, ChinatownScale = 32f;
        public const float CountryShopWidth = 20f;    // the forecourt slab, metres
        public const float TokyoTowerHeight = 110f;   // metres, antenna tip

        [MenuItem("Duel Genesis/Production Assets/Set Up Cherry, Chinatown, Country Shop + Tokyo Tower Models")]
        public static void SetUpAll()
        {
            int made = 0;
            made += SetUpCherries();
            made += SetUpChinatown();
            GenesisSketchfabModels.SetUp("CountryShop");
            made += Prefab($"{Root}/CountryShop/CountryShop.obj", CountryShopPrefab, "Country Shop", fitWidth: CountryShopWidth, meshCollider: true);
            GenesisSketchfabModels.SetUp("TokyoTower");
            made += Prefab($"{Root}/TokyoTower/TokyoTower.obj", TokyoTowerPrefab, "Tokyo Tower", fitHeight: TokyoTowerHeight, meshCollider: true);
            AssetDatabase.SaveAssets();
            Debug.Log($"Duel: Genesis set up the sakura, Chinatown, country shop and Tokyo Tower models: {made} prefabs. " +
                      "Now run World > 18-21 to put them in the city, then World > 17 to re-bake the minimap.");
        }

        // ------------------------------------------------------------------ cherry trees

        private static int SetUpCherries()
        {
            string mats = EnsureMaterials(CherryFolder);
            NormalMap($"{CherryFolder}/Cherry_bark_normal.png");
            var fti = AssetImporter.GetAtPath($"{CherryFolder}/Cherry_foliage.png") as TextureImporter;
            if (fti != null && !fti.alphaIsTransparency) { fti.alphaIsTransparency = true; fti.mipMapsPreserveCoverage = true; fti.alphaTestReferenceValue = 0.4f; fti.SaveAndReimport(); }

            Material bark = Lit($"{mats}/Cherry Bark.mat", Tex($"{CherryFolder}/Cherry_bark.png"), Tex($"{CherryFolder}/Cherry_bark_normal.png"), 0.12f, clip: false, twoSided: false);
            Material bloom = Lit($"{mats}/Cherry Blossom.mat", Tex($"{CherryFolder}/Cherry_foliage.png"), null, 0.25f, clip: true, twoSided: true);

            int made = 0;
            foreach (string id in CherryIds)
            {
                var lods = new GameObject[2];
                for (int l = 0; l < 2; l++)
                {
                    string obj = $"{CherryFolder}/Cherry_{id}_lod{l}.obj";
                    Remap(obj, ("bark_0", bark), ("foliage_1", bloom));
                    lods[l] = AssetDatabase.LoadAssetAtPath<GameObject>(obj);
                }
                if (lods[0] == null) { Debug.LogWarning($"Duel: Genesis: missing {CherryFolder}/Cherry_{id}_lod0.obj"); continue; }

                var root = new GameObject("Sakura " + id);
                var renderers = new Renderer[2][];
                for (int l = 0; l < 2; l++)
                {
                    if (lods[l] == null) { renderers[l] = new Renderer[0]; continue; }
                    var inst = (GameObject)PrefabUtility.InstantiatePrefab(lods[l], root.transform);
                    inst.name = "LOD" + l;
                    inst.transform.localScale = Vector3.one * CherryScale;
                    renderers[l] = inst.GetComponentsInChildren<Renderer>();
                    foreach (var r in renderers[l]) r.shadowCastingMode = l == 0 ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
                }
                // Stand the trunk on the origin.
                Bounds b = GenesisWorldBuilder.RendererBounds(root);
                foreach (Transform c in root.transform) c.localPosition -= new Vector3(b.center.x, b.min.y, b.center.z);
                var group = root.AddComponent<LODGroup>();
                group.SetLODs(new[] { new LOD(0.3f, renderers[0]), new LOD(0.012f, renderers[1]) });
                group.RecalculateBounds();
                var trunk = root.AddComponent<CapsuleCollider>();
                trunk.radius = 0.3f;
                trunk.height = 3f;
                trunk.center = new Vector3(0f, 1.5f, 0f);
                made += Save(root, $"{CherryFolder}/Prefabs/Sakura {id}.prefab");
            }
            return made;
        }

        /// <summary>The realistic sakura prefabs (empty until Set Up has run).</summary>
        public static GameObject[] Cherries() =>
            CherryIds.Select(id => AssetDatabase.LoadAssetAtPath<GameObject>($"{CherryFolder}/Prefabs/Sakura {id}.prefab")).Where(g => g != null).ToArray();

        // ------------------------------------------------------------------ Chinatown houses

        private static int SetUpChinatown()
        {
            string mats = EnsureMaterials(ChinatownFolder);
            NormalMap($"{ChinatownFolder}/Chinatown_normal.png");
            Material house = Lit($"{mats}/Chinatown House.mat", Tex($"{ChinatownFolder}/Chinatown_base.png"), Tex($"{ChinatownFolder}/Chinatown_normal.png"), 0.22f, clip: false, twoSided: true);
            int made = 0;
            for (int i = 1; i <= ChinatownHouses; i++)
            {
                string obj = $"{ChinatownFolder}/Chinatown_House_{i}.obj";
                Remap(obj, ("Material.001_0", house));
                made += Prefab(obj, ChinatownHouse(i), "Chinatown House " + i, scale: ChinatownScale, boxCollider: true);
            }
            return made;
        }

        public static string ChinatownHouse(int i) => $"{ChinatownFolder}/Prefabs/Chinatown House {i}.prefab";

        // ------------------------------------------------------------------ helpers

        /// <summary>
        /// Saves a prefab of a model standing on y = 0, centred on the origin, front +Z. Size by a fixed scale, a target height
        /// or a target footprint width; optional box or mesh colliders.
        /// </summary>
        private static int Prefab(string modelPath, string prefabPath, string name, float scale = 0f, float fitHeight = 0f, float fitWidth = 0f,
                                  bool boxCollider = false, bool meshCollider = false)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            if (model == null) { Debug.LogWarning("Duel: Genesis: missing " + modelPath); return 0; }
            var root = new GameObject(name);
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(model, root.transform);
            inst.name = "Model";
            foreach (var c in inst.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(c);
            Bounds b = GenesisWorldBuilder.RendererBounds(inst);
            float s = scale > 0f ? scale
                    : fitHeight > 0f && b.size.y > 1e-5f ? fitHeight / b.size.y
                    : fitWidth > 0f && Mathf.Max(b.size.x, b.size.z) > 1e-5f ? fitWidth / Mathf.Max(b.size.x, b.size.z)
                    : 1f;
            inst.transform.localScale = Vector3.one * s;
            b = GenesisWorldBuilder.RendererBounds(inst);
            inst.transform.localPosition -= new Vector3(b.center.x, b.min.y, b.center.z);
            b = GenesisWorldBuilder.RendererBounds(inst);
            if (boxCollider)
            {
                var box = root.AddComponent<BoxCollider>();
                box.center = b.center;
                box.size = b.size;
            }
            if (meshCollider)
                foreach (var mf in inst.GetComponentsInChildren<MeshFilter>())
                    if (mf.sharedMesh != null) mf.gameObject.AddComponent<MeshCollider>().sharedMesh = mf.sharedMesh;
            foreach (var r in inst.GetComponentsInChildren<MeshRenderer>())
                GameObjectUtility.SetStaticEditorFlags(r.gameObject, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic);
            return Save(root, prefabPath);
        }

        private static int Save(GameObject root, string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return 1;
        }

        private static string EnsureMaterials(string folder)
        {
            if (!AssetDatabase.IsValidFolder(folder + "/Materials")) AssetDatabase.CreateFolder(folder, "Materials");
            return folder + "/Materials";
        }

        private static Texture2D Tex(string path) => AssetDatabase.LoadAssetAtPath<Texture2D>(path);

        private static void NormalMap(string path)
        {
            var ti = AssetImporter.GetAtPath(path) as TextureImporter;
            if (ti != null && ti.textureType != TextureImporterType.NormalMap) { ti.textureType = TextureImporterType.NormalMap; ti.SaveAndReimport(); }
        }

        private static Material Lit(string path, Texture2D baseMap, Texture2D normal, float smooth, bool clip, bool twoSided)
        {
            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(lit); AssetDatabase.CreateAsset(m, path); }
            m.shader = lit;
            m.SetColor("_BaseColor", Color.white);
            m.SetTexture("_BaseMap", baseMap);
            m.SetFloat("_Smoothness", smooth);
            m.SetFloat("_Metallic", 0f);
            if (normal != null) { m.SetTexture("_BumpMap", normal); m.EnableKeyword("_NORMALMAP"); }
            else m.DisableKeyword("_NORMALMAP");
            m.SetFloat("_AlphaClip", clip ? 1f : 0f);
            m.SetFloat("_Cutoff", 0.4f);
            if (clip) m.EnableKeyword("_ALPHATEST_ON"); else m.DisableKeyword("_ALPHATEST_ON");
            m.SetFloat("_Cull", twoSided ? 0f : 2f);
            m.doubleSidedGI = twoSided;
            m.enableInstancing = true;
            EditorUtility.SetDirty(m);
            return m;
        }

        private static void Remap(string objPath, params (string name, Material mat)[] maps)
        {
            var importer = AssetImporter.GetAtPath(objPath) as ModelImporter;
            if (importer == null) return;
            importer.globalScale = 1f;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
            importer.isReadable = false;
            importer.indexFormat = ModelImporterIndexFormat.Auto;
            foreach (var (name, mat) in maps) importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), name), mat);
            importer.SaveAndReimport();
        }
    }
}
#endif
