using System;
using UnityEngine;

namespace DuelGenesis.Economy
{
    public class GenesisWallet : MonoBehaviour
    {
        private const string SaveKey = "DUEL_GENESIS_GC_V1";

        [Min(0)] public int genesisCredits = 5000;

        public event Action<int> BalanceChanged;

        public int GenesisCredits => genesisCredits;

        private void Awake()
        {
            genesisCredits = PlayerPrefs.GetInt(SaveKey, genesisCredits);
            BalanceChanged?.Invoke(genesisCredits);
        }

        public bool CanAfford(int amount) => genesisCredits >= Mathf.Max(0, amount);

        public bool Spend(int amount)
        {
            amount = Mathf.Max(0, amount);
            if (!CanAfford(amount)) return false;

            genesisCredits -= amount;
            Save();
            Debug.Log($"Spent {amount} GC. Balance: {genesisCredits} GC");
            return true;
        }

        public void Add(int amount)
        {
            genesisCredits += Mathf.Max(0, amount);
            Save();
            Debug.Log($"Earned {amount} GC. Balance: {genesisCredits} GC");
        }

        private void Save()
        {
            PlayerPrefs.SetInt(SaveKey, genesisCredits);
            PlayerPrefs.Save();
            BalanceChanged?.Invoke(genesisCredits);
        }

        [ContextMenu("Reset Genesis Credits")]
        public void ResetCredits()
        {
            genesisCredits = 5000;
            Save();
            Debug.Log("Genesis Credits reset to 5000 GC.");
        }
    }
}
