using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using DuelGenesis.Cards;

namespace DuelGenesis.Dueling
{
    /// <summary>Quick-Play Spells (registered through <see cref="CardEffectLibrary"/>).</summary>
    internal static class QuickPlaySpells
    {
        private static readonly Regex DestroysMonster = new(@"destroy[^.]*monster|monster[^.]*;\s*destroy", RegexOptions.IgnoreCase);
        private static readonly Regex InflictsDamage = new(@"inflict[^.]*damage", RegexOptions.IgnoreCase);

        private static bool OpponentActivation(EffectContext c) =>
            c.Trigger != null && c.Trigger.Player != c.Player && c.Trigger.Card != null &&
            (c.Trigger.Kind == DuelTriggerKind.SpellActivated || c.Trigger.Kind == DuelTriggerKind.TrapActivated || c.Trigger.Kind == DuelTriggerKind.MonsterEffectActivated);

        public static void Register(Action<string, CardEffect> add)
        {
            add("Anti-Magic Arrows", new Scripted
            {
                Can = c => c.Engine.TurnPlayer == c.Player && c.Engine.Phase == DuelPhase.Battle,
                Do = Fx.Sync(c =>
                {
                    c.Engine.SpellTrapLockTurn = c.Engine.TurnNumber;
                    c.Engine.Raise(DuelEventType.EffectResolved, c.Player, c.Card, text: "Anti-Magic Arrows: no Spell or Trap Cards can be activated for the rest of this turn.");
                }),
                Ai = c => c.Opp.SpellTrapsOnField.Count(s => s.FaceDown) >= 1 && c.Me.MonstersOnField.Any(m => m.IsAttackPosition) ? 70 : 0
            });

            add("Fires of Doomsday", new Scripted
            {
                Can = c => c.Me.MonsterCount <= DuelRules.MonsterZoneCount - 2 && !c.Engine.SpecialSummonsLocked,
                Do = Fx.Sync(c =>
                {
                    for (int i = 0; i < 2; i++)
                        c.Engine.SummonToken(c.Player, "Doomsday Token", "Fiend", "DARK", 1, 0, 0, DuelMonsterPosition.FaceUpDefense, c.Card);
                    c.Me.SummonLockTurn = c.Engine.TurnNumber;
                }),
                Ai = c => c.Me.MonsterCount == 0 && c.Opp.MonstersOnField.Any(m => m.IsAttackPosition) ? 55 : 0
            });

            add("Jar Robber", new Scripted
            {
                Can = _ => false,
                Respond = c => c.Trigger != null && c.Trigger.Player != c.Player && c.Trigger.Kind == DuelTriggerKind.SpellActivated && c.Trigger.Card?.Name == "Pot of Greed",
                Do = Fx.Sync(c => { c.Trigger.Negated = true; c.Engine.Draw(c.Player, 1); }),
                Ai = _ => 90
            });

            add("Magnet Reverse", new Scripted
            {
                Can = c => c.Me.HasFreeMonsterZone,
                Tgt = c => CardEffects.Request(c.Me.Graveyard.Concat(c.Me.Banished).Where(x =>
                        x.IsMonster && (Fx.TypeIs(x.Data, "Machine") || Fx.TypeIs(x.Data, "Rock")) && !x.IsExtraDeckCard &&
                        !DuelRules.IsToken(x.Data) && !DuelRules.CanEverBeNormalSummoned(x.Data)),
                    "Magnet Reverse: Special Summon 1 Machine or Rock monster that cannot be Normal Summoned.", "revive"),
                Do = Fx.Sync(c =>
                {
                    if (c.Target != null && (c.Target.Zone == DuelZone.Graveyard || c.Target.Zone == DuelZone.Banished))
                        c.Engine.SpecialSummon(c.Player, c.Target, DuelMonsterPosition.FaceUpAttack, c.Card);
                }),
                Ai = _ => 65
            });

            add("Mind Wipe", new Scripted
            {
                Can = c => c.Opp.Hand.Count >= 1 && c.Opp.Hand.Count <= 3,
                Respond = c => c.Trigger != null && c.Trigger.Player != c.Player && c.Opp.Hand.Count >= 1 && c.Opp.Hand.Count <= 3,
                Do = Fx.Sync(c =>
                {
                    int n = c.Opp.Hand.Count;
                    foreach (DuelCard h in c.Opp.Hand.ToList()) c.Engine.ReturnToDeck(h, shuffle: false);
                    c.Engine.ShuffleDeck(c.OpponentIndex);
                    c.Engine.Draw(c.OpponentIndex, n);
                }),
                Ai = _ => 20
            });

            add("Monster Recovery", new Scripted
            {
                Tgt = c => CardEffects.Request(c.Me.MonstersOnField.Where(m => m.Card.Owner == c.Player).Select(m => m.Card),
                    "Monster Recovery: shuffle 1 of your monsters and your hand into the Deck.", "tribute"),
                Do = Fx.Sync(c =>
                {
                    if (c.Target?.Zone != DuelZone.Monster) return;
                    int n = c.Me.Hand.Count;
                    c.Engine.ReturnToDeck(c.Target, shuffle: false);
                    foreach (DuelCard h in c.Me.Hand.ToList()) c.Engine.ReturnToDeck(h, shuffle: false);
                    c.Engine.ShuffleDeck(c.Player);
                    c.Engine.Draw(c.Player, n);
                }),
                Ai = c => c.Me.Hand.Count >= 3 && c.Me.MonstersOnField.Any(m => m.IsFaceUp && c.Engine.GetAttack(m) < 1000) ? 35 : 0
            });

            add("My Body as a Shield", new Scripted
            {
                Can = _ => false,
                Respond = c => OpponentActivation(c) && c.Me.LifePoints > 1500 && DestroysMonster.IsMatch(c.Trigger.Card.Data.effectText ?? string.Empty) && c.AllMonsters.Any(),
                Cost = Fx.PayCost(1500),
                Do = Fx.Sync(c =>
                {
                    c.Trigger.Negated = true;
                    if (c.Trigger.Kind == DuelTriggerKind.MonsterEffectActivated && c.Engine.FindMonster(c.Trigger.Card) != null)
                        c.Engine.Destroy(c.Trigger.Card, c.Card);
                }),
                Ai = c => c.Me.MonstersOnField.Any(m => m.IsFaceUp && c.Engine.GetAttack(m) >= 1800) && c.Me.LifePoints > 3000 ? 75 : 0
            });

            add("Ring of Defense", new Scripted
            {
                Can = _ => false,
                Respond = c => c.Trigger != null && c.Trigger.Player != c.Player && c.Trigger.Kind == DuelTriggerKind.TrapActivated &&
                               InflictsDamage.IsMatch(c.Trigger.Card?.Data.effectText ?? string.Empty),
                Do = Fx.Sync(c => c.Engine.ShieldEffectDamage(c.Trigger.Card, c.Player, reflect: false)),
                Ai = _ => 70
            });

            add("Soul Reversal", new Scripted
            {
                Tgt = c => CardEffects.Request(c.Me.Graveyard.Where(x => x.IsMonster && (x.Data.typeLine ?? "").IndexOf("Flip", StringComparison.OrdinalIgnoreCase) >= 0),
                    "Soul Reversal: return 1 Flip monster from your Graveyard to the top of your Deck.", "salvage"),
                Do = Fx.Sync(c => { if (c.Target?.Zone == DuelZone.Graveyard) c.Engine.ReturnToDeck(c.Target, shuffle: false); }),
                Ai = _ => 25
            });

            add("Spellbook Organization", new Scripted
            {
                Can = c => c.Me.Deck.Count >= 2,
                Do = c =>
                {
                    List<DuelCard> deck = c.Me.Deck;
                    var top = deck.Skip(Math.Max(0, deck.Count - 3)).Reverse().ToList();   // index 0 = current top
                    var order = new List<DuelCard>();
                    void Next()
                    {
                        var left = top.Where(t => !order.Contains(t)).ToList();
                        if (left.Count <= 1)
                        {
                            order.AddRange(left);
                            foreach (DuelCard t in top) deck.Remove(t);
                            for (int i = order.Count - 1; i >= 0; i--) deck.Add(order[i]);   // order[0] ends on top
                            c.Finish();
                            return;
                        }
                        Fx.Pick(c, c.Player, left, $"Spellbook Organization: choose the card to place {(order.Count == 0 ? "on top" : "next")}.", "search", 1, 1, picks =>
                        {
                            order.Add(picks.Count > 0 ? picks[0] : left[0]);
                            Next();
                        });
                    }
                    Next();
                },
                Ai = _ => 15
            });

            add("Super Rejuvenation", new Scripted
            {
                Do = Fx.Sync(c =>
                {
                    DuelEngine e = c.Engine;
                    int player = c.Player;
                    int turn = e.TurnNumber;
                    e.AtEndPhase(() =>
                    {
                        int dragons = e.History.Count(ev => ev.Turn == turn && ev.Card != null && ev.Card.Owner == player && Fx.TypeIs(ev.Card.Data, "Dragon") &&
                            (ev.Type == DuelEventType.Discarded ||
                             ev.Type == DuelEventType.SentToGraveyard && ev.Other != null && ev.Other != ev.Card &&
                             (ev.Other.IsMonster || string.Equals(ev.Other.Data.typeLine, "Ritual", StringComparison.OrdinalIgnoreCase))));
                        if (dragons > 0)
                        {
                            e.Raise(DuelEventType.EffectResolved, player, null, text: $"Super Rejuvenation: draw {dragons}.");
                            e.Draw(player, dragons);
                        }
                    });
                }),
                Ai = c => c.Engine.History.Any(ev => ev.Turn == c.Engine.TurnNumber && ev.Card != null && ev.Card.Owner == c.Player && Fx.TypeIs(ev.Card.Data, "Dragon") &&
                                                      (ev.Type == DuelEventType.Discarded || ev.Type == DuelEventType.SentToGraveyard)) ? 55 : 0
            });

            add("Tailor of the Fickle", new Scripted
            {
                Can = c => c.AllMonsters.Count(m => m.IsFaceUp) >= 2,
                Tgt = c => CardEffects.Request(c.AllBackrow.Where(s => !s.FaceDown && s.Card.IsSpell && s.EquippedTo != null).Select(s => s.Card),
                    "Tailor of the Fickle: choose an Equip Card to move.", "boost"),
                Do = c =>
                {
                    DuelBackrowState equip = c.Engine.FindBackrow(c.Target);
                    if (equip?.EquippedTo == null) { c.Finish(); return; }
                    DuelMonsterState from = equip.EquippedTo;
                    Fx.Pick(c, c.Player, c.AllMonsters.Where(m => m.IsFaceUp && m != from).Select(m => m.Card), "Tailor of the Fickle: equip it to another monster.", "boost", 1, 1, picks =>
                    {
                        DuelMonsterState to = picks.Count > 0 ? c.Engine.FindMonster(picks[0]) : null;
                        if (to != null && equip.EquippedTo == from)
                        {
                            from.Equips.Remove(equip);
                            equip.EquippedTo = to;
                            to.Equips.Add(equip);
                            c.Engine.Raise(DuelEventType.EffectResolved, c.Player, c.Card, text: $"{equip.Card.Name} moved to {to.Name}.");
                        }
                        c.Finish();
                    });
                },
                Ai = _ => 20
            });

            add("Tricky Spell 4", new Scripted
            {
                Can = c => c.Me.MonstersOnField.Any(m => m.IsFaceUp && m.Name == "The Tricky") && c.Opp.MonsterCount > 0,
                Cost = (c, paid) =>
                {
                    DuelMonsterState tricky = c.Me.MonstersOnField.FirstOrDefault(m => m.IsFaceUp && m.Name == "The Tricky");
                    if (tricky != null) c.Engine.SendToGraveyard(tricky.Card, destroyed: false, cause: c.Card);
                    paid();
                },
                Do = Fx.Sync(c =>
                {
                    int n = c.Opp.MonsterCount;
                    for (int i = 0; i < n; i++)
                    {
                        DuelMonsterState t = c.Engine.SummonToken(c.Player, "Tricky Token", "Spellcaster", "WIND", 5, 2000, 1200, DuelMonsterPosition.FaceUpDefense, c.Card);
                        if (t == null) break;
                        t.CannotDeclareAttack = true;
                    }
                }),
                Ai = c => c.Opp.MonsterCount >= 2 ? 60 : 0
            });
        }
    }
}
