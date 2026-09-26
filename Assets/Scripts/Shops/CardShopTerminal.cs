using DuelGenesis.Economy;
using DuelGenesis.Interaction;
using UnityEngine;

namespace DuelGenesis.Shops
{
    public class CardShopTerminal : MonoBehaviour, IInteractable
    {
        public string shopName = "Genesis Card Shop";
        public int boosterPackCost = 1000;

        public string InteractionPrompt => $"Buy Genesis Pack ({boosterPackCost:N0} GC)";

        public void Interact(GameObject interactor)
        {
            GenesisWallet wallet = interactor.GetComponent<GenesisWallet>();
            if (wallet == null)
            {
                Debug.LogWarning("Player has no GenesisWallet component.");
                return;
            }

            PackOpeningUI packUI = Object.FindFirstObjectByType<PackOpeningUI>();
            if (packUI != null && packUI.IsOpen)
                return;

            if (!wallet.Spend(boosterPackCost))
            {
                Debug.Log($"Not enough GC. Pack costs {boosterPackCost} GC and player has {wallet.GenesisCredits} GC.");
                return;
            }

            if (packUI == null)
            {
                GameObject systems = GameObject.Find("Genesis Runtime Systems");
                if (systems == null)
                    systems = new GameObject("Genesis Runtime Systems");

                packUI = systems.AddComponent<PackOpeningUI>();
            }

            Debug.Log($"Purchased Genesis booster pack for {boosterPackCost} GC.");
            packUI.OpenPack(interactor);
        }
    }
}
