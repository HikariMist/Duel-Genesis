using System;
using System.Collections.Generic;
using System.Linq;
using DuelGenesis.Cards;

namespace DuelGenesis.Dueling
{
    /// <summary>When a monster's printed effect fires.</summary>
    public enum MonsterAbilityKind
    {
        /// <summary>"FLIP:" / "When this card is flipped face-up" — Flip Summon, attacked while face-down, or flipped by an effect.</summary>
        Flip,
        NormalSummoned,
        FlipSummoned,
        SpecialSummoned,
        /// <summary>This card was destroyed by battle. <see cref="EffectContext.Paid"/> is the monster that destroyed it.</summary>
        DestroyedByBattle,
        /// <summary>Sent to the Graveyard after having been flipped face-up (Spear Cretin).</summary>
        SentToGraveyardAfterFlip,
        /// <summary>Sent from the hand to the Graveyard (discarded).</summary>
        Discarded,
        /// <summary>"Once per turn: You can ..." — used by its controller from the field in their Main Phase.</summary>
        Ignition
    }

    /// <summary>One triggered monster effect. <see cref="Do"/> must end with <c>c.Finish()</c> (use <c>Fx.Sync</c>).</summary>
    public sealed class MonsterAbility
    {
        /// <summary>Checked when the effect would resolve; false = nothing happens.</summary>
        public Func<EffectContext, bool> Can;
        /// <summary>Targets picked when the effect activates (null = no targeting).</summary>
        public Func<EffectContext, TargetRequest> Tgt;
        public Action<EffectContext> Do;
    }

    /// <summary>
    /// Flip effects and summon triggers printed on monsters, looked up by card name.
    /// The engine queues them (<see cref="DuelEngine.QueueMonsterAbility"/>) and resolves them one at a time
    /// once nothing else is waiting, so they never interrupt a Spell or Trap mid-resolution.
    /// </summary>
    public static class MonsterAbilities
    {
        private static readonly Dictionary<string, Dictionary<MonsterAbilityKind, MonsterAbility>> Table = new(StringComparer.OrdinalIgnoreCase);

        public static MonsterAbility Get(DuelCard card, MonsterAbilityKind kind)
        {
            if (card == null || !card.IsMonster) return null;
            if (Table.Count == 0) Register();
            return Table.TryGetValue(card.Name ?? string.Empty, out var byKind) && byKind.TryGetValue(kind, out var a) ? a : null;
        }

        public static IEnumerable<string> ImplementedNames
        {
            get { if (Table.Count == 0) Register(); return Table.Keys; }
        }

        private static void Add(string name, MonsterAbilityKind kind, MonsterAbility ability)
        {
            if (!Table.TryGetValue(name, out var byKind)) Table[name] = byKind = new Dictionary<MonsterAbilityKind, MonsterAbility>();
            byKind[kind] = ability;
        }

        private static void Flip(string name, MonsterAbility a) => Add(name, MonsterAbilityKind.Flip, a);

        // ------------------------------------------------------------------------------ helpers

        private static DuelMonsterState Self(EffectContext c) => c.Engine.FindMonster(c.Card);
        private static bool SelfFaceUp(EffectContext c) => Self(c)?.IsFaceUp == true && c.Card.Controller == c.Player;
        private static bool NameHas(DuelCard d, string part) => (d.Name ?? string.Empty).IndexOf(part, StringComparison.OrdinalIgnoreCase) >= 0;
        private static bool IsFlipMonster(DuelCard d) => d.IsMonster && (d.Data.typeLine ?? string.Empty).IndexOf("Flip", StringComparison.OrdinalIgnoreCase) >= 0;
        private static bool IsRitualMonster(DuelCard d) => d.IsMonster && (d.Data.typeLine ?? string.Empty).IndexOf("Ritual", StringComparison.OrdinalIgnoreCase) >= 0;
        private static bool IsRitualSpell(DuelCard d) => d.IsSpell && string.Equals(d.Data.typeLine, "Ritual", StringComparison.OrdinalIgnoreCase);

        private static void Say(EffectContext c, string text) => c.Engine.Raise(DuelEventType.EffectResolved, c.Player, c.Card, text: $"{c.Card.Name}: {text}");

