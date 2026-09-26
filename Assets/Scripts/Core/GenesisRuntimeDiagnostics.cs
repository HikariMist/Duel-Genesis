using System.Collections.Generic;
using System.Linq;
using DuelGenesis.Cards;
using DuelGenesis.Dueling;
using DuelGenesis.Progression;
using DuelGenesis.Shops;
using DuelGenesis.UI;
using UnityEngine;

namespace DuelGenesis.Core
{
    public class GenesisRuntimeDiagnostics : MonoBehaviour
    {
        public static bool LastPassed { get; private set; }
        public static string LastReport { get; private set; } = "Not run";

        private void Start()
        {
            RunChecks();
        }

        public static void RunChecks()
        {
            List<string> failures = new();
            IReadOnlyList<CardData> cards = CardDatabase.All;

            if (cards == null || cards.Count == 0)
                failures.Add("Card database is empty.");
            else
            {
                if (cards.Any(card => card == null))
                    failures.Add("Card database contains a null card.");

                if (cards.Where(card => card != null).Select(card => card.id).Distinct().Count() != cards.Count)
                    failures.Add("Card database contains duplicate IDs.");

                foreach (CardData card in cards.Where(card => card != null))
                {
                    if (string.IsNullOrWhiteSpace(card.id) || string.IsNullOrWhiteSpace(card.cardName))
                        failures.Add("A card is missing an ID or name.");
                    if (card.kind == CardKind.Monster && (card.attack < 0 || card.defense < 0 || card.level < 0))
                        failures.Add($"{card.cardName} has invalid monster stats.");
                }

                if (!cards.Any(card => card.kind == CardKind.Monster)) failures.Add("No Monster cards found.");
                if (!cards.Any(card => card.kind == CardKind.Spell)) failures.Add("No Spell cards found.");
                if (!cards.Any(card => card.kind == CardKind.Trap)) failures.Add("No Trap cards found.");

                string[] requiredEffectCards =
                {
                    "DG001", "DG002", "DG003", "DG004", "DG005", "DG006", "DG007",
                    "DG009", "DG010", "DG011", "DG012", "DG013", "DG014", "DG015",
                    "DG016", "DG017", "DG018", "DG019", "DG020", "DG022", "DG023", "DG024"
                };
                foreach (string id in requiredEffectCards)
                {
                    if (CardDatabase.GetById(id) == null)
                        failures.Add($"Required duel-engine card {id} is missing.");
                }
            }

            if (PlayerDeck.MinimumDeckSize != 40 || PlayerDeck.MaximumDeckSize != 60 || PlayerDeck.MaximumCopiesPerCard != 3)
                failures.Add("Deck rule constants are incorrect.");

            if (StarterLoadout.StarterDeckSize != 40)
                failures.Add("Starter loadout is not exactly 40 cards.");

            if (Object.FindFirstObjectByType<DuelGameController>() == null)
                failures.Add("DuelGameController v0.5 runtime system is missing.");
            if (Object.FindFirstObjectByType<PackOpeningUI>() == null)
                failures.Add("PackOpeningUI runtime system is missing.");
            if (Object.FindFirstObjectByType<DeckBuilderUI>() == null)
                failures.Add("DeckBuilderUI runtime system is missing.");
            if (Object.FindFirstObjectByType<DuelistProfile>() == null)
                failures.Add("DuelistProfile progression system is missing.");
            if (Object.FindFirstObjectByType<ProgressionBridge>() == null)
                failures.Add("ProgressionBridge is missing.");
            if (Object.FindFirstObjectByType<ExternalCardCatalogLoader>() == null)
                failures.Add("ExternalCardCatalogLoader is missing.");
            if (Object.FindFirstObjectByType<DuelArenaFX>() == null)
                failures.Add("DuelArenaFX is missing.");
            if (Object.FindFirstObjectByType<DuelFieldVisualizer>() == null)
                failures.Add("DuelFieldVisualizer is missing.");
            if (Object.FindFirstObjectByType<DuelBackrowVisualizer>() == null)
                failures.Add("DuelBackrowVisualizer is missing.");
            if (Object.FindFirstObjectByType<DuelPileVisualizer>() == null)
                failures.Add("DuelPileVisualizer is missing.");
            if (Object.FindFirstObjectByType<DuelPresentationOverlay>() == null)
                failures.Add("DuelPresentationOverlay is missing.");
            if (Object.FindFirstObjectByType<GenesisMainMenu>() == null)
                failures.Add("GenesisMainMenu is missing.");
            if (Object.FindFirstObjectByType<GenesisProfilePanel>() == null)
                failures.Add("GenesisProfilePanel is missing.");
            if (Object.FindFirstObjectByType<GenesisPauseMenu>() == null)
                failures.Add("GenesisPauseMenu is missing.");

            PlayerDeck deck = Object.FindFirstObjectByType<PlayerDeck>();
            PlayerCollection collection = Object.FindFirstObjectByType<PlayerCollection>();
            if (deck != null)
            {
                foreach (DeckEntry entry in deck.Entries)
                {
                    if (CardDatabase.GetById(entry.cardId) == null)
                        failures.Add($"Deck references missing card ID {entry.cardId}.");
                    if (entry.quantity < 0 || entry.quantity > PlayerDeck.MaximumCopiesPerCard)
                        failures.Add($"Deck contains invalid copy count for {entry.cardId}.");
                    if (collection != null && entry.quantity > collection.GetQuantity(entry.cardId))
                        failures.Add($"Deck uses more copies of {entry.cardId} than the player owns.");
                }
            }

            LastPassed = failures.Count == 0;
            LastReport = LastPassed ? "PASS" : string.Join(" | ", failures);

            if (LastPassed)
                Debug.Log("Duel: Genesis runtime diagnostics PASS — v0.6 playable shell, duel engine and presentation systems are present.");
            else
                Debug.LogError("Duel: Genesis runtime diagnostics FAILED: " + LastReport);
        }
    }
}
