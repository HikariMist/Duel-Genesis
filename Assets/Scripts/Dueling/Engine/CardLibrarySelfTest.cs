using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DuelGenesis.Cards;

namespace DuelGenesis.Dueling
{
    /// <summary>
    /// Headless test for the Spell/Trap library, run against the real card catalog:
    ///  1. Every implemented Spell/Trap is activated on a busy board (as a Main Phase activation and as a
    ///     response to each kind of opponent action). Checks: no exception, the effect finishes, and the
    ///     board stays consistent.
    ///  2. CPU-vs-CPU duels with decks full of implemented Spells/Traps, with invariant checks after every
    ///     action. Reports which cards actually got used.
    /// </summary>
    public static class CardLibrarySelfTest
    {
        public sealed class Report
        {
            public int Implemented;
            public int SpellTrapTotal;
            public int CardsTested;
            public int MainPhaseActivations;
            public int MonsterAbilityCards;
            public int MonsterAbilityRuns;
            public int Responses;
            public int DuelsFinished;
            public int DuelsRun;
            public float AverageTurns;
            public readonly HashSet<string> UsedInDuels = new();
            public readonly List<string> Failures = new();
            /// <summary>Spells/Traps with no working effect yet, by card type ("Trap / Counter").</summary>
            public readonly SortedDictionary<string, List<string>> Missing = new();

            public bool Passed => Failures.Count == 0;

            public override string ToString()
            {
                var sb = new StringBuilder();
                sb.AppendLine($"Spell/Trap cards with working effects: {Implemented} / {SpellTrapTotal}");
                sb.AppendLine($"Per-card test: {CardsTested} cards, {MainPhaseActivations} Main Phase activations, {Responses} responses resolved.");
                sb.AppendLine($"Monster effects (Flip / summon triggers): {MonsterAbilityCards} cards, {MonsterAbilityRuns} effect resolutions tested.");
                sb.AppendLine($"CPU duels: {DuelsFinished}/{DuelsRun} finished, avg {AverageTurns:0.0} turns; {UsedInDuels.Count} different Spell/Trap cards were activated by the CPU.");
                sb.AppendLine(Passed ? "RESULT: PASS" : $"RESULT: {Failures.Count} FAILURE(S)");
                foreach (string f in Failures) sb.AppendLine(" - " + f);
                sb.AppendLine();
                sb.AppendLine("NOT IMPLEMENTED YET (by type):");
                foreach (var pair in Missing.OrderBy(p => p.Value.Count))
                    sb.AppendLine($"  {pair.Key} ({pair.Value.Count}): {string.Join(", ", pair.Value.OrderBy(n => n))}");
                return sb.ToString();
            }
        }

        public static Report Run(IReadOnlyList<CardData> catalog, int duels = 100)
        {
            var report = new Report();
            List<CardData> spellTraps = catalog.Where(c => c.kind != CardKind.Monster).ToList();
            report.SpellTrapTotal = spellTraps.Count;
            List<CardData> implemented = spellTraps.Where(c => CardEffects.Get(c) != null).ToList();
            report.Implemented = implemented.Count;
            foreach (CardData c in spellTraps.Where(c => CardEffects.Get(c) == null))
            {
                string key = $"{c.kind} / {c.typeLine}";
                if (!report.Missing.TryGetValue(key, out var list)) report.Missing[key] = list = new List<string>();
                list.Add(c.cardName);
            }

            List<CardData> monsters = catalog.Where(c => c.kind == CardKind.Monster && DuelRules.CanEverBeNormalSummoned(c)).ToList();
            List<CardData> extra = catalog.Where(c => DuelRules.IsExtraDeckMonster(c)).ToList();

            int seed = 7;
            foreach (CardData card in implemented)
            {
                report.CardsTested++;
                TestCard(card, catalog, monsters, extra, seed++, report);
                if (report.Failures.Count > 40) { report.Failures.Add("(stopped after 40 failures)"); break; }
            }

            TestMonsterAbilities(catalog, monsters, extra, report);
            SimulateDuels(implemented, monsters, extra, duels, report);
            return report;
        }

