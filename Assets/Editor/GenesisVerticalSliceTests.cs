#if UNITY_EDITOR
using System.Collections.Generic;
using DuelGenesis.Cards;
using DuelGenesis.Core;
using DuelGenesis.Progression;
using UnityEditor;
using UnityEngine;

namespace DuelGenesis.EditorTools
{
    [InitializeOnLoad]
    public static class GenesisVerticalSliceTests
    {
        static GenesisVerticalSliceTests()
        {
            EditorApplication.delayCall += RunSilent;
        }

        private static void RunSilent()
        {
            Run(false);
        }

        [MenuItem("Duel Genesis/Run Vertical Slice Validation")]
        public static void RunFromMenu()
        {
            Run(true);
        }

        private static void Run(bool showDialog)
        {
            List<string> failures = new();

            if (StarterLoadout.StarterDeckSize != 40)
                failures.Add($"Starter deck must contain 40 cards, found {StarterLoadout.StarterDeckSize}.");

            // The retired DG001-DG024 prototype cards are gone; validate the real production catalog instead.
            string catalogPath = System.IO.Path.Combine(Application.streamingAssetsPath, "duel_genesis_cards.json");
            if (!System.IO.File.Exists(catalogPath))
                failures.Add("Production card catalog (StreamingAssets/duel_genesis_cards.json) is missing.");
            else
            {
                ExternalCardCatalog catalog = JsonUtility.FromJson<ExternalCardCatalog>(System.IO.File.ReadAllText(catalogPath));
                int count = catalog?.cards?.Count ?? 0;
                if (count < 500)
                    failures.Add($"Production card catalog only has {count} cards.");
            }

            if (DuelistProfile.RequiredXPForNextLevel(1) <= 0)
                failures.Add("Level 1 XP requirement is invalid.");
            if (DuelistProfile.RequiredXPForNextLevel(25) <= DuelistProfile.RequiredXPForNextLevel(1))
                failures.Add("XP progression is not increasing with level.");
            if (DuelistProfile.GetTitleForLevel(100) != "Genesis Legend")
                failures.Add("Level 100 title is not Genesis Legend.");


            if (AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/Scenes/GenesisPrototype.unity") == null)
                failures.Add("GenesisPrototype.unity is missing. Build or restore the playable prototype scene.");


            bool passed = failures.Count == 0;
            string report = passed
                ? "Duel: Genesis vertical-slice validation PASS — scene, starter deck size, progression formulas and production card catalog are valid."
                : "Duel: Genesis vertical-slice validation FAILED:\n- " + string.Join("\n- ", failures);

            if (passed) Debug.Log(report);
            else Debug.LogError(report);

            if (showDialog)
                EditorUtility.DisplayDialog("Duel: Genesis Validation", report, "OK");
        }
    }
}
#endif
