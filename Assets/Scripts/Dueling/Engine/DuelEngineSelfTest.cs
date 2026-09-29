using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DuelGenesis.Cards;

namespace DuelGenesis.Dueling
{
    /// <summary>
    /// Headless verification of the rules engine: scripted rule checks plus full CPU-vs-CPU duels
    /// with invariant checks after every action (no card lost or duplicated, zone limits, LP).
    /// Returns an empty string when everything passes.
    /// </summary>
    public static class DuelEngineSelfTest
    {
        public static string LastSummary { get; private set; } = string.Empty;

        public static string Run(int simulatedDuels = 25)
        {
            var failures = new List<string>();
            try
            {
                CheckTurnStructure(failures);
                CheckTributes(failures);
                CheckBattle(failures);
                CheckHandLimit(failures);
                CheckEffects(failures);
                CheckFlipEffects(failures);
                CheckFieldSpells(failures);
                CheckCounterTraps(failures);
                SimulateDuels(simulatedDuels, failures);
            }
            catch (Exception exception)
            {
                failures.Add("Engine exception: " + exception);
            }
            return failures.Count == 0 ? string.Empty : "Rules engine: " + string.Join(" | ", failures);
        }

        // ------------------------------------------------------------------ synthetic cards

        private static int _id = 900000;

        private static CardData Monster(string name, int level, int atk, int def, string type = "Warrior / Normal", string attribute = "EARTH", string text = "")
        {
            return new CardData((_id++).ToString(), name, CardKind.Monster, CardRarity.Common, attribute, type, level, atk, def, text,
                type.Contains("Normal") ? CardFrameKind.NormalMonster : CardFrameKind.EffectMonster);
        }

        private static CardData Spell(string name, string property = "Normal", string text = "") =>
            new CardData((_id++).ToString(), name, CardKind.Spell, CardRarity.Common, "", property, 0, 0, 0, text, CardFrameKind.Spell);

        private static CardData Trap(string name, string property = "Normal", string text = "") =>
            new CardData((_id++).ToString(), name, CardKind.Trap, CardRarity.Common, "", property, 0, 0, 0, text, CardFrameKind.Trap);

        private static List<CardData> VanillaDeck(int count = 40)
        {
            var deck = new List<CardData>();
            for (int i = 0; i < count; i++) deck.Add(Monster("Vanilla " + i, 4, 1000 + (i % 8) * 100, 1000));
            return deck;
        }

