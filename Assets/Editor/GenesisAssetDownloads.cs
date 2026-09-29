#if UNITY_EDITOR
using System.IO;
using System.IO.Compression;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

namespace DuelGenesis.EditorTools
{
    /// <summary>
    /// Downloads free, openly licensed art packs straight into the project.
    /// Kenney Furniture Kit: CC0 (public domain), https://kenney.nl/assets/furniture-kit
    /// </summary>
    public static class GenesisAssetDownloads
    {
        private const string FurnitureUrl = "https://kenney.nl/media/pages/assets/furniture-kit/440e0608a4-1677580847/kenney_furniture-kit.zip";
        public const string FurnitureFolder = "Assets/ThirdParty/Kenney/FurnitureKit";
        public const string PolyHavenFolder = "Assets/ThirdParty/PolyHaven";
        public const string CityKitFolder = "Assets/ThirdParty/Kenney/CityKitCommercial";
        public const string NatureKitFolder = "Assets/ThirdParty/Kenney/NatureKit";
        public const string TreesFolder = "Assets/ThirdParty/PolyHaven/Trees";
        public static readonly string[] RealisticTrees = { "pine_tree_01", "fir_tree_01", "island_tree_03", "tree_small_02", "jacaranda_tree" };

        /// <summary>
        /// Poly Haven's photo-scanned trees (CC0): the 1K FBX of each plus the textures it references, found through
        /// Poly Haven's public file API. Leaf materials are switched to alpha-cutout and two-sided.
        /// </summary>
        [MenuItem("Duel Genesis/Downloads/Poly Haven Realistic Trees (CC0)")]
        public static void DownloadTrees()
        {
            string project = Directory.GetParent(Application.dataPath).FullName;
            int files = 0, failed = 0;
            try
            {
                for (int t = 0; t < RealisticTrees.Length; t++)
                {
                    string id = RealisticTrees[t];
                    EditorUtility.DisplayProgressBar("Duel: Genesis", "Downloading " + id, t / (float)RealisticTrees.Length);
                    string json = Fetch("https://api.polyhaven.com/files/" + id);
                    if (json == null) { failed++; continue; }
                    int fbx = json.IndexOf("\"fbx\"", System.StringComparison.Ordinal);
                    if (fbx < 0) { failed++; continue; }
                    int k1 = json.IndexOf("\"1k\"", fbx, System.StringComparison.Ordinal);
                    int k2 = json.IndexOf("\"2k\"", k1 + 4, System.StringComparison.Ordinal);
                    string block = json.Substring(k1, (k2 > k1 ? k2 : json.Length) - k1);
                    string dir = Path.Combine(project, TreesFolder, id);
                    Directory.CreateDirectory(dir);
                    var main = System.Text.RegularExpressions.Regex.Match(block, "\"url\":\\s*\"([^\"]+\\.fbx)\"");
                    if (!main.Success) { failed++; continue; }
                    if (Save(main.Groups[1].Value, Path.Combine(dir, id + ".fbx"))) files++; else failed++;
                    foreach (System.Text.RegularExpressions.Match inc in System.Text.RegularExpressions.Regex.Matches(block, "\"([^\"]+\\.(?:jpg|png|exr))\":\\s*\\{[^}]*?\"url\":\\s*\"([^\"]+)\""))
                    {
                        string rel = inc.Groups[1].Value.Replace('/', Path.DirectorySeparatorChar);
                        string dest = Path.Combine(dir, rel);
                        Directory.CreateDirectory(Path.GetDirectoryName(dest));
                        if (Save(inc.Groups[2].Value, dest)) files++; else failed++;
                    }
                }
            }
            finally { EditorUtility.ClearProgressBar(); }

            File.WriteAllText(Path.Combine(project, TreesFolder, "License.txt"), "Trees from Poly Haven (https://polyhaven.com), CC0 1.0: " + string.Join(", ", RealisticTrees) + ".\n");
            AssetDatabase.Refresh();
            FixTreeMaterials();
            Debug.Log($"Duel: Genesis downloaded {files} Poly Haven tree files (CC0) into {TreesFolder}{(failed > 0 ? $", {failed} failed" : "")}.");
        }

