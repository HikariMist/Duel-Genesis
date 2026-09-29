using System;
using System.Collections.Generic;
using System.Linq;
using DuelGenesis.Cards;

namespace DuelGenesis.Dueling
{
    /// <summary>
    /// Card-by-card Spell and Trap implementations built from small building blocks (<see cref="Fx"/>).
    /// Each entry implements the card's main effect as printed. Secondary clauses that need rules the engine
    /// does not model yet (piercing damage, "cannot change battle position" auras, optional graveyard
    /// triggers) are left out; cards whose core effect needs such rules are not registered at all.
    /// </summary>
    public static class CardEffectLibrary
    {
        private static readonly Dictionary<string, CardEffect> Effects = new(StringComparer.OrdinalIgnoreCase);

        public static bool TryGet(string cardName, out CardEffect effect)
        {
            if (Effects.Count == 0) Register();
            return Effects.TryGetValue(cardName ?? string.Empty, out effect);
        }

        public static IEnumerable<string> ImplementedNames
        {
            get
            {
                if (Effects.Count == 0) Register();
                return Effects.Keys;
            }
        }

        private static void Add(string name, CardEffect effect) => Effects[name] = effect;

        // =================================================================================== registry

        private static void Register()
        {
            RegisterLifeAndBurn();
            RegisterMassRemoval();
            RegisterTargetedRemoval();
            RegisterPositions();
            RegisterRecoveryAndSearch();
            RegisterSummoning();
            RegisterHandAndDeck();
            RegisterTempStats();
            RegisterContinuous();
            RegisterEquips();
            RegisterAttackTraps();
            RegisterSummonTraps();
            RegisterCounterTraps();
            RegisterRituals();
            FieldSpells.Register(Add);
            CounterTraps.Register(Add);
        }

        // ----------------------------------------------------------------------------- LP / burn

        private static void RegisterLifeAndBurn()
        {
            Add("Final Flame", Fx.Burn(600));
            Add("Blue Medicine", Fx.Heal(400));
            Add("Red Medicine", Fx.Heal(500));
            Add("Goblin's Secret Remedy", Fx.Heal(600));
            Add("Soul of the Pure", Fx.Heal(800));
            Add("Dian Keto the Cure Master", Fx.Heal(1000));
            Add("Mooyan Curry", Fx.Heal(200));
            Add("Goblin Thief", new Scripted
            {
                Do = Fx.Sync(c => { c.Engine.DealDamage(c.OpponentIndex, 500, c.Card, false); c.Engine.GainLife(c.Player, 500, c.Card); }),
                Ai = c => 55
            });
            Add("Tremendous Fire", new Scripted
            {
                Can = c => c.Me.LifePoints > 500,
                Do = Fx.Sync(c => { c.Engine.DealDamage(c.OpponentIndex, 1000, c.Card, false); c.Engine.DealDamage(c.Player, 500, c.Card, false); }),
                Ai = c => c.Me.LifePoints > 2000 ? 55 : 0
            });
            Add("Meteor of Destruction", new Scripted
            {
                Can = c => c.Opp.LifePoints > 3000,
                Do = Fx.Sync(c => c.Engine.DealDamage(c.OpponentIndex, 1000, c.Card, false)),
                Ai = c => 60
            });
            Add("Rain of Mercy", new Scripted
            {
                Do = Fx.Sync(c => { c.Engine.GainLife(0, 1000, c.Card); c.Engine.GainLife(1, 1000, c.Card); }),
                Ai = c => c.Me.LifePoints < 3000 ? 35 : 0
            });
            Add("Restructer Revolution", new Scripted
            {
                Can = c => c.Opp.Hand.Count > 0,
                Do = Fx.Sync(c => c.Engine.DealDamage(c.OpponentIndex, 200 * c.Opp.Hand.Count, c.Card, false)),
                Ai = c => c.Opp.Hand.Count >= 3 ? 55 : 20
            });
            Add("Poison of the Old Man", new Scripted
            {
                Respond = Fx.OnOpponentAction,
                Do = c => Fx.Choose(c, c.Player, "Choose an effect.",
                    c.Opp.LifePoints <= 800 || c.Me.LifePoints > 3000 ? new[] { "Inflict 800 damage", "Gain 1200 LP" } : new[] { "Gain 1200 LP", "Inflict 800 damage" },
                    (option, label) =>
                    {
                        if (label.StartsWith("Inflict")) c.Engine.DealDamage(c.OpponentIndex, 800, c.Card, false);
                        else c.Engine.GainLife(c.Player, 1200, c.Card);
                        c.Finish();
                    }),
                Ai = c => 50
            });
            Add("Jar of Greed", new Scripted
            {
                Can = c => c.Me.Deck.Count >= 1,
                Respond = c => Fx.OnOpponentAction(c) && c.Me.Deck.Count >= 1,
                Do = Fx.Sync(c => c.Engine.Draw(c.Player, 1)),
                Ai = c => 60
            });
            Add("Reckless Greed", new Scripted
            {
                Can = c => c.Me.Deck.Count >= 2,
                Respond = c => Fx.OnOpponentAction(c) && c.Me.Deck.Count >= 2,
                Do = Fx.Sync(c => { c.Engine.Draw(c.Player, 2); c.Me.SkipDraws += 2; }),
                Ai = c => c.Me.Hand.Count <= 1 ? 55 : 0
            });
            Add("Cup of Ace", new Scripted
            {
                Can = c => c.Me.Deck.Count >= 2 && c.Opp.Deck.Count >= 2,
                Do = Fx.Sync(c =>
                {
                    bool heads = c.Engine.RandomRange(0, 2) == 0;
                    c.Engine.Raise(DuelEventType.EffectResolved, c.Player, c.Card, text: heads ? "Cup of Ace: Heads!" : "Cup of Ace: Tails!");
                    c.Engine.Draw(heads ? c.Player : c.OpponentIndex, 2);
                }),
                Ai = c => 30
            });
            Add("Good Goblin Housekeeping", new Scripted
            {
                Can = c => c.Me.Deck.Count >= 1,
                Respond = c => Fx.OnOpponentAction(c) && c.Me.Deck.Count >= 1,
                Do = c =>
                {
                    int draws = 1 + c.Me.Graveyard.Count(x => x.Name == "Good Goblin Housekeeping");
                    c.Engine.Draw(c.Player, Math.Min(draws, c.Me.Deck.Count));
                    Fx.Pick(c, c.Player, c.Me.Hand, "Return 1 card from your hand to the bottom of your Deck.", "discard", 1, 1, picks =>
                    {
                        foreach (DuelCard x in picks) c.Engine.ReturnToDeck(x, shuffle: false, bottom: true);
                        c.Finish();
                    });
                },
                Ai = c => 60
            });
            Add("Cemetary Bomb", Fx.BurnTrap(c => 100 * c.Opp.Graveyard.Count, c => c.Opp.Graveyard.Count >= 8));
            Add("D.D. Dynamite", Fx.BurnTrap(c => 300 * c.Opp.Banished.Count, c => c.Opp.Banished.Count >= 2));
            Add("Secret Barrel", Fx.BurnTrap(c => 200 * (c.Opp.Hand.Count + c.Opp.MonsterCount + c.Opp.AllBackrow.Count()), c => c.Opp.Hand.Count + c.Opp.MonsterCount >= 4));
            Add("Solar Ray", Fx.BurnTrap(c => 600 * c.Me.MonstersOnField.Count(m => m.IsFaceUp && Fx.AttrIs(m.Card.Data, "LIGHT")),
                c => c.Me.MonstersOnField.Any(m => m.IsFaceUp && Fx.AttrIs(m.Card.Data, "LIGHT"))));
            Add("Blasting the Ruins", Fx.BurnTrap(c => c.Me.Graveyard.Count >= 30 ? 3000 : 0, c => c.Me.Graveyard.Count >= 30));
            Add("Destruction Ring", new Scripted
            {
                Can = c => c.Me.MonstersOnField.Any(m => m.IsFaceUp),
                Respond = c => Fx.OnOpponentAction(c) && c.Me.MonstersOnField.Any(m => m.IsFaceUp),
                Tgt = c => CardEffects.Request(c.Me.MonstersOnField.Where(m => m.IsFaceUp).Select(m => m.Card), "Destroy 1 face-up monster you control.", "tribute"),
                Do = Fx.Sync(c =>
                {
                    if (c.Target?.Zone == DuelZone.Monster) c.Engine.Destroy(c.Target, c.Card);
                    c.Engine.DealDamage(c.Player, 1000, c.Card, false);
                    c.Engine.DealDamage(c.OpponentIndex, 1000, c.Card, false);
                }),
                Ai = c => c.Opp.LifePoints <= 1000 && c.Me.LifePoints > 1000 ? 95 : 0
            });
            Add("Ring of Destruction", new Scripted
            {
                Can = c => false,
                Respond = c => c.Trigger != null && c.Engine.TurnPlayer != c.Player && Candidates(c).Any(),
                Tgt = c => CardEffects.Request(Candidates(c), "Destroy 1 face-up opponent's monster (both players take damage equal to its ATK).", "destroy-monster"),
                Do = Fx.Sync(c =>
                {
                    DuelMonsterState m = c.Engine.FindMonster(c.Target);
                    if (m == null) return;
                    int atk = m.Card.Data.attack;
                    c.Engine.Destroy(m.Card, c.Card);
                    c.Engine.DealDamage(c.Player, atk, c.Card, false);
                    c.Engine.DealDamage(c.OpponentIndex, atk, c.Card, false);
                }),
                Ai = c => Candidates(c).Any(x => x.Data.attack >= c.Opp.LifePoints && c.Me.LifePoints > x.Data.attack) ? 95 : 0
            });

            static IEnumerable<DuelCard> Candidates(EffectContext c) =>
                c.Opp.MonstersOnField.Where(m => m.IsFaceUp && c.Engine.GetAttack(m) <= c.Opp.LifePoints).Select(m => m.Card);
        }

        // ----------------------------------------------------------------------------- mass removal

        private static void RegisterMassRemoval()
        {
            Add("Acid Rain", Fx.DestroyAllFaceUp(m => Fx.TypeIs(m.Card.Data, "Machine")));
            Add("Breath of Light", Fx.DestroyAllFaceUp(m => Fx.TypeIs(m.Card.Data, "Rock")));
            Add("Eradicating Aerosol", Fx.DestroyAllFaceUp(m => Fx.TypeIs(m.Card.Data, "Insect")));
            Add("Eternal Drought", Fx.DestroyAllFaceUp(m => Fx.TypeIs(m.Card.Data, "Fish")));
            Add("Exile of the Wicked", Fx.DestroyAllFaceUp(m => Fx.TypeIs(m.Card.Data, "Fiend")));
            Add("Last Day of Witch", Fx.DestroyAllFaceUp(m => Fx.TypeIs(m.Card.Data, "Spellcaster")));
            Add("Warrior Elimination", Fx.DestroyAllFaceUp(m => Fx.TypeIs(m.Card.Data, "Warrior")));
            Add("Eternal Rest", Fx.DestroyAllFaceUp(m => m.Equips.Count > 0));
            Add("Really Eternal Rest", Fx.Freeable(Fx.DestroyAllFaceUp(m => m.Equips.Count > 0)));
            Add("Needle Ceiling", new Scripted
            {
                Can = c => c.AllMonsters.Count() >= 4,
                Respond = c => Fx.OnOpponentAction(c) && c.AllMonsters.Count() >= 4,
                Do = Fx.Sync(c => { foreach (DuelMonsterState m in c.AllMonsters.Where(m => m.IsFaceUp).ToList()) c.Engine.Destroy(m.Card, c.Card); }),
                Ai = c => Fx.Advantage(c, c.AllMonsters.Where(m => m.IsFaceUp)) >= 1000 ? 80 : 0
            });
            Add("Lightning Vortex", new Scripted
            {
                Can = c => c.Me.Hand.Count >= 2 && c.Opp.MonstersOnField.Any(m => m.IsFaceUp),
                Cost = Fx.DiscardCost(1),
                Do = Fx.Sync(c => { foreach (DuelMonsterState m in c.Opp.MonstersOnField.Where(m => m.IsFaceUp).ToList()) c.Engine.Destroy(m.Card, c.Card); }),
                Ai = c => c.Opp.MonstersOnField.Count(m => m.IsFaceUp) >= 2 ? 85 : c.Opp.MonstersOnField.Any(m => m.IsFaceUp && c.Engine.GetAttack(m) >= 2000) ? 70 : 0
            });
            Add("Thunder Crash", new Scripted
            {
                Can = c => c.Me.MonsterCount > 0,
                Do = Fx.Sync(c =>
                {
                    List<DuelMonsterState> mine = c.Me.MonstersOnField.ToList();
                    foreach (DuelMonsterState m in mine) c.Engine.Destroy(m.Card, c.Card);
                    c.Engine.DealDamage(c.OpponentIndex, 300 * mine.Count, c.Card, false);
                }),
                Ai = c => c.Opp.LifePoints <= 300 * c.Me.MonsterCount ? 90 : 0
            });
            Add("Final Destiny", new Scripted
            {
                Can = c => c.Me.Hand.Count >= 6,
                Cost = Fx.DiscardCost(5),
                Do = Fx.Sync(c =>
                {
                    foreach (DuelMonsterState m in c.AllMonsters.ToList()) c.Engine.Destroy(m.Card, c.Card);
                    foreach (DuelBackrowState s in c.AllBackrow.Where(s => s.Card != c.Card).ToList()) c.Engine.Destroy(s.Card, c.Card);
                }),
                Ai = c => 0
            });
            Add("Giant Trunade", new Scripted
            {
                Can = c => c.AllBackrow.Any(s => s.Card != c.Card),
                Do = Fx.Sync(c => { foreach (DuelBackrowState s in c.AllBackrow.Where(s => s.Card != c.Card).ToList()) c.Engine.ReturnToHand(s.Card, c.Card); }),
                Ai = c => c.Opp.AllBackrow.Count() >= 2 && c.Opp.AllBackrow.Count() > c.Me.AllBackrow.Count(s => s.Card != c.Card) ? 70 : 0
            });
            Add("Gryphon's Feather Duster", new Scripted
            {
                Can = c => c.Me.AllBackrow.Any(s => s.Card != c.Card),
                Do = Fx.Sync(c =>
                {
                    List<DuelBackrowState> mine = c.Me.AllBackrow.Where(s => s.Card != c.Card).ToList();
                    foreach (DuelBackrowState s in mine) c.Engine.Destroy(s.Card, c.Card);
                    c.Engine.GainLife(c.Player, 500 * mine.Count, c.Card);
                }),
                Ai = c => 0
            });
            Add("Spell Shattering Arrow", new Scripted
            {
                Can = c => c.Opp.AllBackrow.Any(s => !s.FaceDown && s.Card.IsSpell),
                Respond = c => Fx.OnOpponentAction(c) && c.Opp.AllBackrow.Any(s => !s.FaceDown && s.Card.IsSpell),
                Do = Fx.Sync(c =>
                {
                    List<DuelBackrowState> spells = c.Opp.AllBackrow.Where(s => !s.FaceDown && s.Card.IsSpell).ToList();
                    foreach (DuelBackrowState s in spells) c.Engine.Destroy(s.Card, c.Card);
                    c.Engine.DealDamage(c.OpponentIndex, 500 * spells.Count, c.Card, false);
                }),
                Ai = c => 75
            });
            Add("Malice Dispersion", Fx.DestroyFaceUpBackrow(s => s.Card.IsTrap && DuelRules.IsContinuous(s.Card.Data), discard: 1));
            Add("Spell Purification", Fx.Freeable(Fx.DestroyFaceUpBackrow(s => s.Card.IsSpell && DuelRules.IsContinuous(s.Card.Data), discard: 1)));
            Add("Remove Trap", Fx.DestroyTargetedBackrow(s => !s.FaceDown && s.Card.IsTrap, "Destroy 1 face-up Trap Card."));
            Add("Burst Stream of Destruction", new Scripted
            {
                Can = c => Fx.ControlsFaceUp(c, "Blue-Eyes White Dragon") && c.Opp.MonsterCount > 0,
                Do = Fx.Sync(c =>
                {
                    foreach (DuelMonsterState m in c.Opp.MonstersOnField.ToList()) c.Engine.Destroy(m.Card, c.Card);
                    foreach (DuelMonsterState m in c.Me.MonstersOnField.Where(m => m.Name == "Blue-Eyes White Dragon")) m.CannotAttackThisTurn = true;
                }),
                Ai = c => 90
            });
            Add("Dark Magic Attack", new Scripted
            {
                Can = c => Fx.ControlsFaceUp(c, "Dark Magician") && c.Opp.AllBackrow.Any(),
                Do = Fx.Sync(c => { foreach (DuelBackrowState s in c.Opp.AllBackrow.ToList()) c.Engine.Destroy(s.Card, c.Card); }),
                Ai = c => 85
            });
            Add("Dark Burning Attack", new Scripted
            {
                Can = c => Fx.ControlsFaceUp(c, "Dark Magician Girl") && c.Opp.MonstersOnField.Any(m => m.IsFaceUp),
                Do = Fx.Sync(c => { foreach (DuelMonsterState m in c.Opp.MonstersOnField.Where(m => m.IsFaceUp).ToList()) c.Engine.Destroy(m.Card, c.Card); }),
                Ai = c => 85
            });
            Add("Burst Breath", new Scripted
            {
                Can = c => c.Me.MonstersOnField.Any(m => Fx.TypeIs(m.Card.Data, "Dragon")),
                Respond = c => Fx.OnOpponentAction(c) && c.Me.MonstersOnField.Any(m => Fx.TypeIs(m.Card.Data, "Dragon")),
                Cost = Fx.TributeCost(m => Fx.TypeIs(m.Card.Data, "Dragon"), "Tribute 1 Dragon-Type monster."),
                Do = Fx.Sync(c =>
                {
                    int atk = c.Value;
                    foreach (DuelMonsterState m in c.AllMonsters.Where(m => m.IsFaceUp && c.Engine.GetDefense(m) <= atk).ToList())
                        c.Engine.Destroy(m.Card, c.Card);
                }),
                Ai = c => 0
            });
            Add("Two-Pronged Attack", new Scripted
            {
                Can = c => c.Me.MonsterCount >= 2 && c.Opp.MonsterCount >= 1,
                Respond = c => Fx.OnOpponentAction(c) && c.Me.MonsterCount >= 2 && c.Opp.MonsterCount >= 1,
                Do = c => Fx.Pick(c, c.Player, c.Me.MonstersOnField.Select(m => m.Card), "Destroy 2 of your monsters.", "tribute", 2, 2, mine =>
                    Fx.Pick(c, c.Player, c.Opp.MonstersOnField.Select(m => m.Card), "Destroy 1 of your opponent's monsters.", "destroy-monster", 1, 1, theirs =>
                    {
                        foreach (DuelCard x in mine.Concat(theirs)) if (x.Zone == DuelZone.Monster) c.Engine.Destroy(x, c.Card);
                        c.Finish();
                    })),
                Ai = c => c.Opp.MonstersOnField.Any(m => c.Engine.GetAttack(m) >= 2500) && c.Me.MonstersOnField.Count(m => c.Engine.GetAttack(m) < 1200) >= 2 ? 70 : 0
            });
        }