        private static List<CardData> StapleDeck(Random random)
        {
            var deck = new List<CardData>();
            string[] spells = { "Pot of Greed", "Dark Hole", "Heavy Storm", "Mystical Space Typhoon", "Swords of Revealing Light", "Change of Heart", "Premature Burial", "Rush Recklessly", "Graceful Charity", "Book of Moon", "Smashing Ground", "Nobleman of Crossout", "Upstart Goblin", "Card Destruction", "Enemy Controller", "Harpie's Feather Duster" };
            string[] traps = { "Mirror Force", "Magic Cylinder", "Trap Hole", "Sakuretsu Armor", "Negate Attack", "Torrential Tribute", "Solemn Judgment", "Call of the Haunted", "Magic Jammer", "Seven Tools of the Bandit", "Bottomless Trap Hole", "Dust Tornado", "Just Desserts", "Threatening Roar", "Spellbinding Circle" };
            for (int i = 0; i < 20; i++) deck.Add(Monster("Beater " + i, 4, 1200 + random.Next(0, 7) * 100, 800 + random.Next(0, 10) * 100, "Warrior / Normal", random.Next(2) == 0 ? "DARK" : "LIGHT"));
            for (int i = 0; i < 3; i++) deck.Add(Monster("Tribute Five " + i, 5, 2100, 1500));
            for (int i = 0; i < 2; i++) deck.Add(Monster("Tribute Seven " + i, 7, 2600, 2100, "Dragon / Normal", "DARK"));
            deck.Add(Monster("Buster Blader", 7, 2600, 2300, "Warrior / Effect", "EARTH", "Gains 500 ATK for each Dragon monster your opponent controls or is in their GY."));
            deck.Add(Monster("Man-Eater Bug", 2, 450, 600, "Insect / Flip Effect", "EARTH", "FLIP: Target 1 monster on the field; destroy it."));
            deck.Add(Monster("Penguin Soldier", 2, 750, 500, "Aqua / Flip Effect", "WATER", "FLIP: You can target up to 2 monsters on the field; return those targets to the hand."));
            deck.Add(Monster("Morphing Jar", 2, 700, 600, "Rock / Flip Effect", "EARTH", "FLIP: Both players discard as many cards as possible from their hands, then each player draws 5 cards."));
            deck.Add(Monster("Cyber Jar", 3, 900, 900, "Rock / Flip Effect", "DARK", "FLIP: Destroy all monsters on the field..."));
            deck.Add(Monster("Spear Cretin", 2, 500, 500, "Fiend / Flip Effect", "DARK", "FLIP: When this card is sent to the Graveyard after being flipped..."));
            deck.Add(Spell("Axe of Despair", "Equip", "The equipped monster gains 1000 ATK."));
            deck.Add(Spell("Mystic Plasma Zone", "Field", "Increase the ATK of all DARK monsters by 500 points and decreases their DEF by 400 points."));
            while (deck.Count < 40)
            {
                bool trap = random.Next(2) == 0;
                deck.Add(trap ? Trap(traps[random.Next(traps.Length)], traps[0] == "" ? "Normal" : "Normal") : Spell(spells[random.Next(spells.Length)], "Normal"));
            }
            // Fix property lines for the cards that need them.
            foreach (CardData c in deck)
            {
                if (c.cardName is "Mystical Space Typhoon" or "Rush Recklessly" or "Book of Moon" or "Enemy Controller") c.typeLine = "Quick-Play";
                if (c.cardName is "Swords of Revealing Light") c.typeLine = "Normal";
                if (c.cardName is "Premature Burial") c.typeLine = "Equip";
                if (c.cardName is "Call of the Haunted" or "Spellbinding Circle") c.typeLine = "Continuous";
                if (c.cardName is "Solemn Judgment" or "Magic Jammer" or "Seven Tools of the Bandit") c.typeLine = "Counter";
            }
            return deck;
        }

        private static DuelEngine NewEngine(List<CardData> a, List<CardData> b, int seed, bool aiBoth)
        {
            DuelEngine engine = new DuelEngine(seed);
            engine.Setup(0, "A", a);
            engine.Setup(1, "B", b);
            if (aiBoth)
            {
                engine.Deciders[0] = new DuelAi(0);
                engine.Deciders[1] = new DuelAi(1);
            }
            return engine;
        }

        private static void Expect(List<string> failures, bool condition, string message)
        {
            if (!condition) failures.Add(message);
        }

        // ------------------------------------------------------------------ scripted rule checks

        private static void CheckTurnStructure(List<string> failures)
        {
            DuelEngine e = NewEngine(VanillaDeck(), VanillaDeck(), 1, aiBoth: true);
            e.StartDuel(0);
            Expect(failures, e.TurnNumber == 1 && e.TurnPlayer == 0 && e.Phase == DuelPhase.Main1, "Turn 1 should start in Main Phase 1.");
            Expect(failures, e.Me(0).Hand.Count == 5, "First player must not draw on turn 1 (hand should be 5).");
            Expect(failures, e.Me(1).Hand.Count == 5, "Second player opening hand should be 5.");
            Expect(failures, !e.CanEnterBattlePhase(0), "No Battle Phase on the first turn of the duel.");
            e.AdvancePhase(0);
            Expect(failures, e.TurnNumber == 2 && e.TurnPlayer == 1 && e.Phase == DuelPhase.Main1, "Main 1 of turn 1 should go straight to the End Phase and pass the turn.");
            Expect(failures, e.Me(1).Hand.Count == 6, "Second player draws at the start of turn 2 (hand should be 6).");
            e.AdvancePhase(1);
            Expect(failures, e.Phase == DuelPhase.Battle, "Turn 2 should be able to enter the Battle Phase.");
            e.AdvancePhase(1);
            Expect(failures, e.Phase == DuelPhase.Main2, "Battle Phase should lead to Main Phase 2.");
            e.AdvancePhase(1);
            Expect(failures, e.TurnNumber == 3 && e.TurnPlayer == 0, "Main Phase 2 should end the turn.");
            Expect(failures, e.Me(0).Deck.Count == 40 - 5 - 1, $"Deck count after one draw is wrong ({e.Me(0).Deck.Count}).");
        }

