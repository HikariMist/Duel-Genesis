#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace DuelGenesis.EditorTools
{
    public static class GenesisBuildTools
    {
        private const string ScenePath = "Assets/Scenes/GenesisPrototype.unity";
        private const string BuildFolder = "Builds/DuelGenesis";
        private const string ExePath = BuildFolder + "/DuelGenesis.exe";

        [MenuItem("Duel Genesis/Build/Windows Playable")]
        public static void BuildWindowsPlayable()
        {
            Build(false);
        }

        [MenuItem("Duel Genesis/Build/Windows Development Build")]
        public static void BuildWindowsDevelopment()
        {
            Build(true);
        }

        private static void Build(bool development)
        {
            if (!File.Exists(ScenePath))
            {
                EditorUtility.DisplayDialog(
                    "Duel: Genesis Build",
                    "GenesisPrototype.unity was not found. Restore or rebuild the playable scene before building.",
                    "OK");
                return;
            }

            Directory.CreateDirectory(BuildFolder);

            PlayerSettings.productName = "Duel Genesis";
            PlayerSettings.companyName = "HikariMist";
            PlayerSettings.bundleVersion = "0.5.0";
            PlayerSettings.defaultScreenWidth = 1600;
            PlayerSettings.defaultScreenHeight = 900;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;

            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(ScenePath, true)
            };

            BuildPlayerOptions options = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = ExePath,
                target = BuildTarget.StandaloneWindows64,
                options = development
                    ? BuildOptions.Development | BuildOptions.AllowDebugging
                    : BuildOptions.None
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;

            if (summary.result == BuildResult.Succeeded)
            {
                string message = $"Build succeeded.\n\n{ExePath}\n\nVersion: 0.5.0\nSize: {summary.totalSize / (1024f * 1024f):0.0} MB";
                Debug.Log("Duel: Genesis Windows build succeeded: " + ExePath);
                EditorUtility.DisplayDialog("Duel: Genesis Build", message, "OK");
            }
            else
            {
                string message = $"Build failed with {summary.totalErrors} error(s). Open the Unity Console for the exact error.";
                Debug.LogError("Duel: Genesis Windows build failed.");
                EditorUtility.DisplayDialog("Duel: Genesis Build", message, "OK");
            }
        }
    }
}
#endif
