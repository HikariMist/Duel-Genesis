using DuelGenesis.Cards;
using DuelGenesis.Dueling;
using DuelGenesis.Economy;
using DuelGenesis.Player;
using DuelGenesis.Progression;
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

            if (playerObject.GetComponent<DuelistProfile>() == null)
                playerObject.AddComponent<DuelistProfile>();

            GameObject systems = GameObject.Find("Genesis Runtime Systems");
            if (systems == null)
                systems = new GameObject("Genesis Runtime Systems");

            // Awake loads optional StreamingAssets/duel_genesis_cards.json immediately,
            // before first-frame starter/deck systems run.
            if (Object.FindFirstObjectByType<ExternalCardCatalogLoader>() == null)
                systems.AddComponent<ExternalCardCatalogLoader>();

            if (Object.FindFirstObjectByType<PackOpeningUI>() == null)
                systems.AddComponent<PackOpeningUI>();

            if (Object.FindFirstObjectByType<DeckBuilderUI>() == null)
                systems.AddComponent<DeckBuilderUI>();

            if (Object.FindFirstObjectByType<DuelPrototype>() == null)
                systems.AddComponent<DuelPrototype>();

            if (Object.FindFirstObjectByType<GenesisHUD>() == null)
                systems.AddComponent<GenesisHUD>();

            if (Object.FindFirstObjectByType<GenesisPauseMenu>() == null)
                systems.AddComponent<GenesisPauseMenu>();

            if (Object.FindFirstObjectByType<PrototypeVisualPolish>() == null)
                systems.AddComponent<PrototypeVisualPolish>();

            if (Object.FindFirstObjectByType<RuntimeTextMeshFixer>() == null)
                systems.AddComponent<RuntimeTextMeshFixer>();

            if (Object.FindFirstObjectByType<DuelArenaFX>() == null)
                systems.AddComponent<DuelArenaFX>();

            if (Object.FindFirstObjectByType<GenesisRuntimeDiagnostics>() == null)
                systems.AddComponent<GenesisRuntimeDiagnostics>();

            if (Object.FindFirstObjectByType<StarterLoadout>() == null)
                systems.AddComponent<StarterLoadout>();

            if (Object.FindFirstObjectByType<ProgressionBridge>() == null)
                systems.AddComponent<ProgressionBridge>();

            if (Object.FindFirstObjectByType<GenesisWelcomeUI>() == null)
                systems.AddComponent<GenesisWelcomeUI>();
        }
    }
}