        private static void CheckTributes(List<string> failures)
        {
            List<CardData> deck = VanillaDeck();
            deck[39] = Monster("Big Seven", 7, 2500, 2000);
            deck[38] = Monster("Mid Five", 5, 2000, 1500);
            DuelEngine e = NewEngine(deck, VanillaDeck(), 2, aiBoth: true);
            e.StartDuel(0);
            DuelCard big = e.DebugPutInHand(0, "Big Seven");
            DuelCard mid = e.DebugPutInHand(0, "Mid Five");
            Expect(failures, !e.CanNormalSummon(0, big, false), "A Level 7 monster needs 2 Tributes.");
            Expect(failures, !e.CanNormalSummon(0, mid, false), "A Level 5 monster needs 1 Tribute.");
            DuelCard small = e.Me(0).Hand.First(c => c.Data.level == 4);
            Expect(failures, e.NormalSummon(0, small, false), "Level 4 Normal Summon failed.");
            Expect(failures, !e.CanNormalSummon(0, mid, false), "Only one Normal Summon per turn.");
        }

        private static void CheckBattle(List<string> failures)
        {
            List<CardData> a = VanillaDeck();
            List<CardData> b = VanillaDeck();
            a[39] = Monster("Striker", 4, 1800, 1000);
            b[39] = Monster("Guard", 4, 1200, 1500);
            DuelEngine e = NewEngine(a, b, 3, aiBoth: true);
            e.StartDuel(1);
            DuelCard guard = e.DebugPutInHand(1, "Guard");
            e.NormalSummon(1, guard, set: false);
            e.AdvancePhase(1);   // turn 1: Main1 -> End -> turn 2 (player 0)
            DuelCard striker = e.DebugPutInHand(0, "Striker");
            e.NormalSummon(0, striker, set: false);
            e.AdvancePhase(0);   // Battle
            DuelMonsterState atk = e.Me(0).MonstersOnField.First();
            DuelMonsterState def = e.Me(1).MonstersOnField.First();
            Expect(failures, e.DeclareAttack(0, atk, def), "Attack declaration failed.");
            Expect(failures, e.Me(1).LifePoints == 8000 - 600, $"1800 ATK vs 1200 ATK should deal 600 (LP {e.Me(1).LifePoints}).");
            Expect(failures, e.Me(1).MonsterCount == 0 && e.Me(1).Graveyard.Any(c => c.Name == "Guard"), "Losing monster should be destroyed.");
            Expect(failures, !e.CanAttack(0, atk), "A monster can only attack once per Battle Phase.");
        }

        private static void CheckHandLimit(List<string> failures)
        {
            DuelEngine e = NewEngine(VanillaDeck(), VanillaDeck(), 4, aiBoth: true);
            e.StartDuel(1);
            e.AdvancePhase(1);                 // turn 2 (player 0), hand 6
            e.AdvancePhase(0); e.AdvancePhase(0); e.AdvancePhase(0);   // to turn 3
            e.AdvancePhase(1); e.AdvancePhase(1); e.AdvancePhase(1);   // to turn 4 (player 0 draws to 7)
            Expect(failures, e.Me(0).Hand.Count == 7, $"Hand should be 7 before the End Phase (was {e.Me(0).Hand.Count}).");
            e.EndTurn(0);
            Expect(failures, e.Me(0).Hand.Count == DuelRules.HandSizeLimit, $"End Phase should discard down to 6 (was {e.Me(0).Hand.Count}).");
        }

