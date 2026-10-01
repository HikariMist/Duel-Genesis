using DuelGenesis.Cards;
using DuelGenesis.Interaction;
using UnityEngine;

namespace DuelGenesis.Shops
{
    public class CardShopTerminal : MonoBehaviour, IInteractable
    {
        public string shopName = "Genesis Card Shop";
        public int boosterPackCost = 1000;

        public string InteractionPrompt => CardDatabase.IsReady
            ? $"Browse booster packs ({GenesisPacks.ShopBlurb(shopName)})"
            : "Real card catalog required";

        /// <summary>Opens the pack counter: nine boosters with their own wrapper art (see GenesisPacks).</summary>
        public void Interact(GameObject interactor)
        {
            if (!CardDatabase.IsReady)
            {
                Debug.LogWarning("Cannot buy a pack: no production card catalog is loaded. Build the real DMO card catalog first.");
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

            packUI.OpenShop(interactor, shopName);
        }
    }
}