        // ------------------------------------------------------------------ monster effects

        /// <summary>Every monster with a Flip effect / summon trigger: put it on a busy board, fire each of its effects, and check it finishes.</summary>
        private static void TestMonsterAbilities(IReadOnlyList<CardData> catalog, List<CardData> monsters, List<CardData> extra, Report report)
        {
            int seed = 5000;
            foreach (CardData card in catalog.Where(c => c.kind == CardKind.Monster))
            {
                bool any = false;
                foreach (MonsterAbilityKind kind in Enum.GetValues(typeof(MonsterAbilityKind)))
                {
                    try
                    {
                        DuelEngine e = BusyBoard(card, catalog, monsters, extra, seed++, firstPlayer: 0);
                        DuelCard testCard = e.Me(0).Deck.Concat(e.Me(0).Hand).FirstOrDefault(c => c.Data == card);
                        if (testCard == null || MonsterAbilities.Get(testCard, kind) == null) continue;
                        any = true;
                        int total = e.AllCards().Count();
                        DuelCard other = e.Me(1).MonstersOnField.Select(m => m.Card).FirstOrDefault();
                        if (kind != MonsterAbilityKind.Discarded && kind != MonsterAbilityKind.DestroyedByBattle && kind != MonsterAbilityKind.SentToGraveyardAfterFlip)
                        {
                            if (e.PlaceMonster(testCard, 0, -1, DuelMonsterPosition.FaceUpDefense) == null) continue;
                        }
                        e.QueueMonsterAbility(kind, testCard, 0, other);
                        e.RunQueuedMonsterAbilities();
                        report.MonsterAbilityRuns++;
                        if (!e.IsOver && e.HasQueuedMonsterAbilities)
                            report.Failures.Add($"{card.cardName} ({kind}): the effect never finished resolving (game would freeze).");
                        if (!e.IsOver && e.IsWaitingForChoice)
                            report.Failures.Add($"{card.cardName} ({kind}): left a choice pending with CPU on both sides.");
                        Check(e, total, card.cardName, kind.ToString(), report);
                    }
                    catch (Exception ex)
                    {
                        report.Failures.Add($"{card.cardName} ({kind}): {ex.GetType().Name}: {ex.Message} {FirstFrame(ex)}");
                    }
                }
                if (any) report.MonsterAbilityCards++;
            }
        }

        // ------------------------------------------------------------------ per-card test

        private static void TestCard(CardData card, IReadOnlyList<CardData> catalog, List<CardData> monsters, List<CardData> extra, int seed, Report report)
        {
            // Scenario A: activate it in our own Main Phase.
            try
            {
                DuelEngine e = BusyBoard(card, catalog, monsters, extra, seed, firstPlayer: 0);
                DuelCard testCard = e.Me(0).Deck.Concat(e.Me(0).Hand).First(c => c.Data == card);
                CardEffect effect = CardEffects.Get(card);
                int total = e.AllCards().Count();
                DuelBackrowState source = e.PlaceBackrow(testCard, 0, -1, faceDown: false);
                if (source == null) { report.Failures.Add($"{card.cardName}: no free zone in test setup."); return; }
                var ctx = new EffectContext(e, 0, testCard, null) { Source = source };
                if (effect.CanActivate(ctx) && Resolve(e, effect, ctx, card.cardName, "Main Phase", report))
                    report.MainPhaseActivations++;
                Check(e, total, card.cardName, "Main Phase", report);
            }
            catch (Exception ex)
            {
                report.Failures.Add($"{card.cardName} (Main Phase): {ex.GetType().Name}: {ex.Message} {FirstFrame(ex)}");
            }

            // Scenario B: chain it to each kind of opponent action.
            if (card.kind == CardKind.Trap || DuelRules.IsQuickPlay(card))
            {
                foreach (DuelTriggerKind kind in Enum.GetValues(typeof(DuelTriggerKind)))
                {
                    try
                    {
                        DuelEngine e = BusyBoard(card, catalog, monsters, extra, seed, firstPlayer: 1);
                        DuelCard testCard = e.Me(0).Deck.Concat(e.Me(0).Hand).First(c => c.Data == card);
                        CardEffect effect = CardEffects.Get(card);
                        int total = e.AllCards().Count();
                        DuelTrigger trigger = MakeTrigger(e, kind);
                        if (trigger == null) continue;
                        DuelBackrowState source = e.PlaceBackrow(testCard, 0, -1, faceDown: false);
                        if (source == null) continue;
                        var ctx = new EffectContext(e, 0, testCard, trigger) { Source = source };
                        if (!effect.CanRespond(ctx)) continue;
                        if (Resolve(e, effect, ctx, card.cardName, "response to " + kind, report)) report.Responses++;
                        Check(e, total, card.cardName, "response to " + kind, report);
                    }
                    catch (Exception ex)
                    {
                        report.Failures.Add($"{card.cardName} (response to {kind}): {ex.GetType().Name}: {ex.Message} {FirstFrame(ex)}");
                    }
                }
            }
        }

