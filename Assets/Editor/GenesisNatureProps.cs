#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DuelGenesis.EditorTools
{
    /// <summary>
    /// Realistic (photo-scanned, CC0) Poly Haven rocks, shrubs, flowers and a stump, plus ground textures, replacing
    /// the low-poly Kenney Nature Kit pieces and the garden's placeholder stones.
    ///   Downloads > Poly Haven Nature Props + Ground Textures (CC0)
    ///   Production Assets > Set Up Poly Haven Nature Props   (URP materials, prefabs with LOD culling)
    ///   World > 14. Swap Low-Poly Nature For Realistic Props (+ textured grass ground)
    /// </summary>
    public static class GenesisNatureProps
    {
        public const string Folder = "Assets/ThirdParty/PolyHaven/Nature";
        public const string PrefabFolder = "Assets/Art/Generated/NatureProps";
        public const string GroundFolder = "Assets/ThirdParty/PolyHaven/Ground";

        public static readonly string[] Rocks = { "rock_moss_set_01", "rock_moss_set_02", "rock_07", "rock_09", "stone_01", "namaqualand_boulder_02" };
        public static readonly string[] Shrubs = { "shrub_02", "shrub_03", "shrub_04", "fern_02" };
        public static readonly string[] Flowers = { "periwinkle_plant", "flower_ursinia" };
        public static readonly string[] Stumps = { "tree_stump_01" };
        public static readonly string[] Ground = { "grass_ground", "gravel_ground_01", "forest_ground_04" };
        public static IEnumerable<string> All => Rocks.Concat(Shrubs).Concat(Flowers).Concat(Stumps);

        [MenuItem("Duel Genesis/Downloads/Poly Haven Nature Props + Ground Textures (CC0)")]
        public static void Download()
        {
            string project = Directory.GetParent(Application.dataPath).FullName;
            int files = 0, failed = 0;
            var ids = All.ToList();
            try
            {
                for (int t = 0; t < ids.Count; t++)
                {
                    string id = ids[t];
                    EditorUtility.DisplayProgressBar("Duel: Genesis", "Downloading " + id, t / (float)(ids.Count + Ground.Length));
                    string json = GenesisAssetDownloads.Fetch("https://api.polyhaven.com/files/" + id);
                    if (json == null) { failed++; continue; }
                    int fbx = json.IndexOf("\"fbx\"", System.StringComparison.Ordinal);
                    int k1 = fbx < 0 ? -1 : json.IndexOf("\"1k\"", fbx, System.StringComparison.Ordinal);
                    if (k1 < 0) { failed++; continue; }
                    int k2 = json.IndexOf("\"2k\"", k1 + 4, System.StringComparison.Ordinal);
                    string block = json.Substring(k1, (k2 > k1 ? k2 : json.Length) - k1);
                    string dir = Path.Combine(project, Folder, id);
                    Directory.CreateDirectory(dir);
                    var main = Regex.Match(block, "\"url\":\\s*\"([^\"]+\\.fbx)\"");
                    if (!main.Success) { failed++; continue; }
                    if (GenesisAssetDownloads.Save(main.Groups[1].Value, Path.Combine(dir, id + ".fbx"))) files++; else failed++;
                    foreach (Match inc in Regex.Matches(block, "\"([^\"]+\\.(?:jpg|png|exr))\":\\s*\\{[^}]*?\"url\":\\s*\"([^\"]+)\""))
                    {
                        string dest = Path.Combine(dir, inc.Groups[1].Value.Replace('/', Path.DirectorySeparatorChar));
                        Directory.CreateDirectory(Path.GetDirectoryName(dest));
                        if (GenesisAssetDownloads.Save(inc.Groups[2].Value, dest)) files++; else failed++;
                    }
                }
                for (int g = 0; g < Ground.Length; g++)
                {
                    string id = Ground[g];
                    EditorUtility.DisplayProgressBar("Duel: Genesis", "Downloading " + id, (ids.Count + g) / (float)(ids.Count + Ground.Length));
                    string dir = Path.Combine(project, GroundFolder, id);
                    Directory.CreateDirectory(dir);
                    foreach (string map in new[] { "diff", "nor_gl", "rough" })
                    {
                        string file = $"{id}_{map}_1k.jpg";
                        if (GenesisAssetDownloads.Save($"https://dl.polyhaven.org/file/ph-assets/Textures/jpg/1k/{id}/{file}", Path.Combine(dir, file))) files++; else failed++;
                    }
                }
            }
            finally { EditorUtility.ClearProgressBar(); }
            File.WriteAllText(Path.Combine(project, Folder, "License.txt"), "Models from Poly Haven (https://polyhaven.com), CC0 1.0: " + string.Join(", ", ids) + ".\n");
            File.WriteAllText(Path.Combine(project, GroundFolder, "License.txt"), "Textures from Poly Haven (https://polyhaven.com), CC0 1.0: " + string.Join(", ", Ground) + ".\n");
            AssetDatabase.Refresh();
            Debug.Log($"Duel: Genesis downloaded {files} Poly Haven nature files (CC0){(failed > 0 ? $", {failed} failed" : "")}.");
            SetUp();
        }

        [MenuItem("Duel Genesis/Production Assets/Set Up Poly Haven Nature Props")]
        public static void SetUp()
        {
            EnsureFolder(PrefabFolder);
            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            int made = 0;
            var log = new System.Text.StringBuilder();
            foreach (string id in All)
            {
                string src = $"{Folder}/{id}";
                var model = AssetDatabase.LoadAssetAtPath<GameObject>($"{src}/{id}.fbx");
                if (model == null) { log.AppendLine(id + ": missing"); continue; }
                var go = (GameObject)PrefabUtility.InstantiatePrefab(model);
                PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                go.name = id;
                int tris = 0;
                foreach (MeshRenderer r in go.GetComponentsInChildren<MeshRenderer>(true))
                {
                    var mf = r.GetComponent<MeshFilter>();
                    if (mf != null && mf.sharedMesh != null) tris += (int)Enumerable.Range(0, mf.sharedMesh.subMeshCount).Sum(i => (long)mf.sharedMesh.GetIndexCount(i)) / 3;
                    r.sharedMaterials = r.sharedMaterials.Select(m => MakeMaterial(src, m != null ? m.name : id, lit)).ToArray();
                }
                bool rock = Rocks.Contains(id) || Stumps.Contains(id);
                if (rock)
                {
                    MeshFilter[] mfs = go.GetComponentsInChildren<MeshFilter>();
                    // One collider from the lightest mesh (the last LOD, if there are LODs).
                    MeshFilter best = mfs.Where(f => f.sharedMesh != null).OrderBy(f => f.sharedMesh.vertexCount).FirstOrDefault();
                    if (best != null) best.gameObject.AddComponent<MeshCollider>().sharedMesh = best.sharedMesh;
                }
                float cull = rock ? 0.006f : 0.02f;
                var lod = go.GetComponent<LODGroup>();
                if (lod != null)
                {
                    // Poly Haven ships its own LODs: keep them, just make sure the last one culls.
                    LOD[] lods = lod.GetLODs();
                    if (lods.Length > 0 && lods[lods.Length - 1].screenRelativeTransitionHeight < cull)
                    {
                        lods[lods.Length - 1].screenRelativeTransitionHeight = cull;
                        for (int i = lods.Length - 2; i >= 0; i--)
                            lods[i].screenRelativeTransitionHeight = Mathf.Max(lods[i].screenRelativeTransitionHeight, lods[i + 1].screenRelativeTransitionHeight + 0.001f);
                        lod.SetLODs(lods);
                    }
                    log.Append($"({lods.Length} LODs) ");
                }
                else
                {
                    lod = go.AddComponent<LODGroup>();
                    lod.SetLODs(new[] { new LOD(cull, go.GetComponentsInChildren<Renderer>()) });
                }
                lod.RecalculateBounds();
                PrefabUtility.SaveAsPrefabAsset(go, $"{PrefabFolder}/{id}.prefab");
                Object.DestroyImmediate(go);
                made++;
                log.AppendLine($"{id}: {tris:N0} triangles");
            }
            SetUpGround();
            AssetDatabase.SaveAssets();
            Debug.Log($"Duel: Genesis set up {made} Poly Haven nature prefabs in {PrefabFolder}.\n{log}");
        }

        public static GameObject Load(string id) => AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabFolder}/{id}.prefab");

        private static Material MakeMaterial(string src, string sourceName, Shader lit)
        {
            string prefix = sourceName.Replace(" (Instance)", "");
            foreach (string suf in new[] { "_diff_1k", "_diff" }) if (prefix.EndsWith(suf)) prefix = prefix.Substring(0, prefix.Length - suf.Length);
            string Tex(string kind)
            {
                foreach (string ext in new[] { ".jpg", ".png" })
                {
                    string p = $"{src}/textures/{prefix}_{kind}_1k{ext}";
                    if (File.Exists(p)) return p;
                }
                return null;
            }
            string diff = Tex("diff"), nor = Tex("nor_gl"), alpha = Tex("alpha");
            string path = $"{PrefabFolder}/{prefix}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(lit); AssetDatabase.CreateAsset(m, path); }
            m.shader = lit;
            Texture2D baseTex = diff == null ? null
                : alpha != null ? PackAlpha(diff, alpha, $"{PrefabFolder}/{prefix}_rgba.png")
                : AssetDatabase.LoadAssetAtPath<Texture2D>(diff);
            m.SetTexture("_BaseMap", baseTex);
            m.SetColor("_BaseColor", Color.white);
            if (nor != null)
            {
                var ti = AssetImporter.GetAtPath(nor) as TextureImporter;
                if (ti != null && ti.textureType != TextureImporterType.NormalMap) { ti.textureType = TextureImporterType.NormalMap; ti.SaveAndReimport(); }
                m.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(nor));
                m.EnableKeyword("_NORMALMAP");
            }
            m.SetFloat("_Smoothness", 0.15f);
            m.SetFloat("_Metallic", 0f);
            if (alpha != null)
            {
                m.SetFloat("_AlphaClip", 1f);
                m.SetFloat("_Cutoff", 0.45f);
                m.EnableKeyword("_ALPHATEST_ON");
                m.SetFloat("_Cull", 0f);
                m.doubleSidedGI = true;
            }
            m.enableInstancing = true;
            EditorUtility.SetDirty(m);
            return m;
        }

        private static Texture2D PackAlpha(string diffPath, string alphaPath, string outPath)
        {
            if (!File.Exists(outPath))
            {
                var d = new Texture2D(2, 2); d.LoadImage(File.ReadAllBytes(diffPath));
                var a = new Texture2D(2, 2); a.LoadImage(File.ReadAllBytes(alphaPath));
                int w = d.width, h = d.height;
                Color[] px = d.GetPixels();
                for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    px[y * w + x].a = a.GetPixelBilinear((x + 0.5f) / w, (y + 0.5f) / h).r;
                var o = new Texture2D(w, h, TextureFormat.RGBA32, true);
                o.SetPixels(px);
                File.WriteAllBytes(outPath, o.EncodeToPNG());
                Object.DestroyImmediate(d); Object.DestroyImmediate(a); Object.DestroyImmediate(o);
                AssetDatabase.ImportAsset(outPath);
            }
            var ti = AssetImporter.GetAtPath(outPath) as TextureImporter;
            if (ti != null && !ti.mipMapsPreserveCoverage)
            {
                ti.alphaIsTransparency = true;
                ti.mipMapsPreserveCoverage = true;
                ti.alphaTestReferenceValue = 0.45f;
                ti.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(outPath);
        }

        /// <summary>Photo-scanned grass on the city's grass (tiled every 4 m) and raked gravel in the rock garden.</summary>
        private static void SetUpGround()
        {
            void Apply(string matPath, string id, float tile, Color tint)
            {
                var m = AssetDatabase.LoadAssetAtPath<Material>(matPath);
                if (m == null) return;
                string dir = $"{GroundFolder}/{id}";
                var diff = AssetDatabase.LoadAssetAtPath<Texture2D>($"{dir}/{id}_diff_1k.jpg");
                if (diff == null) return;
                string norPath = $"{dir}/{id}_nor_gl_1k.jpg";
                var ti = AssetImporter.GetAtPath(norPath) as TextureImporter;
                if (ti != null && ti.textureType != TextureImporterType.NormalMap) { ti.textureType = TextureImporterType.NormalMap; ti.SaveAndReimport(); }
                m.SetTexture("_BaseMap", diff);
                m.SetTextureScale("_BaseMap", new Vector2(tile, tile));
                m.SetColor("_BaseColor", tint);
                var nor = AssetDatabase.LoadAssetAtPath<Texture2D>(norPath);
                if (nor != null) { m.SetTexture("_BumpMap", nor); m.EnableKeyword("_NORMALMAP"); }
                m.SetFloat("_Smoothness", 0.08f);
                EditorUtility.SetDirty(m);
            }
            Apply("Assets/Art/Generated/OpenWorld/OW Grass.mat", "grass_ground", 210f, new Color(0.72f, 0.95f, 0.55f));   // greener than the dry scan   // the 840 m ground slab
            Apply("Assets/Art/Generated/Garden/Raked Gravel.mat", "gravel_ground_01", 6f, Color.white);
            Apply("Assets/Art/Generated/Garden/Moss.mat", "forest_ground_04", 1f, Color.white);
        }

        // ------------------------------------------------------------------ swap into the world

        [MenuItem("Duel Genesis/World/14. Swap Low-Poly Nature For Realistic Props")]
        public static void Swap()
        {
            GameObject city = SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(g => g.name == GenesisWorldBuilder.CityRootName);
            if (city == null) { Debug.LogWarning("Duel: Genesis: no city in the scene."); return; }
            GameObject[] P(string[] ids) => ids.Select(Load).Where(p => p != null).ToArray();
            GameObject[] rocks = P(Rocks), shrubs = P(Shrubs), flowers = P(Flowers), stumps = P(Stumps);
            if (rocks.Length == 0 || shrubs.Length == 0) { Debug.LogWarning("Duel: Genesis: download the Poly Haven nature props first (Downloads)."); return; }
            if (flowers.Length == 0) flowers = shrubs;
            if (stumps.Length == 0) stumps = rocks;

            var targets = new List<(GameObject go, GameObject[] pool, float minH)>();
            foreach (Transform t in city.GetComponentsInChildren<Transform>(true))
            {
                GameObject go = t.gameObject;
                string name = go.name.ToLowerInvariant();
                string src = PrefabUtility.IsAnyPrefabInstanceRoot(go) ? AssetDatabase.GetAssetPath(PrefabUtility.GetCorrespondingObjectFromSource(go)) ?? "" : "";
                string file = Path.GetFileNameWithoutExtension(src).ToLowerInvariant();
                if (src.Contains("NatureKit"))
                {
                    if (file.StartsWith("rock") || file.StartsWith("stone")) targets.Add((go, rocks, 0.4f));
                    else if (file.StartsWith("plant_bush")) targets.Add((go, shrubs, 0.7f));
                    else if (file.StartsWith("flower") || file.StartsWith("grass")) targets.Add((go, flowers, 0.3f));
                    else if (file.StartsWith("log") || file.StartsWith("stump") || file.StartsWith("mushroom")) targets.Add((go, stumps, 0.4f));
                    else if (file.StartsWith("pot_")) continue;   // planters stay
                }
                else if (src.Length == 0 && (name == "shore stone" || name == "water stone" || name == "garden rock"))
                    targets.Add((go, rocks, 0.3f));
            }

            var rng = new System.Random(20260930);
            int n = 0;
            foreach (var (go, pool, minH) in targets)
            {
                if (go == null) continue;
                Bounds b = GenesisWorldBuilder.RendererBounds(go);
                GameObject prefab = pool[rng.Next(pool.Length)];
                var obj = (GameObject)PrefabUtility.InstantiatePrefab(prefab, go.transform.parent);
                obj.name = go.name == "Shore Stone" || go.name == "Water Stone" || go.name == "Garden Rock" ? go.name : prefab.name;
                obj.transform.rotation = Quaternion.Euler(0f, rng.Next(360), 0f);
                Bounds pb = GenesisWorldBuilder.RendererBounds(obj);
                // Match the old piece's footprint (rocks) or height (plants), never tiny.
                float want = pool == rocks ? Mathf.Max(b.size.x, b.size.z) : Mathf.Max(b.size.y, minH);
                float have = pool == rocks ? Mathf.Max(pb.size.x, pb.size.z) : pb.size.y;
                if (have > 0.001f) obj.transform.localScale *= Mathf.Max(want, minH) / have;
                pb = GenesisWorldBuilder.RendererBounds(obj);
                float sink = pool == rocks ? pb.size.y * 0.12f : 0f;   // bed rocks into the ground
                obj.transform.position += new Vector3(b.center.x - pb.center.x, b.min.y - pb.min.y - sink, b.center.z - pb.center.z);
                Object.DestroyImmediate(go);
                n++;
            }
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            Debug.Log($"Duel: Genesis swapped {n} low-poly nature pieces for realistic Poly Haven props.");
        }

        private static void EnsureFolder(string path)
        {
            string[] parts = path.Split('/');
            string cur = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = cur + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(cur, parts[i]);
                cur = next;
            }
        }
    }
}
#endif