        // ----------------------------------------------------------------------------- targeted removal

        private static void RegisterTargetedRemoval()
        {
            Add("Hammer Shot", new Scripted
            {
                Can = c => c.AllMonsters.Any(m => m.IsAttackPosition),
                Do = Fx.Sync(c =>
                {
                    DuelMonsterState top = c.AllMonsters.Where(m => m.IsAttackPosition).OrderByDescending(m => c.Engine.GetAttack(m)).FirstOrDefault();
                    if (top != null) c.Engine.Destroy(top.Card, c.Card);
                }),
                Ai = c =>
                {
                    DuelMonsterState top = c.AllMonsters.Where(m => m.IsAttackPosition).OrderByDescending(m => c.Engine.GetAttack(m)).FirstOrDefault();
                    return top != null && top.Card.Controller != c.Player ? 80 : 0;
                }
            });
            Add("Shield Crush", Fx.DestroyTargetedMonster(m => m.IsDefensePosition, "Destroy 1 Defense Position monster."));
            Add("Soul Taker", new Scripted
            {
                Can = c => c.Opp.MonstersOnField.Any(m => m.IsFaceUp),
                Tgt = c => CardEffects.Request(c.Opp.MonstersOnField.Where(m => m.IsFaceUp).Select(m => m.Card), "Destroy 1 face-up opponent's monster (they gain 1000 LP).", "destroy-monster"),
                Do = Fx.Sync(c =>
                {
                    if (c.Target?.Zone == DuelZone.Monster) c.Engine.Destroy(c.Target, c.Card);
                    c.Engine.GainLife(c.OpponentIndex, 1000, c.Card);
                }),
                Ai = c => c.Opp.MonstersOnField.Any(m => m.IsFaceUp && c.Engine.GetAttack(m) >= 1800) ? 80 : 0
            });
            Add("Tribute to the Doomed", new Scripted
            {
                Can = c => c.Me.Hand.Count >= 2 && c.AllMonsters.Any(),
                Tgt = c => CardEffects.Request(c.AllMonsters.Select(m => m.Card), "Destroy 1 monster on the field.", "destroy-monster"),
                Cost = Fx.DiscardCost(1),
                Do = Fx.Sync(c => { if (c.Target?.Zone == DuelZone.Monster) c.Engine.Destroy(c.Target, c.Card); }),
                Ai = c => c.Opp.MonstersOnField.Any(m => c.Engine.GetAttack(m) >= 1800) ? 80 : 0
            });
            Add("Dark Core", new Scripted
            {
                Can = c => c.Me.Hand.Count >= 2 && c.AllMonsters.Any(m => m.IsFaceUp),
                Tgt = c => CardEffects.Request(c.AllMonsters.Where(m => m.IsFaceUp).Select(m => m.Card), "Banish 1 face-up monster.", "destroy-monster"),
                Cost = Fx.DiscardCost(1),
                Do = Fx.Sync(c => { if (c.Target?.Zone == DuelZone.Monster) c.Engine.Banish(c.Target, c.Card); }),
                Ai = c => c.Opp.MonstersOnField.Any(m => m.IsFaceUp && c.Engine.GetAttack(m) >= 1800) ? 80 : 0
            });
            Add("Offerings to the Doomed", new Scripted
            {
                Can = c => c.AllMonsters.Any(m => m.IsFaceUp),
                Respond = c => Fx.OnOpponentAction(c) && c.Opp.MonstersOnField.Any(m => m.IsFaceUp),
                Tgt = c => CardEffects.Request(c.AllMonsters.Where(m => m.IsFaceUp).Select(m => m.Card), "Destroy 1 face-up monster (you skip your next Draw Phase).", "destroy-monster"),
                Do = Fx.Sync(c => { if (c.Target?.Zone == DuelZone.Monster) c.Engine.Destroy(c.Target, c.Card); c.Me.SkipDraws++; }),
                Ai = c => c.Opp.MonstersOnField.Any(m => m.IsFaceUp && c.Engine.GetAttack(m) >= 2000) ? 75 : 0
            });
            Add("Thousand Knives", new Scripted
            {
                Can = c => Fx.ControlsFaceUp(c, "Dark Magician") && c.Opp.MonsterCount > 0,
                Tgt = c => CardEffects.Request(c.Opp.MonstersOnField.Select(m => m.Card), "Destroy 1 opponent's monster.", "destroy-monster"),
                Do = Fx.Sync(c => { if (c.Target?.Zone == DuelZone.Monster) c.Engine.Destroy(c.Target, c.Card); }),
                Ai = c => 85
            });
            Add("Order to Charge", new Scripted
            {
                Can = c => c.Me.MonstersOnField.Any(m => m.IsFaceUp && Fx.IsNormalMonster(m.Card.Data)) && c.Opp.MonsterCount > 0,
                Respond = c => Fx.OnOpponentAction(c) && c.Me.MonstersOnField.Any(m => m.IsFaceUp && Fx.IsNormalMonster(m.Card.Data)) && c.Opp.MonsterCount > 0,
                Cost = Fx.TributeCost(m => m.IsFaceUp && Fx.IsNormalMonster(m.Card.Data), "Tribute 1 Normal Monster you control."),
                Do = c => Fx.Pick(c, c.Player, c.Opp.MonstersOnField.Select(m => m.Card), "Destroy 1 opponent's monster.", "destroy-monster", 1, 1, picks =>
                {
                    foreach (DuelCard x in picks) if (x.Zone == DuelZone.Monster) c.Engine.Destroy(x, c.Card);
                    c.Finish();
                }),
                Ai = c => c.Opp.MonstersOnField.Any(m => c.Engine.GetAttack(m) >= 2000) ? 70 : 0
            });
            Add("Stamping Destruction", new Scripted
            {
                Can = c => c.Me.MonstersOnField.Any(m => m.IsFaceUp && Fx.TypeIs(m.Card.Data, "Dragon")) && c.AllBackrow.Any(s => s.Card != c.Card),
                Tgt = c => CardEffects.Request(c.AllBackrow.Where(s => s.Card != c.Card).Select(s => s.Card), "Destroy 1 Spell/Trap (its controller takes 500 damage).", "destroy-backrow"),
                Do = Fx.Sync(c =>
                {
                    DuelCard t = c.Target;
                    if (t == null || !t.OnField) return;
                    int controller = t.Controller;
                    c.Engine.Destroy(t, c.Card);
                    c.Engine.DealDamage(controller, 500, c.Card, false);
                }),
                Ai = c => c.Opp.AllBackrow.Any() ? 70 : 0
            });
            Add("Dragon's Gunfire", new Scripted
            {
                Can = c => c.Me.MonstersOnField.Any(m => m.IsFaceUp && Fx.TypeIs(m.Card.Data, "Dragon")),
                Do = c =>
                {
                    List<DuelCard> weak = c.AllMonsters.Where(m => m.IsFaceUp && c.Engine.GetDefense(m) <= 800).Select(m => m.Card).ToList();
                    string[] options = weak.Any(x => x.Controller != c.Player)
                        ? new[] { "Destroy a monster with 800 or less DEF", "Inflict 800 damage" }
                        : new[] { "Inflict 800 damage", "Destroy a monster with 800 or less DEF" };
                    if (weak.Count == 0) options = new[] { "Inflict 800 damage" };
                    Fx.Choose(c, c.Player, "Dragon's Gunfire: choose an effect.", options, (i, label) =>
                    {
                        if (label.StartsWith("Inflict")) { c.Engine.DealDamage(c.OpponentIndex, 800, c.Card, false); c.Finish(); return; }
                        Fx.Pick(c, c.Player, weak, "Destroy 1 monster with 800 or less DEF.", "destroy-monster", 1, 1, picks =>
                        {
                            foreach (DuelCard x in picks) if (x.Zone == DuelZone.Monster) c.Engine.Destroy(x, c.Card);
                            c.Finish();
                        });
                    });
                },
                Ai = c => 65
            });
            Add("Raigeki Break", new Scripted
            {
                Can = c => c.Me.Hand.Count >= 1 && Fx.AllCardsOnField(c).Any(),
                Respond = c => Fx.OnOpponentAction(c) && c.Me.Hand.Count >= 1 && Fx.AllCardsOnField(c).Any(x => x.Controller != c.Player),
                Tgt = c => CardEffects.Request(Fx.AllCardsOnField(c), "Destroy 1 card on the field.", "destroy-backrow"),
                Cost = Fx.DiscardCost(1),
                Do = Fx.Sync(c => { if (c.Target != null && c.Target.OnField) c.Engine.Destroy(c.Target, c.Card); }),
                Ai = c => c.Opp.MonstersOnField.Any(m => c.Engine.GetAttack(m) >= 1800) || c.Opp.AllBackrow.Any() ? 70 : 0
            });
            Add("Phoenix Wing Wind Blast", new Scripted
            {
                Can = c => c.Me.Hand.Count >= 1 && Fx.AllCardsOnField(c).Any(x => x.Controller != c.Player),
                Respond = c => Fx.OnOpponentAction(c) && c.Me.Hand.Count >= 1 && Fx.AllCardsOnField(c).Any(x => x.Controller != c.Player),
                Tgt = c => CardEffects.Request(Fx.AllCardsOnField(c).Where(x => x.Controller != c.Player), "Place 1 card your opponent controls on top of their Deck.", "destroy-backrow"),
                Cost = Fx.DiscardCost(1),
                Do = Fx.Sync(c => { if (c.Target != null && c.Target.OnField) c.Engine.ReturnToDeck(c.Target, shuffle: false); }),
                Ai = c => 70
            });
            Add("Compulsory Evacuation Device", new Scripted
            {
                Can = c => c.AllMonsters.Any(),
                Respond = c => Fx.OnOpponentAction(c) && c.Opp.MonsterCount > 0,
                Tgt = c => CardEffects.Request(c.AllMonsters.Select(m => m.Card), "Return 1 monster on the field to the hand.", "destroy-monster"),
                Do = Fx.Sync(c => { if (c.Target?.Zone == DuelZone.Monster) c.Engine.ReturnToHand(c.Target, c.Card); }),
                Ai = c => c.Opp.MonstersOnField.Any(m => c.Engine.GetAttack(m) >= 2000 || m.Card.IsExtraDeckCard) ? 75 : 0
            });
            Add("Acid Trap Hole", new Scripted
            {
                Can = c => c.AllMonsters.Any(m => m.IsFaceDown),
                Respond = c => Fx.OnOpponentAction(c) && c.Opp.MonstersOnField.Any(m => m.IsFaceDown),
                Tgt = c => CardEffects.Request(c.AllMonsters.Where(m => m.IsFaceDown).Select(m => m.Card), "Target 1 face-down Defense Position monster.", "destroy-monster"),
                Do = Fx.Sync(c =>
                {
                    DuelMonsterState m = c.Engine.FindMonster(c.Target);
                    if (m == null) return;
                    c.Engine.ForcePosition(m, DuelMonsterPosition.FaceUpDefense);
                    if (c.Engine.GetDefense(m) <= 2000) c.Engine.Destroy(m.Card, c.Card);
                    else c.Engine.ForcePosition(m, DuelMonsterPosition.FaceDownDefense);
                }),
                Ai = c => c.Opp.MonstersOnField.Any(m => m.IsFaceDown) ? 65 : 0
            });
        }

        // ----------------------------------------------------------------------------- battle positions

