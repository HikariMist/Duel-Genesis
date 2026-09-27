#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DuelGenesis.Cards;
using DuelGenesis.Dueling;
using DuelGenesis.UI;
using UnityEditor;
using UnityEngine;

namespace DuelGenesis.EditorTools
{
    [InitializeOnLoad]
    public static class GenesisAutomatedSmokeTests
    {
        static GenesisAutomatedSmokeTests()
        {
            EditorApplication.delayCall += RunAutomatically;
        }

        private static void RunAutomatically()
        {
            Run(false);
        }

        [MenuItem("Duel Genesis/Run Automated Smoke Tests")]
        public static void RunFromMenu()
        {
            Run(true);
        }

        private static void Run(bool showDialog)
        {
            List<string> failures = new();
            IReadOnlyList<CardData> cards = CardDatabase.All;

            if (cards.Any(card => card != null && CardDatabase.IsPrototypeId(card.id)))
                failures.Add("Retired prototype cards are still hardcoded into CardDatabase.");

            if (cards.Where(card => card != null).Select(card => card.id).Distinct().Count() != cards.Count)
                failures.Add("Duplicate card IDs detected in the currently loaded production pool.");

            if (PlayerDeck.MinimumDeckSize != 40)
                failures.Add("Minimum deck size must be 40.");
            if (PlayerDeck.MaximumDeckSize != 60)
                failures.Add("Maximum deck size must be 60.");
            if (PlayerDeck.MaximumCopiesPerCard != 3)
                failures.Add("Maximum copies per card must be 3.");

            System.Type duelType = typeof(DuelGameController);
            if (duelType.GetMethod("StartDuel") == null)
                failures.Add("DuelGameController.StartDuel is missing.");
            if (duelType.GetMethod("CloseDuel") == null)
                failures.Add("DuelGameController.CloseDuel is missing.");
            if (duelType.GetProperty("PlayerMonsters") == null || duelType.GetProperty("CpuMonsters") == null)
                failures.Add("DuelGameController monster-state API is missing.");
            if (duelType.GetProperty("PlayerBackrow") == null || duelType.GetProperty("CpuBackrow") == null)
                failures.Add("DuelGameController backrow-state API is missing.");

            if (!System.Enum.IsDefined(typeof(DuelMonsterPosition), DuelMonsterPosition.FaceUpAttack) ||
                !System.Enum.IsDefined(typeof(DuelMonsterPosition), DuelMonsterPosition.FaceUpDefense) ||
                !System.Enum.IsDefined(typeof(DuelMonsterPosition), DuelMonsterPosition.FaceDownDefense))
                failures.Add("Monster position states are incomplete.");

            if (typeof(GenesisMainMenu).GetProperty("IsOpen") == null)
                failures.Add("GenesisMainMenu modal API is missing.");
            if (typeof(GenesisProfilePanel).GetProperty("IsOpen") == null)
                failures.Add("GenesisProfilePanel modal API is missing.");
            if (DuelRules.HandSizeLimit != 6 || DuelRules.StartingLifePoints != 8000 || DuelRules.OpeningHandSize != 5)
                failures.Add("Official duel constants are wrong (hand limit 6, 8000 LP, 5-card opening hand).");
            if (typeof(DuelBoardView).GetMethod("BeginPresentation") == null || typeof(DuelHud) == null)
                failures.Add("Duel presentation (DuelBoardView / DuelHud) is missing.");
            string engineReport = DuelEngineSelfTest.Run();
            if (!string.IsNullOrEmpty(engineReport))
                failures.Add(engineReport);

            if (!File.Exists("Assets/Scenes/GenesisPrototype.unity"))
                failures.Add("GenesisPrototype.unity is missing from Assets/Scenes.");

            bool passed = failures.Count == 0;
            string report = passed
                ? "Duel: Genesis automated smoke tests PASS — production-only card architecture, deck rules, duel and presentation APIs are valid."
                : "Duel: Genesis automated smoke tests FAILED:\n- " + string.Join("\n- ", failures);

            if (passed)
                Debug.Log(report);
            else
                Debug.LogError(report);

            if (showDialog)
                EditorUtility.DisplayDialog("Duel: Genesis Smoke Tests", report, "OK");
        }
    }
}
#endif
