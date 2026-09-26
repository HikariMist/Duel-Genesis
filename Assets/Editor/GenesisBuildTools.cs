#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DuelGenesis.EditorTools
{
    public static class GenesisBuildTools
    {
        private const string ScenePath = "Assets/Scenes/GenesisPrototype.unity";
        private const string BuildFolder = "Builds/DuelGenesis";
        private const string ExePath = BuildFolder + "/DuelGenesis.exe";
        private const string Version = "0.6.0";

        [MenuItem("Duel Genesis/Build/Windows Playable")]
        public static void BuildWindowsPlayable()
        {
            Build(false, false);
        }

        [MenuItem("Duel Genesis/Build/Windows Playable and Run")]
        public static void BuildWindowsPlayableAndRun()
        {
            Build(false, true);
        }

        [MenuItem("Duel Genesis/Build/Windows Development Build")]
        public static void BuildWindowsDevelopment()
        {
            Build(true, false);
        }

        private static void Build(bool development, bool runAfterBuild)
        {
            if (EditorApplication.isCompiling)
            {
                EditorUtility.DisplayDialog(
                    "Duel: Genesis Build",
                    "Unity is still compiling scripts. Wait for compilation to finish, then run the build command again.",
                    "OK");
                return;
            }

            if (!File.Exists(ScenePath))
            {
                EditorUtility.DisplayDialog(
                    "Duel: Genesis Build",
                    "GenesisPrototype.unity was not found. Restore or rebuild the playable scene before building.",
                    "OK");
                return;
            }

            EditorSceneManager.SaveOpenScenes();
            AssetDatabase.SaveAssets();
            Directory.CreateDirectory(BuildFolder);

            PlayerSettings.productName = "Duel Genesis";
            PlayerSettings.companyName = "HikariMist";
            PlayerSettings.bundleVersion = Version;
            PlayerSettings.defaultScreenWidth = 1600;
            PlayerSettings.defaultScreenHeight = 900;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.runInBackground = true;

            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(ScenePath, true)
            };

            BuildOptions buildOptions = development
                ? BuildOptions.Development | BuildOptions.AllowDebugging
                : BuildOptions.None;

            if (runAfterBuild)
                buildOptions |= BuildOptions.AutoRunPlayer;

            BuildPlayerOptions options = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = ExePath,
                target = BuildTarget.StandaloneWindows64,
                options = buildOptions
            };

            Debug.Log($"Duel: Genesis build started — Windows x64 v{Version}.");
            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;

            if (summary.result == BuildResult.Succeeded)
            {
                string message =
                    $"Build succeeded.\n\n{ExePath}\n\nVersion: {Version}\nSize: {summary.totalSize / (1024f * 1024f):0.0} MB" +
                    (runAfterBuild ? "\n\nThe playable build is launching now." : string.Empty);
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