        private static void RegisterPositions()
        {
            Add("Block Attack", Fx.ChangePositionTargeted(m => m.Card.Controller != -1 && m.IsAttackPosition, opponentOnly: true, DuelMonsterPosition.FaceUpDefense,
                "Change 1 face-up Attack Position opponent's monster to Defense Position.", ai: 45));
            Add("Stop Defense", Fx.ChangePositionTargeted(m => m.IsDefensePosition, opponentOnly: true, DuelMonsterPosition.FaceUpAttack,
                "Change 1 opponent's Defense Position monster to Attack Position.", ai: 40));
            Add("Book of Taiyou", Fx.ChangePositionTargeted(m => m.IsFaceDown, opponentOnly: false, DuelMonsterPosition.FaceUpAttack,
                "Flip 1 face-down monster into face-up Attack Position.", ai: 20));
            Add("Ready for Intercepting", Fx.Freeable(Fx.ChangePositionTargeted(m => m.IsFaceUp && (Fx.TypeIs(m.Card.Data, "Warrior") || Fx.TypeIs(m.Card.Data, "Spellcaster")),
                opponentOnly: false, DuelMonsterPosition.FaceDownDefense, "Change 1 Warrior or Spellcaster to face-down Defense Position.", ai: 50)));
            Add("Darkness Approaches", new Scripted
            {
                Can = c => c.Me.Hand.Count >= 3 && c.AllMonsters.Any(m => m.IsFaceUp),
                Tgt = c => CardEffects.Request(c.AllMonsters.Where(m => m.IsFaceUp).Select(m => m.Card), "Change 1 face-up monster to face-down Defense Position.", "position"),
                Cost = Fx.DiscardCost(2),
                Do = Fx.Sync(c => { DuelMonsterState m = c.Engine.FindMonster(c.Target); if (m != null) c.Engine.ForcePosition(m, DuelMonsterPosition.FaceDownDefense); }),
                Ai = c => 0
            });
            Add("Zero Gravity", Fx.Freeable(new Scripted
            {
                Can = c => c.AllMonsters.Any(m => m.IsFaceUp),
                Do = Fx.Sync(c => { foreach (DuelMonsterState m in c.AllMonsters.Where(m => m.IsFaceUp).ToList()) Fx.Flip(c.Engine, m); }),
                Ai = c => c.Opp.MonstersOnField.Count(m => m.IsAttackPosition) > c.Me.MonstersOnField.Count(m => m.IsAttackPosition) ? 55 : 0
            }));
            Add("Windstorm of Etaqua", Fx.Freeable(new Scripted
            {
                Can = c => c.Opp.MonstersOnField.Any(m => m.IsFaceUp),
                Do = Fx.Sync(c => { foreach (DuelMonsterState m in c.Opp.MonstersOnField.Where(m => m.IsFaceUp).ToList()) Fx.Flip(c.Engine, m); }),
                Ai = c => c.Engine.TurnPlayer != c.Player && c.Opp.MonstersOnField.Count(m => m.IsAttackPosition) >= 1 ? 60 : 0
            }));
            Add("Desert Sunlight", Fx.Freeable(new Scripted
            {
                Can = c => c.Me.MonsterCount > 0,
                Do = Fx.Sync(c => { foreach (DuelMonsterState m in c.Me.MonstersOnField.ToList()) c.Engine.ForcePosition(m, DuelMonsterPosition.FaceUpDefense); }),
                Ai = c => 0
            }));
            Add("Kunai with Chain", new Scripted
            {
                Can = c => false,
                Respond = c => CardEffects.IsOpponentTurnTrigger(c, DuelTriggerKind.AttackDeclared) && c.Trigger.Attacker != null,
                Do = Fx.Sync(c => { DuelMonsterState a = c.Trigger?.Attacker; if (a != null && a.Card.Zone == DuelZone.Monster) c.Engine.ForcePosition(a, DuelMonsterPosition.FaceUpDefense); }),
                Ai = c => c.Trigger?.Attacker != null && (c.Trigger.Defender == null || c.Engine.GetAttack(c.Trigger.Attacker) > c.Engine.GetAttack(c.Trigger.Defender)) ? 70 : 0
            });
            Add("The Spell Absorbing Life", Fx.Freeable(new Scripted
            {
                Can = c => c.AllMonsters.Any(),
                Do = Fx.Sync(c =>
                {
                    foreach (DuelMonsterState m in c.AllMonsters.Where(m => m.IsFaceDown).ToList()) c.Engine.ForcePosition(m, DuelMonsterPosition.FaceUpDefense);
                    int effects = c.AllMonsters.Count(m => Fx.IsEffectMonster(m.Card.Data));
                    c.Engine.GainLife(c.Player, 400 * effects, c.Card);
                }),
                Ai = c => c.Opp.MonstersOnField.Any(m => m.IsFaceDown) ? 45 : 0
            }));
            Add("Level Limit - Area B", new Scripted
            {
                Stays = true,
                Do = Fx.Sync(c => { foreach (DuelMonsterState m in c.AllMonsters.Where(m => m.IsFaceUp && m.Level >= 4).ToList()) c.Engine.ForcePosition(m, DuelMonsterPosition.FaceUpDefense); }),
                NoAttack = (e, s, m) => m.IsFaceUp && m.Level >= 4,
                Ai = c => c.Opp.MonstersOnField.Count(m => m.Level >= 4) > c.Me.MonstersOnField.Count(m => m.Level >= 4) ? 55 : 0
            });
            Add("Dragon Capture Jar", new Scripted
            {
                Stays = true,
                Respond = Fx.OnOpponentAction,
                Do = Fx.Sync(c => { foreach (DuelMonsterState m in c.AllMonsters.Where(m => m.IsFaceUp && Fx.TypeIs(m.Card.Data, "Dragon")).ToList()) c.Engine.ForcePosition(m, DuelMonsterPosition.FaceUpDefense); }),
                NoAttack = (e, s, m) => m.IsFaceUp && Fx.TypeIs(m.Card.Data, "Dragon"),
                Ai = c => c.Opp.MonstersOnField.Any(m => Fx.TypeIs(m.Card.Data, "Dragon")) ? 65 : 0
            });
            Add("Final Attack Orders", new Scripted
            {
                Stays = true,
                Respond = Fx.OnOpponentAction,
                Do = Fx.Sync(c => { foreach (DuelMonsterState m in c.AllMonsters.Where(m => m.IsFaceUp).ToList()) c.Engine.ForcePosition(m, DuelMonsterPosition.FaceUpAttack); }),
                Ai = c => 0
            });
        }

        // ----------------------------------------------------------------------------- recovery and search

        private static void RegisterRecoveryAndSearch()
        {
            Add("Monster Reborn", new Scripted
            {
                Can = c => c.Me.HasFreeMonsterZone && Fx.RevivableInAnyGY(c).Any(),
                Tgt = c => CardEffects.Request(Fx.RevivableInAnyGY(c), "Special Summon 1 monster from either Graveyard.", "revive"),
                Do = Fx.Sync(c => { if (c.Target?.Zone == DuelZone.Graveyard) c.Engine.SpecialSummon(c.Player, c.Target, DuelMonsterPosition.FaceUpAttack, c.Card); }),
                Ai = c => Fx.RevivableInAnyGY(c).Any(x => x.Data.attack >= 1600) ? 85 : 0
            });
            Add("Silent Doom", Fx.ReviveFromOwnGY(x => Fx.IsNormalMonster(x.Data), DuelMonsterPosition.FaceUpDefense, "Special Summon 1 Normal Monster from your Graveyard in Defense Position."));
            Add("Rite of Spirit", Fx.Freeable(Fx.ReviveFromOwnGY(x => x.Name.StartsWith("Gravekeeper's"), DuelMonsterPosition.FaceUpAttack, "Special Summon 1 \"Gravekeeper's\" monster from your Graveyard.")));
            Add("Book of Life", new Scripted
            {
                Can = c => c.Me.HasFreeMonsterZone && c.Me.Graveyard.Any(x => x.IsMonster && Fx.TypeIs(x.Data, "Zombie")) && c.Opp.Graveyard.Any(x => x.IsMonster),
                Tgt = c => CardEffects.Request(c.Me.Graveyard.Where(x => x.IsMonster && Fx.TypeIs(x.Data, "Zombie") && !x.IsExtraDeckCard), "Special Summon 1 Zombie from your Graveyard.", "revive"),
                Do = c => Fx.Pick(c, c.Player, c.Opp.Graveyard.Where(x => x.IsMonster), "Banish 1 monster from your opponent's Graveyard.", "revive", 1, 1, picks =>
                {
                    if (c.Target?.Zone == DuelZone.Graveyard) c.Engine.SpecialSummon(c.Player, c.Target, DuelMonsterPosition.FaceUpAttack, c.Card);
                    foreach (DuelCard x in picks) c.Engine.Banish(x, c.Card);
                    c.Finish();
                }),
                Ai = c => 70
            });
            Add("The Shallow Grave", new Scripted
            {
                Can = c => c.Me.HasFreeMonsterZone && c.Me.Graveyard.Any(x => x.IsMonster && !x.IsExtraDeckCard),
                Do = c => Fx.Pick(c, c.Player, c.Me.Graveyard.Where(x => x.IsMonster && !x.IsExtraDeckCard), "Set 1 monster from your Graveyard.", "revive", 1, 1, mine =>
                {
                    foreach (DuelCard x in mine) c.Engine.SpecialSummon(c.Player, x, DuelMonsterPosition.FaceDownDefense, c.Card);
                    Fx.Pick(c, c.OpponentIndex, c.Opp.HasFreeMonsterZone ? c.Opp.Graveyard.Where(x => x.IsMonster && !x.IsExtraDeckCard) : Enumerable.Empty<DuelCard>(),
                        "Set 1 monster from your Graveyard.", "revive", 1, 1, theirs =>
                        {
                            foreach (DuelCard x in theirs) c.Engine.SpecialSummon(c.OpponentIndex, x, DuelMonsterPosition.FaceDownDefense, c.Card);
                            c.Finish();
                        });
                }),
                Ai = c => c.Me.Graveyard.Where(x => x.IsMonster).Select(x => x.Data.attack).DefaultIfEmpty(0).Max() >
                          c.Opp.Graveyard.Where(x => x.IsMonster).Select(x => x.Data.attack).DefaultIfEmpty(0).Max() + 500 ? 50 : 0
            });
            Add("Dark Factory of Mass Production", Fx.AddFromGraveyard(x => Fx.IsNormalMonster(x.Data), 2, 2, "Add 2 Normal Monsters from your Graveyard to your hand."));
            Add("The Warrior Returning Alive", Fx.AddFromGraveyard(x => x.IsMonster && Fx.TypeIs(x.Data, "Warrior"), 1, 1, "Add 1 Warrior from your Graveyard to your hand."));
            Add("Fairy of the Spring", Fx.AddFromGraveyard(x => DuelRules.IsEquip(x.Data), 1, 1, "Add 1 Equip Spell from your Graveyard to your hand."));
            Add("Monster Reincarnation", new Scripted
            {
                Can = c => c.Me.Hand.Count >= 2 && c.Me.Graveyard.Any(x => x.IsMonster),
                Tgt = c => CardEffects.Request(c.Me.Graveyard.Where(x => x.IsMonster), "Add 1 monster from your Graveyard to your hand.", "revive"),
                Cost = Fx.DiscardCost(1),
                Do = Fx.Sync(c => { if (c.Target?.Zone == DuelZone.Graveyard) c.Engine.AddToHand(c.Target, c.Card); }),
                Ai = c => c.Me.Graveyard.Any(x => x.IsMonster && x.Data.attack >= 2000) ? 55 : 0
            });
            Add("Spell Reproduction", new Scripted
            {
                Can = c => c.Me.Hand.Count(x => x.IsSpell && x != c.Card) >= 2 && c.Me.Graveyard.Any(x => x.IsSpell),
                Tgt = c => CardEffects.Request(c.Me.Graveyard.Where(x => x.IsSpell), "Add 1 Spell from your Graveyard to your hand.", "revive"),
                Cost = Fx.DiscardCost(2, x => x.IsSpell),
                Do = Fx.Sync(c => { if (c.Target?.Zone == DuelZone.Graveyard) c.Engine.AddToHand(c.Target, c.Card); }),
                Ai = c => c.Me.Graveyard.Any(x => x.Name is "Pot of Greed" or "Dark Hole" or "Monster Reborn" or "Heavy Storm") ? 50 : 0
            });
            Add("Backup Soldier", Fx.Freeable(new Scripted
            {
                Can = c => c.Me.Graveyard.Count(x => x.IsMonster) >= 5 && Pool(c).Any(),
                Do = c => Fx.Pick(c, c.Player, Pool(c), "Add up to 3 non-Effect Monsters with 1500 or less ATK to your hand.", "revive", 1, 3, picks =>
                {
                    foreach (DuelCard x in picks) c.Engine.AddToHand(x, c.Card);
                    c.Finish();
                }),
                Ai = c => 60
            }));
            static IEnumerable<DuelCard> Pool(EffectContext c) => c.Me.Graveyard.Where(x => x.IsMonster && !Fx.IsEffectMonster(x.Data) && x.Data.attack <= 1500);

            Add("Reinforcement of the Army", Fx.Search(x => x.IsMonster && Fx.TypeIs(x.Data, "Warrior") && x.Data.level <= 4, "Add 1 Level 4 or lower Warrior from your Deck to your hand."));
            Add("Fossil Dig", Fx.Search(x => x.IsMonster && Fx.TypeIs(x.Data, "Dinosaur") && x.Data.level <= 6, "Add 1 Level 6 or lower Dinosaur from your Deck to your hand."));
            Add("Toon Table of Contents", Fx.Search(x => x.Name.StartsWith("Toon"), "Add 1 \"Toon\" card from your Deck to your hand."));
            Add("Emblem of Dragon Destroyer", new Scripted
            {
                Can = c => BusterBladers(c).Any(),
                Do = c => Fx.Pick(c, c.Player, BusterBladers(c), "Add 1 \"Buster Blader\" from your Deck or Graveyard to your hand.", "revive", 1, 1, picks =>
                {
                    foreach (DuelCard x in picks) c.Engine.AddToHand(x, c.Card);
                    c.Engine.ShuffleDeck(c.Player);
                    c.Finish();
                }),
                Ai = c => 70
            });
            static IEnumerable<DuelCard> BusterBladers(EffectContext c) => c.Me.Deck.Concat(c.Me.Graveyard).Where(x => x.Name == "Buster Blader");

            Add("Foolish Burial", new Scripted
            {
                Can = c => c.Me.Deck.Any(x => x.IsMonster),
                Do = c => Fx.Pick(c, c.Player, c.Me.Deck.Where(x => x.IsMonster), "Send 1 monster from your Deck to the Graveyard.", "revive", 1, 1, picks =>
                {
                    foreach (DuelCard x in picks) c.Engine.SendToGraveyard(x, false, c.Card);
                    c.Engine.ShuffleDeck(c.Player);
                    c.Finish();
                }),
                Ai = c => c.Me.Hand.Any(x => x.Name is "Monster Reborn" or "Premature Burial" or "Call of the Haunted") ? 65 : 0
            });
            Add("The Cheerful Coffin", new Scripted
            {
                Can = c => c.Me.Hand.Any(x => x.IsMonster),
                Do = c => Fx.Pick(c, c.Player, c.Me.Hand.Where(x => x.IsMonster), "Discard up to 3 monsters.", "discard", 1, 3, picks =>
                {
                    foreach (DuelCard x in picks) c.Engine.Discard(x);
                    c.Finish();
                }),
                Ai = c => 0
            });
            Add("Soul Release", new Scripted
            {
                Can = c => c.Engine.Duelists.Any(d => d.Graveyard.Any()),
                Do = c => Fx.Pick(c, c.Player, c.Opp.Graveyard.Concat(c.Me.Graveyard), "Banish up to 5 cards from any Graveyard.", "destroy-monster", 1, 5, picks =>
                {
                    foreach (DuelCard x in picks) c.Engine.Banish(x, c.Card);
                    c.Finish();
                }),
                Ai = c => c.Opp.Graveyard.Count(x => x.IsMonster && x.Data.attack >= 1800) >= 1 ? 40 : 0
            });
            Add("Gravedigger Ghoul", new Scripted
            {
                Can = c => c.Opp.Graveyard.Any(x => x.IsMonster),
                Do = c => Fx.Pick(c, c.Player, c.Opp.Graveyard.Where(x => x.IsMonster), "Banish up to 2 monsters from your opponent's Graveyard.", "destroy-monster", 1, 2, picks =>
                {
                    foreach (DuelCard x in picks) c.Engine.Banish(x, c.Card);
                    c.Finish();
                }),
                Ai = c => c.Opp.Graveyard.Any(x => x.IsMonster && x.Data.attack >= 2000) ? 40 : 0
            });
        }

        // ----------------------------------------------------------------------------- special summons