        private static void CheckCounterTraps(List<string> failures)
        {
            // Barrel Behind the Door sends a burn back to the player who activated it.
            List<CardData> a = VanillaDeck();
            List<CardData> b = VanillaDeck();
            a[39] = Spell("Final Flame", "Normal", "Inflict 600 damage to your opponent.");
            b[39] = Trap("Barrel Behind the Door", "Counter", "Activate only when a card's effect that would inflict damage to you is activated. Your opponent takes the damage instead.");
            DuelEngine e = NewEngine(a, b, 31, aiBoth: true);
            e.StartDuel(1);
            e.SetSpellTrap(1, e.DebugPutInHand(1, "Barrel Behind the Door"));
            e.AdvancePhase(1);
            Expect(failures, e.Activate(0, e.DebugPutInHand(0, "Final Flame")), "Final Flame could not be activated.");
            Expect(failures, e.Me(1).LifePoints == 8000 && e.Me(0).LifePoints == 7400,
                $"Barrel Behind the Door should reflect 600 (LP {e.Me(0).LifePoints} / {e.Me(1).LifePoints}).");
        }

        private static void CheckFieldSpells(List<string> failures)
        {
            // Fusion Gate: activate from the hand, then use it face-up to Fusion Summon by banishing the materials.
            {
                List<CardData> a = VanillaDeck();
                a[39] = Spell("Fusion Gate", "Field", "While this card is on the field: The turn player can Fusion Summon 1 Fusion Monster from their Extra Deck, by banishing Fusion Materials listed on it from their hand or field.");
                a[38] = Monster("Baby Dragon", 3, 1200, 700, "Dragon / Normal", "WIND");
                a[37] = Monster("Alligator's Sword", 4, 1500, 1200, "Beast / Normal", "EARTH");
                a.Add(new CardData((_id++).ToString(), "Alligator's Sword Dragon", CardKind.Monster, CardRarity.Common, "WIND", "Dragon / Fusion / Effect", 5, 1700, 1500,
                    "\"Baby Dragon\" + \"Alligator's Sword\"\nThis card can attack directly...", CardFrameKind.FusionMonster));
                DuelEngine e = NewEngine(a, VanillaDeck(), 21, aiBoth: true);
                e.StartDuel(0);
                DuelCard gate = e.DebugPutInHand(0, "Fusion Gate");
                e.DebugPutInHand(0, "Baby Dragon");
                e.DebugPutInHand(0, "Alligator's Sword");
                Expect(failures, e.Activate(0, gate), "Fusion Gate could not be activated from the hand.");
                Expect(failures, e.FindBackrow(gate) != null, "Fusion Gate should stay on the field.");
                Expect(failures, e.CanActivate(0, gate), "Fusion Gate's on-field Fusion Summon should be usable.");
                e.Activate(0, gate);
                Expect(failures, e.Me(0).MonstersOnField.Any(m => m.Name == "Alligator's Sword Dragon"), "Fusion Gate should Fusion Summon Alligator's Sword Dragon.");
                Expect(failures, e.Me(0).Banished.Count == 2, $"Both materials should be banished (banished {e.Me(0).Banished.Count}).");
            }
            // A Legendary Ocean: a Level 5 WATER monster needs no Tribute.
            {
                List<CardData> a = VanillaDeck();
                a[39] = Spell("A Legendary Ocean", "Field", "All WATER monsters on the field gain 200 ATK/DEF. Reduce the Level of all WATER monsters in both players' hands and on the field by 1.");
                a[38] = Monster("Big Fish", 5, 1900, 1200, "Fish / Normal", "WATER");
                DuelEngine e = NewEngine(a, VanillaDeck(), 22, aiBoth: true);
                e.StartDuel(0);
                DuelCard fish = e.DebugPutInHand(0, "Big Fish");
                Expect(failures, e.TributesRequired(fish) == 1, "A Level 5 needs 1 Tribute without A Legendary Ocean.");
                e.Activate(0, e.DebugPutInHand(0, "A Legendary Ocean"));
                Expect(failures, e.TributesRequired(fish) == 0, "A Legendary Ocean should make the Level 5 WATER monster a Level 4.");
                Expect(failures, e.NormalSummon(0, fish, set: false) && e.GetAttack(e.FindMonster(fish)) == 2100, "Big Fish should be summoned without Tribute with 2100 ATK.");
            }
        }

