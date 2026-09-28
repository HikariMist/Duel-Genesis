#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace DuelGenesis.EditorTools
{
    /// <summary>
    /// Finds which city textures are on screen (probe) so the pack's own advertising can be replaced
    /// with the Duel Genesis artwork.
    /// </summary>
    public static class GenesisAdReplacer
    {
        private const string TexDir = "Assets/ZRNAssets/005339_08932_25_14/Textures/";
        private const string OutDir = "Assets/Art/Generated/CityAds";
        private const string ArtDir = "Assets/Resources/DuelGenesis/Artwork/";

        /// <summary>One advertising panel inside a city texture atlas (pixel rect, top-left origin).</summary>
        private struct Patch
        {
            public string Texture; public int X0, Y0, X1, Y1; public int Art; public float FocusX, FocusY;
            public Patch(string texture, int x0, int y0, int x1, int y1, int art, float fx, float fy)
            { Texture = texture; X0 = x0; Y0 = y0; X1 = x1; Y1 = y1; Art = art; FocusX = fx; FocusY = fy; }
        }

        // The ZENRIN pack's "Pocket Queries" mascot ads, found with the contact-sheet export and edge-snapped.
        // Art: 1 = Yugi/Kaiba/Blue-Eyes key art, 2 = Kaiba/Blue-Eyes/Gaia/Yugi poster, 3 = monster line-up poster.
        private static readonly Patch[] Patches =
        {
            new Patch("005339_08932_25_14_28.png", 773, 884, 912, 992, 1, 0.35f, 0.40f),
            new Patch("005339_08932_25_14_30.png", 906, 260, 984, 513, 3, 0.87f, 0.50f),
            new Patch("005339_08932_25_14_39.png", 906, 388, 1014, 645, 2, 0.88f, 0.50f),
            new Patch("005339_08932_25_14_42.png", 517, 266, 897, 372, 3, 0.50f, 0.35f),
            new Patch("005339_08932_25_14_54.png", 1, 805, 110, 996, 1, 0.20f, 0.50f),
            new Patch("005339_08932_25_14_54.png", 113, 804, 397, 993, 2, 0.50f, 0.50f),
            new Patch("005339_08932_25_14_54.png", 402, 805, 512, 996, 1, 0.55f, 0.50f),
            new Patch("005339_08932_25_14_66.png", 695, 759, 955, 999, 3, 0.50f, 0.50f),
            new Patch("005339_08932_25_14_80.png", 16, 18, 202, 274, 2, 0.30f, 0.50f),
            new Patch("00_TexturesPlus/building-1.png", 326, 14, 698, 254, 1, 0.50f, 0.50f),
        };

        [MenuItem("Duel Genesis/World/5. Replace City Ads With Artwork")]
        public static void ReplaceMenu()
        {
            GameObject city = GameObject.Find(GenesisWorldBuilder.CityRootName);
            if (city == null) { EditorUtility.DisplayDialog("City Ads", "Build Genesis City first.", "OK"); return; }
            var log = new StringBuilder();
            int swapped = Replace(city.transform, log);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            Debug.Log($"Duel: Genesis replaced the city ads ({swapped} material slots).\n{log}");
        }

        /// <summary>Paints the artwork over every mascot ad in copies of the atlases and points the city's
        /// materials at the copies. The pack's own files are never modified.</summary>
        public static int Replace(Transform cityRoot, StringBuilder log)
        {
            var art = new Dictionary<int, Texture2D>();
            for (int i = 1; i <= 3; i++)
            {
                string path = ArtDir + $"genesis_art_{i}.png";
                if (!File.Exists(path)) continue;
                var t = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                t.LoadImage(File.ReadAllBytes(path));
                art[i] = t;
            }
            if (art.Count == 0) { log.AppendLine("City ads: no artwork found."); return 0; }

            EnsureFolder(OutDir);
            EnsureFolder(OutDir + "/Materials");
            var replacements = new Dictionary<string, Texture2D>();   // original asset path -> painted copy
            foreach (var group in Patches.GroupBy(p => p.Texture))
            {
                string source = TexDir + group.Key;
                if (!File.Exists(source)) { log.AppendLine("City ads: missing " + source); continue; }
                var atlas = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                atlas.LoadImage(File.ReadAllBytes(source));
                foreach (Patch p in group)
                    if (art.TryGetValue(p.Art, out Texture2D a)) Paint(atlas, p, a);
                atlas.Apply();

                string outPath = $"{OutDir}/{Path.GetFileNameWithoutExtension(group.Key)}_dg.png";
                File.WriteAllBytes(outPath, atlas.EncodeToPNG());
                Object.DestroyImmediate(atlas);
                AssetDatabase.ImportAsset(outPath);
                if (AssetImporter.GetAtPath(outPath) is TextureImporter imp && AssetImporter.GetAtPath(source) is TextureImporter orig)
                {
                    imp.wrapMode = orig.wrapMode;
                    imp.filterMode = orig.filterMode;
                    imp.mipmapEnabled = true;
                    imp.anisoLevel = 4;
                    imp.maxTextureSize = Mathf.Max(orig.maxTextureSize, 1024);
                    imp.alphaIsTransparency = orig.alphaIsTransparency;
                    imp.SaveAndReimport();
                }
                replacements[source] = AssetDatabase.LoadAssetAtPath<Texture2D>(outPath);
            }
            foreach (Texture2D t in art.Values) Object.DestroyImmediate(t);

            // Point every city material that uses an original atlas at its painted copy.
            var copies = new Dictionary<Material, Material>();
            int swapped = 0;
            foreach (Renderer r in cityRoot.GetComponentsInChildren<Renderer>(true))
            {
                Material[] mats = r.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < mats.Length; i++)
                {
                    Material m = mats[i];
                    if (m == null) continue;
                    if (!copies.TryGetValue(m, out Material copy))
                    {
                        copy = null;
                        Texture t = m.HasProperty("_BaseMap") ? m.GetTexture("_BaseMap") : null;
                        if (t == null && m.HasProperty("_MainTex")) t = m.GetTexture("_MainTex");
                        if (t != null && replacements.TryGetValue(AssetDatabase.GetAssetPath(t), out Texture2D painted) && painted != null)
                        {
                            string matPath = $"{OutDir}/Materials/{m.name} DG.mat";
                            copy = AssetDatabase.LoadAssetAtPath<Material>(matPath);
                            if (copy == null)
                            {
                                copy = new Material(m) { name = m.name + " DG" };
                                AssetDatabase.CreateAsset(copy, matPath);
                            }
                            else copy.CopyPropertiesFromMaterial(m);
                            if (copy.HasProperty("_BaseMap")) copy.SetTexture("_BaseMap", painted);
                            if (copy.HasProperty("_MainTex")) copy.SetTexture("_MainTex", painted);
                            EditorUtility.SetDirty(copy);
                        }
                        copies[m] = copy;
                    }
                    if (copy != null) { mats[i] = copy; changed = true; swapped++; }
                }
                if (changed) r.sharedMaterials = mats;
            }
            AssetDatabase.SaveAssets();
            log.AppendLine($"City ads: painted {replacements.Count} atlases, {Patches.Length} ads, {swapped} material slots now use them.");
            return swapped;
        }

        private static void Paint(Texture2D atlas, Patch p, Texture2D art)
        {
            int w = p.X1 - p.X0, h = p.Y1 - p.Y0;
            if (w <= 0 || h <= 0) return;
            float rectAspect = w / (float)h, artAspect = art.width / (float)art.height;
            // Cover-fit around the focus point so the important part of the artwork stays in view.
            float cw = 1f, ch = 1f;
            if (artAspect > rectAspect) cw = rectAspect / artAspect; else ch = artAspect / rectAspect;
            float u0 = Mathf.Clamp(p.FocusX - cw * 0.5f, 0f, 1f - cw);
            float v0 = Mathf.Clamp(p.FocusY - ch * 0.5f, 0f, 1f - ch);   // from the top
            int height = atlas.height;
            var pixels = new Color[w * h];
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float u = u0 + (x + 0.5f) / w * cw;
                float vTop = v0 + (y + 0.5f) / h * ch;
                Color c = art.GetPixelBilinear(u, 1f - vTop);
                c.a = 1f;
                pixels[(h - 1 - y) * w + x] = c;     // Texture2D rows run bottom-up
            }
            atlas.SetPixels(p.X0, height - p.Y1, w, h, pixels);
        }

        private static void EnsureFolder(string path)
        {
            string[] parts = path.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }

        [MenuItem("Duel Genesis/World/Probe City Textures In Game View")]
        public static void Probe()
        {
            Camera cam = Camera.main;
            if (cam == null) { Debug.LogWarning("No main camera."); return; }
            Physics.SyncTransforms();
            const int cols = 48, rows = 27;
            var names = new Dictionary<string, char>();
            var legend = new StringBuilder();
            var grid = new StringBuilder();
            var uvs = new Dictionary<string, Rect>();
            for (int r = rows - 1; r >= 0; r--)
            {
                for (int c = 0; c < cols; c++)
                {
                    Ray ray = cam.ViewportPointToRay(new Vector3((c + 0.5f) / cols, (r + 0.5f) / rows, 0f));
                    string key = Surface(ray, out Vector2 uv);
                    if (key == null) { grid.Append('.'); continue; }
                    if (!names.TryGetValue(key, out char ch))
                    {
                        ch = (char)(names.Count < 26 ? 'A' + names.Count : names.Count < 52 ? 'a' + names.Count - 26 : '0' + (names.Count - 52) % 10);
                        names[key] = ch;
                    }
                    Rect box = uvs.TryGetValue(key, out Rect b) ? b : new Rect(uv, Vector2.zero);
                    box.xMin = Mathf.Min(box.xMin, uv.x); box.yMin = Mathf.Min(box.yMin, uv.y);
                    box.xMax = Mathf.Max(box.xMax, uv.x); box.yMax = Mathf.Max(box.yMax, uv.y);
                    uvs[key] = box;
                    grid.Append(ch);
                }
                grid.AppendLine();
            }
            foreach (var kv in names) legend.AppendLine($"{kv.Value} = {kv.Key}   uv {uvs[kv.Key]}");
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/DG-TextureProbe.txt", $"Camera {cam.transform.position} fwd {cam.transform.forward}\n\n{grid}\n{legend}");
            Debug.Log("Duel: Genesis texture probe written to Logs/DG-TextureProbe.txt");
        }

        [MenuItem("Duel Genesis/World/Export City Texture Contact Sheets")]
        public static void ExportSheets()
        {
            string root = "Assets/ZRNAssets/005339_08932_25_14/Textures";
            string[] files = Directory.GetFiles(root, "*.*", SearchOption.AllDirectories)
                .Where(f => f.EndsWith(".png") || f.EndsWith(".tga") && false)
                .Select(f => f.Replace('\\', '/')).OrderBy(f => f).ToArray();
            const int thumb = 160, cols = 10, rows = 7, per = cols * rows;
            Directory.CreateDirectory("Logs/CitySheets");
            var index = new StringBuilder();
            var rt = RenderTexture.GetTemporary(thumb, thumb, 0, RenderTextureFormat.ARGB32);
            for (int sheetNo = 0; sheetNo * per < files.Length; sheetNo++)
            {
                var sheet = new Texture2D(cols * thumb, rows * thumb, TextureFormat.RGB24, false);
                sheet.SetPixels32(Enumerable.Repeat(new Color32(40, 0, 40, 255), cols * thumb * rows * thumb).ToArray());
                for (int i = 0; i < per && sheetNo * per + i < files.Length; i++)
                {
                    string file = files[sheetNo * per + i];
                    var src = new Texture2D(2, 2);
                    if (!src.LoadImage(File.ReadAllBytes(file))) { Object.DestroyImmediate(src); continue; }
                    Graphics.Blit(src, rt);
                    RenderTexture.active = rt;
                    int cx = i % cols, cy = rows - 1 - i / cols;
                    sheet.ReadPixels(new Rect(0, 0, thumb, thumb), cx * thumb, cy * thumb);
                    RenderTexture.active = null;
                    index.AppendLine($"sheet {sheetNo} cell {i} (col {i % cols}, row {i / cols}) = {file} [{src.width}x{src.height}]");
                    Object.DestroyImmediate(src);
                }
                sheet.Apply();
                File.WriteAllBytes($"Logs/CitySheets/sheet_{sheetNo:00}.png", sheet.EncodeToPNG());
                Object.DestroyImmediate(sheet);
            }
            RenderTexture.ReleaseTemporary(rt);
            File.WriteAllText("Logs/CitySheets/index.txt", index.ToString());
            Debug.Log($"Duel: Genesis exported {files.Length} city textures to Logs/CitySheets.");
        }

        /// <summary>
        /// True when a raycast hit lands on (or within a small margin of) one of the city's advertising panels,
        /// original or repainted. Billboards use it so they never cover the painted wall ads.
        /// </summary>
        public static bool IsAdPanel(RaycastHit hit, int marginPx = 24)
        {
            if (!(hit.collider is MeshCollider mc) || mc.sharedMesh == null) return false;
            Renderer renderer = hit.collider.GetComponent<Renderer>();
            if (renderer == null) return false;
            int sub = SubmeshOf(mc.sharedMesh, hit.triangleIndex);
            Material m = sub >= 0 && sub < renderer.sharedMaterials.Length ? renderer.sharedMaterials[sub] : null;
            Texture t = m == null ? null : m.HasProperty("_BaseMap") ? m.GetTexture("_BaseMap") : m.mainTexture;
            if (t == null) return false;
            string path = AssetDatabase.GetAssetPath(t);
            string file = Path.GetFileNameWithoutExtension(path);
            if (file.EndsWith("_dg")) file = file.Substring(0, file.Length - 3);

            int w = t.width, h = t.height;
            if (AssetImporter.GetAtPath(path) is TextureImporter importer) importer.GetSourceTextureWidthAndHeight(out w, out h);
            Vector2 uv = hit.textureCoord;
            float px = (uv.x - Mathf.Floor(uv.x)) * w;
            float py = (1f - (uv.y - Mathf.Floor(uv.y))) * h;   // patches use a top-left origin
            foreach (Patch p in Patches)
            {
                if (Path.GetFileNameWithoutExtension(p.Texture) != file) continue;
                if (px >= p.X0 - marginPx && px <= p.X1 + marginPx && py >= p.Y0 - marginPx && py <= p.Y1 + marginPx) return true;
            }
            return false;
        }

        private static string Surface(Ray ray, out Vector2 uv)
        {
            uv = Vector2.zero;
            if (!Physics.Raycast(ray, out RaycastHit hit, 400f, ~0, QueryTriggerInteraction.Ignore)) return null;
            if (!(hit.collider is MeshCollider mc) || mc.sharedMesh == null) return "collider:" + hit.collider.name;
            Renderer renderer = hit.collider.GetComponent<Renderer>();
            if (renderer == null) return "norenderer:" + hit.collider.name;
            Mesh mesh = mc.sharedMesh;
            int sub = SubmeshOf(mesh, hit.triangleIndex);
            Material m = sub >= 0 && sub < renderer.sharedMaterials.Length ? renderer.sharedMaterials[sub] : null;
            Texture t = m == null ? null : m.HasProperty("_BaseMap") ? m.GetTexture("_BaseMap") : m.mainTexture;
            uv = hit.textureCoord;
            return $"{(t != null ? AssetDatabase.GetAssetPath(t) : "notex")} | mat {(m != null ? m.name : "none")} | obj {hit.collider.name} sub {sub}";
        }

        private static int SubmeshOf(Mesh mesh, int triangle)
        {
            if (triangle < 0) return -1;
            int index = triangle * 3;
            for (int s = 0; s < mesh.subMeshCount; s++)
            {
                var d = mesh.GetSubMesh(s);
                if (index >= d.indexStart && index < d.indexStart + d.indexCount) return s;
            }
            return -1;
        }
    }
}
#endif
