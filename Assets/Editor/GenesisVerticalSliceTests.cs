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

            for (int i = 1; i <= 20; i++)
            {
                string id = $"DG{i:000}";
                if (CardDatabase.GetById(id) == null)
                    failures.Add($"Starter card {id} is missing from CardDatabase.");
            }

            if (DuelistProfile.RequiredXPForNextLevel(1) <= 0)
                failures.Add("Level 1 XP requirement is invalid.");
            if (DuelistProfile.RequiredXPForNextLevel(25) <= DuelistProfile.RequiredXPForNextLevel(1))
                failures.Add("XP progression is not increasing with level.");
            if (DuelistProfile.GetTitleForLevel(100) != "Genesis Legend")
                failures.Add("Level 100 title is not Genesis Legend.");

            if (CardDatabase.All.Count < 24)
                failures.Add("Prototype card pool is unexpectedly small.");

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/Scenes/GenesisPrototype.unity") == null)
                failures.Add("GenesisPrototype.unity is missing. Build or restore the playable prototype scene.");

            if (AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/StreamingAssets/duel_genesis_cards.example.json") == null)
                failures.Add("External card catalog example JSON is missing.");

            bool passed = failures.Count == 0;
            string report = passed
                ? "Duel: Genesis vertical-slice validation PASS — scene, starter deck, progression formulas, card pool and import template are valid."
                : "Duel: Genesis vertical-slice validation FAILED:\n- " + string.Join("\n- ", failures);

            if (passed) Debug.Log(report);
            else Debug.LogError(report);

            if (showDialog)
                EditorUtility.DisplayDialog("Duel: Genesis Validation", report, "OK");
        }
    }
}
#endif
