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
            }

            if (PlayerDeck.MinimumDeckSize != 40 || PlayerDeck.MaximumDeckSize != 60 || PlayerDeck.MaximumCopiesPerCard != 3)
                failures.Add("Deck rule constants are incorrect.");

            if (StarterLoadout.StarterDeckSize != 40)
                failures.Add("Starter loadout is not exactly 40 cards.");

            if (Object.FindFirstObjectByType<DuelPrototype>() == null)
                failures.Add("DuelPrototype runtime system is missing.");
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
                Debug.Log("Duel: Genesis runtime diagnostics PASS — playable vertical slice systems are present.");
            else
                Debug.LogError("Duel: Genesis runtime diagnostics FAILED: " + LastReport);
        }
    }
}
