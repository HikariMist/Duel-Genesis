using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using DuelGenesis.Cards;

namespace DuelGenesis.Dueling
{
    /// <summary>
    /// Field Spells whose effects go beyond a plain stat boost (those are parsed from the card text).
    /// Registered through <see cref="CardEffectLibrary"/>.
    /// </summary>
    internal static class FieldSpells
    {
        public static void Register(Action<string, CardEffect> add)
        {
            add("A Legendary Ocean", new LegendaryOcean());
            add("Chorus of Sanctuary", new ChorusOfSanctuary());
            add("The Sanctuary in the Sky", new SanctuaryInTheSky());
            add("Array of Revealing Light", new ArrayOfRevealingLight());
            add("Harpies' Hunting Ground", new HarpiesHuntingGround());
            add("Centrifugal Field", new CentrifugalField());
            add("Fusion Gate", new FusionGate());
            add("The Seal of Orichalcos", new SealOfOrichalcos());
        }

        public static bool IsType(DuelMonsterState m, string type) => Fx.TypeIs(m.Card.Data, type);

        // ------------------------------------------------------------------ fusion materials

        private static readonly Regex Quoted = new("\"([^\"]+)\"");
        private static readonly Regex CountOfType = new(@"^(\d+)\s+(.+?)(?:-Type)?\s+monsters?", RegexOptions.IgnoreCase);

        /// <summary>
        /// The materials printed on the first line of a Fusion Monster: either named cards
        /// ("Baby Dragon" + "Alligator's Sword") or a count of a Type ("5 Dragon monsters").
        /// Each entry is a test for one material.
        /// </summary>
        public static List<Func<DuelCard, bool>> Materials(CardData fusion, out List<string> names)
        {
            names = new List<string>();
            var tests = new List<Func<DuelCard, bool>>();
            string first = (fusion.effectText ?? string.Empty).Split('\n')[0].Trim();
            MatchCollection quoted = Quoted.Matches(first);
            if (quoted.Count > 0)
            {
                foreach (Match m in quoted)
                {
                    string name = m.Groups[1].Value;
                    names.Add(name);
                    tests.Add(c => c.IsMonster && c.Name == name);
                }
                return tests;
            }
            Match count = CountOfType.Match(first);
            if (count.Success)
            {
                int n = int.Parse(count.Groups[1].Value);
                string type = count.Groups[2].Value.Trim();
                for (int i = 0; i < n; i++)
                {
                    names.Add(type + " monster");
                    tests.Add(c => c.IsMonster && Fx.TypeIs(c.Data, type));
                }
            }
            return tests;
        }

        /// <summary>Can every material be matched by a different card from <paramref name="pool"/>?</summary>
        public static bool CanMatch(List<Func<DuelCard, bool>> tests, List<DuelCard> pool)
        {
            if (tests.Count == 0) return false;
            var used = new HashSet<DuelCard>();
            // Most specific tests first so a generic test does not steal a named card.
            foreach (var test in tests.OrderBy(t => pool.Count(t)))
            {
                DuelCard pick = pool.FirstOrDefault(c => !used.Contains(c) && test(c));
                if (pick == null) return false;
                used.Add(pick);
            }
            return true;
        }
    }

    // =====================================================================================

    /// <summary>All WATER monsters on the field gain 200 ATK/DEF (the Level reduction is in <see cref="DuelEngine.LevelOf"/>).</summary>
    internal sealed class LegendaryOcean : CardEffect
    {
        public override bool StaysOnField => true;
        public override void Resolve(EffectContext ctx) => ctx.Finish();
        public override int AttackModifier(DuelEngine e, DuelBackrowState s, DuelMonsterState m) => Fx.AttrIs(m.Card.Data, "WATER") ? 200 : 0;
        public override int DefenseModifier(DuelEngine e, DuelBackrowState s, DuelMonsterState m) => Fx.AttrIs(m.Card.Data, "WATER") ? 200 : 0;
        public override int AiValue(EffectContext ctx) =>
            ctx.Me.MonstersOnField.Count(m => Fx.AttrIs(m.Card.Data, "WATER")) + ctx.Me.Hand.Count(c => Fx.AttrIs(c.Data, "WATER")) > 0 ? 55 : 15;
    }

