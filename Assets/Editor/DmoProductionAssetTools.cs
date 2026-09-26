#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using DuelGenesis.Cards;
using UnityEditor;
using UnityEngine;

namespace DuelGenesis.EditorTools
{
    public static class DmoProductionAssetTools
    {
        private static string ProjectRoot => Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        private static string CardRoot => Path.Combine(ProjectRoot, "Cards", "DMO_card_art");
        private static string FaceSource => Path.Combine(CardRoot, "cards");
        private static string FrameSource => Path.Combine(CardRoot, "card_backs_frames");
        private static string CharacterModelSource => Path.Combine(ProjectRoot, "Characters", "Assets", "Viewer", "Resources", "Models");
        private static string CharacterAnimationSource => Path.Combine(ProjectRoot, "Characters", "Assets", "Viewer", "Resources", "Animations");

        [MenuItem("Duel Genesis/Production Assets/Scan DMO Libraries")]
        public static void ScanLibraries()
        {
            int cardFaces = Directory.Exists(FaceSource)
                ? Directory.GetFiles(FaceSource, "*.png", SearchOption.TopDirectoryOnly).Length
                : 0;
            int models = Directory.Exists(CharacterModelSource)
                ? Directory.GetFiles(CharacterModelSource, "*.prefab", SearchOption.TopDirectoryOnly).Length
                : 0;
            int animations = Directory.Exists(CharacterAnimationSource)
                ? Directory.GetFiles(CharacterAnimationSource, "*.anim", SearchOption.AllDirectories).Length
                : 0;

            HashSet<string> modelKeys = Directory.Exists(CharacterModelSource)
                ? Directory.GetFiles(CharacterModelSource, "*.prefab", SearchOption.TopDirectoryOnly)
                    .Select(path => Normalize(Path.GetFileNameWithoutExtension(path)))
                    .ToHashSet()
                : new HashSet<string>();

            int artModelMatches = Directory.Exists(FaceSource)
                ? Directory.GetFiles(FaceSource, "*.png", SearchOption.TopDirectoryOnly)
                    .Count(path => modelKeys.Contains(Normalize(Path.GetFileNameWithoutExtension(path))))
                : 0;

            List<string> missingFrames = ValidateFrameFiles();
            string frameStatus = missingFrames.Count == 0
                ? "All supported card-frame source images found."
                : "Missing frame files: " + string.Join(", ", missingFrames);

            string report =
                $"DMO production library scan complete.\n\n" +
                $"Card faces: {cardFaces:N0}\n" +
                $"Character prefabs: {models:N0}\n" +
                $"Animation clips: {animations:N0}\n" +
                $"Exact normalized card/model matches: {artModelMatches:N0}\n\n" +
                frameStatus + "\n\n" +
                "Card-face PNGs are authoritative. Duel: Genesis uses the exact supplied card image first, so its printed frame cannot be accidentally replaced with the wrong frame. Frame textures are only the fallback when a complete card-face image is unavailable.";

            Debug.Log(report);
            EditorUtility.DisplayDialog("Duel: Genesis — DMO Asset Scan", report, "OK");
        }

        [MenuItem("Duel Genesis/Production Assets/Verify Card Frame Sources")]
        public static void VerifyCardFrameSources()
        {
            List<string> missing = ValidateFrameFiles();
            if (missing.Count == 0)
            {
                EditorUtility.DisplayDialog(
                    "Duel: Genesis — Card Frames",
                    "PASS — Normal, Effect, Fusion, Ritual, Synchro, Xyz, Spell, Trap and Token frame sources are present, plus the anime card back.",
                    "OK");
                return;
            }

            EditorUtility.DisplayDialog(
                "Duel: Genesis — Card Frames",
                "Missing source images:\n\n" + string.Join("\n", missing),
                "OK");
        }

