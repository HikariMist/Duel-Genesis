#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using DuelGenesis.Cards;
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
                "DG001", "DG006", "DG009", "DG010", "DG011", "DG012",
                "DG013", "DG016", "DG017", "DG018", "DG020", "DG023", "DG024"
            };

            foreach (string id in requiredIds)
            {
                if (CardDatabase.GetById(id) == null)
                    failures.Add($"Required prototype card {id} is missing.");
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

            bool passed = failures.Count == 0;
            string report = passed
                ? "Duel: Genesis automated smoke tests PASS — database, pack rolls and deck constants are valid."
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
