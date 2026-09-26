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
        private const string Version = "0.7.0";

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
                DmoProductionAssetTools.CopyCardLibraryToBuiltPlayer(ExePath, out string cardArtReport);

                bool launched = false;
                if (runAfterBuild)
                {
                    try
                    {
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = Path.GetFullPath(ExePath),
                            UseShellExecute = true
                        });
                        launched = true;
                    }
                    catch (System.Exception exception)
                    {
                        Debug.LogWarning("Duel: Genesis build succeeded but automatic launch failed: " + exception.Message);
                    }
                }

                string message =
                    $"Build succeeded.\n\n{ExePath}\n\nVersion: {Version}\nUnity player size: {summary.totalSize / (1024f * 1024f):0.0} MB\n\n{cardArtReport}" +
                    (launched ? "\n\nThe playable build is launching now." : string.Empty);
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