        public static bool CopyCardLibraryToBuiltPlayer(string executablePath, out string report)
        {
            report = string.Empty;
            if (!Directory.Exists(FaceSource) || !Directory.Exists(FrameSource))
            {
                report = "DMO card-art source folders were not found; build completed without the external production card library.";
                Debug.LogWarning(report);
                return false;
            }

            string absoluteExe = Path.GetFullPath(executablePath);
            string buildRoot = Path.GetDirectoryName(absoluteExe);
            if (string.IsNullOrWhiteSpace(buildRoot))
            {
                report = "Could not resolve the Windows build folder for production card-art copying.";
                Debug.LogWarning(report);
                return false;
            }

            string dataFolder = Path.Combine(buildRoot, Path.GetFileNameWithoutExtension(absoluteExe) + "_Data");
            string streamingRoot = Path.Combine(dataFolder, "StreamingAssets");
            string faceDestination = Path.Combine(streamingRoot, "DMOCardFaces");
            string frameDestination = Path.Combine(streamingRoot, "DMOCardFrames");

            try
            {
                Directory.CreateDirectory(streamingRoot);
                CopyDirectoryWithProgress(FaceSource, faceDestination, "Copying production card faces");
                CopyDirectoryWithProgress(FrameSource, frameDestination, "Copying production card frames");

                int faceCount = Directory.GetFiles(faceDestination, "*.png", SearchOption.TopDirectoryOnly).Length;
                int frameCount = Directory.GetFiles(frameDestination, "*.png", SearchOption.TopDirectoryOnly).Length;
                report = $"Production card library copied into the standalone build: {faceCount:N0} card faces and {frameCount:N0} frame/back images.";
                Debug.Log("Duel: Genesis — " + report);
                return true;
            }
            catch (Exception exception)
            {
                report = "Production card-art copy failed: " + exception.Message;
                Debug.LogError("Duel: Genesis — " + report);
                return false;
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        private static List<string> ValidateFrameFiles()
        {
            string[] required =
            {
                ProductionCardArtRegistry.FrameFileName(CardFrameKind.NormalMonster),
                ProductionCardArtRegistry.FrameFileName(CardFrameKind.EffectMonster),
                ProductionCardArtRegistry.FrameFileName(CardFrameKind.FusionMonster),
                ProductionCardArtRegistry.FrameFileName(CardFrameKind.RitualMonster),
                ProductionCardArtRegistry.FrameFileName(CardFrameKind.SynchroMonster),
                ProductionCardArtRegistry.FrameFileName(CardFrameKind.XyzMonster),
                ProductionCardArtRegistry.FrameFileName(CardFrameKind.Spell),
                ProductionCardArtRegistry.FrameFileName(CardFrameKind.Trap),
                ProductionCardArtRegistry.FrameFileName(CardFrameKind.Token),
                "Card Back Anime 1.png"
            };

            List<string> missing = new();
            foreach (string file in required.Distinct())
            {
                if (!File.Exists(Path.Combine(FrameSource, file)))
                    missing.Add(file);
            }
            return missing;
        }

        private static void CopyDirectoryWithProgress(string source, string destination, string title)
        {
            string[] files = Directory.GetFiles(source, "*", SearchOption.TopDirectoryOnly);
            if (Directory.Exists(destination))
                Directory.Delete(destination, true);
            Directory.CreateDirectory(destination);

            for (int i = 0; i < files.Length; i++)
            {
                string file = files[i];
                float progress = files.Length == 0 ? 1f : (i + 1f) / files.Length;
                EditorUtility.DisplayProgressBar(title, Path.GetFileName(file), progress);
                File.Copy(file, Path.Combine(destination, Path.GetFileName(file)), true);
            }
        }

        private static string Normalize(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;
            StringBuilder builder = new StringBuilder(value.Length);
            foreach (char c in value)
            {
                if (char.IsLetterOrDigit(c))
                    builder.Append(char.ToLowerInvariant(c));
            }
            return builder.ToString();
        }
    }
}
#endif