    /// <summary>Increase the DEF of all Defense Position monsters by 500.</summary>
    internal sealed class ChorusOfSanctuary : CardEffect
    {
        public override bool StaysOnField => true;
        public override void Resolve(EffectContext ctx) => ctx.Finish();
        public override int DefenseModifier(DuelEngine e, DuelBackrowState s, DuelMonsterState m) => m.IsDefensePosition ? 500 : 0;
        public override int AiValue(EffectContext ctx) => ctx.Me.MonstersOnField.Count(m => m.IsDefensePosition) > ctx.Opp.MonstersOnField.Count(m => m.IsDefensePosition) ? 50 : 15;
    }

    /// <summary>Battle damage to the controller of a Fairy monster from a battle involving that monster becomes 0.</summary>
    internal sealed class SanctuaryInTheSky : CardEffect
    {
        public override bool StaysOnField => true;
        public override void Resolve(EffectContext ctx) => ctx.Finish();
        public override bool PreventsBattleDamage(DuelEngine e, DuelBackrowState s, int player, DuelMonsterState ownMonster) =>
            ownMonster != null && ownMonster.IsFaceUp && FieldSpells.IsType(ownMonster, "Fairy");
        public override int AiValue(EffectContext ctx) =>
            ctx.Me.MonstersOnField.Any(m => FieldSpells.IsType(m, "Fairy")) || ctx.Me.Hand.Any(c => Fx.TypeIs(c.Data, "Fairy")) ? 55 : 10;
    }

    /// <summary>Declare 1 Type: monsters of that Type cannot attack the turn they are Normal, Flip or Special Summoned.</summary>
    internal sealed class ArrayOfRevealingLight : CardEffect
    {
        private static readonly string[] Types =
        {
            "Dragon", "Spellcaster", "Warrior", "Fiend", "Machine", "Beast", "Beast-Warrior", "Winged Beast", "Fairy", "Zombie",
            "Insect", "Aqua", "Pyro", "Rock", "Plant", "Thunder", "Sea Serpent", "Fish", "Reptile", "Dinosaur", "Psychic", "Divine-Beast"
        };

        public override bool StaysOnField => true;

        public override void Resolve(EffectContext ctx)
        {
            // Most common Type among the opponent's cards first (the CPU picks option 0).
            IEnumerable<DuelCard> theirs = ctx.Opp.Deck.Concat(ctx.Opp.Hand).Concat(ctx.Opp.Graveyard).Concat(ctx.Opp.MonstersOnField.Select(m => m.Card));
            string[] options = Types.OrderByDescending(t => theirs.Count(c => Fx.TypeIs(c.Data, t))).ToArray();
            Fx.Choose(ctx, ctx.Player, "Array of Revealing Light: declare 1 monster Type.", options, (i, label) =>
            {
                DuelBackrowState s = ctx.Engine.FindBackrow(ctx.Card);
                if (s != null) s.Declared = label;
                ctx.Engine.Raise(DuelEventType.EffectResolved, ctx.Player, ctx.Card, text: $"Array of Revealing Light: {label} monsters cannot attack the turn they are Summoned.");
                ctx.Finish();
            });
        }

        public override bool ForbidsAttack(DuelEngine e, DuelBackrowState s, DuelMonsterState m) =>
            !string.IsNullOrEmpty(s.Declared) && FieldSpells.IsType(m, s.Declared) && m.LastSummonTurn == e.TurnNumber;

        public override int AiValue(EffectContext ctx) => ctx.Opp.MonsterCount + ctx.Opp.Hand.Count > 3 ? 30 : 10;
    }

    /// <summary>Winged Beasts gain 200 ATK/DEF; a Harpie Lady Summon lets its summoner destroy 1 Spell/Trap on the field.</summary>
    internal sealed class HarpiesHuntingGround : CardEffect
    {
        public override bool StaysOnField => true;
        public override void Resolve(EffectContext ctx) => ctx.Finish();
        public override int AttackModifier(DuelEngine e, DuelBackrowState s, DuelMonsterState m) => FieldSpells.IsType(m, "Winged Beast") ? 200 : 0;
        public override int DefenseModifier(DuelEngine e, DuelBackrowState s, DuelMonsterState m) => FieldSpells.IsType(m, "Winged Beast") ? 200 : 0;

        private static bool IsHarpieLady(DuelCard c) =>
            c.Name.StartsWith("Harpie Lady", StringComparison.OrdinalIgnoreCase) ||
            (c.Data.effectText ?? string.Empty).IndexOf("always treated as \"Harpie Lady\"", StringComparison.OrdinalIgnoreCase) >= 0;