        /// <summary>Runs targets → cost → resolve the way the engine does. Returns true if it finished.</summary>
        private static bool Resolve(DuelEngine e, CardEffect effect, EffectContext ctx, string name, string when, Report report)
        {
            TargetRequest request = effect.Targets(ctx);
            if (request != null)
            {
                if (request.Candidates.Count < Math.Max(1, request.Min)) return false;
                ctx.Targets.AddRange(request.Candidates.Take(Math.Max(1, request.Min)));
            }

            bool paid = false, finished = false;
            effect.PayCost(ctx, () => paid = true);
            if (e.IsOver) return true;
            if (!paid) { report.Failures.Add($"{name} ({when}): the cost never completed."); return false; }

            ctx.Done = () => finished = true;
            effect.Resolve(ctx);
            if (e.IsOver) return true;
            if (!finished) { report.Failures.Add($"{name} ({when}): the effect never finished resolving (game would freeze)."); return false; }
            if (e.IsWaitingForChoice) { report.Failures.Add($"{name} ({when}): left a choice pending with CPU on both sides."); return false; }

            DuelBackrowState still = e.FindBackrow(ctx.Card);
            if (still != null && (!effect.StaysOnField || effect.ShouldLeaveAfterResolve(ctx)))
                e.SendToGraveyard(ctx.Card, false, ctx.Card);

            // Continuous effects must be safe to query every frame.
            foreach (DuelistState d in e.Duelists)
            foreach (DuelMonsterState m in d.MonstersOnField.ToList())
            {
                e.GetAttack(m);
                e.GetDefense(m);
                e.ForbiddenToAttack(m);
            }
            return true;
        }

        private static void Check(DuelEngine e, int total, string name, string when, Report report)
        {
            string problem = Invariants(e, total);
            if (problem != null) report.Failures.Add($"{name} ({when}): {problem}");
        }

        private static DuelTrigger MakeTrigger(DuelEngine e, DuelTriggerKind kind)
        {
            DuelMonsterState attacker = e.Me(1).MonstersOnField.FirstOrDefault(m => m.IsAttackPosition);
            DuelMonsterState defender = e.Me(0).MonstersOnField.FirstOrDefault();
            DuelCard oppSpell = e.Me(1).SpellTrapsOnField.Select(s => s.Card).FirstOrDefault();
            switch (kind)
            {
                case DuelTriggerKind.AttackDeclared:
                    return attacker == null ? null : new DuelTrigger { Kind = kind, Player = 1, Card = attacker.Card, Attacker = attacker, Defender = defender };
                case DuelTriggerKind.NormalSummoned:
                case DuelTriggerKind.FlipSummoned:
                case DuelTriggerKind.SpecialSummoned:
                    return attacker == null ? null : new DuelTrigger { Kind = kind, Player = 1, Card = attacker.Card };
                case DuelTriggerKind.MonsterEffectActivated:
                    return attacker == null ? null : new DuelTrigger { Kind = kind, Player = 1, Card = attacker.Card };
                case DuelTriggerKind.SpellActivated:
                case DuelTriggerKind.TrapActivated:
                    return oppSpell == null ? null : new DuelTrigger { Kind = kind, Player = 1, Card = oppSpell };
                default:
                    return new DuelTrigger { Kind = kind, Player = 1 };
            }
        }

