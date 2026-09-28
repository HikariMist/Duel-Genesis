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
