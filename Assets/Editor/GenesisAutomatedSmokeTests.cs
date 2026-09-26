#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using DuelGenesis.Cards;
using DuelGenesis.Dueling;
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

            if (cards.Count < 20)
                failures.Add($"Expected a usable prototype card pool, found only {cards.Count} cards.");

            if (cards.Select(card => card.id).Distinct().Count() != cards.Count)
                failures.Add("Duplicate card IDs detected.");

            string[] requiredIds =
            {
                "DG001", "DG002", "DG003", "DG004", "DG005", "DG006", "DG007",
                "DG009", "DG010", "DG011", "DG012", "DG013", "DG014", "DG015",
                "DG016", "DG017", "DG018", "DG019", "DG020", "DG022", "DG023", "DG024"
            };

            foreach (string id in requiredIds)
            {
                if (CardDatabase.GetById(id) == null)
                    failures.Add($"Required duel-engine card {id} is missing.");
            }

            for (int i = 0; i < 250; i++)
            {
                CardData normal = CardDatabase.GetRandomCard(false);
                CardData guaranteed = CardDatabase.GetRandomCard(true);

                if (normal == null)
                {
                    failures.Add("Normal pack roll returned null.");
                    break;
                }

                if (guaranteed == null || guaranteed.rarity == CardRarity.Common)
                {
                    failures.Add("Guaranteed rare pack slot returned an invalid result.");
                    break;
                }
            }

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

            bool passed = failures.Count == 0;
            string report = passed
                ? "Duel: Genesis automated smoke tests PASS — v0.5 database, pack, deck and duel-controller API are valid."
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
