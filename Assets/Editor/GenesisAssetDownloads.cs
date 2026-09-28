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