        /// <summary>Extracts each tree's materials and makes leaves alpha-cutout and two-sided.</summary>
        public static void FixTreeMaterials()
        {
            foreach (string id in RealisticTrees)
            {
                string path = $"{TreesFolder}/{id}/{id}.fbx";
                var importer = AssetImporter.GetAtPath(path) as ModelImporter;
                if (importer == null) continue;
                if (importer.materialLocation != ModelImporterMaterialLocation.External)
                {
                    importer.materialLocation = ModelImporterMaterialLocation.External;
                    importer.materialSearch = ModelImporterMaterialSearch.Local;
                    importer.SaveAndReimport();
                }
                foreach (string g in AssetDatabase.FindAssets("t:Material", new[] { $"{TreesFolder}/{id}" }))
                {
                    var m = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(g));
                    if (m == null) continue;
                    m.enableInstancing = true;
                    EditorUtility.SetDirty(m);
                    Texture tex = m.HasProperty("_BaseMap") ? m.GetTexture("_BaseMap") : m.mainTexture;
                    string n = (m.name + " " + (tex != null ? tex.name : "")).ToLowerInvariant();
                    bool leafy = n.Contains("leaf") || n.Contains("leaves") || n.Contains("needle") || n.Contains("twig") || n.Contains("branch") || n.Contains("foliage") || n.Contains("flower");
                    if (!leafy) continue;
                    m.SetFloat("_AlphaClip", 1f);
                    m.SetFloat("_Cutoff", 0.45f);
                    m.EnableKeyword("_ALPHATEST_ON");
                    m.SetFloat("_Cull", 0f);
                    m.doubleSidedGI = true;
                    EditorUtility.SetDirty(m);
                }
            }
            AssetDatabase.SaveAssets();
        }

        private static string Fetch(string url)
        {
            using (var r = UnityWebRequest.Get(url))
            {
                var op = r.SendWebRequest();
                while (!op.isDone) { }
                if (r.result != UnityWebRequest.Result.Success) { Debug.LogWarning($"Duel: Genesis: {url}: {r.error}"); return null; }
                return r.downloadHandler.text;
            }
        }

        private static bool Save(string url, string dest)
        {
            using (var r = UnityWebRequest.Get(url))
            {
                r.downloadHandler = new DownloadHandlerFile(dest);
                var op = r.SendWebRequest();
                while (!op.isDone) { }
                if (r.result == UnityWebRequest.Result.Success) return true;
                Debug.LogWarning($"Duel: Genesis could not download {url}: {r.error}");
                return false;
            }
        }

        public const string UIPackFolder = "Assets/Resources/DuelGenesis/UI/SciFi";   // Resources: the creator loads them at runtime

        [MenuItem("Duel Genesis/Downloads/Kenney UI Pack Sci-Fi (CC0) - creator and menu skin")]
        public static void DownloadUIPack()
        {
            string project = Directory.GetParent(Application.dataPath).FullName;
            string url = "https://kenney.nl/media/pages/assets/ui-pack-sci-fi/b67c2acd31-1724181109/kenney_ui-pack-space-expansion.zip";
            string zipPath = Path.Combine(project, "Temp", "kenney_ui-pack-sci-fi.zip");
            Directory.CreateDirectory(Path.GetDirectoryName(zipPath));
            using (var request = UnityWebRequest.Get(url))
            {
                request.downloadHandler = new DownloadHandlerFile(zipPath);
                var op = request.SendWebRequest();
                while (!op.isDone) { }
                if (request.result != UnityWebRequest.Result.Success) { Debug.LogError("Duel: Genesis could not download the UI pack: " + request.error); return; }
            }
            string dest = Path.Combine(project, UIPackFolder);
            Directory.CreateDirectory(dest);
            foreach (string old in Directory.GetFiles(dest, "*.png*")) File.Delete(old);   // earlier unprefixed copies
            var names = new System.Collections.Generic.List<string>();
            using (ZipArchive zip = ZipFile.OpenRead(zipPath))
                foreach (ZipArchiveEntry e in zip.Entries)
                {
                    if (string.IsNullOrEmpty(e.Name)) continue;
                    string lower = e.FullName.Replace('\\', '/').ToLowerInvariant();
                    // Keep the default-resolution PNGs (skip @2x/double and vector copies) and the licence.
                    if (lower.EndsWith(".png") && !lower.Contains("double") && !lower.Contains("vector") && !lower.Contains("@2x") && !lower.Contains("preview") && !lower.Contains("sample"))
                    {
                        // Colour sets share file names: prefix the colour folder (Blue_, Red_, Extra_ ...).
                        string[] parts = e.FullName.Replace('\\', '/').Split('/');
                        string colour = parts.Length >= 3 ? parts[parts.Length - 3] : "";
                        string file = (string.IsNullOrEmpty(colour) ? "" : colour + "_") + e.Name;
                        e.ExtractToFile(Path.Combine(dest, file), true);
                        names.Add(file);
                    }
                    else if (lower.EndsWith("license.txt")) e.ExtractToFile(Path.Combine(dest, "License.txt"), true);
                }
            File.WriteAllText(Path.Combine(project, "Logs", "UIPackFiles.txt"), string.Join("\n", names));
            AssetDatabase.Refresh();
            foreach (string f in Directory.GetFiles(dest, "*.png"))
            {
                var imp = AssetImporter.GetAtPath(UIPackFolder + "/" + Path.GetFileName(f)) as TextureImporter;
                if (imp == null) continue;
                imp.textureType = TextureImporterType.GUI;
                imp.mipmapEnabled = false;
                imp.filterMode = FilterMode.Bilinear;
                imp.SaveAndReimport();
            }
            Debug.Log($"Duel: Genesis downloaded the Kenney UI Pack Sci-Fi (CC0, {new FileInfo(zipPath).Length / 1048576f:0.0} MB): {names.Count} images in {UIPackFolder}.");
        }