        public override void OnMonsterSummoned(DuelEngine e, DuelBackrowState s, DuelMonsterState m, int player, bool special)
        {
            if (!IsHarpieLady(m.Card)) return;
            if (!special && m.IsFaceDown) return;
            DuelCard source = s.Card;
            e.QueueTriggered(done =>
            {
                var ctx = new EffectContext(e, player, source, null) { Done = done };
                var targets = e.Duelists.SelectMany(d => d.AllBackrow).Select(b => b.Card).ToList();
                if (targets.Count == 0) { done(); return; }
                e.Raise(DuelEventType.CardActivated, player, source, text: "Harpies' Hunting Ground: destroy 1 Spell/Trap on the field.");
                Fx.Pick(ctx, player, targets, "Harpies' Hunting Ground: destroy 1 Spell/Trap on the field.", "destroy-backrow", 1, 1, picks =>
                {
                    foreach (DuelCard p in picks) if (p.OnField) e.Destroy(p, source);
                    done();
                });
            });
        }

        public override int AiValue(EffectContext ctx) =>
            ctx.Me.MonstersOnField.Any(m => FieldSpells.IsType(m, "Winged Beast")) || ctx.Me.Hand.Any(c => Fx.TypeIs(c.Data, "Winged Beast")) ? 55 : 15;
    }

    /// <summary>When a Fusion Monster is destroyed by a card effect: its owner Special Summons 1 of its listed materials from their GY.</summary>
    internal sealed class CentrifugalField : CardEffect
    {
        public override bool StaysOnField => true;
        public override void Resolve(EffectContext ctx) => ctx.Finish();

        public override void OnMonsterDestroyedByEffect(DuelEngine e, DuelBackrowState s, DuelCard monster)
        {
            if (monster.Data.ResolvedFrameKind != CardFrameKind.FusionMonster) return;
            var tests = FieldSpells.Materials(monster.Data, out _);
            if (tests.Count == 0) return;
            int owner = monster.Owner;
            DuelCard source = s.Card;
            e.QueueTriggered(done =>
            {
                DuelistState d = e.Duelists[owner];
                var options = d.Graveyard.Where(c => c != monster && tests.Any(t => t(c))).ToList();
                if (options.Count == 0 || !d.HasFreeMonsterZone) { done(); return; }
                var ctx = new EffectContext(e, owner, source, null) { Done = done };
                e.Raise(DuelEventType.CardActivated, owner, source, text: $"Centrifugal Field: {d.Name} may bring back a material of {monster.Name}.");
                Fx.Pick(ctx, owner, options, "Centrifugal Field: Special Summon 1 Fusion Material from your Graveyard.", "revive", 1, 1, picks =>
                {
                    if (picks.Count > 0) e.SpecialSummon(owner, picks[0], DuelMonsterPosition.FaceUpAttack, source);
                    done();
                });
            });
        }

        public override int AiValue(EffectContext ctx) => ctx.Me.MonstersOnField.Any(m => m.Card.Data.ResolvedFrameKind == CardFrameKind.FusionMonster) ? 40 : 10;
    }

    /// <summary>The turn player can Fusion Summon by banishing the listed materials from their hand or field.</summary>
    internal sealed class FusionGate : CardEffect
    {
        public override bool StaysOnField => true;
        public override bool HasFaceUpEffect => true;

        private static List<DuelCard> Pool(EffectContext c) => c.Me.Hand.Where(x => x.IsMonster).Concat(c.Me.MonstersOnField.Select(m => m.Card)).ToList();

        private static List<DuelCard> Summonable(EffectContext c)
        {
            List<DuelCard> pool = Pool(c);
            return c.Me.ExtraDeck.Where(f => f.Data.ResolvedFrameKind == CardFrameKind.FusionMonster &&
                                             FieldSpells.CanMatch(FieldSpells.Materials(f.Data, out _), pool)).ToList();
        }

        // From the hand: just place it. Face-up on the field: Fusion Summon.
        public override bool CanActivate(EffectContext ctx) => !ctx.FromFaceUp || Summonable(ctx).Count > 0;

