using DuelGenesis.Cards;
using DuelGenesis.Economy;
using DuelGenesis.Player;
using DuelGenesis.Shops;
using DuelGenesis.UI;
using UnityEngine;

namespace DuelGenesis.Core
{
    public static class GenesisBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Initialize()
        {
            ThirdPersonPlayerController player = Object.FindFirstObjectByType<ThirdPersonPlayerController>();
            if (player == null) return;

            GameObject playerObject = player.gameObject;

            if (playerObject.GetComponent<PlayerCollection>() == null)
                playerObject.AddComponent<PlayerCollection>();

            if (playerObject.GetComponent<PlayerDeck>() == null)
                playerObject.AddComponent<PlayerDeck>();

            if (playerObject.GetComponent<GenesisWallet>() == null)
                playerObject.AddComponent<GenesisWallet>();

            GameObject systems = GameObject.Find("Genesis Runtime Systems");
            if (systems == null)
                systems = new GameObject("Genesis Runtime Systems");

            if (Object.FindFirstObjectByType<PackOpeningUI>() == null)
                systems.AddComponent<PackOpeningUI>();

            if (Object.FindFirstObjectByType<DeckBuilderUI>() == null)
                systems.AddComponent<DeckBuilderUI>();

            if (Object.FindFirstObjectByType<GenesisHUD>() == null)
                systems.AddComponent<GenesisHUD>();
        }
    }
}