        [MenuItem("Duel Genesis/Downloads/Kenney City Kit Commercial (CC0) - buildings for the open city")]
        public static void DownloadCityKit() => DownloadKit("City Kit (Commercial)",
            "https://kenney.nl/media/pages/assets/city-kit-commercial/a742d900eb-1753115042/kenney_city-kit-commercial_2.1.zip", CityKitFolder);

        [MenuItem("Duel Genesis/Downloads/Kenney Nature Kit (CC0) - trees, rocks, flowers")]
        public static void DownloadNatureKit() => DownloadKit("Nature Kit",
            "https://kenney.nl/media/pages/assets/nature-kit/37ac38a37b-1677698939/kenney_nature-kit.zip", NatureKitFolder);

        /// <summary>Downloads a Kenney kit (CC0) and keeps only its FBX models and licence.</summary>
        private static void DownloadKit(string title, string url, string folder)
        {
            string project = Directory.GetParent(Application.dataPath).FullName;
            string zipPath = Path.Combine(project, "Temp", Path.GetFileName(url));
            Directory.CreateDirectory(Path.GetDirectoryName(zipPath));
            using (var request = UnityWebRequest.Get(url))
            {
                request.downloadHandler = new DownloadHandlerFile(zipPath);
                var op = request.SendWebRequest();
                while (!op.isDone)
                    if (EditorUtility.DisplayCancelableProgressBar("Duel: Genesis", $"Downloading Kenney {title}...", request.downloadProgress)) { request.Abort(); break; }
                EditorUtility.ClearProgressBar();
                if (request.result != UnityWebRequest.Result.Success) { Debug.LogError($"Duel: Genesis could not download {title}: {request.error}"); return; }
            }
            string dest = Path.Combine(project, folder);
            Directory.CreateDirectory(dest);
            int models = 0;
            using (ZipArchive zip = ZipFile.OpenRead(zipPath))
                foreach (ZipArchiveEntry e in zip.Entries)
                {
                    if (string.IsNullOrEmpty(e.Name)) continue;
                    string lower = e.FullName.Replace('\\', '/').ToLowerInvariant();
                    if (lower.EndsWith(".fbx")) { e.ExtractToFile(Path.Combine(dest, e.Name), true); models++; }
                    else if (lower.EndsWith("license.txt")) e.ExtractToFile(Path.Combine(dest, "License.txt"), true);
                    else if (lower.EndsWith(".png") && (lower.Contains("texture") || lower.Contains("colormap"))) e.ExtractToFile(Path.Combine(dest, e.Name), true);
                }
            long bytes = new FileInfo(zipPath).Length;
            AssetDatabase.Refresh();
            Debug.Log($"Duel: Genesis downloaded Kenney {title} (CC0, {bytes / 1048576f:0.0} MB): {models} models in {folder}.");
        }

        /// <summary>Poly Haven texture sets (CC0) used by the Genesis Duel Center: colour + normal map, 1K.</summary>
        public static readonly string[] DuelCenterTextures = { "marble_01", "concrete_panels", "wooden_panels" };

