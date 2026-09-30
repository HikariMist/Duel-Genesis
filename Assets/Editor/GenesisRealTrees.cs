#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace DuelGenesis.EditorTools
{
    /// <summary>
    /// Turns the Poly Haven scanned trees (CC0, film-resolution FBX in Assets/ThirdParty/PolyHaven/Trees) into
    /// game-ready prefabs in Assets/Art/Generated/Trees:
    ///   - bark is simplified by clustering vertices on a small grid (UV seams kept),
    ///   - leaf/twig cards are thinned (a share of the cards kept and scaled up to keep the crown full),
    ///   - URP Lit materials: bark with its normal map, foliage alpha-cut and two-sided (diffuse + alpha packed into one texture),
    ///   - a LOD group that culls the tree when it is small on screen, and a trunk collider.
    /// </summary>
    public static class GenesisRealTrees
    {
        public const string OutFolder = "Assets/Art/Generated/Trees";
        public static readonly string[] Trees = { "island_tree_03", "tree_small_02", "jacaranda_tree", "fir_tree_01" };   // pine_tree_01 (650 MB) left out for now

        // Per tree: share of foliage cards kept, bark grid cell (metres, before scaling).
        private static readonly Dictionary<string, (float keep, float cell)> Settings = new Dictionary<string, (float, float)>
        {
            ["island_tree_03"] = (0.35f, 0.035f),
            ["tree_small_02"] = (0.4f, 0.03f),
            ["jacaranda_tree"] = (0.3f, 0.04f),
            ["fir_tree_01"] = (0.22f, 0.04f),
            ["pine_tree_01"] = (0.15f, 0.05f),
        };

        /// <summary>The baked prefab for a tree id, or null if it hasn't been baked.</summary>
        public static GameObject Load(string id) => AssetDatabase.LoadAssetAtPath<GameObject>($"{OutFolder}/{id}/{id}.prefab");

        [MenuItem("Duel Genesis/Production Assets/Bake Game-Ready Poly Haven Trees")]
        public static void BakeAll()
        {
            var log = new System.Text.StringBuilder();
            foreach (string id in Trees)
            {
                try { log.AppendLine(Bake(id)); }
                catch (System.Exception e) { log.AppendLine($"{id}: FAILED {e.Message}"); }
                finally { EditorUtility.UnloadUnusedAssetsImmediate(); }
            }
            AssetDatabase.SaveAssets();
            File.WriteAllText("Logs/DG-TreeBake.txt", log.ToString());
            Debug.Log("Duel: Genesis baked the Poly Haven trees:\n" + log);
        }

        [MenuItem("Duel Genesis/DEV/Preview Baked Trees (screenshots)")]
        public static void Preview()
        {
            string dir = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Logs", "Shots");
            Directory.CreateDirectory(dir);
            var temp = new List<GameObject>();
            float x = 0f;
            foreach (string id in Trees)
            {
                GameObject prefab = Load(id);
                if (prefab == null) continue;
                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                go.hideFlags = HideFlags.DontSave;
                Bounds b = GenesisWorldBuilder.RendererBounds(go);
                go.transform.localScale *= 10f / Mathf.Max(0.01f, b.size.y);
                b = GenesisWorldBuilder.RendererBounds(go);
                go.transform.position += new Vector3(x - b.center.x, 400f - b.min.y, 2000f - b.center.z);
                temp.Add(go);
                x += 14f;
            }
            float mid = (x - 14f) * 0.5f;
            GenesisShots.Shot(dir, "trees_1_row", new Vector3(mid, 405f, 1965f), new Vector3(mid, 405f, 2000f), 60f);
            if (temp.Count > 0) GenesisShots.Shot(dir, "trees_2_close", new Vector3(0f, 404f, 1990f), new Vector3(0f, 405f, 2000f), 60f);
            foreach (var g in temp) Object.DestroyImmediate(g);
            Debug.Log("Duel: Genesis captured the baked trees into " + dir);
        }

        public static string Bake(string id)
        {
            string src = $"{GenesisAssetDownloads.TreesFolder}/{id}";
            string fbx = $"{src}/{id}.fbx";
            var importer = AssetImporter.GetAtPath(fbx) as ModelImporter;
            if (importer == null) return $"{id}: not in the project";
            if (!importer.isReadable) { importer.isReadable = true; importer.SaveAndReimport(); }
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(fbx);
            float cell = Settings.TryGetValue(id, out var s) ? s.cell : 0.04f;

            string outDir = $"{OutFolder}/{id}";
            EnsureFolder(outDir);

            // Gather every mesh in the model, in the model's space, grouped by material name.
            var groups = new Dictionary<string, Part>();
            int srcTris = 0;
                        foreach (MeshFilter mf in model.GetComponentsInChildren<MeshFilter>(true))
            {
                Mesh m = mf.sharedMesh;
                var r = mf.GetComponent<MeshRenderer>();
                if (m == null || r == null) continue;
                Matrix4x4 xf = mf.transform.localToWorldMatrix;   // includes the FBX root's unit scale and Z-up rotation
                Vector3[] v = m.vertices;
                Vector3[] n = m.normals;
                Vector2[] uv = m.uv;
                Vector4[] tan = m.tangents;
                for (int sm = 0; sm < m.subMeshCount; sm++)
                {
                    Material mat = sm < r.sharedMaterials.Length ? r.sharedMaterials[sm] : null;
                    string key = mat != null ? mat.name : "default";
                    if (!groups.TryGetValue(key, out Part p)) groups[key] = p = new Part { Name = key };
                    int[] tris = m.GetTriangles(sm);
                    srcTris += tris.Length / 3;
                    var remap = new Dictionary<int, int>();
                    foreach (int t in tris)
                    {
                        if (!remap.TryGetValue(t, out int ni))
                        {
                            ni = p.V.Count;
                            remap[t] = ni;
                            p.V.Add(xf.MultiplyPoint3x4(v[t]));
                            p.N.Add(n.Length > t ? xf.MultiplyVector(n[t]).normalized : Vector3.up);
                            p.UV.Add(uv.Length > t ? uv[t] : Vector2.zero);
                            p.T.Add(tan.Length > t ? (Vector4)(xf.MultiplyVector(tan[t])).normalized + new Vector4(0, 0, 0, tan[t].w) : new Vector4(1, 0, 0, 1));
                        }
                        p.I.Add(ni);
                    }
                }
            }
            if (groups.Count == 0) return $"{id}: no meshes";

            // Two levels of detail: close-up (dense crown) and mid-distance (thinned harder, coarser bark).
            int leafTris = groups.Values.Where(g => IsFoliage(src, g.Name)).Sum(g => g.I.Count / 3);
            var info = new System.Text.StringBuilder();
            Mesh BuildLod(int level, float leafTarget, float cellMul, float maxGrow, out List<Part> parts)
            {
                var rng = new System.Random(id.GetHashCode() + level);
                float keepShare = leafTris > 0 ? Mathf.Clamp01(leafTarget / leafTris) : 1f;
                parts = new List<Part>();
                foreach (Part p in groups.Values)
                {
                    bool leaves = IsFoliage(src, p.Name);
                    bool twigs = !leaves && p.Name.ToLowerInvariant().Contains("branch") && p.I.Count / 3 > 20000;
                    Part o = leaves ? ThinCards(p, keepShare, maxGrow, rng)
                           : twigs ? ThinCards(Cluster(p, cell * cellMul), Mathf.Clamp01(leafTarget * 0.3f / (p.I.Count / 3f)), 1f, rng)   // fine twigs: drop some whole, no growth
                           : Cluster(p, cell * cellMul);
                    o.Name = p.Name;
                    o.Foliage = leaves;
                    if (o.I.Count > 0) parts.Add(o);
                }
                var mesh = new Mesh { name = $"{id}_lod{level}", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
                var allV = new List<Vector3>(); var allN = new List<Vector3>(); var allUV = new List<Vector2>(); var allT = new List<Vector4>();
                var subs = new List<List<int>>();
                foreach (Part p in parts)
                {
                    int b = allV.Count;
                    allV.AddRange(p.V); allN.AddRange(p.N); allUV.AddRange(p.UV); allT.AddRange(p.T);
                    subs.Add(p.I.Select(i => i + b).ToList());
                }
                mesh.SetVertices(allV);
                mesh.SetNormals(allN);
                mesh.SetUVs(0, allUV);
                mesh.SetTangents(allT);
                mesh.subMeshCount = subs.Count;
                for (int i = 0; i < subs.Count; i++) mesh.SetTriangles(subs[i], i);
                mesh.RecalculateBounds();
                string meshPath = $"{outDir}/{id}_lod{level}.asset";
                AssetDatabase.DeleteAsset(meshPath);
                AssetDatabase.CreateAsset(mesh, meshPath);
                var pl = parts;
                info.Append($" LOD{level} {subs.Sum(x => x.Count) / 3:N0} tris ({string.Join(", ", pl.Select(q => $"{q.Name.Replace(id + "_", "")} {q.I.Count / 3:N0}"))});");
                return mesh;
            }
            Mesh lod0 = BuildLod(0, 360000f, 1f, 2.3f, out List<Part> parts0);
            Mesh lod1 = BuildLod(1, 90000f, 2f, 3.6f, out List<Part> parts1);
            Mesh lod2 = BuildLod(2, 22000f, 4f, 5.5f, out List<Part> parts2);
            AssetDatabase.DeleteAsset($"{outDir}/{id}_game.asset");

            var go = new GameObject(id);
            Renderer MakeLod(string name, Mesh m, List<Part> parts)
            {
                var child = new GameObject(name);
                child.transform.SetParent(go.transform, false);
                child.AddComponent<MeshFilter>().sharedMesh = m;
                var r = child.AddComponent<MeshRenderer>();
                r.sharedMaterials = parts.Select(p => BuildMaterial(src, outDir, p.Name, p.Foliage)).ToArray();
                return r;
            }
            Renderer r0 = MakeLod("LOD0", lod0, parts0), r1 = MakeLod("LOD1", lod1, parts1), r2 = MakeLod("LOD2", lod2, parts2);
            r2.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;   // far trees: no shadow cost
            Bounds bb = lod0.bounds;
            var col = go.AddComponent<CapsuleCollider>();
            float trunkR = Mathf.Clamp(Mathf.Min(bb.size.x, bb.size.z) * 0.035f, 0.15f, 0.6f);
            col.radius = trunkR;
            col.height = bb.size.y * 0.5f;
            col.center = new Vector3(bb.center.x, bb.min.y + bb.size.y * 0.25f, bb.center.z);
            var lod = go.AddComponent<LODGroup>();
            lod.SetLODs(new[] { new LOD(0.3f, new[] { r0 }), new LOD(0.07f, new[] { r1 }), new LOD(0.01f, new[] { r2 }) });
            lod.RecalculateBounds();
            string prefab = $"{outDir}/{id}.prefab";
            PrefabUtility.SaveAsPrefabAsset(go, prefab);
            Object.DestroyImmediate(go);

            // Free the huge readable source again.
            importer.isReadable = false;
            importer.SaveAndReimport();

            return $"{id}: {srcTris:N0} source triangles ({leafTris:N0} leaves), {bb.size.x:0.0} x {bb.size.y:0.0} x {bb.size.z:0.0} m, base y {bb.min.y:0.00};{info}";
        }

        private sealed class Part
        {
            public string Name;
            public bool Foliage;
            public List<Vector3> V = new List<Vector3>();
            public List<Vector3> N = new List<Vector3>();
            public List<Vector2> UV = new List<Vector2>();
            public List<Vector4> T = new List<Vector4>();
            public List<int> I = new List<int>();
        }

        private static string Prefix(string matName)
        {
            string n = matName.Replace(" (Instance)", "");
            foreach (string suf in new[] { "_diff_1k", "_diff_2k", "_diff" }) if (n.EndsWith(suf)) return n.Substring(0, n.Length - suf.Length);
            return n;
        }

        private static string Tex(string src, string prefix, string kind)
        {
            foreach (string ext in new[] { ".png", ".jpg", ".exr" })
            {
                string p = $"{src}/textures/{prefix}_{kind}_1k{ext}";
                if (File.Exists(p)) return p;
            }
            return null;
        }

        private static bool IsFoliage(string src, string matName) => Tex(src, Prefix(matName), "alpha") != null;

        /// <summary>Keeps a share of the leaf cards (connected pieces) and scales each kept card up round its centre.</summary>
        private static Part ThinCards(Part p, float keep, float maxGrow, System.Random rng)
        {
            int n = p.V.Count;
            int[] parent = Enumerable.Range(0, n).ToArray();
            int Find(int x) { while (parent[x] != x) { parent[x] = parent[parent[x]]; x = parent[x]; } return x; }
            for (int i = 0; i < p.I.Count; i += 3)
            {
                int a = Find(p.I[i]), b = Find(p.I[i + 1]), c = Find(p.I[i + 2]);
                parent[b] = a; parent[Find(c)] = a;
            }
            var keepRoot = new Dictionary<int, bool>();
            var outP = new Part();
            var remap = new Dictionary<int, int>();
            float grow = Mathf.Min(maxGrow, 1f / Mathf.Sqrt(Mathf.Max(keep, 0.01f)));
            // Card centres, for scaling.
            var sum = new Dictionary<int, (Vector3 s, int c)>();
            for (int i = 0; i < n; i++)
            {
                int r = Find(i);
                sum.TryGetValue(r, out var e);
                sum[r] = (e.s + p.V[i], e.c + 1);
            }
            for (int i = 0; i < p.I.Count; i += 3)
            {
                int r = Find(p.I[i]);
                if (!keepRoot.TryGetValue(r, out bool k)) keepRoot[r] = k = rng.NextDouble() < keep;
                if (!k) continue;
                Vector3 centre = sum[r].s / sum[r].c;
                for (int j = 0; j < 3; j++)
                {
                    int vi = p.I[i + j];
                    if (!remap.TryGetValue(vi, out int ni))
                    {
                        ni = outP.V.Count;
                        remap[vi] = ni;
                        outP.V.Add(centre + (p.V[vi] - centre) * grow);
                        outP.N.Add(p.N[vi]); outP.UV.Add(p.UV[vi]); outP.T.Add(p.T[vi]);
                    }
                    outP.I.Add(ni);
                }
            }
            return outP;
        }

        /// <summary>Vertex clustering: vertices in the same grid cell with the same UV cell merge; collapsed triangles are dropped.</summary>
        private static Part Cluster(Part p, float cell)
        {
            var outP = new Part();
            var map = new Dictionary<(int, int, int, int, int), int>();
            var counts = new List<int>();
            int[] rep = new int[p.V.Count];
            for (int i = 0; i < p.V.Count; i++)
            {
                Vector3 v = p.V[i];
                Vector2 uv = p.UV[i];
                var key = (Mathf.FloorToInt(v.x / cell), Mathf.FloorToInt(v.y / cell), Mathf.FloorToInt(v.z / cell), Mathf.FloorToInt(uv.x * 48f), Mathf.FloorToInt(uv.y * 48f));
                if (!map.TryGetValue(key, out int ni))
                {
                    ni = outP.V.Count;
                    map[key] = ni;
                    outP.V.Add(Vector3.zero); outP.N.Add(Vector3.zero); outP.UV.Add(Vector2.zero); outP.T.Add(Vector4.zero);
                    counts.Add(0);
                }
                outP.V[ni] += v; outP.N[ni] += p.N[i]; outP.UV[ni] += uv; outP.T[ni] += p.T[i];
                counts[ni]++;
                rep[i] = ni;
            }
            for (int i = 0; i < outP.V.Count; i++)
            {
                float c = counts[i];
                outP.V[i] /= c;
                outP.N[i] = outP.N[i].normalized;
                outP.UV[i] /= c;
                Vector4 t = outP.T[i] / c;
                Vector3 t3 = new Vector3(t.x, t.y, t.z).normalized;
                outP.T[i] = new Vector4(t3.x, t3.y, t3.z, t.w < 0f ? -1f : 1f);
            }
            var seen = new HashSet<(int, int, int)>();
            for (int i = 0; i < p.I.Count; i += 3)
            {
                int a = rep[p.I[i]], b = rep[p.I[i + 1]], c = rep[p.I[i + 2]];
                if (a == b || b == c || a == c) continue;
                if (!seen.Add((a, b, c))) continue;
                outP.I.Add(a); outP.I.Add(b); outP.I.Add(c);
            }
            return outP;
        }

        private static Material BuildMaterial(string src, string outDir, string matName, bool foliage)
        {
            string prefix = Prefix(matName);
            string diff = Tex(src, prefix, "diff"), nor = Tex(src, prefix, "nor_gl"), alpha = Tex(src, prefix, "alpha");
            string path = $"{outDir}/{prefix}.mat";
            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(lit); AssetDatabase.CreateAsset(m, path); }
            m.shader = lit;
            Texture2D baseTex = null;
            if (foliage && diff != null && alpha != null) baseTex = PackAlpha(diff, alpha, $"{outDir}/{prefix}_rgba.png");
            else if (diff != null) baseTex = AssetDatabase.LoadAssetAtPath<Texture2D>(diff);
            m.SetTexture("_BaseMap", baseTex);
            m.SetColor("_BaseColor", Color.white);
            if (nor != null && !nor.EndsWith(".exr"))
            {
                var ti = AssetImporter.GetAtPath(nor) as TextureImporter;
                if (ti != null && ti.textureType != TextureImporterType.NormalMap) { ti.textureType = TextureImporterType.NormalMap; ti.SaveAndReimport(); }
                m.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(nor));
                m.EnableKeyword("_NORMALMAP");
            }
            m.SetFloat("_Smoothness", foliage ? 0.25f : 0.12f);
            m.SetFloat("_Metallic", 0f);
            if (foliage)
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

        /// <summary>Writes diffuse RGB + alpha-mask A into one PNG (1k) and imports it for alpha cut-out.</summary>
        private static Texture2D PackAlpha(string diffPath, string alphaPath, string outPath)
        {
            if (!File.Exists(outPath))
            {
                var d = new Texture2D(2, 2); d.LoadImage(File.ReadAllBytes(diffPath));
                var a = new Texture2D(2, 2); a.LoadImage(File.ReadAllBytes(alphaPath));
                int w = d.width, h = d.height;
                Color[] dc = d.GetPixels();
                var outT = new Texture2D(w, h, TextureFormat.RGBA32, true);
                var px = new Color[w * h];
                for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    Color c = dc[y * w + x];
                    c.a = a.GetPixelBilinear((x + 0.5f) / w, (y + 0.5f) / h).r;
                    px[y * w + x] = c;
                }
                outT.SetPixels(px);
                File.WriteAllBytes(outPath, outT.EncodeToPNG());
                Object.DestroyImmediate(d); Object.DestroyImmediate(a); Object.DestroyImmediate(outT);
                AssetDatabase.ImportAsset(outPath);
            }
            var ti = AssetImporter.GetAtPath(outPath) as TextureImporter;
            if (ti != null && (!ti.alphaIsTransparency || !ti.mipMapsPreserveCoverage))
            {
                ti.alphaIsTransparency = true;
                ti.mipMapsPreserveCoverage = true;   // leaves don't vanish in the distance
                ti.alphaTestReferenceValue = 0.45f;
                ti.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(outPath);
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