        /// <summary>
        /// A mid-game board: both players have face-up and face-down monsters, a Set card, monsters in the
        /// Graveyard and banished, and a full hand. Cards the tested card names ("Dark Magician"...) are put
        /// on its controller's field, in the Deck and in the Graveyard, so support cards have something to find.
        /// </summary>
        private static DuelEngine BusyBoard(CardData card, IReadOnlyList<CardData> catalog, List<CardData> monsters, List<CardData> extra, int seed, int firstPlayer)
        {
            var random = new Random(seed);
            List<CardData> named = NamedMonsters(card, catalog);

            List<CardData> Deck(bool withTestCard)
            {
                var deck = new List<CardData>();
                foreach (CardData n in named) { deck.Add(n); deck.Add(n); deck.Add(n); }
                while (deck.Count < 36) deck.Add(monsters[random.Next(monsters.Count)]);
                // Filler Set cards (no effect) so "destroy a Spell/Trap" effects have something to hit.
                for (int i = 0; i < 3; i++)
                    deck.Add(new CardData("filler-" + i, "Test Set Card " + i, CardKind.Trap, CardRarity.Common, "", "Normal", 0, 0, 0, "", CardFrameKind.Trap));
                if (withTestCard) deck.Add(card);
                deck.AddRange(extra.Take(3));
                return deck;
            }

            var e = new DuelEngine(seed);
            e.Setup(0, "Tester", Deck(true));
            e.Setup(1, "Opponent", Deck(false));
            e.Deciders[0] = new DuelAi(0);
            e.Deciders[1] = new DuelAi(1);
            e.StartDuel(firstPlayer);

            for (int p = 0; p < 2; p++)
            {
                DuelistState d = e.Me(p);
                // Monsters named by the tested card go face-up on its controller's field first.
                var fieldCards = new List<DuelCard>();
                bool ritual = card.typeLine == "Ritual";
                if (p == 0 && ritual)
                    foreach (CardData n in named) e.DebugPutInHand(0, n.cardName);
                if (p == 0 && !ritual)
                    foreach (CardData n in named)
                    {
                        DuelCard c = d.Deck.FirstOrDefault(x => x.Data == n);
                        if (c != null) fieldCards.Add(c);
                    }
                fieldCards.AddRange(d.Deck.Where(x => x.IsMonster && !fieldCards.Contains(x)).Take(Math.Max(0, 3 - fieldCards.Count)));
                DuelMonsterPosition[] positions = { DuelMonsterPosition.FaceUpAttack, DuelMonsterPosition.FaceUpDefense, DuelMonsterPosition.FaceDownDefense, DuelMonsterPosition.FaceUpAttack };
                for (int i = 0; i < fieldCards.Count && i < 4; i++)
                    e.PlaceMonster(fieldCards[i], p, -1, positions[i]);

                foreach (DuelCard c in d.Deck.Where(x => x.IsMonster).Take(5).ToList()) e.SendToGraveyard(c, false, null);
                foreach (CardData n in p == 0 ? named : new List<CardData>())
                {
                    DuelCard c = d.Deck.FirstOrDefault(x => x.Data == n);
                    if (c != null) e.SendToGraveyard(c, false, null);
                }
                foreach (DuelCard c in d.Deck.Where(x => x.IsMonster).Take(2).ToList()) e.Banish(c, null);

                DuelCard set = d.Deck.Concat(d.Hand).FirstOrDefault(x => x.Data.id.StartsWith("filler-"));
                if (set != null)
                {
                    DuelBackrowState s = e.PlaceBackrow(set, p, -1, faceDown: p == 1);
                    if (s != null) s.SetTurn = 0;
                }
            }
            return e;
        }