        private static void RegisterSummoning()
        {
            Add("Sage's Stone", Fx.SummonNamed(c => Fx.ControlsFaceUp(c, "Dark Magician Girl"), "Dark Magician", fromHand: true, fromDeck: true, fromGrave: false));
            Add("Knight's Title", Fx.TributeInto("Dark Magician", "Dark Magician Knight", quick: false));
            Add("Dedication through Light and Darkness", Fx.TributeInto("Dark Magician", "Dark Magician of Chaos", quick: true));
            Add("Elegant Egotist", new Scripted
            {
                Can = c => c.Me.HasFreeMonsterZone && c.AllMonsters.Any(m => m.IsFaceUp && m.Name == "Harpie Lady") && Pool(c).Any(),
                Do = c => Fx.Pick(c, c.Player, Pool(c), "Special Summon 1 \"Harpie Lady\" or \"Harpie Lady Sisters\".", "revive", 1, 1, picks =>
                {
                    foreach (DuelCard x in picks) c.Engine.SpecialSummon(c.Player, x, DuelMonsterPosition.FaceUpAttack, c.Card);
                    c.Engine.ShuffleDeck(c.Player);
                    c.Finish();
                }),
                Ai = c => 75
            });
            static IEnumerable<DuelCard> Pool(EffectContext c) => c.Me.Hand.Concat(c.Me.Deck).Where(x => x.Name is "Harpie Lady" or "Harpie Lady Sisters");

            Add("Dark Magic Curtain", new Scripted
            {
                Can = c => c.Me.HasFreeMonsterZone && c.Me.LifePoints >= 2 && c.Me.Deck.Any(x => x.Name == "Dark Magician"),
                Cost = (c, paid) => { c.Engine.PayLife(c.Player, c.Me.LifePoints / 2, c.Card); paid(); },
                Do = Fx.Sync(c =>
                {
                    DuelCard dm = c.Me.Deck.FirstOrDefault(x => x.Name == "Dark Magician");
                    if (dm != null) c.Engine.SpecialSummon(c.Player, dm, DuelMonsterPosition.FaceUpAttack, c.Card);
                    c.Engine.ShuffleDeck(c.Player);
                }),
                Ai = c => c.Me.LifePoints >= 4000 ? 60 : 0
            });
            Add("Tribute Doll", new Scripted
            {
                Can = c => c.Me.MonsterCount > 0 && c.Me.Hand.Any(x => x.IsMonster && x.Data.level == 7 && DuelRules.CanEverBeNormalSummoned(x.Data)),
                Cost = Fx.TributeCost(m => true, "Tribute 1 monster."),
                Do = c => Fx.Pick(c, c.Player, c.Me.Hand.Where(x => x.IsMonster && x.Data.level == 7 && DuelRules.CanEverBeNormalSummoned(x.Data)),
                    "Special Summon 1 Level 7 monster from your hand.", "revive", 1, 1, picks =>
                    {
                        foreach (DuelCard x in picks)
                        {
                            DuelMonsterState m = c.Engine.SpecialSummon(c.Player, x, DuelMonsterPosition.FaceUpAttack, c.Card);
                            if (m != null) m.CannotAttackThisTurn = true;
                        }
                        c.Finish();
                    }),
                Ai = c => 60
            });
            Add("Ultra Evolution Pill", new Scripted
            {
                Can = c => c.Me.MonstersOnField.Any(m => Fx.TypeIs(m.Card.Data, "Reptile")) && c.Me.Hand.Any(x => x.IsMonster && Fx.TypeIs(x.Data, "Dinosaur")),
                Cost = Fx.TributeCost(m => Fx.TypeIs(m.Card.Data, "Reptile"), "Tribute 1 Reptile monster."),
                Do = c => Fx.Pick(c, c.Player, c.Me.Hand.Where(x => x.IsMonster && Fx.TypeIs(x.Data, "Dinosaur")), "Special Summon 1 Dinosaur from your hand.", "revive", 1, 1, picks =>
                {
                    foreach (DuelCard x in picks) c.Engine.SpecialSummon(c.Player, x, DuelMonsterPosition.FaceUpAttack, c.Card);
                    c.Finish();
                }),
                Ai = c => 70
            });
            Add("Insect Imitation", new Scripted
            {
                Can = c => c.Me.MonstersOnField.Any(m => Upgrades(c, m.Level).Any()),
                Cost = (c, paid) => Fx.TributeCost(m => Upgrades(c, m.Level).Any(), "Tribute 1 monster (an Insect 1 Level higher is in your Deck).")(c, paid),
                Do = c =>
                {
                    int level = c.Paid != null ? c.Paid.Data.level : 0;
                    Fx.Pick(c, c.Player, Upgrades(c, level), $"Special Summon 1 Level {level + 1} Insect from your Deck.", "revive", 1, 1, picks =>
                    {
                        foreach (DuelCard x in picks) c.Engine.SpecialSummon(c.Player, x, DuelMonsterPosition.FaceUpAttack, c.Card);
                        c.Engine.ShuffleDeck(c.Player);
                        c.Finish();
                    });
                },
                Ai = c => 50
            });
            static IEnumerable<DuelCard> Upgrades(EffectContext c, int level) =>
                c.Me.Deck.Where(x => x.IsMonster && Fx.TypeIs(x.Data, "Insect") && x.Data.level == level + 1 && !x.IsExtraDeckCard);

            Add("Monster Gate", new Scripted
            {
                Can = c => c.Me.MonsterCount > 0 && c.Me.Deck.Any(x => x.IsMonster && DuelRules.CanEverBeNormalSummoned(x.Data)),
                Cost = Fx.TributeCost(m => true, "Tribute 1 monster."),
                Do = Fx.Sync(c =>
                {
                    while (c.Me.Deck.Count > 0)
                    {
                        DuelCard top = c.Me.Deck[c.Me.Deck.Count - 1];
                        if (top.IsMonster && DuelRules.CanEverBeNormalSummoned(top.Data))
                        {
                            c.Engine.SpecialSummon(c.Player, top, DuelMonsterPosition.FaceUpAttack, c.Card);
                            break;
                        }
                        c.Engine.SendToGraveyard(top, false, c.Card);
                    }
                }),
                Ai = c => c.Me.MonstersOnField.Any(m => c.Engine.GetAttack(m) < 1000) ? 45 : 0
            });
            Add("Archfiend's Roar", Fx.Freeable(new Scripted
            {
                Can = c => c.Me.HasFreeMonsterZone && c.Me.LifePoints > 500 && Pool3(c).Any(),
                Tgt = c => CardEffects.Request(Pool3(c), "Special Summon 1 \"Archfiend\" monster from your Graveyard.", "revive"),
                Cost = Fx.PayCost(500),
                Do = Fx.Sync(c =>
                {
                    if (c.Target?.Zone != DuelZone.Graveyard) return;
                    DuelMonsterState m = c.Engine.SpecialSummon(c.Player, c.Target, DuelMonsterPosition.FaceUpAttack, c.Card);
                    if (m != null) c.Engine.AtEndPhase(() => { if (m.Card.Zone == DuelZone.Monster) c.Engine.Destroy(m.Card, c.Card); });
                }),
                Ai = c => 55
            }));
            static IEnumerable<DuelCard> Pool3(EffectContext c) => c.Me.Graveyard.Where(x => x.IsMonster && (x.Name.Contains("Archfiend") || x.Name.Contains("Terrorking")) && !x.IsExtraDeckCard);

            Add("Return from the Different Dimension", Fx.Freeable(new Scripted
            {
                Can = c => c.Me.HasFreeMonsterZone && c.Me.Banished.Any(x => x.IsMonster && !x.IsExtraDeckCard) && c.Me.LifePoints >= 2,
                Cost = (c, paid) => { c.Engine.PayLife(c.Player, c.Me.LifePoints / 2, c.Card); paid(); },
                Do = Fx.Sync(c =>
                {
                    var summoned = new List<DuelMonsterState>();
                    foreach (DuelCard x in c.Me.Banished.Where(x => x.IsMonster && !x.IsExtraDeckCard).OrderByDescending(x => x.Data.attack).ToList())
                    {
                        if (!c.Me.HasFreeMonsterZone) break;
                        DuelMonsterState m = c.Engine.SpecialSummon(c.Player, x, DuelMonsterPosition.FaceUpAttack, c.Card);
                        if (m != null) summoned.Add(m);
                    }
                    c.Engine.AtEndPhase(() => { foreach (DuelMonsterState m in summoned) if (m.Card.Zone == DuelZone.Monster) c.Engine.Banish(m.Card, c.Card); });
                }),
                Ai = c => c.Me.Banished.Count(x => x.IsMonster) >= 2 && c.Me.LifePoints >= 3000 ? 60 : 0
            }));
            Add("Hidden Soldiers", new Scripted
            {
                Can = c => false,
                Respond = c => c.Trigger != null && c.Trigger.Player != c.Player && c.Me.HasFreeMonsterZone &&
                               (c.Trigger.Kind == DuelTriggerKind.NormalSummoned || c.Trigger.Kind == DuelTriggerKind.FlipSummoned) &&
                               c.Me.Hand.Any(x => x.IsMonster && x.Data.level <= 4 && Fx.AttrIs(x.Data, "DARK")),
                Do = c => Fx.Pick(c, c.Player, c.Me.Hand.Where(x => x.IsMonster && x.Data.level <= 4 && Fx.AttrIs(x.Data, "DARK")),
                    "Special Summon 1 Level 4 or lower DARK monster from your hand.", "revive", 1, 1, picks =>
                    {
                        foreach (DuelCard x in picks) c.Engine.SpecialSummon(c.Player, x, DuelMonsterPosition.FaceUpAttack, c.Card);
                        c.Finish();
                    }),
                Ai = c => 70
            });
            Add("Interdimensional Matter Transporter", new Scripted
            {
                Can = c => c.Me.MonstersOnField.Any(m => m.IsFaceUp),
                Respond = c => Fx.OnOpponentAction(c) && c.Me.MonstersOnField.Any(m => m.IsFaceUp),
                Tgt = c => CardEffects.Request(c.Me.MonstersOnField.Where(m => m.IsFaceUp).Select(m => m.Card), "Banish 1 face-up monster you control until the End Phase.", "boost"),
                Do = Fx.Sync(c =>
                {
                    DuelCard t = c.Target;
                    if (t?.Zone != DuelZone.Monster) return;
                    int owner = t.Owner;
                    c.Engine.Banish(t, c.Card);
                    c.Engine.AtEndPhase(() =>
                    {
                        if (t.Zone == DuelZone.Banished && c.Engine.Me(owner).HasFreeMonsterZone)
                            c.Engine.SpecialSummon(owner, t, DuelMonsterPosition.FaceUpAttack, c.Card);
                    });
                }),
                Ai = c => c.Trigger?.Kind == DuelTriggerKind.SpellActivated || c.Trigger?.Kind == DuelTriggerKind.TrapActivated ? 55 : 0
            });
        }

        // ----------------------------------------------------------------------------- hand / deck disruption

        private static void RegisterHandAndDeck()
        {
            Add("Delinquent Duo", new Scripted
            {
                Can = c => c.Me.LifePoints > 1000 && c.Opp.Hand.Count > 0,
                Cost = Fx.PayCost(1000),
                Do = Fx.Sync(c =>
                {
                    for (int i = 0; i < 2 && c.Opp.Hand.Count > 0; i++)
                        c.Engine.Discard(c.Opp.Hand[c.Engine.RandomRange(0, c.Opp.Hand.Count)]);
                }),
                Ai = c => c.Opp.Hand.Count >= 2 && c.Me.LifePoints >= 3000 ? 65 : 0
            });
            Add("Disturbance Strategy", Fx.Freeable(new Scripted
            {
                Can = c => c.Opp.Hand.Count > 0,
                Do = Fx.Sync(c =>
                {
                    int n = c.Opp.Hand.Count;
                    foreach (DuelCard x in c.Opp.Hand.ToList()) c.Engine.ReturnToDeck(x, shuffle: false);
                    c.Engine.ShuffleDeck(c.OpponentIndex);
                    c.Engine.Draw(c.OpponentIndex, n);
                }),
                Ai = c => 0
            }));
            Add("Heavy Slump", Fx.Freeable(new Scripted
            {
                Can = c => c.Opp.Hand.Count >= 8,
                Do = Fx.Sync(c =>
                {
                    foreach (DuelCard x in c.Opp.Hand.ToList()) c.Engine.ReturnToDeck(x, shuffle: false);
                    c.Engine.ShuffleDeck(c.OpponentIndex);
                    c.Engine.Draw(c.OpponentIndex, 2);
                }),
                Ai = c => 90
            }));
            Add("Trap Dustshoot", Fx.Freeable(new Scripted
            {
                Can = c => c.Opp.Hand.Count >= 4,
                Do = c => Fx.Pick(c, c.Player, c.Opp.Hand.Where(x => x.IsMonster), "Your opponent's hand: return 1 monster to their Deck.", "revive", 1, 1, picks =>
                {
                    foreach (DuelCard x in picks) c.Engine.ReturnToDeck(x, shuffle: true);
                    c.Finish();
                }),
                Ai = c => 60
            }));
            Add("Time Seal", Fx.Freeable(new Scripted
            {
                Do = Fx.Sync(c => c.Opp.SkipDraws++),
                Ai = c => 45
            }));
            Add("Assault on GHQ", Fx.Freeable(new Scripted
            {
                Can = c => c.Me.MonsterCount > 0,
                Tgt = c => CardEffects.Request(c.Me.MonstersOnField.Select(m => m.Card), "Destroy 1 monster you control.", "tribute"),
                Do = Fx.Sync(c =>
                {
                    if (c.Target?.Zone == DuelZone.Monster) c.Engine.Destroy(c.Target, c.Card);
                    for (int i = 0; i < 2 && c.Opp.Deck.Count > 0; i++) c.Engine.SendToGraveyard(c.Opp.Deck[c.Opp.Deck.Count - 1], false, c.Card);
                }),
                Ai = c => 0
            }));
            Add("Altar for Tribute", Fx.Freeable(new Scripted
            {
                Can = c => c.Me.MonsterCount > 0,
                Tgt = c => CardEffects.Request(c.Me.MonstersOnField.Select(m => m.Card), "Send 1 monster you control to the Graveyard (gain LP equal to its original ATK).", "tribute"),
                Do = Fx.Sync(c =>
                {
                    if (c.Target?.Zone != DuelZone.Monster) return;
                    int atk = c.Target.Data.attack;
                    c.Engine.SendToGraveyard(c.Target, false, c.Card);
                    c.Engine.GainLife(c.Player, atk, c.Card);
                }),
                Ai = c => 0
            }));
            Add("Fiend Comedian", Fx.Freeable(new Scripted
            {
                Can = c => c.Opp.Graveyard.Count > 0,
                Do = Fx.Sync(c =>
                {
                    bool right = c.Engine.RandomRange(0, 2) == 0;
                    c.Engine.Raise(DuelEventType.EffectResolved, c.Player, c.Card, text: right ? "Fiend Comedian: called it right!" : "Fiend Comedian: called it wrong.");
                    if (right) foreach (DuelCard x in c.Opp.Graveyard.ToList()) c.Engine.Banish(x, c.Card);
                    else for (int i = 0, n = c.Opp.Graveyard.Count; i < n && c.Me.Deck.Count > 0; i++) c.Engine.SendToGraveyard(c.Me.Deck[c.Me.Deck.Count - 1], false, c.Card);
                }),
                Ai = c => 0
            }));
        }

        // ----------------------------------------------------------------------------- temporary stat changes

