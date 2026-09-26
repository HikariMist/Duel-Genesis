#if UNITY_EDITOR
using DuelGenesis.Cards;
using DuelGenesis.Core;
using DuelGenesis.Economy;
using UnityEditor;
using UnityEngine;

namespace DuelGenesis.EditorTools
{
    public static class GenesisDevelopmentTools
    {
        [MenuItem("Duel Genesis/DEV/Reset All Local Prototype Progress")]
        public static void ResetAllProgress()
        {
            if (!EditorUtility.DisplayDialog(
                    "Reset Duel: Genesis Progress",
                    "This clears local GC, cards, deck, duelist level, record, pack count and starter-claim data for this Unity project. Continue?",
                    "Reset",
                    "Cancel"))
                return;

            string[] keys =
            {
                "DUEL_GENESIS_GC_V1",
                "DUEL_GENESIS_COLLECTION_V1",
                "DUEL_GENESIS_MAIN_DECK_V1",
                "DUEL_GENESIS_LEVEL_V1",
                "DUEL_GENESIS_XP_V1",
                "DUEL_GENESIS_WINS_V1",
                "DUEL_GENESIS_LOSSES_V1",
                "DUEL_GENESIS_PACKS_V1",
                "DUEL_GENESIS_STARTER_V1"
            };

            foreach (string key in keys)
                PlayerPrefs.DeleteKey(key);

            PlayerPrefs.Save();
            Debug.Log("Duel: Genesis local prototype progress reset. Enter Play Mode to receive the first-run starter loadout.");
        }

        [MenuItem("Duel Genesis/DEV/Add 50,000 GC In Play Mode")]
        public static void AddTestCredits()
        {
            if (!Application.isPlaying)
            {
                EditorUtility.DisplayDialog("Duel: Genesis", "Enter Play Mode first.", "OK");
                return;
            }

            GenesisWallet wallet = Object.FindFirstObjectByType<GenesisWallet>();
            if (wallet == null)
            {
                EditorUtility.DisplayDialog("Duel: Genesis", "GenesisWallet was not found.", "OK");
                return;
            }

            wallet.Add(50000);
        }

        [MenuItem("Duel Genesis/DEV/Re-run Runtime Diagnostics In Play Mode")]
        public static void RunRuntimeDiagnostics()
        {
            if (!Application.isPlaying)
            {
                EditorUtility.DisplayDialog("Duel: Genesis", "Enter Play Mode first.", "OK");
                return;
            }

            GenesisRuntimeDiagnostics.RunChecks();
            EditorUtility.DisplayDialog("Duel: Genesis Diagnostics", GenesisRuntimeDiagnostics.LastReport, "OK");
        }
    }
}
#endif