        /// <summary>Take control of a monster for as long as the Charmer stays face-up on the field.</summary>
        private static void TakeWhileFaceUp(EffectContext c, DuelMonsterState target)
        {
            if (target == null || target.Card.Controller == c.Player) return;
            if (!SelfFaceUp(c)) { Say(c, "is no longer face-up on the field, so control does not change."); return; }
            c.Engine.SwitchControl(target, c.Player, temporary: false);
            if (target.Card.Controller == c.Player) target.ControlHeldBy = c.Card;
        }

        private static MonsterAbility Charmer(string attribute, bool targets) => targets
            ? new MonsterAbility
            {
                Tgt = c => CardEffects.Request(c.Opp.MonstersOnField.Where(m => m.IsFaceUp && Fx.AttrIs(m.Card.Data, attribute)).Select(m => m.Card),
                    $"Take control of 1 face-up {attribute} monster your opponent controls.", "control"),
                Do = Fx.Sync(c => TakeWhileFaceUp(c, c.Engine.FindMonster(c.Target)))
            }
            : new MonsterAbility
            {
                Do = c => Fx.Pick(c, c.Player, c.Opp.MonstersOnField.Where(m => m.IsFaceUp && Fx.AttrIs(m.Card.Data, attribute)).Select(m => m.Card),
                    $"Take control of 1 {attribute} monster your opponent controls.", "control", 1, 1,
                    picks => { if (picks.Count > 0) TakeWhileFaceUp(c, c.Engine.FindMonster(picks[0])); c.Finish(); })
            };

        /// <summary>Special Summon 1 card from your Deck that matches <paramref name="filter"/>.</summary>
        private static MonsterAbility SummonFromDeck(Func<DuelCard, bool> filter, string prompt) => new()
        {
            Do = c =>
            {
                if (!c.Me.HasFreeMonsterZone) { Say(c, "no free Monster Zone."); c.Finish(); return; }
                Fx.Pick(c, c.Player, c.Me.Deck.Where(d => d.IsMonster && filter(d)), prompt, "revive", 1, 1, picks =>
                {
                    if (picks.Count > 0) c.Engine.SpecialSummon(c.Player, picks[0], DuelMonsterPosition.FaceUpAttack, c.Card);
                    else Say(c, "no card in the Deck can be Special Summoned.");
                    c.Engine.ShuffleDeck(c.Player);
                    c.Finish();
                });
            }
        };

        /// <summary>"You can add 1 ... from your Deck to your hand" (optional: the player may pick nothing).</summary>
        private static MonsterAbility SearchDeck(Func<DuelCard, bool> filter, string prompt) => new()
        {
            Do = c => Fx.Pick(c, c.Player, c.Me.Deck.Where(filter), prompt, "search", 0, 1, picks =>
            {
                if (picks.Count > 0) c.Engine.AddToHand(picks[0], c.Card);
                c.Engine.ShuffleDeck(c.Player);
                c.Finish();
            })
        };

        private static MonsterAbility SalvageFromGY(Func<DuelCard, bool> filter, string prompt) => new()
        {
            Tgt = c => CardEffects.Request(c.Me.Graveyard.Where(d => d != c.Card && filter(d)), prompt, "salvage"),
            Do = Fx.Sync(c => { if (c.Target?.Zone == DuelZone.Graveyard) c.Engine.AddToHand(c.Target, c.Card); })
        };

        /// <summary>"If this card is destroyed by battle: the monster that destroyed it loses 500 ATK and DEF."</summary>
        private static readonly MonsterAbility WeakenDestroyer = new()
        {
            Do = Fx.Sync(c =>
            {
                DuelMonsterState killer = c.Engine.FindMonster(c.Paid);
                if (killer == null) return;
                killer.PermAttack -= 500;
                killer.PermDefense -= 500;
                Say(c, $"{killer.Name} loses 500 ATK and DEF.");
            })
        };

        // ------------------------------------------------------------------------------ registry