        private static void RegisterTempStats()
        {
            Add("Castle Walls", Fx.Freeable(Fx.TempStatTargeted(0, 500, mineOnly: false, "Increase 1 face-up monster's DEF by 500 until the end of this turn.")));
            Add("The Reliable Guardian", Fx.TempStatTargeted(0, 700, mineOnly: false, "Increase 1 face-up monster's DEF by 700 until the end of this turn."));
            Add("Snake Fang", Fx.Freeable(Fx.TempStatTargeted(0, -500, mineOnly: false, "Decrease 1 face-up monster's DEF by 500 until the end of this turn.")));
            Add("Micro Ray", Fx.Freeable(new Scripted
            {
                Can = c => c.AllMonsters.Any(m => m.IsFaceUp),
                Tgt = c => CardEffects.Request(c.AllMonsters.Where(m => m.IsFaceUp).Select(m => m.Card), "1 face-up monster's DEF becomes 0 until the end of this turn.", "weaken"),
                Do = Fx.Sync(c => { DuelMonsterState m = c.Engine.FindMonster(c.Target); if (m != null) m.TempDefense -= c.Engine.GetDefense(m); }),
                Ai = c => 30
            }));
            Add("Mask of Weakness", new Scripted
            {
                Can = c => false,
                Respond = c => CardEffects.IsOpponentTurnTrigger(c, DuelTriggerKind.AttackDeclared) && c.Trigger.Attacker != null,
                Do = Fx.Sync(c => { DuelMonsterState a = c.Trigger?.Attacker; if (a != null) a.TempAttack -= 700; }),
                Ai = c => Fx.AttackWouldBeTurned(c, 700) ? 80 : 0
            });

            Add("Curse of Aging", Fx.Freeable(new Scripted
            {
                Can = c => c.Me.Hand.Count >= 1 && c.Opp.MonsterCount > 0,
                Cost = Fx.DiscardCost(1),
                Do = Fx.Sync(c => { foreach (DuelMonsterState m in c.Opp.MonstersOnField) { m.TempAttack -= 500; m.TempDefense -= 500; } }),
                Ai = c => c.Engine.TurnPlayer != c.Player && c.Trigger?.Kind == DuelTriggerKind.AttackDeclared ? 55 : 0
            }));
            Add("Skull Dice", Fx.Freeable(new Scripted
            {
                Can = c => c.Opp.MonsterCount > 0,
                Do = Fx.Sync(c =>
                {
                    int roll = Fx.Die(c);
                    foreach (DuelMonsterState m in c.Opp.MonstersOnField) { m.TempAttack -= roll * 100; m.TempDefense -= roll * 100; }
                }),
                Ai = c => c.Trigger?.Kind == DuelTriggerKind.AttackDeclared ? 55 : 0
            }));
            Add("Graceful Dice", new Scripted
            {
                Can = c => c.Me.MonsterCount > 0,
                Respond = c => Fx.OnOpponentAction(c) && c.Me.MonsterCount > 0,
                Do = Fx.Sync(c =>
                {
                    int roll = Fx.Die(c);
                    foreach (DuelMonsterState m in c.Me.MonstersOnField) { m.TempAttack += roll * 100; m.TempDefense += roll * 100; }
                }),
                Ai = c => c.Engine.Phase == DuelPhase.Battle ? 45 : 0
            });
            Add("Pyramid Energy", new Scripted
            {
                Can = c => c.Me.MonstersOnField.Any(m => m.IsFaceUp),
                Respond = c => Fx.OnOpponentAction(c) && c.Me.MonstersOnField.Any(m => m.IsFaceUp),
                Do = c => Fx.Choose(c, c.Player, "Pyramid Energy: choose an effect.", new[] { "+200 ATK to your monsters", "+500 DEF to your monsters" }, (i, label) =>
                {
                    foreach (DuelMonsterState m in c.Me.MonstersOnField.Where(m => m.IsFaceUp))
                        if (i == 0) m.TempAttack += 200; else m.TempDefense += 500;
                    c.Finish();
                }),
                Ai = c => c.Engine.Phase == DuelPhase.Battle ? 40 : 0
            });
            Add("The Big March of Animals", new Scripted
            {
                Can = c => c.Me.MonstersOnField.Any(m => m.IsFaceUp && Fx.TypeIs(m.Card.Data, "Beast")),
                Respond = c => Fx.OnOpponentAction(c) && c.Me.MonstersOnField.Any(m => m.IsFaceUp && Fx.TypeIs(m.Card.Data, "Beast")),
                Do = Fx.Sync(c =>
                {
                    List<DuelMonsterState> beasts = c.Me.MonstersOnField.Where(m => m.IsFaceUp && Fx.TypeIs(m.Card.Data, "Beast")).ToList();
                    foreach (DuelMonsterState m in beasts) m.TempAttack += 200 * beasts.Count;
                }),
                Ai = c => c.Engine.Phase == DuelPhase.Battle ? 45 : 0
            });
            Add("Energy Drain", Fx.Freeable(new Scripted
            {
                Can = c => c.Me.MonstersOnField.Any(m => m.IsFaceUp) && c.Opp.Hand.Count > 0,
                Tgt = c => CardEffects.Request(c.Me.MonstersOnField.Where(m => m.IsFaceUp).Select(m => m.Card), "1 face-up monster you control gains 200 ATK/DEF for each card in your opponent's hand.", "boost"),
                Do = Fx.Sync(c => { DuelMonsterState m = c.Engine.FindMonster(c.Target); if (m != null) { m.TempAttack += 200 * c.Opp.Hand.Count; m.TempDefense += 200 * c.Opp.Hand.Count; } }),
                Ai = c => c.Opp.Hand.Count >= 3 && c.Trigger?.Kind == DuelTriggerKind.AttackDeclared ? 60 : 0
            }));
            Add("Deal of Phantom", Fx.Freeable(new Scripted
            {
                Can = c => c.AllMonsters.Any(m => m.IsFaceUp),
                Tgt = c => CardEffects.Request(c.AllMonsters.Where(m => m.IsFaceUp).Select(m => m.Card), "1 face-up monster gains 100 ATK for each monster in your Graveyard.", "boost"),
                Do = Fx.Sync(c => { DuelMonsterState m = c.Engine.FindMonster(c.Target); if (m != null) m.TempAttack += 100 * c.Me.Graveyard.Count(x => x.IsMonster); }),
                Ai = c => c.Me.Graveyard.Count(x => x.IsMonster) >= 4 && c.Trigger?.Kind == DuelTriggerKind.AttackDeclared ? 50 : 0
            }));
            Add("Limiter Removal", new Scripted
            {
                Can = c => c.Me.MonstersOnField.Any(m => m.IsFaceUp && Fx.TypeIs(m.Card.Data, "Machine")),
                Respond = c => Fx.OnOpponentAction(c) && c.Me.MonstersOnField.Any(m => m.IsFaceUp && Fx.TypeIs(m.Card.Data, "Machine")),
                Do = Fx.Sync(c =>
                {
                    List<DuelMonsterState> machines = c.Me.MonstersOnField.Where(m => m.IsFaceUp && Fx.TypeIs(m.Card.Data, "Machine")).ToList();
                    foreach (DuelMonsterState m in machines) m.TempAttack += c.Engine.GetAttack(m);
                    c.Engine.AtEndPhase(() => { foreach (DuelMonsterState m in machines) if (m.Card.Zone == DuelZone.Monster) c.Engine.Destroy(m.Card, c.Card); });
                }),
                Ai = c => c.Engine.Phase == DuelPhase.Battle && c.Me.MonstersOnField.Count(m => Fx.TypeIs(m.Card.Data, "Machine") && m.IsAttackPosition && !m.HasAttacked) >= 1 ? 50 : 0
            });
            Add("Wild Nature's Release", new Scripted
            {
                Can = c => Beasts(c).Any(),
                Tgt = c => CardEffects.Request(Beasts(c).Select(m => m.Card), "1 Beast or Beast-Warrior gains ATK equal to its DEF (destroyed in the End Phase).", "boost"),
                Do = Fx.Sync(c =>
                {
                    DuelMonsterState m = c.Engine.FindMonster(c.Target);
                    if (m == null) return;
                    m.TempAttack += c.Engine.GetDefense(m);
                    c.Engine.AtEndPhase(() => { if (m.Card.Zone == DuelZone.Monster) c.Engine.Destroy(m.Card, c.Card); });
                }),
                Ai = c => c.Engine.Phase == DuelPhase.Main1 ? 35 : 0
            });
            static IEnumerable<DuelMonsterState> Beasts(EffectContext c) =>
                c.AllMonsters.Where(m => m.IsFaceUp && (Fx.TypeIs(m.Card.Data, "Beast") || Fx.TypeIs(m.Card.Data, "Beast-Warrior")));

            Add("Mystik Wok", new Scripted
            {
                Can = c => c.Me.MonsterCount > 0,
                Respond = c => Fx.OnOpponentAction(c) && c.Me.MonsterCount > 0,
                Cost = Fx.TributeCost(m => true, "Tribute 1 monster."),
                Do = Fx.Sync(c => { if (c.Paid != null) c.Engine.GainLife(c.Player, Math.Max(c.Paid.Data.attack, c.Paid.Data.defense), c.Card); }),
                Ai = c => 0
            });
            Add("Emergency Provisions", new Scripted
            {
                Can = c => c.Me.AllBackrow.Any(s => s.Card != c.Card),
                Respond = c => Fx.OnOpponentAction(c) && c.Me.AllBackrow.Any(s => s.Card != c.Card),
                Do = c => Fx.Pick(c, c.Player, c.Me.AllBackrow.Where(s => s.Card != c.Card).Select(s => s.Card), "Send any number of your other Spell/Trap Cards to the Graveyard (1000 LP each).", "discard", 1, 5, picks =>
                {
                    foreach (DuelCard x in picks) c.Engine.SendToGraveyard(x, false, c.Card);
                    c.Engine.GainLife(c.Player, 1000 * picks.Count, c.Card);
                    c.Finish();
                }),
                Ai = c => 0
            });
            Add("Enchanted Javelin", new Scripted
            {
                Can = c => false,
                Respond = c => CardEffects.IsOpponentTurnTrigger(c, DuelTriggerKind.AttackDeclared) && c.Trigger.Attacker != null,
                Do = Fx.Sync(c => { if (c.Trigger?.Attacker != null) c.Engine.GainLife(c.Player, c.Engine.GetAttack(c.Trigger.Attacker), c.Card); }),
                Ai = c => c.Trigger?.Attacker != null && c.Engine.GetAttack(c.Trigger.Attacker) >= 1800 ? 60 : 0
            });
        }

        // ----------------------------------------------------------------------------- continuous

        private static void RegisterContinuous()
        {
            Add("Banner of Courage", new Scripted
            {
                Stays = true,
                Atk = (e, s, m) => m.IsFaceUp && m.Card.Controller == s.Card.Controller && e.TurnPlayer == s.Card.Controller && e.Phase == DuelPhase.Battle ? 200 : 0,
                Ai = c => c.Me.MonsterCount > 0 ? 50 : 20
            });
            Add("Yellow Luster Shield", new Scripted
            {
                Stays = true,
                Def = (e, s, m) => m.IsFaceUp && m.Card.Controller == s.Card.Controller ? 300 : 0,
                Ai = c => 25
            });
            Add("The A. Forces", new Scripted
            {
                Stays = true,
                Atk = (e, s, m) =>
                {
                    if (!m.IsFaceUp || m.Card.Controller != s.Card.Controller || !Fx.TypeIs(m.Card.Data, "Warrior")) return 0;
                    int n = e.Me(s.Card.Controller).MonstersOnField.Count(x => x.IsFaceUp && (Fx.TypeIs(x.Card.Data, "Warrior") || Fx.TypeIs(x.Card.Data, "Spellcaster")));
                    return 200 * n;
                },
                Ai = c => c.Me.MonstersOnField.Any(m => Fx.TypeIs(m.Card.Data, "Warrior")) ? 60 : 15
            });
            Add("Fire Formation - Tenki", new Scripted
            {
                Stays = true,
                Do = c =>
                {
                    List<DuelCard> pool = c.Me.Deck.Where(x => x.IsMonster && Fx.TypeIs(x.Data, "Beast-Warrior") && x.Data.level <= 4).ToList();
                    Fx.Pick(c, c.Player, pool, "You can add 1 Level 4 or lower Beast-Warrior from your Deck to your hand.", "revive", 0, 1, picks =>
                    {
                        foreach (DuelCard x in picks) c.Engine.AddToHand(x, c.Card);
                        if (picks.Count > 0) c.Engine.ShuffleDeck(c.Player);
                        c.Finish();
                    });
                },
                Atk = (e, s, m) => m.IsFaceUp && m.Card.Controller == s.Card.Controller && Fx.TypeIs(m.Card.Data, "Beast-Warrior") ? 100 : 0,
                Ai = c => 55
            });
            Add("Insect Barrier", new Scripted
            {
                Stays = true,
                NoAttack = (e, s, m) => m.Card.Controller != s.Card.Controller && m.IsFaceUp && Fx.TypeIs(m.Card.Data, "Insect"),
                Ai = c => c.Opp.MonstersOnField.Any(m => Fx.TypeIs(m.Card.Data, "Insect")) ? 60 : 0
            });
            Add("Gravity Bind", new Scripted
            {
                Stays = true,
                Respond = Fx.OnOpponentAction,
                NoAttack = (e, s, m) => m.Level >= 4,
                Ai = c => c.Opp.MonstersOnField.Count(m => m.Level >= 4) > c.Me.MonstersOnField.Count(m => m.Level >= 4) ? 70 : 0
            });
            Add("Messenger of Peace", new Scripted
            {
                Stays = true,
                NoAttack = (e, s, m) => e.GetAttack(m) >= 1500,
                Standby = (e, s, turn) =>
                {
                    if (turn != s.Card.Controller) return;
                    if (!e.PayLife(turn, 100, s.Card)) e.Destroy(s.Card, s.Card);
                },
                Ai = c => c.Opp.MonstersOnField.Count(m => c.Engine.GetAttack(m) >= 1500) > c.Me.MonstersOnField.Count(m => c.Engine.GetAttack(m) >= 1500) ? 65 : 0
            });
            Add("The Dark Door", new Scripted
            {
                Stays = true,
                NoAttack = (e, s, m) => e.Me(m.Card.Controller).MonstersOnField.Any(x => x.HasAttacked),
                Ai = c => c.Opp.MonsterCount >= 2 && c.Opp.MonsterCount > c.Me.MonsterCount ? 50 : 0
            });
            Add("Vengeful Bog Spirit", new Scripted
            {
                Stays = true,
                NoAttack = (e, s, m) => m.SummonedTurn == e.TurnNumber,
                Ai = c => 20
            });
            Add("Dark Snake Syndrome", new Scripted
            {
                Stays = true,
                Standby = (e, s, turn) =>
                {
                    if (turn != s.Card.Controller) return;
                    int dmg = 200 << Math.Min(s.Counter, 5);
                    s.Counter++;
                    e.DealDamage(0, dmg, s.Card, false);
                    e.DealDamage(1, dmg, s.Card, false);
                },
                Ai = c => c.Me.LifePoints > c.Opp.LifePoints + 1500 ? 45 : 0
            });
            Add("Burning Land", new Scripted
            {
                Stays = true,
                Do = Fx.Sync(c => { foreach (DuelistState d in c.Engine.Duelists) if (d.FieldSpell != null) c.Engine.Destroy(d.FieldSpell.Card, c.Card); }),
                Standby = (e, s, turn) => e.DealDamage(turn, 500, s.Card, false),
                Ai = c => c.Opp.FieldSpell != null ? 55 : 0
            });
            Add("Swords of Concealing Light", new Scripted
            {
                Stays = true,
                Do = Fx.Sync(c =>
                {
                    foreach (DuelMonsterState m in c.Opp.MonstersOnField.ToList()) c.Engine.ForcePosition(m, DuelMonsterPosition.FaceDownDefense);
                    if (c.Source != null) c.Source.Counter = 0;
                }),
                Standby = (e, s, turn) =>
                {
                    if (turn != s.Card.Controller) return;
                    s.Counter++;
                    if (s.Counter >= 2) e.Destroy(s.Card, s.Card);
                },
                Ai = c => c.Opp.MonstersOnField.Count(m => m.IsFaceUp) >= 2 ? 65 : 0
            });
            Add("Minor Goblin Official", new Scripted
            {
                Stays = true,
                Can = c => c.Opp.LifePoints <= 3000,
                Respond = c => Fx.OnOpponentAction(c) && c.Opp.LifePoints <= 3000,
                Standby = (e, s, turn) => { if (turn != s.Card.Controller) e.DealDamage(turn, 500, s.Card, false); },
                Ai = c => 70
            });
            Add("Nightmare Wheel", new Scripted
            {
                Stays = true,
                Locks = true,
                Can = c => c.Opp.MonsterCount > 0,
                Respond = c => Fx.OnOpponentAction(c) && c.Opp.MonsterCount > 0,
                Tgt = c => c.Trigger?.Attacker != null
                    ? CardEffects.Request(new[] { c.Trigger.Attacker.Card }, "Bind the attacking monster.", "lock")
                    : CardEffects.Request(c.Opp.MonstersOnField.Select(m => m.Card), "Target 1 opponent's monster: it cannot attack or change its battle position.", "lock"),
                Do = Fx.Sync(c => { DuelMonsterState m = c.Engine.FindMonster(c.Target); if (m != null && c.Source != null) { c.Source.LinkedMonster = m; m.CannotChangePosition = true; } }),
                LeaveAfter = c => c.Source == null || c.Source.LinkedMonster == null,
                Standby = (e, s, turn) => { if (turn == s.Card.Controller && s.LinkedMonster != null) e.DealDamage(1 - s.Card.Controller, 500, s.Card, false); },
                Leave = (e, s) => { if (s.LinkedMonster != null) s.LinkedMonster.CannotChangePosition = false; },
                Ai = c => c.Opp.MonstersOnField.Any(m => c.Engine.GetAttack(m) >= 1800) ? 70 : 0
            });
            Add("Shadow Spell", new Scripted
            {
                Stays = true,
                Locks = true,
                Can = c => c.Opp.MonstersOnField.Any(m => m.IsFaceUp),
                Respond = c => Fx.OnOpponentAction(c) && c.Opp.MonstersOnField.Any(m => m.IsFaceUp),
                Tgt = c => c.Trigger?.Attacker != null
                    ? CardEffects.Request(new[] { c.Trigger.Attacker.Card }, "Bind the attacking monster (it loses 700 ATK).", "lock")
                    : CardEffects.Request(c.Opp.MonstersOnField.Where(m => m.IsFaceUp).Select(m => m.Card), "Target 1 face-up opponent's monster: it loses 700 ATK and cannot attack.", "lock"),
                Do = Fx.Sync(c => { DuelMonsterState m = c.Engine.FindMonster(c.Target); if (m != null && c.Source != null) { c.Source.LinkedMonster = m; m.CannotChangePosition = true; } }),
                LeaveAfter = c => c.Source == null || c.Source.LinkedMonster == null,
                Atk = (e, s, m) => s.LinkedMonster == m ? -700 : 0,
                Leave = (e, s) => { if (s.LinkedMonster != null) s.LinkedMonster.CannotChangePosition = false; },
                Ai = c => c.Opp.MonstersOnField.Any(m => m.IsFaceUp && c.Engine.GetAttack(m) >= 1800) ? 75 : 0
            });
            Add("Rare Metalmorph", new Scripted
            {
                Stays = true,
                Can = c => c.AllMonsters.Any(m => m.IsFaceUp && Fx.TypeIs(m.Card.Data, "Machine")),
                Respond = c => Fx.OnOpponentAction(c) && c.Me.MonstersOnField.Any(m => m.IsFaceUp && Fx.TypeIs(m.Card.Data, "Machine")),
                Tgt = c => CardEffects.Request(c.AllMonsters.Where(m => m.IsFaceUp && Fx.TypeIs(m.Card.Data, "Machine")).Select(m => m.Card), "Target 1 Machine: it gains 500 ATK.", "boost"),
                Do = Fx.Sync(c => { DuelMonsterState m = c.Engine.FindMonster(c.Target); if (m != null && c.Source != null) c.Source.LinkedMonster = m; }),
                LeaveAfter = c => c.Source == null || c.Source.LinkedMonster == null,
                Atk = (e, s, m) => s.LinkedMonster == m ? 500 : 0,
                Ai = c => c.Trigger?.Kind == DuelTriggerKind.AttackDeclared ? 55 : 30
            });
            Add("Soul Resurrection", new Scripted
            {
                Stays = true,
                Can = c => c.Me.HasFreeMonsterZone && Pool(c).Any(),
                Respond = c => Fx.OnOpponentAction(c) && c.Me.HasFreeMonsterZone && Pool(c).Any(),
                Tgt = c => CardEffects.Request(Pool(c), "Special Summon 1 Normal Monster from your Graveyard in Defense Position.", "revive"),
                Do = Fx.Sync(c =>
                {
                    if (c.Target?.Zone != DuelZone.Graveyard || c.Source == null) return;
                    DuelMonsterState m = c.Engine.SpecialSummon(c.Player, c.Target, DuelMonsterPosition.FaceUpDefense, c.Card);
                    if (m != null) c.Source.LinkedMonster = m;
                }),
                LeaveAfter = c => c.Source == null || c.Source.LinkedMonster == null,
                Leave = (e, s) =>
                {
                    DuelMonsterState linked = s.LinkedMonster;
                    s.LinkedMonster = null;
                    if (linked != null && linked.Card.Zone == DuelZone.Monster) e.Destroy(linked.Card, s.Card);
                },
                Ai = c => Pool(c).Any(x => x.Data.defense >= 1800) ? 60 : 0
            });
            static IEnumerable<DuelCard> Pool(EffectContext c) => c.Me.Graveyard.Where(x => Fx.IsNormalMonster(x.Data));

            Add("Wall of Revealing Light", new Scripted
            {
                Stays = true,
                Can = c => c.Me.LifePoints > 1000,
                Respond = c => Fx.OnOpponentAction(c) && c.Me.LifePoints > 1000,
                Cost = (c, paid) =>
                {
                    int max = Math.Min(5, (c.Me.LifePoints - 1) / 1000);
                    int strongest = c.Opp.MonstersOnField.Select(m => c.Engine.GetAttack(m)).DefaultIfEmpty(1000).Max();
                    int best = Math.Max(1, Math.Min(max, (strongest + 999) / 1000));
                    string[] options = Enumerable.Range(1, Math.Max(1, max)).OrderBy(n => n == best ? 0 : 1).ThenBy(n => n).Select(n => $"Pay {n * 1000} LP").ToArray();
                    Fx.Choose(c, c.Player, "Wall of Revealing Light: how much will you pay?", options, (i, label) =>
                    {
                        int amount = int.Parse(label.Split(' ')[1]);
                        c.Engine.PayLife(c.Player, amount, c.Card);
                        c.Value = amount;
                        if (c.Source != null) c.Source.Counter = amount;
                        paid();
                    });
                },
                Do = Fx.Sync(c => { if (c.Source != null) c.Source.Counter = c.Value; }),
                NoAttack = (e, s, m) => m.Card.Controller != s.Card.Controller && e.GetAttack(m) <= s.Counter,
                Ai = c => c.Me.LifePoints >= 3000 && c.Opp.MonsterCount >= 2 ? 60 : 0
            });
        }

