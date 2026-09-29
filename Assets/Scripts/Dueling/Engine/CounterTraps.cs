using System;
using System.Linq;
using System.Text.RegularExpressions;
using DuelGenesis.Cards;

namespace DuelGenesis.Dueling
{
    /// <summary>Counter Traps that read what the chained card does (its text and targets).</summary>
    internal static class CounterTraps
    {
        private const RegexOptions Opt = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;
        private static readonly Regex DestroysSpellTrap = new(@"destroy[^.]*\b(Spell|Trap|Spells|Traps)\b|\b(Spell|Trap)[^.]*;\s*destroy", Opt);
        private static readonly Regex DestroysOneSpellTrap = new(@"\b(1|one)\s+(Spell|Trap)", Opt);
        private static readonly Regex InflictsDamage = new(@"inflict[^.]*damage", Opt);

        private static string Text(DuelCard c) => c?.Data?.effectText ?? string.Empty;
        private static bool ByOpponent(EffectContext c) => c.Trigger != null && c.Trigger.Player != c.Player && c.Trigger.Card != null;
        private static bool SpellOrTrapActivated(EffectContext c) =>
            c.Trigger.Kind == DuelTriggerKind.SpellActivated || c.Trigger.Kind == DuelTriggerKind.TrapActivated;
        private static bool TargetsExactlyOneMonster(EffectContext c, Func<DuelCard, bool> filter = null) =>
            c.Trigger.Targets.Count == 1 && c.Trigger.Targets[0].IsMonster && c.Trigger.Targets[0].Zone == DuelZone.Monster &&
            (filter == null || filter(c.Trigger.Targets[0]));
        private static bool WouldDamageMe(EffectContext c) =>
            ByOpponent(c) && (SpellOrTrapActivated(c) || c.Trigger.Kind == DuelTriggerKind.MonsterEffectActivated) && InflictsDamage.IsMatch(Text(c.Trigger.Card));

        /// <summary>Negate the chained Spell/Trap and destroy it (the engine sends a negated card to the GY).</summary>
        private static void Negate(EffectContext c) => c.Trigger.Negated = true;

        public static void Register(Action<string, CardEffect> add)
        {
            add("Riryoku Field", new Scripted
            {
                Can = _ => false,
                Respond = c => ByOpponent(c) && c.Trigger.Kind == DuelTriggerKind.SpellActivated && TargetsExactlyOneMonster(c),
                Do = Fx.Sync(Negate),
                Ai = c => TargetsExactlyOneMonster(c, t => t.Controller == c.Player) ? 80 : 30
            });

            add("Tutan Mask", new Scripted
            {
                Can = _ => false,
                Respond = c => ByOpponent(c) && SpellOrTrapActivated(c) && TargetsExactlyOneMonster(c, t => Fx.TypeIs(t.Data, "Zombie")),
                Do = Fx.Sync(Negate),
                Ai = _ => 80
            });

            add("Spell Shield Type-8", new Scripted
            {
                Can = _ => false,
                Respond = c => ByOpponent(c) && c.Trigger.Kind == DuelTriggerKind.SpellActivated &&
                               (TargetsExactlyOneMonster(c) || c.Me.Hand.Any(h => h.IsSpell)),
                Cost = (c, paid) =>
                {
                    if (TargetsExactlyOneMonster(c)) { paid(); return; }
                    Fx.Pick(c, c.Player, c.Me.Hand.Where(h => h.IsSpell), "Spell Shield Type-8: send 1 Spell from your hand to the Graveyard.", "discard", 1, 1, picks =>
                    {
                        foreach (DuelCard p in picks) c.Engine.Discard(p);
                        paid();
                    });
                },
                Do = Fx.Sync(Negate),
                Ai = c => TargetsExactlyOneMonster(c, t => t.Controller == c.Player) ? 80 : 55
            });

            add("Curse of Royal", new Scripted
            {
                Can = _ => false,
                Respond = c => ByOpponent(c) && SpellOrTrapActivated(c) &&
                               (c.Trigger.Targets.Count == 1 && !c.Trigger.Targets[0].IsMonster && Text(c.Trigger.Card).IndexOf("destroy", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                DestroysOneSpellTrap.IsMatch(Text(c.Trigger.Card)) && DestroysSpellTrap.IsMatch(Text(c.Trigger.Card))),
                Do = Fx.Sync(Negate),
                Ai = _ => 75
            });

            add("Judgment of Anubis", new Scripted
            {
                Can = _ => false,
                Respond = c => ByOpponent(c) && c.Trigger.Kind == DuelTriggerKind.SpellActivated && c.Me.Hand.Count > 0 && DestroysSpellTrap.IsMatch(Text(c.Trigger.Card)),
                Cost = Fx.DiscardCost(1),
                Do = c =>
                {
                    Negate(c);
                    Fx.Pick(c, c.Player, c.Opp.MonstersOnField.Where(m => m.IsFaceUp).Select(m => m.Card),
                        "Judgment of Anubis: you can destroy 1 face-up monster your opponent controls and inflict damage equal to its ATK.", "destroy", 0, 1, picks =>
                        {
                            DuelMonsterState m = picks.Count > 0 ? c.Engine.FindMonster(picks[0]) : null;
                            if (m != null)
                            {
                                int atk = c.Engine.GetAttack(m);
                                c.Engine.Destroy(m.Card, c.Card);
                                c.Engine.DealDamage(c.OpponentIndex, atk, c.Card, battle: false);
                            }
                            c.Finish();
                        });
                },
                Ai = _ => 85
            });

            add("Divine Wrath", new Scripted
            {
                Can = _ => false,
                Respond = c => ByOpponent(c) && c.Trigger.Kind == DuelTriggerKind.MonsterEffectActivated && c.Me.Hand.Count > 0,
                Cost = Fx.DiscardCost(1),
                Do = Fx.Sync(c =>
                {
                    Negate(c);
                    if (c.Engine.FindMonster(c.Trigger.Card) != null) c.Engine.Destroy(c.Trigger.Card, c.Card);
                }),
                Ai = _ => 70
            });

            add("Barrel Behind the Door", new Scripted
            {
                Can = _ => false,
                Respond = WouldDamageMe,
                Do = Fx.Sync(c => c.Engine.ShieldEffectDamage(c.Trigger.Card, c.Player, reflect: true)),
                Ai = _ => 85
            });

            add("Trap of Board Eraser", new Scripted
            {
                Can = _ => false,
                Respond = WouldDamageMe,
                Do = c =>
                {
                    c.Engine.ShieldEffectDamage(c.Trigger.Card, c.Player, reflect: false);
                    Fx.Pick(c, c.OpponentIndex, c.Opp.Hand, "Trap of Board Eraser: discard 1 card from your hand.", "discard", 1, 1, picks =>
                    {
                        foreach (DuelCard p in picks) c.Engine.Discard(p);
                        c.Finish();
                    });
                },
                Ai = _ => 80
            });
        }
    }
}