        [MenuItem("Duel Genesis/Downloads/Poly Haven Textures For The Duel Center (CC0, about 6 MB)")]
        public static void DownloadPolyHaven()
        {
            string project = Directory.GetParent(Application.dataPath).FullName;
            int ok = 0, failed = 0;
            var files = new System.Collections.Generic.List<(string id, string file)>();
            foreach (string id in DuelCenterTextures)
            {
                files.Add((id, $"{id}_diff_1k.jpg"));
                files.Add((id, $"{id}_nor_gl_1k.jpg"));
            }
            try
            {
                for (int i = 0; i < files.Count; i++)
                {
                    var (id, file) = files[i];
                    string url = $"https://dl.polyhaven.org/file/ph-assets/Textures/jpg/1k/{id}/{file}";
                    string dest = Path.Combine(project, PolyHavenFolder, id, file);
                    Directory.CreateDirectory(Path.GetDirectoryName(dest));
                    EditorUtility.DisplayProgressBar("Duel: Genesis", "Downloading " + file, i / (float)files.Count);
                    using (var request = UnityWebRequest.Get(url))
                    {
                        request.downloadHandler = new DownloadHandlerFile(dest);
                        var op = request.SendWebRequest();
                        while (!op.isDone) { }
                        if (request.result == UnityWebRequest.Result.Success) ok++;
                        else { failed++; Debug.LogWarning($"Duel: Genesis could not download {url}: {request.error}"); if (File.Exists(dest)) File.Delete(dest); }
                    }
                }
            }
            finally { EditorUtility.ClearProgressBar(); }

            File.WriteAllText(Path.Combine(project, PolyHavenFolder, "License.txt"),
                "Textures from Poly Haven (https://polyhaven.com), CC0 1.0 (public domain): " + string.Join(", ", DuelCenterTextures) + ".\n");
            AssetDatabase.Refresh();
            foreach (string id in DuelCenterTextures)
            {
                var importer = AssetImporter.GetAtPath($"{PolyHavenFolder}/{id}/{id}_nor_gl_1k.jpg") as TextureImporter;
                if (importer != null && importer.textureType != TextureImporterType.NormalMap)
                {
                    importer.textureType = TextureImporterType.NormalMap;
                    importer.SaveAndReimport();
                }
            }
            Debug.Log($"Duel: Genesis downloaded {ok} Poly Haven texture files (CC0) into {PolyHavenFolder}{(failed > 0 ? $", {failed} failed" : "")}. Re-run World > 9 (or 8) to apply them.");
        }

        [MenuItem("Duel Genesis/Downloads/Kenney Furniture Kit (CC0, 5 MB)")]
        public static void DownloadFurnitureKit()
        {
            string project = Directory.GetParent(Application.dataPath).FullName;
            string zipPath = Path.Combine(project, "Temp", "kenney_furniture-kit.zip");
            Directory.CreateDirectory(Path.GetDirectoryName(zipPath));

            using (var request = UnityWebRequest.Get(FurnitureUrl))
            {
                request.downloadHandler = new DownloadHandlerFile(zipPath);
                var op = request.SendWebRequest();
                while (!op.isDone)
                {
                    if (EditorUtility.DisplayCancelableProgressBar("Duel: Genesis", "Downloading Kenney Furniture Kit...", request.downloadProgress))
                    {
                        request.Abort();
                        break;
                    }
                }
                EditorUtility.ClearProgressBar();
                if (request.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogError("Duel: Genesis could not download the Furniture Kit: " + request.error);
                    return;
                }
            }

            string dest = Path.Combine(project, FurnitureFolder);
            string previews = Path.Combine(project, "GenesisImport", "KenneyFurniturePreviews");
            Directory.CreateDirectory(dest);
            Directory.CreateDirectory(previews);
            int models = 0, images = 0;
            using (ZipArchive zip = ZipFile.OpenRead(zipPath))
            {
                foreach (ZipArchiveEntry e in zip.Entries)
                {
                    if (string.IsNullOrEmpty(e.Name)) continue;
                    string path = e.FullName.Replace('\\', '/');
                    string lower = path.ToLowerInvariant();
                    if (lower.Contains("fbx format/"))
                    {
                        e.ExtractToFile(Path.Combine(dest, e.Name), true);
                        models++;
                    }
                    else if (lower.StartsWith("isometric/") || lower.Contains("preview"))
                    {
                        if (lower.EndsWith(".png")) { e.ExtractToFile(Path.Combine(previews, e.Name), true); images++; }
                    }
                    else if (lower.EndsWith("license.txt"))
                        e.ExtractToFile(Path.Combine(dest, "License.txt"), true);
                }
            }
            AssetDatabase.Refresh();
            string list = string.Join(", ", Directory.GetFiles(dest, "*.fbx").Select(Path.GetFileNameWithoutExtension).OrderBy(n => n));
            Debug.Log($"Duel: Genesis downloaded the Kenney Furniture Kit (CC0): {models} model files into {FurnitureFolder}, {images} preview images.\nModels: {list}");
        }
    }
}
#endif