        // ----------------------------------------------------------------------------- equips

        private static void RegisterEquips()
        {
            Add("Big Bang Shot", Fx.Equip(400, 0, banishOnLeave: true));
            Add("Gravity Axe - Grarl", Fx.Equip(500, 0));
            Add("Malevolent Nuzzler", Fx.Equip(700, 0));
            Add("Butterfly Dagger - Elma", Fx.Equip(300, 0));
            Add("Horn of the Unicorn", Fx.Equip(700, 700, toTopOfDeckOnGrave: true));
            Add("Sword of Deep-Seated", Fx.Equip(500, 500, toTopOfDeckOnGrave: true));
            Add("Mask of Brutality", Fx.Equip(1000, -1000, upkeep: 1000));
            Add("Shine Palace", Fx.Equip(700, 0, allowed: m => Fx.AttrIs(m.Card.Data, "LIGHT")));
            Add("Fusion Sword Murasame Blade", Fx.Equip(800, 0, allowed: m => Fx.TypeIs(m.Card.Data, "Warrior")));
            Add("Insect Armor with Laser Cannon", Fx.Equip(700, 0, allowed: m => Fx.TypeIs(m.Card.Data, "Insect")));
            Add("Cyber Shield", Fx.Equip(500, 0, allowed: m => m.Name is "Harpie Lady" or "Harpie Lady Sisters"));
            Add("Cestus of Dagla", Fx.Equip(500, 0, allowed: m => Fx.TypeIs(m.Card.Data, "Fairy")));
            Add("Fuhma Shuriken", Fx.Equip(700, 0, allowed: m => m.Name.Contains("Ninja"), burnOnGrave: 700));
            Add("Dragon Nails", Fx.Equip(600, 0, allowed: m => Fx.AttrIs(m.Card.Data, "DARK")));
            Add("Fusion Weapon", Fx.Equip(1500, 1500, allowed: m => m.Card.Data.ResolvedFrameKind == CardFrameKind.FusionMonster && m.Level <= 6));
            Add("Ritual Weapon", Fx.Equip(1500, 1500, allowed: m => m.Card.Data.ResolvedFrameKind == CardFrameKind.RitualMonster && m.Level <= 6));
            Add("Rod of Silence - Kay'est", Fx.Equip(0, 500));
            Add("Horn of Light", Fx.Equip(0, 800));
            Add("Wicked-Breaking Flamberge - Baou", Fx.Equip(500, 0, discard: 1));
            Add("Lightning Blade", new Scripted
            {
                Stays = true,
                Can = c => c.AllMonsters.Any(m => m.IsFaceUp && Fx.TypeIs(m.Card.Data, "Warrior")),
                Tgt = c => CardEffects.Request(c.AllMonsters.Where(m => m.IsFaceUp && Fx.TypeIs(m.Card.Data, "Warrior")).Select(m => m.Card), "Equip to 1 Warrior (+800 ATK; all WATER monsters lose 500 ATK).", "boost"),
                Do = Fx.EquipResolve(m => Fx.TypeIs(m.Card.Data, "Warrior")),
                LeaveAfter = c => c.Source == null || c.Source.EquippedTo == null,
                Atk = (e, s, m) => (s.EquippedTo == m ? 800 : 0) + (m.IsFaceUp && Fx.AttrIs(m.Card.Data, "WATER") ? -500 : 0),
                Ai = c => c.Me.MonstersOnField.Any(m => Fx.TypeIs(m.Card.Data, "Warrior")) ? 60 : 0
            });
            Add("Mage Power", new Scripted
            {
                Stays = true,
                Can = c => c.AllMonsters.Any(m => m.IsFaceUp),
                Tgt = c => CardEffects.Request(c.AllMonsters.Where(m => m.IsFaceUp).Select(m => m.Card), "Equip to 1 face-up monster (+500 ATK/DEF per Spell/Trap you control).", "boost"),
                Do = Fx.EquipResolve(m => true),
                LeaveAfter = c => c.Source == null || c.Source.EquippedTo == null,
                Atk = (e, s, m) => s.EquippedTo == m ? 500 * e.Me(s.Card.Controller).AllBackrow.Count() : 0,
                Def = (e, s, m) => s.EquippedTo == m ? 500 * e.Me(s.Card.Controller).AllBackrow.Count() : 0,
                Ai = c => c.Me.MonstersOnField.Any(m => m.IsFaceUp) ? 60 : 0
            });
            Add("United We Stand", new Scripted
            {
                Stays = true,
                Can = c => c.AllMonsters.Any(m => m.IsFaceUp),
                Tgt = c => CardEffects.Request(c.AllMonsters.Where(m => m.IsFaceUp).Select(m => m.Card), "Equip to 1 face-up monster (+800 ATK/DEF per face-up monster you control).", "boost"),
                Do = Fx.EquipResolve(m => true),
                LeaveAfter = c => c.Source == null || c.Source.EquippedTo == null,
                Atk = (e, s, m) => s.EquippedTo == m ? 800 * e.Me(s.Card.Controller).MonstersOnField.Count(x => x.IsFaceUp) : 0,
                Def = (e, s, m) => s.EquippedTo == m ? 800 * e.Me(s.Card.Controller).MonstersOnField.Count(x => x.IsFaceUp) : 0,
                Ai = c => c.Me.MonstersOnField.Any(m => m.IsFaceUp) ? 70 : 0
            });
            Add("Paralyzing Potion", new Scripted
            {
                Stays = true,
                Can = c => c.Opp.MonstersOnField.Any(m => m.IsFaceUp && !Fx.TypeIs(m.Card.Data, "Machine")),
                Tgt = c => CardEffects.Request(c.AllMonsters.Where(m => m.IsFaceUp && !Fx.TypeIs(m.Card.Data, "Machine")).Select(m => m.Card), "Equip to 1 non-Machine monster: it cannot attack.", "weaken"),
                Do = Fx.EquipResolve(m => !Fx.TypeIs(m.Card.Data, "Machine")),
                LeaveAfter = c => c.Source == null || c.Source.EquippedTo == null,
                NoAttack = (e, s, m) => s.EquippedTo == m,
                Ai = c => c.Opp.MonstersOnField.Any(m => m.IsFaceUp && !Fx.TypeIs(m.Card.Data, "Machine") && c.Engine.GetAttack(m) >= 1800) ? 65 : 0
            });
            Add("Mask of the Accursed", new Scripted
            {
                Stays = true,
                Can = c => c.Opp.MonstersOnField.Any(m => m.IsFaceUp),
                Tgt = c => CardEffects.Request(c.AllMonsters.Where(m => m.IsFaceUp).Select(m => m.Card), "Equip to 1 face-up monster: it cannot attack, and its controller takes 500 each of your Standby Phases.", "weaken"),
                Do = Fx.EquipResolve(m => true),
                LeaveAfter = c => c.Source == null || c.Source.EquippedTo == null,
                NoAttack = (e, s, m) => s.EquippedTo == m,
                Standby = (e, s, turn) => { if (turn == s.Card.Controller && s.EquippedTo != null) e.DealDamage(s.EquippedTo.Card.Controller, 500, s.Card, false); },
                Ai = c => c.Opp.MonstersOnField.Any(m => m.IsFaceUp && c.Engine.GetAttack(m) >= 1800) ? 70 : 0
            });
            Add("Ekibyo Drakmord", new Scripted
            {
                Stays = true,
                Can = c => c.Opp.MonstersOnField.Any(m => m.IsFaceUp),
                Tgt = c => CardEffects.Request(c.AllMonsters.Where(m => m.IsFaceUp).Select(m => m.Card), "Equip to 1 face-up monster: it cannot attack and is destroyed after 2 turns.", "weaken"),
                Do = Fx.EquipResolve(m => true),
                LeaveAfter = c => c.Source == null || c.Source.EquippedTo == null,
                NoAttack = (e, s, m) => s.EquippedTo == m,
                Standby = (e, s, turn) =>
                {
                    if (s.EquippedTo == null || turn != s.EquippedTo.Card.Controller) return;
                    s.Counter++;
                    if (s.Counter >= 2) e.Destroy(s.EquippedTo.Card, s.Card);
                },
                Ai = c => c.Opp.MonstersOnField.Any(m => m.IsFaceUp && c.Engine.GetAttack(m) >= 2000) ? 75 : 0
            });
            Add("Snatch Steal", new Scripted
            {
                Stays = true,
                Can = c => c.Me.HasFreeMonsterZone && c.Opp.MonstersOnField.Any(m => m.IsFaceUp),
                Tgt = c => CardEffects.Request(c.Opp.MonstersOnField.Where(m => m.IsFaceUp).Select(m => m.Card), "Take control of 1 face-up opponent's monster.", "steal"),
                Do = Fx.Sync(c =>
                {
                    DuelMonsterState m = c.Engine.FindMonster(c.Target);
                    if (m == null || c.Source == null) return;
                    c.Engine.SwitchControl(m, c.Player, temporary: false);
                    c.Source.EquippedTo = m;
                    c.Source.LinkedMonster = m;           // remembered after the equip link is cleared
                    m.Equips.Add(c.Source);
                    c.Source.Counter = c.OpponentIndex;   // original controller
                }),
                LeaveAfter = c => c.Source == null || c.Source.EquippedTo == null,
                Standby = (e, s, turn) => { if (s.EquippedTo != null && turn != s.Card.Controller) e.GainLife(turn, 1000, s.Card); },
                Leave = (e, s) =>
                {
                    DuelMonsterState m = s.LinkedMonster;
                    s.LinkedMonster = null;
                    if (m != null && m.Card.Zone == DuelZone.Monster && m.Card.Controller != s.Counter) e.SwitchControl(m, s.Counter, temporary: false);
                },
                Ai = c => c.Opp.MonstersOnField.Any(m => m.IsFaceUp && c.Engine.GetAttack(m) >= 1800) ? 85 : 0
            });
            Add("Autonomous Action Unit", new Scripted
            {
                Stays = true,
                Can = c => c.Me.HasFreeMonsterZone && c.Me.LifePoints > 1500 && c.Opp.Graveyard.Any(x => x.IsMonster && !x.IsExtraDeckCard),
                Tgt = c => CardEffects.Request(c.Opp.Graveyard.Where(x => x.IsMonster && !x.IsExtraDeckCard), "Special Summon 1 monster from your opponent's Graveyard.", "revive"),
                Cost = Fx.PayCost(1500),
                Do = Fx.ReviveAndEquip(),
                LeaveAfter = c => c.Source == null || c.Source.EquippedTo == null,
                Leave = Fx.DestroyEquipped,
                Ai = c => c.Opp.Graveyard.Any(x => x.IsMonster && x.Data.attack >= 2200) && c.Me.LifePoints >= 4000 ? 70 : 0
            });
            Add("D.D.R. - Different Dimension Reincarnation", new Scripted
            {
                Stays = true,
                Can = c => c.Me.HasFreeMonsterZone && c.Me.Hand.Count >= 2 && c.Me.Banished.Any(x => x.IsMonster && !x.IsExtraDeckCard),
                Tgt = c => CardEffects.Request(c.Me.Banished.Where(x => x.IsMonster && !x.IsExtraDeckCard), "Special Summon 1 of your banished monsters.", "revive"),
                Cost = Fx.DiscardCost(1),
                Do = Fx.ReviveAndEquip(),
                LeaveAfter = c => c.Source == null || c.Source.EquippedTo == null,
                Leave = Fx.DestroyEquipped,
                Ai = c => c.Me.Banished.Any(x => x.IsMonster && x.Data.attack >= 1800) ? 65 : 0
            });
        }

        // ----------------------------------------------------------------------------- attack-response traps