        private static void CheckFlipEffects(List<string> failures)
        {
            // 1) Man-Eater Bug Set, attacked face-down: destroyed, but its FLIP effect still destroys the attacker.
            {
                List<CardData> a = VanillaDeck();
                List<CardData> b = VanillaDeck();
                a[39] = Monster("Striker", 4, 1800, 1000);
                b[39] = Monster("Man-Eater Bug", 2, 450, 600, "Insect / Flip Effect", "EARTH", "FLIP: Target 1 monster on the field; destroy it.");
                DuelEngine e = NewEngine(a, b, 11, aiBoth: true);
                e.StartDuel(1);
                e.NormalSummon(1, e.DebugPutInHand(1, "Man-Eater Bug"), set: true);
                e.AdvancePhase(1);
                e.NormalSummon(0, e.DebugPutInHand(0, "Striker"), set: false);
                e.AdvancePhase(0);
                DuelMonsterState striker = e.Me(0).MonstersOnField.First();
                e.DeclareAttack(0, striker, e.Me(1).MonstersOnField.First());
                Expect(failures, e.Me(1).Graveyard.Any(c => c.Name == "Man-Eater Bug"), "Man-Eater Bug should be destroyed by battle.");
                Expect(failures, e.Me(0).Graveyard.Any(c => c.Name == "Striker"), "Man-Eater Bug's FLIP effect should destroy the attacker.");
            }
            // 2) Flip Summon: Des Koala burns 400 per card in the opponent's hand.
            {
                List<CardData> a = VanillaDeck();
                a[39] = Monster("Des Koala", 3, 1100, 1800, "Beast / Flip Effect", "EARTH", "FLIP: Inflict 400 damage to your opponent for each card in their hand.");
                DuelEngine e = NewEngine(a, VanillaDeck(), 12, aiBoth: true);
                e.StartDuel(0);
                e.NormalSummon(0, e.DebugPutInHand(0, "Des Koala"), set: true);
                e.AdvancePhase(0); e.AdvancePhase(0); e.AdvancePhase(0);
                e.AdvancePhase(1); e.AdvancePhase(1); e.AdvancePhase(1);   // back to player 0, turn 3
                int opponentHand = e.Me(1).Hand.Count;
                DuelMonsterState koala = e.Me(0).MonstersOnField.First();
                Expect(failures, e.FlipSummon(0, koala), "Des Koala could not be Flip Summoned.");
                Expect(failures, e.Me(1).LifePoints == 8000 - 400 * opponentHand, $"Des Koala should burn {400 * opponentHand} (LP {e.Me(1).LifePoints}).");
            }
            // 3) Charmer: control lasts only while the Charmer is face-up on the field.
            {
                List<CardData> a = VanillaDeck();
                List<CardData> b = VanillaDeck();
                a[39] = Monster("Hiita the Fire Charmer", 3, 500, 1500, "Spellcaster / Flip Effect", "FIRE", "FLIP: Target 1 face-up FIRE monster your opponent controls; take control of that target while this card is face-up on the field.");
                b[39] = Monster("Fire Beast", 4, 1600, 1000, "Pyro / Normal", "FIRE");
                DuelEngine e = NewEngine(a, b, 13, aiBoth: true);
                e.StartDuel(0);
                e.NormalSummon(0, e.DebugPutInHand(0, "Hiita the Fire Charmer"), set: true);
                e.AdvancePhase(0); e.AdvancePhase(0); e.AdvancePhase(0);
                e.NormalSummon(1, e.DebugPutInHand(1, "Fire Beast"), set: false);
                e.AdvancePhase(1); e.AdvancePhase(1); e.AdvancePhase(1);
                DuelMonsterState hiita = e.Me(0).MonstersOnField.First(m => m.Name.StartsWith("Hiita"));
                e.FlipSummon(0, hiita);
                Expect(failures, e.Me(0).MonstersOnField.Any(m => m.Name == "Fire Beast"), "Hiita should take control of the FIRE monster.");
                e.Destroy(hiita.Card, null);
                Expect(failures, e.Me(1).MonstersOnField.Any(m => m.Name == "Fire Beast"), "Control should return when the Charmer leaves the field.");
            }
        }

