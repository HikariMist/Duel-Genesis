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