        private static void RegisterAttackTraps()
        {
            Add("Draining Shield", new Scripted
            {
                Can = c => false,
                Respond = c => CardEffects.IsOpponentTurnTrigger(c, DuelTriggerKind.AttackDeclared) && c.Trigger.Attacker != null,
                Do = Fx.Sync(c =>
                {
                    DuelMonsterState a = c.Trigger.Attacker;
                    c.Trigger.Negated = true;
                    if (a != null) c.Engine.GainLife(c.Player, c.Engine.GetAttack(a), c.Card);
                }),
                Ai = c => c.Trigger?.Attacker != null && c.Engine.GetAttack(c.Trigger.Attacker) >= 1500 ? 80 : 0
            });
            Add("Widespread Ruin", new Scripted
            {
                Can = c => false,
                Respond = c => CardEffects.IsOpponentTurnTrigger(c, DuelTriggerKind.AttackDeclared) && c.Opp.MonstersOnField.Any(m => m.IsAttackPosition),
                Do = Fx.Sync(c =>
                {
                    DuelMonsterState top = c.Opp.MonstersOnField.Where(m => m.IsAttackPosition).OrderByDescending(m => c.Engine.GetAttack(m)).FirstOrDefault();
                    if (top != null) c.Engine.Destroy(top.Card, c.Card);
                }),
                Ai = c => 85
            });
            Add("Malevolent Catastrophe", new Scripted
            {
                Can = c => false,
                Respond = c => CardEffects.IsOpponentTurnTrigger(c, DuelTriggerKind.AttackDeclared) && c.AllBackrow.Any(s => s.Card != c.Card),
                Do = Fx.Sync(c => { foreach (DuelBackrowState s in c.AllBackrow.Where(s => s.Card != c.Card).ToList()) c.Engine.Destroy(s.Card, c.Card); }),
                Ai = c => c.Opp.AllBackrow.Count() >= 2 && c.Opp.AllBackrow.Count() > c.Me.AllBackrow.Count(s => s.Card != c.Card) ? 75 : 0
            });
            Add("Thunder of Ruler", new Scripted
            {
                Can = c => false,
                Respond = c => c.Trigger != null && c.Trigger.Player != c.Player && c.Engine.TurnPlayer != c.Player && c.Engine.Phase != DuelPhase.Battle,
                Do = Fx.Sync(c => c.Opp.CannotAttackThisTurn = true),
                Ai = c => c.Opp.MonstersOnField.Sum(m => c.Engine.GetAttack(m)) >= 2500 ? 70 : 0
            });
        }

        // ----------------------------------------------------------------------------- summon-response traps

        private static void RegisterSummonTraps()
        {
            Add("House of Adhesive Tape", new Scripted
            {
                Can = c => false,
                Respond = c => c.Trigger != null && c.Trigger.Player != c.Player &&
                               (c.Trigger.Kind == DuelTriggerKind.NormalSummoned || c.Trigger.Kind == DuelTriggerKind.FlipSummoned) &&
                               c.Engine.FindMonster(c.Trigger.Card) is { } m && c.Engine.GetDefense(m) <= 500,
                Do = Fx.Sync(c => { if (c.Trigger.Card?.Zone == DuelZone.Monster) c.Engine.Destroy(c.Trigger.Card, c.Card); }),
                Ai = c => 80
            });
        }

        // ----------------------------------------------------------------------------- counter traps

        private static void RegisterCounterTraps()
        {
            Add("Magic Drain", Fx.NegateSpell(cost: null, ai: 75));
            Add("Cursed Seal of the Forbidden Spell", Fx.NegateSpell(cost: Fx.DiscardCost(1, x => x.IsSpell), requireHand: x => x.IsSpell, ai: 80));
            Add("Spell Vanishing", Fx.NegateSpell(cost: Fx.DiscardCost(2), requireHandCount: 2, ai: 70));
            Add("Armor Break", new Scripted
            {
                Can = c => false,
                Respond = c => c.Trigger != null && c.Trigger.Kind == DuelTriggerKind.SpellActivated && c.Trigger.Player != c.Player &&
                               c.Trigger.Card != null && DuelRules.IsEquip(c.Trigger.Card.Data),
                Do = Fx.Sync(c => c.Trigger.Negated = true),
                Ai = c => 70
            });
            Add("Trap Jammer", new Scripted
            {
                Can = c => false,
                Respond = c => c.Trigger != null && c.Trigger.Kind == DuelTriggerKind.TrapActivated && c.Trigger.Player != c.Player && c.Engine.Phase == DuelPhase.Battle,
                Do = Fx.Sync(c => c.Trigger.Negated = true),
                Ai = c => 80
            });
            Add("Horn of Heaven", new Scripted
            {
                Can = c => false,
                Respond = c => c.Trigger != null && c.Trigger.Player != c.Player && c.Me.MonsterCount > 0 &&
                               (c.Trigger.Kind == DuelTriggerKind.NormalSummoned || c.Trigger.Kind == DuelTriggerKind.FlipSummoned || c.Trigger.Kind == DuelTriggerKind.SpecialSummoned),
                Cost = Fx.TributeCost(m => true, "Tribute 1 monster."),
                Do = Fx.Sync(c =>
                {
                    DuelCard t = c.Trigger.Card;
                    if (t?.Zone != DuelZone.Monster) return;
                    c.Engine.Raise(DuelEventType.SummonNegated, c.Trigger.Player, t, text: $"The summon of {t.Name} was negated.");
                    c.Engine.Destroy(t, c.Card);
                }),
                Ai = c => c.Trigger?.Card != null && c.Trigger.Card.Data.attack >= 2200 && c.Me.MonstersOnField.Any(m => c.Engine.GetAttack(m) < 1200) ? 80 : 0
            });
        }

        // ----------------------------------------------------------------------------- rituals

        private static void RegisterRituals()
        {
            Add("Black Luster Ritual", Fx.Ritual(x => x.Name == "Black Luster Soldier", exact: false));
            Add("Black Magic Ritual", Fx.Ritual(x => x.Name == "Magician of Black Chaos", exact: false));
            Add("Contract with the Dark Master", Fx.Ritual(x => x.Name == "Dark Master - Zorc", exact: false));
            Add("Curse of the Masked Beast", Fx.Ritual(x => x.Name == "The Masked Beast", exact: false));
            Add("Black Illusion Ritual", Fx.Ritual(x => x.Name == "Relinquished", exact: false));
            Add("Doriado's Blessing", Fx.Ritual(x => x.Name == "Elemental Mistress Doriado", exact: false));
            Add("Contract with the Abyss", Fx.Ritual(x => Fx.AttrIs(x.Data, "DARK"), exact: true));
            Add("Earth Chant", Fx.Ritual(x => Fx.AttrIs(x.Data, "EARTH"), exact: true));
            Add("Advanced Ritual Art", Fx.Ritual(x => true, exact: true, materialsFromDeckNormals: true));
        }
    }

    // =====================================================================================================
    //  Scripted effect + building blocks
    // =====================================================================================================

    /// <summary>A card effect assembled from delegates (see <see cref="CardEffectLibrary"/>).</summary>
    internal sealed class Scripted : CardEffect
    {
        public Func<EffectContext, bool> Can;
        public Func<EffectContext, bool> Respond;
        public Func<EffectContext, TargetRequest> Tgt;
        public Action<EffectContext, Action> Cost;
        public Action<EffectContext> Do;
        public Func<EffectContext, int> Ai;
        public bool Stays;
        public bool Locks;
        public Func<EffectContext, bool> LeaveAfter;
        public Func<DuelEngine, DuelBackrowState, DuelMonsterState, int> Atk;
        public Func<DuelEngine, DuelBackrowState, DuelMonsterState, int> Def;
        public Func<DuelEngine, DuelBackrowState, DuelMonsterState, bool> NoAttack;
        public Action<DuelEngine, DuelBackrowState, int> Standby;
        public Action<DuelEngine, DuelBackrowState> Leave;
        public Action<DuelEngine, DuelCard> ToGrave;

        public override bool StaysOnField => Stays;
        public override bool LocksLinkedMonster => Locks;
        public override bool ShouldLeaveAfterResolve(EffectContext ctx) => LeaveAfter?.Invoke(ctx) ?? false;
        public override bool CanActivate(EffectContext ctx) => Can?.Invoke(ctx) ?? true;
        public override bool CanRespond(EffectContext ctx) => Respond?.Invoke(ctx) ?? false;
        public override TargetRequest Targets(EffectContext ctx) => Tgt?.Invoke(ctx);
        public override void PayCost(EffectContext ctx, Action paid) { if (Cost == null) paid(); else Cost(ctx, paid); }
        public override void Resolve(EffectContext ctx) { if (Do == null) ctx.Finish(); else Do(ctx); }
        public override int AttackModifier(DuelEngine engine, DuelBackrowState source, DuelMonsterState monster) => Atk?.Invoke(engine, source, monster) ?? 0;
        public override int DefenseModifier(DuelEngine engine, DuelBackrowState source, DuelMonsterState monster) => Def?.Invoke(engine, source, monster) ?? 0;
        public override bool ForbidsAttack(DuelEngine engine, DuelBackrowState source, DuelMonsterState monster) => NoAttack?.Invoke(engine, source, monster) ?? false;
        public override void OnStandby(DuelEngine engine, DuelBackrowState source, int turnPlayer) => Standby?.Invoke(engine, source, turnPlayer);
        public override void OnLeaveField(DuelEngine engine, DuelBackrowState source) => Leave?.Invoke(engine, source);
        public override void OnSentToGraveyardFromField(DuelEngine engine, DuelCard card) => ToGrave?.Invoke(engine, card);
        public override int AiValue(EffectContext ctx) => Ai?.Invoke(ctx) ?? 40;
    }

    internal static class Fx
    {
        // ---------------------------------------------------------------- card tests

        /// <summary>Monster Type is the first part of the type line ("Beast-Warrior / Effect").</summary>
        public static bool TypeIs(CardData d, string type)
        {
            if (d == null || d.kind != CardKind.Monster) return false;
            string first = (d.typeLine ?? string.Empty).Split('/')[0].Trim();
            return first.Equals(type, StringComparison.OrdinalIgnoreCase);
        }

        public static bool AttrIs(CardData d, string attribute) => d != null && string.Equals(d.attribute, attribute, StringComparison.OrdinalIgnoreCase);
        public static bool IsNormalMonster(CardData d) => d != null && d.kind == CardKind.Monster && d.ResolvedFrameKind == CardFrameKind.NormalMonster;
        public static bool IsEffectMonster(CardData d) => d != null && d.kind == CardKind.Monster && (d.typeLine ?? string.Empty).IndexOf("Effect", StringComparison.OrdinalIgnoreCase) >= 0;
        public static bool ControlsFaceUp(EffectContext c, string name) => c.Me.MonstersOnField.Any(m => m.IsFaceUp && m.Name == name);
        public static int Die(EffectContext c)
        {
            int roll = c.Engine.RandomRange(1, 7);
            c.Engine.Raise(DuelEventType.EffectResolved, c.Player, c.Card, text: $"{c.Card.Name}: rolled a {roll}.");
            return roll;
        }

        public static IEnumerable<DuelCard> AllCardsOnField(EffectContext c) =>
            c.AllMonsters.Select(m => m.Card).Concat(c.AllBackrow.Where(s => s.Card != c.Card).Select(s => s.Card));

        public static IEnumerable<DuelCard> RevivableInAnyGY(EffectContext c) =>
            c.Engine.Duelists.SelectMany(d => d.Graveyard).Where(x => x.IsMonster && !x.IsExtraDeckCard && DuelRules.CanEverBeNormalSummoned(x.Data));

        public static int Advantage(EffectContext c, IEnumerable<DuelMonsterState> removed) =>
            removed.Sum(m => (m.Card.Controller == c.Player ? -1 : 1) * (800 + c.Engine.GetAttack(m)));

        public static bool AttackWouldBeTurned(EffectContext c, int loss)
        {
            DuelMonsterState a = c.Trigger?.Attacker, d = c.Trigger?.Defender;
            if (a == null) return false;
            int atk = c.Engine.GetAttack(a);
            if (d == null) return atk >= 1500;
            int wall = d.IsAttackPosition ? c.Engine.GetAttack(d) : c.Engine.GetDefense(d);
            return atk > wall && atk - loss <= wall;
        }

        // ---------------------------------------------------------------- timing

        /// <summary>Free-timing Traps / Quick-Plays: usable when the opponent does anything that opens a window.</summary>
        public static bool OnOpponentAction(EffectContext c) => c.Trigger != null && c.Trigger.Player != c.Player;

        /// <summary>Lets a Trap that is normally "activate whenever" respond to opponent actions as well.</summary>
        public static Scripted Freeable(Scripted s)
        {
            Func<EffectContext, bool> can = s.Can ?? (_ => true);
            s.Respond ??= c => OnOpponentAction(c) && can(c);
            return s;
        }

        // ---------------------------------------------------------------- flow

        public static Action<EffectContext> Sync(Action<EffectContext> body) => c => { body(c); c.Finish(); };

        public static void Pick(EffectContext c, int player, IEnumerable<DuelCard> candidates, string prompt, string context, int min, int max, Action<List<DuelCard>> then)
        {
            List<DuelCard> list = candidates.Distinct().ToList();
            if (list.Count == 0 || max <= 0) { then(new List<DuelCard>()); return; }
            c.Engine.Ask(new DuelChoice
            {
                Player = player,
                Title = c.Card.Name,
                Prompt = prompt,
                SourceCard = c.Card,
                Candidates = list,
                MinCount = Math.Min(min, list.Count),
                MaxCount = Math.Min(max, list.Count),
                Context = context,
                OnCards = picks => then(picks ?? new List<DuelCard>())
            });
        }

        public static void Choose(EffectContext c, int player, string prompt, string[] options, Action<int, string> then)
        {
            if (options.Length <= 1) { then(0, options.Length == 1 ? options[0] : string.Empty); return; }
            c.Engine.Ask(new DuelChoice
            {
                Player = player,
                Kind = DuelChoiceKind.SelectOption,
                Title = c.Card.Name,
                Prompt = prompt,
                SourceCard = c.Card,
                Options = options.ToList(),
                Context = "option",
                OnOption = i => then(i, options[Math.Max(0, Math.Min(i, options.Length - 1))])
            });
        }

        // ---------------------------------------------------------------- costs

        public static Action<EffectContext, Action> DiscardCost(int count, Func<DuelCard, bool> filter = null) => (c, paid) =>
        {
            List<DuelCard> pool = c.Me.Hand.Where(x => x != c.Card && (filter == null || filter(x))).ToList();
            Pick(c, c.Player, pool, count == 1 ? "Discard 1 card." : $"Discard {count} cards.", "discard", count, count, picks =>
            {
                foreach (DuelCard x in picks) c.Engine.Discard(x);
                c.Paid = picks.FirstOrDefault();
                paid();
            });
        };

        public static Action<EffectContext, Action> PayCost(int lp) => (c, paid) => { c.Engine.PayLife(c.Player, lp, c.Card); paid(); };

        public static Action<EffectContext, Action> TributeCost(Func<DuelMonsterState, bool> filter, string prompt) => (c, paid) =>
        {
            Pick(c, c.Player, c.Me.MonstersOnField.Where(filter).Select(m => m.Card), prompt, "tribute", 1, 1, picks =>
            {
                DuelCard t = picks.FirstOrDefault();
                if (t != null)
                {
                    DuelMonsterState m = c.Engine.FindMonster(t);
                    c.Value = m != null ? c.Engine.GetAttack(m) : t.Data.attack;
                    c.Paid = t;
                    c.Engine.SendToGraveyard(t, false, c.Card);
                }
                paid();
            });
        };

        // ---------------------------------------------------------------- effect templates

        public static Scripted Burn(int amount) => new()
        {
            Do = Sync(c => c.Engine.DealDamage(c.OpponentIndex, amount, c.Card, false)),
            Ai = c => 50
        };

        public static Scripted Heal(int amount) => new()
        {
            Do = Sync(c => c.Engine.GainLife(c.Player, amount, c.Card)),
            Ai = c => c.Me.LifePoints < 4000 ? 40 : 5
        };

        public static Scripted BurnTrap(Func<EffectContext, int> amount, Func<EffectContext, bool> worthIt) => new()
        {
            Can = c => amount(c) > 0,
            Respond = c => OnOpponentAction(c) && amount(c) > 0,
            Do = Sync(c => c.Engine.DealDamage(c.OpponentIndex, amount(c), c.Card, false)),
            Ai = c => worthIt(c) || amount(c) >= c.Opp.LifePoints ? 70 : 0
        };

        public static Scripted DestroyAllFaceUp(Func<DuelMonsterState, bool> filter) => new()
        {
            Can = c => c.AllMonsters.Any(m => m.IsFaceUp && filter(m)),
            Do = Sync(c => { foreach (DuelMonsterState m in c.AllMonsters.Where(m => m.IsFaceUp && filter(m)).ToList()) c.Engine.Destroy(m.Card, c.Card); }),
            Ai = c => Advantage(c, c.AllMonsters.Where(m => m.IsFaceUp && filter(m))) >= 800 ? 80 : 0
        };

