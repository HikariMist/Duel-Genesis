using UnityEngine;

namespace DuelGenesis.Economy
{
    public class GenesisWallet : MonoBehaviour
    {
        [Min(0)] public int genesisCredits = 5000;

        public bool CanAfford(int amount) => genesisCredits >= Mathf.Max(0, amount);

        public bool Spend(int amount)
        {
            amount = Mathf.Max(0, amount);
            if (!CanAfford(amount)) return false;
            genesisCredits -= amount;
            Debug.Log($"Spent {amount} GC. Balance: {genesisCredits} GC");
            return true;
        }

        public void Add(int amount)
        {
            genesisCredits += Mathf.Max(0, amount);
            Debug.Log($"Earned {amount} GC. Balance: {genesisCredits} GC");
        }
    }
}