        private static void CheckEffects(List<string> failures)
        {
            List<CardData> a = VanillaDeck();
            a[39] = Spell("Pot of Greed", "Normal", "Draw 2 cards.");
            a[38] = Spell("Dark Hole", "Normal", "Destroy all monsters on the field.");
            DuelEngine e = NewEngine(a, VanillaDeck(), 5, aiBoth: true);
            e.StartDuel(0);
            DuelCard pot = e.DebugPutInHand(0, "Pot of Greed");
            int hand = e.Me(0).Hand.Count;
            Expect(failures, e.Activate(0, pot), "Pot of Greed could not be activated.");
            Expect(failures, e.Me(0).Hand.Count == hand + 1 && pot.Zone == DuelZone.Graveyard, "Pot of Greed should draw 2 and go to the GY.");

            Expect(failures, CardEffects.Get(Spell("Beast Fangs", "Equip", "A Beast-Type monster equipped with this card increases its ATK and DEF by 300 points.")) != null,
                "Generic equip parser failed on Beast Fangs.");
            Expect(failures, CardEffects.Get(Spell("Gaia Power", "Field", "All EARTH monsters gain 500 ATK and lose 400 DEF.")) != null,
                "Generic field parser failed on Gaia Power.");
        }

        // ------------------------------------------------------------------ full simulations

        private static void SimulateDuels(int count, List<string> failures)
        {
            var summary = new StringBuilder();
            int finished = 0, totalTurns = 0, deckOuts = 0;
            for (int game = 0; game < count && failures.Count < 5; game++)
            {
                Random random = new Random(1000 + game);
                DuelEngine e = NewEngine(StapleDeck(random), StapleDeck(random), 2000 + game, aiBoth: true);
                var ai = new[] { (DuelAi)e.Deciders[0], (DuelAi)e.Deciders[1] };
                int totalCards = e.AllCards().Count();
                e.StartDuel(game % 2);

                int steps = 0;
                while (!e.IsOver && steps++ < 20000 && e.TurnNumber < 150)
                {
                    if (e.IsWaitingForChoice)
                    {
                        failures.Add($"Game {game}: engine waiting for a choice with AI on both sides ({e.PendingChoice.Title}).");
                        break;
                    }
                    int before = e.TurnNumber;
                    if (!ai[e.TurnPlayer].TakeAction(e)) e.AdvancePhase(e.TurnPlayer);
                    if (e.TurnNumber != before) ai[e.TurnPlayer].OnTurnStart();

                    string invariant = CheckInvariants(e, totalCards);
                    if (invariant != null)
                    {
                        failures.Add($"Game {game}, turn {e.TurnNumber}: {invariant}");
                        break;
                    }
                }

                if (e.IsOver) finished++;
                if (e.EndReason.Contains("Deck Out")) deckOuts++;
                totalTurns += e.TurnNumber;
            }

            LastSummary = $"{finished}/{count} simulated duels finished, avg {(count > 0 ? totalTurns / (float)count : 0):0.0} turns, {deckOuts} by deck-out.";
            if (finished < count * 0.8f) failures.Add("Too many simulated duels stalled: " + LastSummary);
        }

        private static string CheckInvariants(DuelEngine e, int totalCards)
        {
            var cards = e.AllCards().ToList();
            if (cards.Count != totalCards) return $"card count changed ({cards.Count} vs {totalCards}).";
            if (cards.Select(c => c.Uid).Distinct().Count() != cards.Count) return "a card is in two places at once.";
            foreach (DuelistState d in e.Duelists)
            {
                if (d.LifePoints < 0) return "negative Life Points.";
                foreach (DuelMonsterState m in d.MonstersOnField)
                    if (m.Card.Zone != DuelZone.Monster || m.Card.Controller != d.Index || m.Card.Slot != m.Slot) return $"monster bookkeeping wrong for {m.Name}.";
                foreach (DuelBackrowState s in d.SpellTrapsOnField)
                    if (s.Card.Zone != DuelZone.SpellTrap || s.Card.Slot != s.Slot) return $"spell/trap bookkeeping wrong for {s.Name}.";
                if (d.Hand.Any(c => c.Zone != DuelZone.Hand) || d.Graveyard.Any(c => c.Zone != DuelZone.Graveyard) || d.Deck.Any(c => c.Zone != DuelZone.Deck))
                    return "a list contains a card whose zone disagrees.";
            }
            return null;
        }
    }
}