        private static void Register()
        {
            // ---- destruction
            Flip("Man-Eater Bug", new MonsterAbility
            {
                Tgt = c => CardEffects.Request(c.AllMonsters.Select(m => m.Card), "Man-Eater Bug: destroy 1 monster on the field.", "destroy"),
                Do = Fx.Sync(c => { if (c.Target?.Zone == DuelZone.Monster) c.Engine.Destroy(c.Target, c.Card); })
            });
            Flip("Night Assailant", new MonsterAbility
            {
                Tgt = c => CardEffects.Request(c.Opp.MonstersOnField.Select(m => m.Card), "Night Assailant: destroy 1 monster your opponent controls.", "destroy"),
                Do = Fx.Sync(c => { if (c.Target?.Zone == DuelZone.Monster) c.Engine.Destroy(c.Target, c.Card); })
            });
            Add("Night Assailant", MonsterAbilityKind.Discarded, SalvageFromGY(d => IsFlipMonster(d) && d.Name != "Night Assailant",
                "Night Assailant: return 1 Flip monster from your Graveyard to your hand."));
            Flip("Magnetic Mosquito", new MonsterAbility
            {
                Do = Fx.Sync(c =>
                {
                    foreach (DuelMonsterState m in c.AllMonsters.Where(m => m.IsFaceUp && Fx.TypeIs(m.Card.Data, "Machine")).ToList())
                        c.Engine.Destroy(m.Card, c.Card);
                })
            });
            Flip("Armed Ninja", new MonsterAbility
            {
                Tgt = c => CardEffects.Request(c.AllBackrow.Where(s => s.FaceDown || s.Card.IsSpell).Select(s => s.Card),
                    "Armed Ninja: target 1 Spell Card on the field (a Set card is revealed first).", "destroy-backrow"),
                Do = Fx.Sync(c =>
                {
                    DuelBackrowState s = c.Engine.FindBackrow(c.Target);
                    if (s == null) return;
                    if (s.Card.IsSpell) c.Engine.Destroy(s.Card, c.Card);
                    else Say(c, $"revealed \"{s.Card.Name}\" — not a Spell, so it is returned face-down.");
                })
            });
            Flip("Dragon Piper", new MonsterAbility
            {
                Do = Fx.Sync(c =>
                {
                    var jars = c.AllBackrow.Where(s => !s.FaceDown && s.Card.Name == "Dragon Capture Jar").ToList();
                    foreach (DuelBackrowState s in jars) c.Engine.Destroy(s.Card, c.Card);
                    if (jars.Count == 0) return;
                    foreach (DuelMonsterState m in c.AllMonsters.Where(m => m.IsFaceUp && Fx.TypeIs(m.Card.Data, "Dragon")).ToList())
                        c.Engine.ForcePosition(m, DuelMonsterPosition.FaceUpAttack);
                })
            });
            Flip("Weather Report", new MonsterAbility
            {
                Do = Fx.Sync(c =>
                {
                    var swords = c.Opp.AllBackrow.Where(s => !s.FaceDown && s.Card.Name == "Swords of Revealing Light").ToList();
                    foreach (DuelBackrowState s in swords) c.Engine.Destroy(s.Card, c.Card);
                    if (swords.Count > 0) Say(c, "Swords of Revealing Light destroyed.");
                })
            });
            Flip("Fossil Dyna Pachycephalo", new MonsterAbility
            {
                Do = Fx.Sync(c =>
                {
                    foreach (DuelMonsterState m in c.AllMonsters.Where(m => m.SpecialSummoned).ToList())
                        c.Engine.Destroy(m.Card, c.Card);
                })
            });
            Flip("Cyber Jar", new MonsterAbility
            {
                Do = Fx.Sync(c =>
                {
                    foreach (DuelMonsterState m in c.AllMonsters.ToList()) c.Engine.Destroy(m.Card, c.Card);
                    foreach (int p in new[] { c.Player, c.OpponentIndex })
                    {
                        DuelistState d = c.Engine.Duelists[p];
                        var top = d.Deck.Skip(Math.Max(0, d.Deck.Count - 5)).Reverse().ToList();
                        if (top.Count > 0)
                            c.Engine.Raise(DuelEventType.Message, p, c.Card, text: $"{d.Name} reveals: {string.Join(", ", top.Select(t => t.Name))}.");
                        foreach (DuelCard card in top)
                        {
                            bool summon = card.IsMonster && !card.IsExtraDeckCard && card.Data.level <= 4 && d.HasFreeMonsterZone;
                            if (summon)
                                c.Engine.SpecialSummon(p, card, card.Data.attack >= card.Data.defense ? DuelMonsterPosition.FaceUpAttack : DuelMonsterPosition.FaceDownDefense,
                                    c.Card, openWindow: false);
                            else c.Engine.AddToHand(card, c.Card);
                        }
                    }
                })
            });

            // ---- control
            Flip("Aussa the Earth Charmer", Charmer("EARTH", targets: false));
            Flip("Wynn the Wind Charmer", Charmer("WIND", targets: false));
            Flip("Lyna the Light Charmer", Charmer("LIGHT", targets: false));
            Flip("Eria the Water Charmer", Charmer("WATER", targets: true));
            Flip("Hiita the Fire Charmer", Charmer("FIRE", targets: true));
            Flip("Jowls of Dark Demise", new MonsterAbility
            {
                Do = c => Fx.Pick(c, c.Player, c.Opp.MonstersOnField.Select(m => m.Card),
                    "Jowls of Dark Demise: take control of 1 of your opponent's monsters until the end of this turn.", "control", 1, 1, picks =>
                    {
                        DuelMonsterState m = picks.Count > 0 ? c.Engine.FindMonster(picks[0]) : null;
                        if (m != null)
                        {
                            c.Engine.SwitchControl(m, c.Player, temporary: true);
                            if (m.Card.Controller == c.Player) m.CanAttackDirectly = true;
                        }
                        c.Finish();
                    })
            });

            // ---- bounce
            Flip("Penguin Soldier", new MonsterAbility
            {
                Do = c => Fx.Pick(c, c.Player, c.AllMonsters.Select(m => m.Card), "Penguin Soldier: return up to 2 monsters to the hand (optional).",
                    "bounce", 0, 2, picks =>
                    {
                        foreach (DuelCard p in picks) if (p.Zone == DuelZone.Monster) c.Engine.ReturnToHand(p, c.Card);
                        c.Finish();
                    })
            });
            Flip("Nightmare Penguin", new MonsterAbility
            {
                Tgt = c => CardEffects.Request(c.Opp.MonstersOnField.Select(m => m.Card).Concat(c.Opp.AllBackrow.Select(s => s.Card)),
                    "Nightmare Penguin: return 1 card your opponent controls to the hand.", "bounce"),
                Do = Fx.Sync(c => { if (c.Target != null && c.Target.OnField) c.Engine.ReturnToHand(c.Target, c.Card); })
            });

            // ---- hand / deck / graveyard
            Flip("Magician of Faith", SalvageFromGY(d => d.IsSpell, "Magician of Faith: add 1 Spell from your Graveyard to your hand."));
            Flip("Mask of Darkness", SalvageFromGY(d => d.IsTrap, "Mask of Darkness: add 1 Trap from your Graveyard to your hand."));
            Flip("Des Feral Imp", new MonsterAbility
            {
                Tgt = c => CardEffects.Request(c.Me.Graveyard.Where(d => d != c.Card), "Des Feral Imp: shuffle 1 card from your Graveyard into your Deck.", "salvage"),
                Do = Fx.Sync(c => { if (c.Target?.Zone == DuelZone.Graveyard) c.Engine.ReturnToDeck(c.Target, shuffle: true); })
            });
            Flip("Needle Worm", new MonsterAbility
            {
                Do = Fx.Sync(c =>
                {
                    for (int i = 0; i < 5 && c.Opp.Deck.Count > 0; i++)
                        c.Engine.SendToGraveyard(c.Opp.Deck[c.Opp.Deck.Count - 1], destroyed: false, cause: c.Card);
                })
            });
            Flip("Morphing Jar", new MonsterAbility
            {
                Do = Fx.Sync(c =>
                {
                    foreach (int p in new[] { c.Player, c.OpponentIndex })
                        foreach (DuelCard h in c.Engine.Duelists[p].Hand.ToList()) c.Engine.Discard(h);
                    c.Engine.Draw(c.Player, 5);
                    if (!c.Engine.IsOver) c.Engine.Draw(c.OpponentIndex, 5);
                })
            });
            Flip("Hiro's Shadow Scout", new MonsterAbility
            {
                Do = Fx.Sync(c =>
                {
                    int before = c.Opp.Hand.Count;
                    c.Engine.Draw(c.OpponentIndex, 3);
                    var drawn = c.Opp.Hand.Skip(before).ToList();
                    if (drawn.Count > 0) Say(c, $"{c.Opp.Name} drew {string.Join(", ", drawn.Select(d => d.Name))}.");
                    foreach (DuelCard d in drawn.Where(d => d.IsSpell)) c.Engine.Discard(d);
                })
            });
            Flip("Rigorous Reaver", new MonsterAbility
            {
                Do = c => Fx.Pick(c, c.Player, c.Me.Hand, "Rigorous Reaver: discard 1 card.", "discard", 1, 1, mine =>
                {
                    foreach (DuelCard d in mine) c.Engine.Discard(d);
                    Fx.Pick(c, c.OpponentIndex, c.Opp.Hand, "Rigorous Reaver: discard 1 card.", "discard", 1, 1, theirs =>
                    {
                        foreach (DuelCard d in theirs) c.Engine.Discard(d);
                        c.Finish();
                    });
                })
            });
            Add("Rigorous Reaver", MonsterAbilityKind.DestroyedByBattle, WeakenDestroyer);
            Flip("The Stern Mystic", new MonsterAbility
            {
                Do = Fx.Sync(c =>
                {
                    var hidden = c.AllMonsters.Where(m => m.IsFaceDown).Select(m => m.Card).Concat(c.AllBackrow.Where(s => s.FaceDown).Select(s => s.Card)).ToList();
                    Say(c, hidden.Count == 0 ? "there are no face-down cards." : "reveals " + string.Join(", ", hidden.Select(h => $"{h.Name} ({c.Engine.Duelists[h.Controller].Name})")) + ".");
                })
            });

            // ---- life points / stats
            Flip("Des Koala", new MonsterAbility
            {
                Do = Fx.Sync(c => c.Engine.DealDamage(c.OpponentIndex, 400 * c.Opp.Hand.Count, c.Card, battle: false))
            });
            Flip("Slate Warrior", new MonsterAbility
            {
                Do = Fx.Sync(c =>
                {
                    DuelMonsterState me = Self(c);
                    if (me == null || me.IsFaceDown) return;
                    me.PermAttack += 500;
                    me.PermDefense += 500;
                })
            });
            Add("Slate Warrior", MonsterAbilityKind.DestroyedByBattle, WeakenDestroyer);

            // ---- Special Summons
            Flip("Gravekeeper's Spy", SummonFromDeck(d => NameHas(d, "Gravekeeper's") && d.Data.attack <= 1500,
                "Gravekeeper's Spy: Special Summon 1 \"Gravekeeper's\" monster with 1500 or less ATK from your Deck."));
            Flip("Batteryman Micro-Cell", SummonFromDeck(d => NameHas(d, "Batteryman") && d.Name != "Batteryman Micro-Cell" && d.Data.level <= 4,
                "Batteryman Micro-Cell: Special Summon 1 Level 4 or lower \"Batteryman\" monster from your Deck."));
            Add("Batteryman Micro-Cell", MonsterAbilityKind.DestroyedByBattle, new MonsterAbility { Do = Fx.Sync(c => c.Engine.Draw(c.Player, 1)) });
            Add("Spear Cretin", MonsterAbilityKind.SentToGraveyardAfterFlip, new MonsterAbility
            {
                Do = c =>
                {
                    void Revive(int p, Action next)
                    {
                        DuelistState d = c.Engine.Duelists[p];
                        if (!d.HasFreeMonsterZone) { next(); return; }
                        Fx.Pick(c, p, d.Graveyard.Where(x => x.IsMonster && !x.IsExtraDeckCard), "Spear Cretin: Special Summon 1 monster from your Graveyard.",
                            "revive", 1, 1, picks =>
                            {
                                if (picks.Count > 0) c.Engine.SpecialSummon(p, picks[0], DuelMonsterPosition.FaceUpAttack, c.Card, openWindow: false);
                                next();
                            });
                    }
                    Revive(c.Player, () => Revive(c.OpponentIndex, c.Finish));
                }
            });

            // ---- ignition effects ("Once per turn: You can ...")
            Add("Relinquished", MonsterAbilityKind.Ignition, new MonsterAbility
            {
                Can = c => SelfFaceUp(c) && !Self(c).Equips.Any(s => s.Card.IsMonster) && c.Opp.MonsterCount > 0 && c.Me.HasFreeSpellTrapZone,
                Tgt = c => CardEffects.Request(c.Opp.MonstersOnField.Select(m => m.Card), "Relinquished: equip 1 monster your opponent controls to this card.", "control"),
                Do = Fx.Sync(c =>
                {
                    DuelMonsterState me = Self(c);
                    if (me == null || me.IsFaceDown || c.Target?.Zone != DuelZone.Monster) return;
                    c.Engine.EquipMonsterCard(c.Target, me);
                })
            });

            // ---- summon triggers ("When this card is Normal or Flip Summoned")
            var dragonSeeker = new MonsterAbility
            {
                Tgt = c => CardEffects.Request(c.AllMonsters.Where(m => m.IsFaceUp && Fx.TypeIs(m.Card.Data, "Dragon")).Select(m => m.Card),
                    "Dragon Seeker: destroy 1 face-up Dragon monster.", "destroy"),
                Do = Fx.Sync(c => { if (c.Target?.Zone == DuelZone.Monster) c.Engine.Destroy(c.Target, c.Card); })
            };
            var senju = SearchDeck(IsRitualMonster, "Senju of the Thousand Hands: add 1 Ritual Monster from your Deck to your hand.");
            var manju = SearchDeck(d => IsRitualMonster(d) || IsRitualSpell(d), "Manju of the Ten Thousand Hands: add 1 Ritual Monster or Ritual Spell from your Deck to your hand.");
            var sonic = SearchDeck(IsRitualSpell, "Sonic Bird: add 1 Ritual Spell from your Deck to your hand.");
            var clown = new MonsterAbility
            {
                Tgt = c => CardEffects.Request(c.AllMonsters.Where(m => m.IsFaceUp).Select(m => m.Card), "Ryu-Kishin Clown: change the battle position of 1 face-up monster.", "position"),
                Do = Fx.Sync(c => { DuelMonsterState m = c.Engine.FindMonster(c.Target); if (m != null && m.IsFaceUp) Fx.Flip(c.Engine, m); })
            };
            foreach (MonsterAbilityKind k in new[] { MonsterAbilityKind.NormalSummoned, MonsterAbilityKind.FlipSummoned })
            {
                Add("Dragon Seeker", k, dragonSeeker);
                Add("Senju of the Thousand Hands", k, senju);
                Add("Manju of the Ten Thousand Hands", k, manju);
                Add("Sonic Bird", k, sonic);
            }
            foreach (MonsterAbilityKind k in new[] { MonsterAbilityKind.NormalSummoned, MonsterAbilityKind.FlipSummoned, MonsterAbilityKind.SpecialSummoned })
                Add("Ryu-Kishin Clown", k, clown);
            Add("Sauropod Brachion", MonsterAbilityKind.FlipSummoned, new MonsterAbility
            {
                Do = Fx.Sync(c =>
                {
                    foreach (DuelMonsterState m in c.AllMonsters.Where(m => m.Card != c.Card && m.IsFaceUp).ToList())
                        c.Engine.ForcePosition(m, DuelMonsterPosition.FaceDownDefense);
                })
            });
        }
    }

