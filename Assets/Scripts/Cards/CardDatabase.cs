using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DuelGenesis.Cards
{
    public static class CardDatabase
    {
        private static readonly List<CardData> Cards = new()
        {
            // COMMONS
            new CardData("DG001", "Genesis Apprentice", CardKind.Monster, CardRarity.Common, "DARK", "Spellcaster / Effect", 3, 1200, 900, "If this card is Normal Summoned: draw 1 card, then place 1 card from your hand on the bottom of the Deck."),
            new CardData("DG002", "Metro Familiar", CardKind.Monster, CardRarity.Common, "DARK", "Beast / Effect", 2, 800, 1000, "Once per turn, if you control a Spellcaster monster, this card gains 400 ATK until the end of the turn."),
            new CardData("DG003", "Circuit Squire", CardKind.Monster, CardRarity.Common, "LIGHT", "Warrior / Effect", 4, 1600, 1200, "When this card destroys a monster by battle: gain 300 LP."),
            new CardData("DG004", "Neon Wyvern", CardKind.Monster, CardRarity.Common, "WIND", "Dragon / Effect", 4, 1700, 900, "If this card attacks, it gains 200 ATK during damage calculation only."),
            new CardData("DG005", "Alley Alchemist", CardKind.Monster, CardRarity.Common, "EARTH", "Spellcaster / Effect", 3, 1300, 1500, "Once per turn: you can discard 1 card; gain 500 LP."),
            new CardData("DG006", "Data Imp", CardKind.Monster, CardRarity.Common, "DARK", "Fiend / Effect", 1, 300, 300, "If this card is sent from the field to the GY: your opponent loses 200 LP."),
            new CardData("DG007", "Pulse Guardian", CardKind.Monster, CardRarity.Common, "LIGHT", "Machine / Effect", 4, 1400, 1900, "While this card is in Defense Position, other monsters you control cannot be targeted for attacks."),
            new CardData("DG008", "Street Duelist", CardKind.Monster, CardRarity.Common, "EARTH", "Warrior / Normal", 4, 1800, 1000, "A talented underground duelist who fights for reputation across Genesis City."),
            new CardData("DG009", "Quick Charge", CardKind.Spell, CardRarity.Common, "SPELL", "Quick-Play Spell", 0, 0, 0, "Target 1 monster you control; it gains 500 ATK until the end of this turn."),
            new CardData("DG010", "Genesis Recycle", CardKind.Spell, CardRarity.Common, "SPELL", "Normal Spell", 0, 0, 0, "Target 1 monster in your GY; shuffle it into the Deck, then draw 1 card."),
            new CardData("DG011", "Back Alley Ambush", CardKind.Trap, CardRarity.Common, "TRAP", "Normal Trap", 0, 0, 0, "When an opponent's monster declares an attack: that attacking monster loses 700 ATK until the end of the turn."),
            new CardData("DG012", "Signal Jam", CardKind.Trap, CardRarity.Common, "TRAP", "Counter Trap", 0, 0, 0, "When your opponent activates a Spell Card: discard 1 card; negate that activation."),

            // RARES
            new CardData("DG013", "Genesis Sorcerer", CardKind.Monster, CardRarity.Rare, "DARK", "Spellcaster / Effect", 6, 2300, 1800, "If you activated a Spell this turn, you can Normal Summon this card without Tributing. You can only use this effect once per turn."),
            new CardData("DG014", "Chrome Paladin", CardKind.Monster, CardRarity.Rare, "LIGHT", "Warrior / Effect", 5, 2100, 2000, "Once per turn, when another card you control would be destroyed, you can reduce this card's ATK by 500 instead."),
            new CardData("DG015", "Holo Dragon", CardKind.Monster, CardRarity.Rare, "LIGHT", "Dragon / Effect", 6, 2400, 1600, "If this card is Special Summoned: reveal the top card of your Deck. If it is a Monster Card, this card gains 400 ATK."),
            new CardData("DG016", "Arcane Transit", CardKind.Spell, CardRarity.Rare, "SPELL", "Normal Spell", 0, 0, 0, "Add 1 Level 4 or lower Spellcaster monster from your Deck to your hand, then discard 1 card."),
            new CardData("DG017", "Ranked Barrier", CardKind.Trap, CardRarity.Rare, "TRAP", "Normal Trap", 0, 0, 0, "If you would take 1500 or more battle damage from one attack, reduce that damage to 0."),
            new CardData("DG018", "Genesis Market", CardKind.Spell, CardRarity.Rare, "SPELL", "Field Spell", 0, 0, 0, "Once per turn, when you draw a card outside your Draw Phase, gain 300 LP."),

            // SUPER RARES
            new CardData("DG019", "Arcane Mainframe", CardKind.Monster, CardRarity.SuperRare, "DARK", "Machine / Spellcaster / Effect", 7, 2600, 2300, "Once per turn: banish 1 Spell from your GY; negate the effects of 1 face-up card on the field until the end of the turn."),
            new CardData("DG020", "Genesis Valkyrie", CardKind.Monster, CardRarity.SuperRare, "LIGHT", "Warrior / Effect", 7, 2700, 2100, "If you control no monsters, you can Special Summon this card from your hand, but its ATK becomes 2000 until the end of the turn."),
            new CardData("DG021", "Quantum Ritual", CardKind.Spell, CardRarity.SuperRare, "SPELL", "Ritual Spell", 0, 0, 0, "Ritual Summon 1 Ritual Monster from your hand by Tributing monsters whose total Levels equal or exceed its Level."),
            new CardData("DG022", "Zero-Latency Counter", CardKind.Trap, CardRarity.SuperRare, "TRAP", "Counter Trap", 0, 0, 0, "When a monster effect is activated: pay 800 LP; negate the activation and destroy that monster."),

            // ULTRA RARES
            new CardData("DG023", "Genesis Archmage", CardKind.Monster, CardRarity.UltraRare, "DARK", "Spellcaster / Effect", 8, 2900, 2500, "Once per turn: target 1 Spell/Trap on the field; banish it. If you have 3 or more Spells in your GY, this effect can be activated during either player's turn."),
            new CardData("DG024", "Genesis Leviathan", CardKind.Monster, CardRarity.UltraRare, "WATER", "Sea Serpent / Effect", 8, 3000, 2400, "If this card is Tribute Summoned: return up to 2 other cards on the field to the hand. This card cannot attack the turn you use this effect.")
        };

        public static IReadOnlyList<CardData> All => Cards;

        public static CardData GetById(string id)
        {
            return Cards.FirstOrDefault(c => c.id == id);
        }

        public static void RegisterOrReplace(CardData card)
        {
            if (card == null || string.IsNullOrWhiteSpace(card.id))
                return;

            int index = Cards.FindIndex(existing => existing.id == card.id);
            if (index >= 0)
                Cards[index] = card;
            else
                Cards.Add(card);
        }

        public static int RegisterOrReplace(IEnumerable<CardData> cards)
        {
            if (cards == null) return 0;
            int count = 0;
            foreach (CardData card in cards)
            {
                if (card == null || string.IsNullOrWhiteSpace(card.id))
                    continue;
                RegisterOrReplace(card);
                count++;
            }
            return count;
        }

        public static CardData GetRandomCard(bool guaranteedRareOrBetter = false)
        {
            CardRarity rarity = RollRarity(guaranteedRareOrBetter);
            List<CardData> pool = Cards.Where(c => c.rarity == rarity).ToList();

            if (pool.Count == 0)
                pool = Cards.ToList();

            return pool[Random.Range(0, pool.Count)];
        }

        private static CardRarity RollRarity(bool guaranteedRareOrBetter)
        {
            float roll = Random.value * 100f;

            if (guaranteedRareOrBetter)
            {
                if (roll < 3f) return CardRarity.UltraRare;
                if (roll < 15f) return CardRarity.SuperRare;
                return CardRarity.Rare;
            }

            if (roll < 1.5f) return CardRarity.UltraRare;
            if (roll < 8f) return CardRarity.SuperRare;
            if (roll < 30f) return CardRarity.Rare;
            return CardRarity.Common;
        }
    }
}