        public override void Resolve(EffectContext ctx)
        {
            if (!ctx.FromFaceUp) { ctx.Finish(); return; }
            Fx.Pick(ctx, ctx.Player, Summonable(ctx), "Fusion Gate: choose a Fusion Monster to Fusion Summon.", "revive", 1, 1, fusions =>
            {
                if (fusions.Count == 0) { ctx.Finish(); return; }
                DuelCard fusion = fusions[0];
                List<Func<DuelCard, bool>> tests = FieldSpells.Materials(fusion.Data, out List<string> names);
                var chosen = new List<DuelCard>();

                void Next(int i)
                {
                    if (i >= tests.Count)
                    {
                        foreach (DuelCard m in chosen) ctx.Engine.Banish(m, ctx.Card);
                        DuelMonsterState summoned = ctx.Engine.SpecialSummon(ctx.Player, fusion, DuelMonsterPosition.FaceUpAttack, ctx.Card);
                        if (summoned != null)
                            ctx.Engine.Raise(DuelEventType.EffectResolved, ctx.Player, fusion, text: $"Fusion Gate: {fusion.Name} was Fusion Summoned!");
                        ctx.Finish();
                        return;
                    }
                    // Only offer cards that still leave the remaining materials matchable.
                    List<DuelCard> pool = Pool(ctx).Where(x => !chosen.Contains(x)).ToList();
                    var rest = tests.Skip(i + 1).ToList();
                    var options = pool.Where(x => tests[i](x) && (rest.Count == 0 || FieldSpells.CanMatch(rest, pool.Where(p => p != x).ToList()))).ToList();
                    if (options.Count == 0) { ctx.Finish(); return; }
                    if (options.Count == 1) { chosen.Add(options[0]); Next(i + 1); return; }
                    Fx.Pick(ctx, ctx.Player, options, $"Fusion Gate: banish {names[i]} (material {i + 1} of {tests.Count}).", "tribute", 1, 1, picks =>
                    {
                        chosen.Add(picks.Count > 0 ? picks[0] : options[0]);
                        Next(i + 1);
                    });
                }
                Next(0);
            });
        }

        public override int AiValue(EffectContext ctx) => ctx.FromFaceUp ? (Summonable(ctx).Count > 0 ? 90 : 0) : (Summonable(ctx).Count > 0 || ctx.Me.ExtraDeck.Count > 0 ? 45 : 5);
    }

    /// <summary>
    /// +500 ATK to your monsters; once per turn it is not destroyed by card effects; your lowest-ATK monster
    /// can't be attacked while you control 2+ face-up Attack Position monsters; on activation destroy your
    /// Special Summoned monsters; you can't Special Summon from the Extra Deck; once per Duel.
    /// </summary>
    internal sealed class SealOfOrichalcos : CardEffect
    {
        private const string Key = "The Seal of Orichalcos";
        public override bool StaysOnField => true;

        public override bool CanActivate(EffectContext ctx) => !ctx.Me.OncePerDuel.Contains(Key);

        public override void Resolve(EffectContext ctx)
        {
            ctx.Me.OncePerDuel.Add(Key);
            foreach (DuelMonsterState m in ctx.Me.MonstersOnField.Where(m => m.SpecialSummoned).ToList())
                ctx.Engine.Destroy(m.Card, ctx.Card);
            ctx.Finish();
        }

        public override int AttackModifier(DuelEngine e, DuelBackrowState s, DuelMonsterState m) => m.Card.Controller == s.Card.Controller ? 500 : 0;

        public override bool ResistsDestruction(DuelEngine e, DuelBackrowState s, DuelCard cause)
        {
            if (cause == null) return false;
            if (s.Counter == e.TurnNumber) return false;
            s.Counter = e.TurnNumber;
            return true;
        }

        public override bool ProtectsFromAttack(DuelEngine e, DuelBackrowState s, DuelMonsterState defender)
        {
            int owner = s.Card.Controller;
            if (defender.Card.Controller != owner) return false;
            var attackers = e.Me(owner).MonstersOnField.Where(m => m.IsAttackPosition).ToList();
            if (attackers.Count < 2 || !defender.IsAttackPosition) return false;
            int lowest = attackers.Min(e.GetAttack);
            return e.GetAttack(defender) == lowest && attackers.Any(m => e.GetAttack(m) > lowest);
        }

        public override bool BlocksSpecialSummon(DuelEngine e, DuelBackrowState s, int player, DuelCard card) =>
            player == s.Card.Controller && card.IsExtraDeckCard;

        public override int AiValue(EffectContext ctx) =>
            ctx.Me.MonstersOnField.Count(m => !m.SpecialSummoned) >= 1 && !ctx.Me.MonstersOnField.Any(m => m.SpecialSummoned) ? 60 : 20;
    }
}