    /// <summary>Nightmare Penguin: face-up WATER monsters you control gain 200 ATK.</summary>
    internal sealed class NightmarePenguinAura : MonsterEffect
    {
        public override int AuraAttackModifier(DuelEngine engine, DuelMonsterState source, DuelMonsterState target) =>
            target.Card.Controller == source.Card.Controller && Fx.AttrIs(target.Card.Data, "WATER") ? 200 : 0;
    }

    /// <summary>Relinquished: ATK/DEF become the equipped monster's; the equipped monster is destroyed in its place in battle.</summary>
    internal sealed class RelinquishedStats : MonsterEffect
    {
        private static DuelBackrowState Absorbed(DuelMonsterState self) => self.Equips.FirstOrDefault(s => s.Card.IsMonster);
        public override int SelfAttackModifier(DuelEngine engine, DuelMonsterState self) => Absorbed(self)?.Card.Data.attack ?? 0;
        public override int SelfDefenseModifier(DuelEngine engine, DuelMonsterState self) => Absorbed(self)?.Card.Data.defense ?? 0;
        public override DuelCard BattleSubstitute(DuelEngine engine, DuelMonsterState self) => Absorbed(self)?.Card;
        public override bool MirrorsBattleDamage(DuelEngine engine, DuelMonsterState self) => Absorbed(self) != null;
    }

    /// <summary>Blade Knight: gains 400 ATK while you have 1 or fewer cards in your hand.</summary>
    internal sealed class BladeKnight : MonsterEffect
    {
        public override int SelfAttackModifier(DuelEngine engine, DuelMonsterState self) =>
            engine.Me(self.Card.Controller).Hand.Count <= 1 ? 400 : 0;
    }
}