        public static Scripted DestroyFaceUpBackrow(Func<DuelBackrowState, bool> filter, int discard) => new()
        {
            Can = c => c.Me.Hand.Count > discard && c.AllBackrow.Any(s => s.Card != c.Card && !s.FaceDown && filter(s)),
            Respond = c => OnOpponentAction(c) && c.Me.Hand.Count >= discard && c.Opp.AllBackrow.Any(s => !s.FaceDown && filter(s)),
            Cost = discard > 0 ? DiscardCost(discard) : null,
            Do = Sync(c => { foreach (DuelBackrowState s in c.AllBackrow.Where(s => s.Card != c.Card && !s.FaceDown && filter(s)).ToList()) c.Engine.Destroy(s.Card, c.Card); }),
            Ai = c => c.Opp.AllBackrow.Any(s => !s.FaceDown && filter(s)) ? 70 : 0
        };

        public static Scripted DestroyTargetedBackrow(Func<DuelBackrowState, bool> filter, string prompt) => new()
        {
            Can = c => c.AllBackrow.Any(s => s.Card != c.Card && filter(s)),
            Tgt = c => CardEffects.Request(c.AllBackrow.Where(s => s.Card != c.Card && filter(s)).Select(s => s.Card), prompt, "destroy-backrow"),
            Do = Sync(c => { if (c.Target != null && c.Target.OnField) c.Engine.Destroy(c.Target, c.Card); }),
            Ai = c => c.Opp.AllBackrow.Any(filter) ? 65 : 0
        };

        public static Scripted DestroyTargetedMonster(Func<DuelMonsterState, bool> filter, string prompt) => new()
        {
            Can = c => c.AllMonsters.Any(filter),
            Tgt = c => CardEffects.Request(c.AllMonsters.Where(filter).Select(m => m.Card), prompt, "destroy-monster"),
            Do = Sync(c => { if (c.Target?.Zone == DuelZone.Monster) c.Engine.Destroy(c.Target, c.Card); }),
            Ai = c => c.Opp.MonstersOnField.Any(filter) ? 70 : 0
        };

        public static Scripted ChangePositionTargeted(Func<DuelMonsterState, bool> filter, bool opponentOnly, DuelMonsterPosition to, string prompt, int ai) => new()
        {
            Can = c => (opponentOnly ? c.Opp.MonstersOnField : c.AllMonsters).Any(filter),
            Tgt = c => CardEffects.Request((opponentOnly ? c.Opp.MonstersOnField : c.AllMonsters).Where(filter).Select(m => m.Card), prompt, "position"),
            Do = Sync(c => { DuelMonsterState m = c.Engine.FindMonster(c.Target); if (m != null) c.Engine.ForcePosition(m, to); }),
            Ai = c => c.Opp.MonstersOnField.Any(filter) ? ai : 0
        };

        public static void Flip(DuelEngine e, DuelMonsterState m) =>
            e.ForcePosition(m, m.IsAttackPosition ? DuelMonsterPosition.FaceUpDefense : DuelMonsterPosition.FaceUpAttack);

        public static Scripted TempStatTargeted(int atk, int def, bool mineOnly, string prompt) => new()
        {
            Can = c => (mineOnly ? c.Me.MonstersOnField : c.AllMonsters).Any(m => m.IsFaceUp),
            Tgt = c => CardEffects.Request((mineOnly ? c.Me.MonstersOnField : c.AllMonsters).Where(m => m.IsFaceUp).Select(m => m.Card), prompt, atk + def >= 0 ? "boost" : "weaken"),
            Do = Sync(c => { DuelMonsterState m = c.Engine.FindMonster(c.Target); if (m != null) { m.TempAttack += atk; m.TempDefense += def; } }),
            Ai = c => c.Trigger?.Kind == DuelTriggerKind.AttackDeclared ? 50 : 0
        };

        public static Scripted ReviveFromOwnGY(Func<DuelCard, bool> filter, DuelMonsterPosition position, string prompt) => new()
        {
            Can = c => c.Me.HasFreeMonsterZone && c.Me.Graveyard.Any(x => x.IsMonster && !x.IsExtraDeckCard && filter(x)),
            Tgt = c => CardEffects.Request(c.Me.Graveyard.Where(x => x.IsMonster && !x.IsExtraDeckCard && filter(x)), prompt, "revive"),
            Do = Sync(c => { if (c.Target?.Zone == DuelZone.Graveyard) c.Engine.SpecialSummon(c.Player, c.Target, position, c.Card); }),
            Ai = c => 60
        };

        public static Scripted AddFromGraveyard(Func<DuelCard, bool> filter, int min, int max, string prompt) => new()
        {
            Can = c => c.Me.Graveyard.Count(filter) >= min,
            Do = c => Pick(c, c.Player, c.Me.Graveyard.Where(filter), prompt, "revive", min, max, picks =>
            {
                foreach (DuelCard x in picks) c.Engine.AddToHand(x, c.Card);
                c.Finish();
            }),
            Ai = c => 55
        };

        public static Scripted Search(Func<DuelCard, bool> filter, string prompt) => new()
        {
            Can = c => c.Me.Deck.Any(filter),
            Do = c => Pick(c, c.Player, c.Me.Deck.Where(filter), prompt, "revive", 1, 1, picks =>
            {
                foreach (DuelCard x in picks) c.Engine.AddToHand(x, c.Card);
                c.Engine.ShuffleDeck(c.Player);
                c.Finish();
            }),
            Ai = c => 75
        };

        public static Scripted SummonNamed(Func<EffectContext, bool> condition, string name, bool fromHand, bool fromDeck, bool fromGrave) => new()
        {
            Can = c => condition(c) && c.Me.HasFreeMonsterZone && Pool(c, name, fromHand, fromDeck, fromGrave).Any(),
            Do = Sync(c =>
            {
                DuelCard x = Pool(c, name, fromHand, fromDeck, fromGrave).FirstOrDefault();
                if (x != null) c.Engine.SpecialSummon(c.Player, x, DuelMonsterPosition.FaceUpAttack, c.Card);
                if (fromDeck) c.Engine.ShuffleDeck(c.Player);
            }),
            Ai = c => 80
        };

        private static IEnumerable<DuelCard> Pool(EffectContext c, string name, bool hand, bool deck, bool grave) =>
            (hand ? c.Me.Hand : Enumerable.Empty<DuelCard>())
            .Concat(deck ? c.Me.Deck : Enumerable.Empty<DuelCard>())
            .Concat(grave ? c.Me.Graveyard : Enumerable.Empty<DuelCard>())
            .Where(x => x.Name == name);

        public static Scripted TributeInto(string from, string into, bool quick) => new()
        {
            Can = c => c.Me.MonstersOnField.Any(m => m.IsFaceUp && m.Name == from) && Pool(c, into, true, true, true).Any(),
            Respond = quick ? c => OnOpponentAction(c) && c.Me.MonstersOnField.Any(m => m.IsFaceUp && m.Name == from) && Pool(c, into, true, true, true).Any() : null,
            Cost = TributeCost(m => m.IsFaceUp && m.Name == from, $"Tribute 1 \"{from}\"."),
            Do = Sync(c =>
            {
                DuelCard x = Pool(c, into, true, true, true).FirstOrDefault();
                if (x != null) c.Engine.SpecialSummon(c.Player, x, DuelMonsterPosition.FaceUpAttack, c.Card);
                c.Engine.ShuffleDeck(c.Player);
            }),
            Ai = c => 85
        };

        public static Scripted Equip(int atk, int def, Func<DuelMonsterState, bool> allowed = null, int burnOnGrave = 0,
            bool toTopOfDeckOnGrave = false, int upkeep = 0, int discard = 0, bool banishOnLeave = false)
        {
            allowed ??= _ => true;
            string what = atk != 0 ? (atk > 0 ? $"+{atk} ATK" : $"{atk} ATK") : string.Empty;
            if (def != 0) what += (what.Length > 0 ? " / " : string.Empty) + (def > 0 ? $"+{def} DEF" : $"{def} DEF");
            return new Scripted
            {
                Stays = true,
                Can = c => c.AllMonsters.Any(m => m.IsFaceUp && allowed(m)) && c.Me.Hand.Count > discard,
                Tgt = c => CardEffects.Request(c.AllMonsters.Where(m => m.IsFaceUp && allowed(m)).Select(m => m.Card), $"Equip to 1 face-up monster ({what}).", atk + def >= 0 ? "boost" : "weaken"),
                Cost = discard > 0 ? DiscardCost(discard) : null,
                Do = EquipResolve(allowed, remember: banishOnLeave),
                LeaveAfter = c => c.Source == null || c.Source.EquippedTo == null,
                Leave = banishOnLeave ? (e, s) =>
                {
                    DuelMonsterState m = s.LinkedMonster;
                    s.LinkedMonster = null;
                    if (m != null && m.Card.Zone == DuelZone.Monster) e.Banish(m.Card, s.Card);
                } : null,
                Atk = (e, s, m) => s.EquippedTo == m ? atk : 0,
                Def = (e, s, m) => s.EquippedTo == m ? def : 0,
                ToGrave = (e, card) =>
                {
                    if (burnOnGrave > 0) e.DealDamage(1 - card.Controller, burnOnGrave, card, false);
                    if (toTopOfDeckOnGrave && card.Zone == DuelZone.Graveyard) e.ReturnToDeck(card, shuffle: false);
                },
                Standby = upkeep > 0 ? (e, s, turn) => { if (turn == s.Card.Controller && !e.PayLife(turn, upkeep, s.Card)) e.Destroy(s.Card, s.Card); } : null,
                Ai = c => atk + def > 0 ? (c.Me.MonstersOnField.Any(m => m.IsFaceUp && allowed(m)) ? 55 : 0) : (c.Opp.MonstersOnField.Any(m => m.IsFaceUp && allowed(m)) ? 45 : 0)
            };
        }

        public static Action<EffectContext> EquipResolve(Func<DuelMonsterState, bool> allowed, bool remember = false) => Sync(c =>
        {
            DuelMonsterState m = c.Engine.FindMonster(c.Target);
            if (m != null && c.Source != null && m.IsFaceUp && allowed(m))
            {
                c.Source.EquippedTo = m;
                if (remember) c.Source.LinkedMonster = m;
                m.Equips.Add(c.Source);
            }
        });

        public static Action<EffectContext> ReviveAndEquip() => Sync(c =>
        {
            if (c.Target == null || c.Source == null || (c.Target.Zone != DuelZone.Graveyard && c.Target.Zone != DuelZone.Banished)) return;
            DuelMonsterState m = c.Engine.SpecialSummon(c.Player, c.Target, DuelMonsterPosition.FaceUpAttack, c.Card);
            if (m == null) return;
            c.Source.EquippedTo = m;
            m.Equips.Add(c.Source);
            c.Source.LinkedMonster = m;
        });

        public static void DestroyEquipped(DuelEngine e, DuelBackrowState s)
        {
            DuelMonsterState linked = s.LinkedMonster;
            s.LinkedMonster = null;
            if (linked != null && linked.Card.Zone == DuelZone.Monster) e.Destroy(linked.Card, s.Card);
        }

        public static Scripted NegateSpell(Action<EffectContext, Action> cost, int ai, Func<DuelCard, bool> requireHand = null, int requireHandCount = 0) => new()
        {
            Can = c => false,
            Respond = c => c.Trigger != null && c.Trigger.Kind == DuelTriggerKind.SpellActivated && c.Trigger.Player != c.Player &&
                           (requireHand == null || c.Me.Hand.Any(requireHand)) && c.Me.Hand.Count >= requireHandCount,
            Cost = cost,
            Do = Sync(c => c.Trigger.Negated = true),
            Ai = c =>
            {
                string n = c.Trigger?.Card?.Name ?? string.Empty;
                return n is "Dark Hole" or "Heavy Storm" or "Harpie's Feather Duster" or "Change of Heart" or "Brain Control" or "Monster Reborn" or
                    "Pot of Greed" or "Graceful Charity" or "Lightning Vortex" or "Snatch Steal" or "Swords of Revealing Light" ? ai : 0;
            }
        };

        /// <summary>
        /// Ritual Spell: Ritual Summon a matching Ritual Monster from the hand by Tributing monsters from the
        /// hand or field whose total Levels equal (exact) or reach (or more) its Level.
        /// </summary>
        public static Scripted Ritual(Func<DuelCard, bool> which, bool exact, int fixedLevel = 0, bool materialsFromDeckNormals = false)
        {
            IEnumerable<DuelCard> Targets(EffectContext c) =>
                c.Me.Hand.Where(x => x.IsMonster && x.Data.ResolvedFrameKind == CardFrameKind.RitualMonster && which(x) && CanPay(c, x));

            IEnumerable<DuelCard> Materials(EffectContext c, DuelCard ritual) =>
                materialsFromDeckNormals
                    ? c.Me.Deck.Where(x => IsNormalMonster(x.Data))
                    : c.Me.Hand.Where(x => x.IsMonster && x != ritual).Concat(c.Me.MonstersOnField.Select(m => m.Card));

            int Need(DuelCard ritual) => fixedLevel > 0 ? fixedLevel : ritual.Data.level;

            bool CanPay(EffectContext c, DuelCard ritual)
            {
                List<int> levels = Materials(c, ritual).Select(x => Math.Max(1, x.Data.level)).ToList();
                int need = Need(ritual);
                if (!exact) return levels.Sum() >= need;
                // Subset sum for exact totals.
                var reachable = new HashSet<int> { 0 };
                foreach (int l in levels)
                    foreach (int r in reachable.ToList())
                        if (r + l <= need) reachable.Add(r + l);
                return reachable.Contains(need);
            }

            return new Scripted
            {
                Can = c => c.Me.HasFreeMonsterZone || c.Me.MonsterCount > 0 ? Targets(c).Any() : false,
                Tgt = c => CardEffects.Request(Targets(c), "Choose the Ritual Monster to Ritual Summon.", "revive"),
                Do = c =>
                {
                    DuelCard ritual = c.Target;
                    if (ritual == null || ritual.Zone != DuelZone.Hand) { c.Finish(); return; }
                    int need = Need(ritual);
                    List<DuelCard> pool = Materials(c, ritual).ToList();
                    string prompt = $"Tribute monsters whose total Levels {(exact ? "equal exactly" : "equal")} {need}{(exact ? "" : " or more")}" +
                                    (materialsFromDeckNormals ? " (Normal Monsters from your Deck)." : ".");
                    Pick(c, c.Player, pool, prompt, "tribute", 1, pool.Count, picks =>
                    {
                        List<DuelCard> chosen = FixMaterials(picks, pool, need, exact);
                        if (chosen == null)
                        {
                            c.Engine.Raise(DuelEventType.EffectResolved, c.Player, c.Card, text: "The Tribute did not match the Ritual Monster's Level — the Ritual failed.");
                            c.Finish();
                            return;
                        }
                        foreach (DuelCard x in chosen) c.Engine.SendToGraveyard(x, false, c.Card);
                        if (c.Me.HasFreeMonsterZone && ritual.Zone == DuelZone.Hand)
                            c.Engine.SpecialSummon(c.Player, ritual, DuelMonsterPosition.FaceUpAttack, c.Card);
                        if (materialsFromDeckNormals) c.Engine.ShuffleDeck(c.Player);
                        c.Finish();
                    });
                },
                Ai = c => 85
            };
        }

        /// <summary>Tops up / trims a Ritual Tribute selection so it satisfies the Level requirement.</summary>
        private static List<DuelCard> FixMaterials(List<DuelCard> picks, List<DuelCard> pool, int need, bool exact)
        {
            int Lv(DuelCard x) => Math.Max(1, x.Data.level);
            List<DuelCard> chosen = picks.ToList();
            int sum = chosen.Sum(Lv);
            if (!exact)
            {
                foreach (DuelCard x in pool.Except(chosen).OrderBy(x => x.Data.attack))
                {
                    if (sum >= need) break;
                    chosen.Add(x);
                    sum += Lv(x);
                }
                return sum >= need ? chosen : null;
            }
            if (sum == need) return chosen;
            // Exact: find the cheapest subset of the whole pool that sums to the Level.
            List<DuelCard> ordered = pool.OrderBy(x => x.Data.attack).ToList();
            var best = new Dictionary<int, List<DuelCard>> { [0] = new List<DuelCard>() };
            foreach (DuelCard x in ordered)
                foreach (var kv in best.ToList())
                {
                    int total = kv.Key + Lv(x);
                    if (total <= need && !best.ContainsKey(total)) best[total] = kv.Value.Append(x).ToList();
                }
            return best.TryGetValue(need, out List<DuelCard> found) ? found : null;
        }
    }
}