        private static List<CardData> NamedMonsters(CardData card, IReadOnlyList<CardData> catalog)
        {
            var result = new List<CardData>();
            string text = card.effectText ?? string.Empty;
            int start = 0;
            while ((start = text.IndexOf('"', start)) >= 0)
            {
                int end = text.IndexOf('"', start + 1);
                if (end < 0) break;
                string name = text.Substring(start + 1, end - start - 1);
                CardData m = catalog.FirstOrDefault(c => c.cardName == name && c.kind == CardKind.Monster);
                if (m != null && !result.Contains(m)) result.Add(m);
                start = end + 1;
            }
            return result.Take(3).ToList();
        }

        // ------------------------------------------------------------------ CPU duels

        private static void SimulateDuels(List<CardData> implemented, List<CardData> monsters, List<CardData> extra, int count, Report report)
        {
            int totalTurns = 0;
            for (int game = 0; game < count; game++)
            {
                var random = new Random(5000 + game);
                List<CardData> Deck()
                {
                    var deck = new List<CardData>();
                    for (int i = 0; i < 22; i++) deck.Add(monsters[random.Next(monsters.Count)]);
                    for (int i = 0; i < 18; i++) deck.Add(implemented[random.Next(implemented.Count)]);
                    deck.AddRange(extra.OrderBy(_ => random.Next()).Take(5));
                    return deck;
                }

                DuelEngine e = new DuelEngine(9000 + game);
                e.Setup(0, "A", Deck());
                e.Setup(1, "B", Deck());
                var ai = new[] { new DuelAi(0), new DuelAi(1) };
                e.Deciders[0] = ai[0];
                e.Deciders[1] = ai[1];
                int total = e.AllCards().Count();
                report.DuelsRun++;

                try
                {
                    e.StartDuel(game % 2);
                    int steps = 0;
                    while (!e.IsOver && steps++ < 20000 && e.TurnNumber < 150)
                    {
                        if (e.IsWaitingForChoice)
                        {
                            report.Failures.Add($"Duel {game}: stuck waiting for a choice ({e.PendingChoice.Title}: {e.PendingChoice.Prompt}).");
                            break;
                        }
                        int before = e.TurnNumber;
                        if (!ai[e.TurnPlayer].TakeAction(e)) e.AdvancePhase(e.TurnPlayer);
                        if (e.TurnNumber != before) ai[e.TurnPlayer].OnTurnStart();
                        string problem = Invariants(e, total);
                        if (problem != null)
                        {
                            report.Failures.Add($"Duel {game}, turn {e.TurnNumber}: {problem} Last card used: {LastActivated(e)}.");
                            break;
                        }
                    }
                }
                catch (Exception ex)
                {
                    report.Failures.Add($"Duel {game}: {ex.GetType().Name}: {ex.Message} {FirstFrame(ex)} Last card used: {LastActivated(e)}.");
                }

                foreach (DuelEvent ev in e.History.Where(ev => ev.Type == DuelEventType.CardActivated && ev.Card != null))
                    report.UsedInDuels.Add(ev.Card.Name);
                if (e.IsOver) report.DuelsFinished++;
                totalTurns += e.TurnNumber;
                if (report.Failures.Count > 60) break;
            }
            report.AverageTurns = report.DuelsRun > 0 ? totalTurns / (float)report.DuelsRun : 0f;
            if (report.DuelsFinished < report.DuelsRun * 0.8f)
                report.Failures.Add($"Too many CPU duels stalled ({report.DuelsFinished}/{report.DuelsRun} finished).");
        }

        private static string LastActivated(DuelEngine e) =>
            e.History.LastOrDefault(ev => ev.Type == DuelEventType.CardActivated)?.Card?.Name ?? "none";

        private static string FirstFrame(Exception ex)
        {
            string trace = ex.StackTrace ?? string.Empty;
            string line = trace.Split('\n').FirstOrDefault(l => l.Contains("CardEffectLibrary") || l.Contains("CardEffects")) ?? trace.Split('\n').FirstOrDefault();
            return line == null ? string.Empty : "@ " + line.Trim();
        }

        private static string Invariants(DuelEngine e, int totalCards)
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
