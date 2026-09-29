#if UNITY_EDITOR
using System.IO;
using System.Linq;
using DuelGenesis.Dueling;
using UnityEditor;
using UnityEngine;

namespace DuelGenesis.EditorTools
{
    public static class DuelEditorTools
    {
        public const string LegacyMarker = "// DG-LEGACY-REMOVED";

        [MenuItem("Duel Genesis/Rules/Run Rules Engine Self-Test (200 CPU duels)")]
        public static void RunRulesSelfTest()
        {
            string report = DuelEngineSelfTest.Run(200);
            string summary = DuelEngineSelfTest.LastSummary;
            if (string.IsNullOrEmpty(report))
            {
                Debug.Log("Duel: Genesis rules engine self-test PASS — " + summary);
                EditorUtility.DisplayDialog("Rules Engine", "PASS\n\n" + summary, "OK");
            }
            else
            {
                Debug.LogError("Duel: Genesis rules engine self-test FAILED — " + report + "\n" + summary);
                EditorUtility.DisplayDialog("Rules Engine", "FAILED\n\n" + report, "OK");
            }
        }

        [MenuItem("Duel Genesis/Rules/Test Spell & Trap Library (every card + 100 CPU duels)")]
        public static void RunCardLibraryTest()
        {
            string json = File.ReadAllText(Path.Combine(Application.streamingAssetsPath, DuelGenesis.Cards.ExternalCardCatalogLoader.CatalogFileName));
            var catalog = JsonUtility.FromJson<DuelGenesis.Cards.ExternalCardCatalog>(json).cards
                .Select(r => r.ToCardData()).Where(c => c != null).ToList();
            CardLibrarySelfTest.Report report = CardLibrarySelfTest.Run(catalog, 100);
            string text = report.ToString() + "\nCards the CPU activated in duels:\n" + string.Join(", ", report.UsedInDuels.OrderBy(n => n));
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/DG-CardLibraryTest.txt", text);
            if (report.Passed) Debug.Log("Duel: Genesis Spell/Trap library test PASS\n" + text);
            else Debug.LogError("Duel: Genesis Spell/Trap library test FAILED\n" + text);
        }

        /// <summary>Deletes (with their .meta files) scripts that were emptied by the duel rebuild.</summary>
        [MenuItem("Duel Genesis/Maintenance/Remove Legacy Duel Scripts")]
        public static void RemoveLegacyScripts()
        {
            string[] files = Directory.GetFiles("Assets", "*.cs", SearchOption.AllDirectories)
                .Where(path => File.ReadAllText(path).TrimStart().StartsWith(LegacyMarker))
                .Select(path => path.Replace('\\', '/'))
                .ToArray();

            foreach (string path in files)
                AssetDatabase.DeleteAsset(path);
            AssetDatabase.Refresh();
            Debug.Log($"Duel: Genesis removed {files.Length} legacy duel script(s): {string.Join(", ", files.Select(Path.GetFileName))}");
        }

        [InitializeOnLoadMethod]
        private static void AutoRemoveLegacyScripts()
        {
            // Run once after the rebuild lands so the project never keeps dead scripts around.
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                bool any = Directory.GetFiles("Assets/Scripts/Dueling", "*.cs", SearchOption.TopDirectoryOnly)
                    .Any(path => File.ReadAllText(path).TrimStart().StartsWith(LegacyMarker));
                if (any) RemoveLegacyScripts();
            };
        }
    }
}
#endif
