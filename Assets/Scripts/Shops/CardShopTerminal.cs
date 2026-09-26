using DuelGenesis.Economy;
using DuelGenesis.Interaction;
using UnityEngine;

namespace DuelGenesis.Shops
{
    public class CardShopTerminal : MonoBehaviour, IInteractable
    {
        public string shopName = "Genesis Card Shop";
        public int boosterPackCost = 1000;

        public string InteractionPrompt => $"Open {shopName}";

        public void Interact(GameObject interactor)
        {
            GenesisWallet wallet = interactor.GetComponent<GenesisWallet>();
            if (wallet == null)
            {
                Debug.LogWarning("Player has no GenesisWallet component.");
                return;
            }

            if (wallet.Spend(boosterPackCost))
            {
                Debug.Log($"Purchased 1 booster pack for {boosterPackCost} GC. Pack opening UI will be connected next.");
            }
            else
            {
                Debug.Log($"Not enough GC. Pack costs {boosterPackCost} GC and player has {wallet.genesisCredits} GC.");
            }
        }
    }
}
