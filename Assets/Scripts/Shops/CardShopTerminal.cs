using DuelGenesis.Cards;
using DuelGenesis.Economy;
using DuelGenesis.Interaction;
using UnityEngine;

namespace DuelGenesis.Shops
{
    public class CardShopTerminal : MonoBehaviour, IInteractable
    {
        public string shopName = "Genesis Card Shop";
        public int boosterPackCost = 1000;

        public string InteractionPrompt => CardDatabase.IsReady
            ? $"Buy Genesis Pack ({boosterPackCost:N0} GC)"
            : "Real card catalog required";

        public void Interact(GameObject interactor)
        {
            if (!CardDatabase.IsReady)
            {
                Debug.LogWarning("Cannot buy a pack: no production card catalog is loaded. Build the real DMO card catalog first.");
                return;
            }

            GenesisWallet wallet = interactor.GetComponent<GenesisWallet>();
            if (wallet == null)
            {
                Debug.LogWarning("Player has no GenesisWallet component.");
                return;
            }

            PackOpeningUI packUI = Object.FindFirstObjectByType<PackOpeningUI>();
            if (packUI != null && packUI.IsOpen)
                return;

            if (packUI == null)
            {
                GameObject systems = GameObject.Find("Genesis Runtime Systems");
                if (systems == null)
                    systems = new GameObject("Genesis Runtime Systems");

                packUI = systems.AddComponent<PackOpeningUI>();
            }

            if (!wallet.Spend(boosterPackCost))
            {
                Debug.Log($"Not enough GC. Pack costs {boosterPackCost} GC and player has {wallet.GenesisCredits} GC.");
                return;
            }

            if (!packUI.OpenPack(interactor))
            {
                wallet.Add(boosterPackCost);
                Debug.LogWarning("Pack opening failed, so the purchase was refunded.");
                return;
            }

            Debug.Log($"Purchased production booster pack for {boosterPackCost} GC.");
        }
    }
}
