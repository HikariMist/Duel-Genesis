using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using DuelGenesis.Cards;

namespace DuelGenesis.Dueling
{
    public sealed class TargetRequest
    {
        public List<DuelCard> Candidates = new();
        public int Min = 1;
        public int Max = 1;
        public string Prompt = "Select a target.";
        public string Context = "target";
    }

    public sealed class EffectContext
    {
        public readonly DuelEngine Engine;
        public readonly int Player;
        public readonly DuelCard Card;
        public readonly DuelTrigger Trigger;
        public readonly List<DuelCard> Targets = new();
        public DuelBackrowState Source;
        /// <summary>The card was already face-up on the field: this is its on-field effect, not its activation.</summary>
        public bool FromFaceUp;
        public Action Done;
        /// <summary>Whatever a cost paid with (the Tributed / discarded card), for effects that depend on it.</summary>
        public DuelCard Paid;
        /// <summary>A number chosen or paid as a cost (Life Points paid, option picked...).</summary>
        public int Value;

        public EffectContext(DuelEngine engine, int player, DuelCard card, DuelTrigger trigger)
        {
            Engine = engine;
            Player = player;
            Card = card;
            Trigger = trigger;
        }

        public int OpponentIndex => 1 - Player;
        public DuelistState Me => Engine.Me(Player);
        public DuelistState Opp => Engine.Opponent(Player);
        public DuelCard Target => Targets.Count > 0 ? Targets[0] : null;
        public void Finish() => Done?.Invoke();

        public IEnumerable<DuelMonsterState> AllMonsters => Engine.Duelists.SelectMany(d => d.MonstersOnField);
        public IEnumerable<DuelBackrowState> AllBackrow => Engine.Duelists.SelectMany(d => d.AllBackrow);
    }

    /// <summary>Behaviour of a Spell or Trap card. Override only what the card does.</summary>
    public abstract class CardEffect
    {
        /// <summary>Continuous / Equip / Field cards stay on the field after resolving.</summary>
        public virtual bool StaysOnField => false;
        public virtual bool ShouldLeaveAfterResolve(EffectContext ctx) => false;
        public virtual bool LocksLinkedMonster => false;

        /// <summary>Activation from the hand / Set zone during your own Main Phase.</summary>
        public virtual bool CanActivate(EffectContext ctx) => true;
        /// <summary>Activation in a response window (ctx.Trigger is set).</summary>
        public virtual bool CanRespond(EffectContext ctx) => false;
        public virtual TargetRequest Targets(EffectContext ctx) => null;
        public virtual void PayCost(EffectContext ctx, Action paid) => paid();
        public abstract void Resolve(EffectContext ctx);

        public virtual int AttackModifier(DuelEngine engine, DuelBackrowState source, DuelMonsterState monster) => 0;
        public virtual int DefenseModifier(DuelEngine engine, DuelBackrowState source, DuelMonsterState monster) => 0;
        public virtual bool PreventsOpponentAttacks(DuelEngine engine, DuelBackrowState source) => false;
        /// <summary>Face-up card that forbids a specific monster (either side) from attacking (Gravity Bind, Messenger of Peace...).</summary>
        public virtual bool ForbidsAttack(DuelEngine engine, DuelBackrowState source, DuelMonsterState monster) => false;
        /// <summary>Called for every face-up Spell/Trap during each Standby Phase (either player's).</summary>
        public virtual void OnStandby(DuelEngine engine, DuelBackrowState source, int turnPlayer) { }
        public virtual void OnLeaveField(DuelEngine engine, DuelBackrowState source) { }
        public virtual void OnSentToGraveyardFromField(DuelEngine engine, DuelCard card) { }

        /// <summary>The face-up card has an effect its controller can use in their Main Phase (Fusion Gate).</summary>
        public virtual bool HasFaceUpEffect => false;
        /// <summary>Battle damage to <paramref name="player"/> from a battle involving their <paramref name="ownMonster"/> becomes 0.</summary>
        public virtual bool PreventsBattleDamage(DuelEngine engine, DuelBackrowState source, int player, DuelMonsterState ownMonster) => false;
        /// <summary>This monster cannot be chosen as an attack target.</summary>
        public virtual bool ProtectsFromAttack(DuelEngine engine, DuelBackrowState source, DuelMonsterState defender) => false;
        /// <summary>This face-up card survives an attempt to destroy it (e.g. once per turn).</summary>
        public virtual bool ResistsDestruction(DuelEngine engine, DuelBackrowState source, DuelCard cause) => false;
        /// <summary>A monster was Normal, Flip or Special Summoned while this card is face-up.</summary>
        public virtual void OnMonsterSummoned(DuelEngine engine, DuelBackrowState source, DuelMonsterState monster, int player, bool special) { }
        /// <summary>A monster on the field was destroyed by a card effect (not by battle).</summary>
        public virtual void OnMonsterDestroyedByEffect(DuelEngine engine, DuelBackrowState source, DuelCard monster) { }
        /// <summary>Stops <paramref name="player"/> Special Summoning <paramref name="card"/>.</summary>
        public virtual bool BlocksSpecialSummon(DuelEngine engine, DuelBackrowState source, int player, DuelCard card) => false;

        /// <summary>How much the CPU wants to use this card right now (0 or less = don't).</summary>
        public virtual int AiValue(EffectContext ctx) => 40;
    }

    /// <summary>Passive effects printed on monsters (stat changes that always apply).</summary>
    public abstract class MonsterEffect
    {
        public virtual int SelfAttackModifier(DuelEngine engine, DuelMonsterState self) => 0;
        public virtual int SelfDefenseModifier(DuelEngine engine, DuelMonsterState self) => 0;
        /// <summary>Change this face-up monster (<paramref name="source"/>) applies to another monster (<paramref name="target"/>).</summary>
        public virtual int AuraAttackModifier(DuelEngine engine, DuelMonsterState source, DuelMonsterState target) => 0;
        /// <summary>A card destroyed instead of this monster when it would be destroyed by battle.</summary>
        public virtual DuelCard BattleSubstitute(DuelEngine engine, DuelMonsterState self) => null;
        /// <summary>Battle damage its controller takes from battles involving it is also dealt to the opponent.</summary>
        public virtual bool MirrorsBattleDamage(DuelEngine engine, DuelMonsterState self) => false;
    }

    /// <summary>Registry of implemented card effects, looked up by card name.</summary>
    public static class CardEffects
    {
        private static readonly Dictionary<string, CardEffect> Named = new(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, MonsterEffect> Monsters = new(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, CardEffect> Parsed = new(StringComparer.OrdinalIgnoreCase);
        private static readonly HashSet<string> Unparseable = new(StringComparer.OrdinalIgnoreCase);

        static CardEffects()
        {
            // ---- Draw / hand
            Named["Pot of Greed"] = new DrawSpell(2);
            Named["Upstart Goblin"] = new UpstartGoblin();
            Named["Graceful Charity"] = new GracefulCharity();
            Named["Card Destruction"] = new CardDestruction();

            // ---- Mass / targeted removal
            Named["Dark Hole"] = new DarkHole();
            Named["Heavy Storm"] = new HeavyStorm();
            Named["Harpie's Feather Duster"] = new HarpiesFeatherDuster();
            Named["Mystical Space Typhoon"] = new DestroySpellTrap(quickPlay: true);
            Named["Dust Tornado"] = new DestroySpellTrap(quickPlay: false, opponentOnly: true);
            Named["Smashing Ground"] = new SmashingGround();
            Named["Nobleman of Crossout"] = new NoblemanOfCrossout();

            // ---- Control / position
            Named["Change of Heart"] = new TakeControl(lifeCost: 0, faceUpOnly: false);
            Named["Brain Control"] = new TakeControl(lifeCost: 800, faceUpOnly: true);
            Named["Enemy Controller"] = new EnemyController();
            Named["Book of Moon"] = new BookOfMoon();
            Named["Swords of Revealing Light"] = new SwordsOfRevealingLight();
            Named["Spellbinding Circle"] = new SpellbindingCircle();

            // ---- Revival
            Named["Premature Burial"] = new Revive(lifeCost: 800, equip: true);
            Named["Call of the Haunted"] = new Revive(lifeCost: 0, equip: false);

            // ---- Stat changes
            Named["Rush Recklessly"] = new TempBoost(700);
            Named["Black Pendant"] = new EquipBoost(500, 0, burnOnGraveyard: 500);
            Named["Axe of Despair"] = new EquipBoost(1000, 0);

            // ---- Attack responses
            Named["Mirror Force"] = new MirrorForce();
            Named["Magic Cylinder"] = new MagicCylinder();
            Named["Negate Attack"] = new NegateAttack();
            Named["Magical Hats"] = new MagicalHats();
            Named["Sakuretsu Armor"] = new SakuretsuArmor();
            Named["Threatening Roar"] = new ThreateningRoar();

            // ---- Summon responses
            Named["Trap Hole"] = new SummonTrap(minimumAttack: 1000, banish: false, includeSpecial: false);
            Named["Bottomless Trap Hole"] = new SummonTrap(minimumAttack: 1500, banish: true, includeSpecial: true);
            Named["Torrential Tribute"] = new TorrentialTribute();

            // ---- Counter Traps
            Named["Solemn Judgment"] = new SolemnJudgment();
            Named["Magic Jammer"] = new MagicJammer();
            Named["Seven Tools of the Bandit"] = new SevenTools();

            // ---- Burn
            Named["Just Desserts"] = new JustDesserts();

            // ---- Monsters with passive stat effects
            Monsters["Buster Blader"] = new BusterBlader();
            Monsters["Nightmare Penguin"] = new NightmarePenguinAura();
            Monsters["Blade Knight"] = new BladeKnight();
            Monsters["Relinquished"] = new RelinquishedStats();
        }

        public static CardEffect Get(CardData card)
        {
            if (card == null || card.kind == CardKind.Monster) return null;
            if (Named.TryGetValue(card.cardName, out CardEffect effect)) return effect;
            if (CardEffectLibrary.TryGet(card.cardName, out effect)) return effect;
            if (Parsed.TryGetValue(card.cardName, out effect)) return effect;
            if (Unparseable.Contains(card.cardName)) return null;

            effect = GenericEffects.TryParse(card);
            if (effect != null) Parsed[card.cardName] = effect;
            else Unparseable.Add(card.cardName);
            return effect;
        }

        public static MonsterEffect GetMonster(CardData card)
        {
            if (card == null || card.kind != CardKind.Monster) return null;
            return Monsters.TryGetValue(card.cardName, out MonsterEffect effect) ? effect : null;
        }

        public static bool IsImplemented(CardData card) => card != null && (card.kind == CardKind.Monster || Get(card) != null);

        // ------------------------------------------------------------------ targeting helpers

        internal static TargetRequest Request(IEnumerable<DuelCard> candidates, string prompt, string context, int min = 1, int max = 1)
        {
            return new TargetRequest { Candidates = candidates.ToList(), Prompt = prompt, Context = context, Min = min, Max = max };
        }

        internal static bool IsOpponentTurnTrigger(EffectContext ctx, DuelTriggerKind kind) =>
            ctx.Trigger != null && ctx.Trigger.Kind == kind && ctx.Trigger.Player != ctx.Player;
    }

    // =====================================================================================
    //  Generic effects parsed from common card text patterns
    // =====================================================================================

    internal static class GenericEffects
    {
        private const RegexOptions Opt = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;

        // Equip Spells: conditional or unusual equips are rejected so they are never mis-played.
        private static readonly string[] EquipRejects =
        {
            "for each", "special summon", "during damage calculation", "cannot attack", "only a monster", "only equip",
            "take control", "level", "standby phase", "select 1", "declare", "send 1 card", "can only be used", "is treated as"
        };

        private static readonly Regex[] EquipRestrictions =
        {
            new(@"Equip only to (?:a |an )?(?<t>""[^""]+"")", Opt),
            new(@"Equip only to (?:a|an) (?<t>.+?) monster", Opt),
            new(@"^(?:A|An) (?<t>.+?) monster equipped", Opt),
            new(@"of (?:a|an) (?<t>.+?) monster equipped", Opt)
        };

        private static readonly (Regex pattern, string kind)[] EquipStats =
        {
            (new Regex(@"gains? (\d+) ATK/DEF", Opt), "both"),
            (new Regex(@"gains? (\d+) ATK and DEF", Opt), "both"),
            (new Regex(@"gains? (\d+) ATK and loses? (\d+) DEF", Opt), "atk-def"),
            (new Regex(@"increases its ATK and DEF by (\d+)", Opt), "both"),
            (new Regex(@"Increase the ATK and DEF of .*? by (\d+)", Opt), "both"),
            (new Regex(@"increases its ATK by (\d+) points and decreases its DEF by (\d+)", Opt), "atk-def"),
            (new Regex(@"increases its ATK by (\d+)", Opt), "atk"),
            (new Regex(@"Increase the DEF of .*? by (\d+)", Opt), "def"),
            (new Regex(@"loses? (\d+) ATK/DEF", Opt), "-both"),
            (new Regex(@"loses? (\d+) ATK and DEF", Opt), "-both"),
            (new Regex(@"gains? (\d+) ATK", Opt), "atk"),
            (new Regex(@"gains? (\d+) DEF", Opt), "def"),
            (new Regex(@"loses? (\d+) ATK", Opt), "-atk")
        };

        // Field Spells: "All X monsters gain 200 ATK/DEF", "All X monsters gain 500 ATK and lose 400 DEF", ...
        private static readonly (Regex pattern, string kind)[] FieldStats =
        {
            (new Regex(@"All (?<t>.+?) monsters(?: on the field)? gain (?<a>\d+) ATK and lose (?<d>\d+) DEF", Opt), "atk-def"),
            (new Regex(@"All (?<t>.+?) monsters(?: on the field)? gain (?<a>\d+) ATK/DEF", Opt), "both"),
            (new Regex(@"All (?<t>.+?) monsters(?: on the field)? gain (?<a>\d+) ATK and DEF", Opt), "both"),
            (new Regex(@"all (?<t>.+?) monsters(?: on the field)? lose (?<a>\d+) ATK/DEF", Opt), "-both"),
            (new Regex(@"Increase the ATK of all (?<t>.+?) monsters by (?<a>\d+) points and decreases? their DEF by (?<d>\d+) points", Opt), "atk-def"),
            (new Regex(@"All (?<t>.+?) monsters(?: on the field)? gain (?<a>\d+) ATK\b", Opt), "atk")
        };

        private static readonly Regex InflictOpponent = new(@"^Inflict (\d+) damage to your opponent\.?$", Opt);
        private static readonly Regex GainLp = new(@"^(?:Increase your Life Points by|You gain|Gain) (\d+) (?:Life Points|LP)\.?$", Opt);
        private static readonly Regex ClauseSplit = new(@"(?<=\.)\s+|,\s+also\s+|;\s+", Opt);

        public static CardEffect TryParse(CardData card)
        {
            string text = (card.effectText ?? string.Empty).Replace("\r", " ").Replace("\n", " ").Trim();
            if (text.Length == 0 || card.kind != CardKind.Spell) return null;

            if (card.typeLine == "Normal")
            {
                Match burn = InflictOpponent.Match(text);
                if (burn.Success) return new BurnSpell(int.Parse(burn.Groups[1].Value));
                Match heal = GainLp.Match(text);
                if (heal.Success) return new HealSpell(int.Parse(heal.Groups[1].Value));
                return null;
            }

            if (DuelRules.IsEquip(card)) return ParseEquip(text);
            if (DuelRules.IsFieldSpell(card)) return ParseField(text);
            return null;
        }

        private static CardEffect ParseEquip(string text)
        {
            string lower = text.ToLowerInvariant();
            if (EquipRejects.Any(reject => lower.Contains(reject))) return null;

            foreach ((Regex pattern, string kind) in EquipStats)
            {
                Match m = pattern.Match(text);
                if (!m.Success) continue;
                int a = int.Parse(m.Groups[1].Value);
                int second = m.Groups.Count > 2 && m.Groups[2].Success ? int.Parse(m.Groups[2].Value) : 0;
                (int atk, int def) = kind switch
                {
                    "both" => (a, a),
                    "atk-def" => (a, -second),
                    "atk" => (a, 0),
                    "def" => (0, a),
                    "-both" => (-a, -a),
                    "-atk" => (-a, 0),
                    _ => (0, 0)
                };
                if (atk == 0 && def == 0) return null;

                string restriction = null;
                foreach (Regex r in EquipRestrictions)
                {
                    Match rm = r.Match(text);
                    if (!rm.Success) continue;
                    restriction = rm.Groups["t"].Value.Replace("-Type", string.Empty).Trim('"', ' ');
                    int orIndex = restriction.IndexOf("\" or", StringComparison.Ordinal);
                    if (orIndex > 0) restriction = restriction.Substring(0, orIndex).Trim('"', ' ');
                    break;
                }
                return new EquipBoost(atk, def, restriction: restriction);
            }
            return null;
        }

        private static CardEffect ParseField(string text)
        {
            if (text.IndexOf("you control", StringComparison.OrdinalIgnoreCase) >= 0) return null;
            var boosts = new List<(string type, int atk, int def)>();
            foreach (string clause in ClauseSplit.Split(text))
            {
                foreach ((Regex pattern, string kind) in FieldStats)
                {
                    Match m = pattern.Match(clause);
                    if (!m.Success) continue;
                    int a = int.Parse(m.Groups["a"].Value);
                    int d = m.Groups["d"].Success && m.Groups["d"].Value.Length > 0 ? int.Parse(m.Groups["d"].Value) : 0;
                    (int atk, int def) = kind switch
                    {
                        "atk-def" => (a, -d),
                        "both" => (a, a),
                        "-both" => (-a, -a),
                        _ => (a, 0)
                    };
                    foreach (string t in SplitTypes(m.Groups["t"].Value)) boosts.Add((t, atk, def));
                    break;
                }
            }
            return boosts.Count > 0 ? new FieldBoostSpell(boosts) : null;
        }

        private static IEnumerable<string> SplitTypes(string raw)
        {
            return raw.Replace("-Type", string.Empty).Replace(" and ", ",").Replace(" or ", ",").Split(',')
                .Select(s => s.Trim())
                .Where(s => s.Length > 0);
        }
    }

    // =====================================================================================
    //  Effect implementations
    // =====================================================================================

    internal sealed class DrawSpell : CardEffect
    {
        private readonly int _count;
        public DrawSpell(int count) { _count = count; }
        public override bool CanActivate(EffectContext ctx) => ctx.Me.Deck.Count >= _count;
        public override void Resolve(EffectContext ctx) { ctx.Engine.Draw(ctx.Player, _count); ctx.Finish(); }
        public override int AiValue(EffectContext ctx) => 100;
    }

    internal sealed class UpstartGoblin : CardEffect
    {
        public override bool CanActivate(EffectContext ctx) => ctx.Me.Deck.Count >= 1;
        public override void Resolve(EffectContext ctx)
        {
            ctx.Engine.Draw(ctx.Player, 1);
            ctx.Engine.GainLife(ctx.OpponentIndex, 1000, ctx.Card);
            ctx.Finish();
        }
        public override int AiValue(EffectContext ctx) => 70;
    }

    internal sealed class GracefulCharity : CardEffect
    {
        public override bool CanActivate(EffectContext ctx) => ctx.Me.Deck.Count >= 3;
        public override void Resolve(EffectContext ctx)
        {
            if (!ctx.Engine.Draw(ctx.Player, 3)) { ctx.Finish(); return; }
            List<DuelCard> hand = ctx.Me.Hand.ToList();
            int discard = Math.Min(2, hand.Count);
            if (discard == 0) { ctx.Finish(); return; }
            ctx.Engine.Ask(new DuelChoice
            {
                Player = ctx.Player,
                Title = "Graceful Charity",
                Prompt = "Discard 2 cards.",
                SourceCard = ctx.Card,
                Candidates = hand,
                MinCount = discard,
                MaxCount = discard,
                Context = "discard",
                OnCards = picks =>
                {
                    foreach (DuelCard c in picks) ctx.Engine.Discard(c);
                    ctx.Finish();
                }
            });
        }
        public override int AiValue(EffectContext ctx) => 90;
    }

    internal sealed class CardDestruction : CardEffect
    {
        public override bool CanActivate(EffectContext ctx) => ctx.Me.Hand.Count > 1 || ctx.Opp.Hand.Count > 0;
        public override void Resolve(EffectContext ctx)
        {
            foreach (DuelistState d in ctx.Engine.Duelists)
            {
                int count = d.Hand.Count;
                foreach (DuelCard c in d.Hand.ToList()) ctx.Engine.Discard(c);
                if (!ctx.Engine.Draw(d.Index, count)) break;
            }
            ctx.Finish();
        }
        public override int AiValue(EffectContext ctx) => ctx.Me.Hand.Count <= 2 && ctx.Opp.Hand.Count >= 3 ? 45 : 0;
    }

    internal sealed class DarkHole : CardEffect
    {
        public override bool CanActivate(EffectContext ctx) => ctx.AllMonsters.Any();
        public override void Resolve(EffectContext ctx)
        {
            foreach (DuelMonsterState m in ctx.AllMonsters.ToList()) ctx.Engine.Destroy(m.Card, ctx.Card);
            ctx.Finish();
        }
        public override int AiValue(EffectContext ctx)
        {
            int theirs = ctx.Opp.MonstersOnField.Sum(m => 1000 + ctx.Engine.GetAttack(m));
            int mine = ctx.Me.MonstersOnField.Sum(m => 1000 + ctx.Engine.GetAttack(m));
            return theirs - mine >= 1500 ? 90 : 0;
        }
    }

    internal sealed class HeavyStorm : CardEffect
    {
        public override bool CanActivate(EffectContext ctx) => ctx.AllBackrow.Any(s => s.Card != ctx.Card);
        public override void Resolve(EffectContext ctx)
        {
            foreach (DuelBackrowState s in ctx.AllBackrow.Where(s => s.Card != ctx.Card).ToList())
                ctx.Engine.Destroy(s.Card, ctx.Card);
            ctx.Finish();
        }
        public override int AiValue(EffectContext ctx)
        {
            int theirs = ctx.Opp.AllBackrow.Count();
            int mine = ctx.Me.AllBackrow.Count(s => s.Card != ctx.Card);
            return theirs >= 2 && theirs > mine ? 80 : 0;
        }
    }

    internal sealed class HarpiesFeatherDuster : CardEffect
    {
        public override bool CanActivate(EffectContext ctx) => ctx.Opp.AllBackrow.Any();
        public override void Resolve(EffectContext ctx)
        {
            foreach (DuelBackrowState s in ctx.Opp.AllBackrow.ToList()) ctx.Engine.Destroy(s.Card, ctx.Card);
            ctx.Finish();
        }
        public override int AiValue(EffectContext ctx) => ctx.Opp.AllBackrow.Count() >= 1 ? 85 : 0;
    }

    internal sealed class DestroySpellTrap : CardEffect
    {
        private readonly bool _quickPlay;
        private readonly bool _opponentOnly;
        public DestroySpellTrap(bool quickPlay, bool opponentOnly = false) { _quickPlay = quickPlay; _opponentOnly = opponentOnly; }

        private IEnumerable<DuelCard> Candidates(EffectContext ctx) =>
            (_opponentOnly ? ctx.Opp.AllBackrow : ctx.AllBackrow).Where(s => s.Card != ctx.Card).Select(s => s.Card);

        public override bool CanActivate(EffectContext ctx) => Candidates(ctx).Any();
        public override bool CanRespond(EffectContext ctx) =>
            ctx.Trigger != null && ctx.Trigger.Player != ctx.Player && Candidates(ctx).Any() &&
            (ctx.Trigger.Kind == DuelTriggerKind.AttackDeclared || ctx.Trigger.Kind == DuelTriggerKind.SpellActivated || ctx.Trigger.Kind == DuelTriggerKind.TrapActivated);

        public override TargetRequest Targets(EffectContext ctx) =>
            CardEffects.Request(Candidates(ctx), "Target 1 Spell/Trap Card to destroy.", "destroy-backrow");

        public override void Resolve(EffectContext ctx)
        {
            DuelCard target = ctx.Target;
            if (target != null && target.OnField) ctx.Engine.Destroy(target, ctx.Card);
            ctx.Finish();
        }

        public override int AiValue(EffectContext ctx) => ctx.Opp.AllBackrow.Any() ? 60 : 0;
    }

    internal sealed class SmashingGround : CardEffect
    {
        public override bool CanActivate(EffectContext ctx) => ctx.Opp.MonstersOnField.Any(m => m.IsFaceUp);
        public override void Resolve(EffectContext ctx)
        {
            DuelMonsterState target = ctx.Opp.MonstersOnField.Where(m => m.IsFaceUp)
                .OrderByDescending(m => ctx.Engine.GetDefense(m)).FirstOrDefault();
            if (target != null) ctx.Engine.Destroy(target.Card, ctx.Card);
            ctx.Finish();
        }
        public override int AiValue(EffectContext ctx) => 75;
    }

    internal sealed class NoblemanOfCrossout : CardEffect
    {
        private static IEnumerable<DuelCard> Candidates(EffectContext ctx) => ctx.AllMonsters.Where(m => m.IsFaceDown).Select(m => m.Card);
        public override bool CanActivate(EffectContext ctx) => Candidates(ctx).Any();
        public override TargetRequest Targets(EffectContext ctx) =>
            CardEffects.Request(Candidates(ctx), "Target 1 face-down monster to banish.", "destroy-monster");
        public override void Resolve(EffectContext ctx)
        {
            DuelMonsterState m = ctx.Engine.FindMonster(ctx.Target);
            if (m != null && m.IsFaceDown) ctx.Engine.Banish(m.Card, ctx.Card);
            ctx.Finish();
        }
        public override int AiValue(EffectContext ctx) => ctx.Opp.MonstersOnField.Any(m => m.IsFaceDown) ? 70 : 0;
    }

    internal sealed class TakeControl : CardEffect
    {
        private readonly int _lifeCost;
        private readonly bool _faceUpOnly;
        public TakeControl(int lifeCost, bool faceUpOnly) { _lifeCost = lifeCost; _faceUpOnly = faceUpOnly; }

        private IEnumerable<DuelCard> Candidates(EffectContext ctx) =>
            ctx.Opp.MonstersOnField.Where(m => !_faceUpOnly || m.IsFaceUp).Select(m => m.Card);

        public override bool CanActivate(EffectContext ctx) =>
            ctx.Me.HasFreeMonsterZone && ctx.Me.LifePoints > _lifeCost && Candidates(ctx).Any();

        public override TargetRequest Targets(EffectContext ctx) =>
            CardEffects.Request(Candidates(ctx), "Target 1 opponent's monster to take control of until the End Phase.", "steal");

        public override void PayCost(EffectContext ctx, Action paid)
        {
            if (_lifeCost > 0) ctx.Engine.PayLife(ctx.Player, _lifeCost, ctx.Card);
            paid();
        }

        public override void Resolve(EffectContext ctx)
        {
            DuelMonsterState m = ctx.Engine.FindMonster(ctx.Target);
            if (m != null && m.Card.Controller == ctx.OpponentIndex)
            {
                ctx.Engine.SwitchControl(m, ctx.Player, temporary: true);
                if (m.IsFaceDown) ctx.Engine.ForcePosition(m, DuelMonsterPosition.FaceUpDefense);
            }
            ctx.Finish();
        }

        public override int AiValue(EffectContext ctx) =>
            ctx.Engine.Phase == DuelPhase.Main1 && ctx.Opp.MonstersOnField.Any(m => m.IsFaceUp && ctx.Engine.GetAttack(m) >= 1500) ? 80 : 0;
    }

    internal sealed class EnemyController : CardEffect
    {
        private static IEnumerable<DuelCard> Candidates(EffectContext ctx) => ctx.Opp.MonstersOnField.Where(m => m.IsFaceUp).Select(m => m.Card);
        public override bool CanActivate(EffectContext ctx) => Candidates(ctx).Any();
        public override bool CanRespond(EffectContext ctx) =>
            CardEffects.IsOpponentTurnTrigger(ctx, DuelTriggerKind.AttackDeclared) && ctx.Trigger.Attacker != null;
        public override TargetRequest Targets(EffectContext ctx)
        {
            if (ctx.Trigger?.Attacker != null) return CardEffects.Request(new[] { ctx.Trigger.Attacker.Card }, "Change the attacker's battle position.", "position");
            return CardEffects.Request(Candidates(ctx), "Target 1 face-up opponent's monster to change its battle position.", "position");
        }
        public override void Resolve(EffectContext ctx)
        {
            DuelMonsterState m = ctx.Engine.FindMonster(ctx.Target);
            if (m != null && m.IsFaceUp)
                ctx.Engine.ForcePosition(m, m.IsAttackPosition ? DuelMonsterPosition.FaceUpDefense : DuelMonsterPosition.FaceUpAttack);
            ctx.Finish();
        }
        public override int AiValue(EffectContext ctx) =>
            ctx.Engine.Phase == DuelPhase.Main1 && ctx.Opp.MonstersOnField.Any(m => m.IsAttackPosition && ctx.Engine.GetAttack(m) >= 2000) ? 50 : 0;
    }

    internal sealed class BookOfMoon : CardEffect
    {
        private static IEnumerable<DuelCard> Candidates(EffectContext ctx) => ctx.AllMonsters.Where(m => m.IsFaceUp).Select(m => m.Card);
        public override bool CanActivate(EffectContext ctx) => ctx.Opp.MonstersOnField.Any(m => m.IsFaceUp);
        public override bool CanRespond(EffectContext ctx) =>
            CardEffects.IsOpponentTurnTrigger(ctx, DuelTriggerKind.AttackDeclared) && ctx.Trigger.Attacker != null;
        public override TargetRequest Targets(EffectContext ctx)
        {
            if (ctx.Trigger?.Attacker != null) return CardEffects.Request(new[] { ctx.Trigger.Attacker.Card }, "Change the attacker to face-down Defense Position.", "position");
            return CardEffects.Request(Candidates(ctx), "Target 1 face-up monster to change to face-down Defense Position.", "position");
        }
        public override void Resolve(EffectContext ctx)
        {
            DuelMonsterState m = ctx.Engine.FindMonster(ctx.Target);
            if (m != null && m.IsFaceUp) ctx.Engine.ForcePosition(m, DuelMonsterPosition.FaceDownDefense);
            ctx.Finish();
        }
        public override int AiValue(EffectContext ctx) =>
            ctx.Engine.Phase == DuelPhase.Main1 && ctx.Opp.MonstersOnField.Any(m => m.IsFaceUp && ctx.Engine.GetAttack(m) >= 2200) ? 45 : 0;
    }

    internal sealed class SwordsOfRevealingLight : CardEffect
    {
        public override bool StaysOnField => true;
        public override bool CanActivate(EffectContext ctx) => true;
        public override void Resolve(EffectContext ctx)
        {
            foreach (DuelMonsterState m in ctx.Opp.MonstersOnField.Where(m => m.IsFaceDown).ToList())
                ctx.Engine.ForcePosition(m, DuelMonsterPosition.FaceUpDefense);
            if (ctx.Source != null) ctx.Source.TurnsRemaining = 3;
            ctx.Finish();
        }
        public override bool PreventsOpponentAttacks(DuelEngine engine, DuelBackrowState source) => source.TurnsRemaining > 0;
        public override int AiValue(EffectContext ctx) =>
            ctx.Opp.MonstersOnField.Sum(m => ctx.Engine.GetAttack(m)) > ctx.Me.MonstersOnField.Sum(m => ctx.Engine.GetAttack(m)) ? 70 : 10;
    }

    internal sealed class SpellbindingCircle : CardEffect
    {
        public override bool StaysOnField => true;
        public override bool LocksLinkedMonster => true;
        private static IEnumerable<DuelCard> Candidates(EffectContext ctx) => ctx.Opp.MonstersOnField.Where(m => m.IsFaceUp).Select(m => m.Card);
        public override bool CanActivate(EffectContext ctx) => Candidates(ctx).Any();
        public override bool CanRespond(EffectContext ctx) => CardEffects.IsOpponentTurnTrigger(ctx, DuelTriggerKind.AttackDeclared);
        public override TargetRequest Targets(EffectContext ctx)
        {
            if (ctx.Trigger?.Attacker != null) return CardEffects.Request(new[] { ctx.Trigger.Attacker.Card }, "Bind the attacking monster.", "lock");
            return CardEffects.Request(Candidates(ctx), "Target 1 opponent's monster: it cannot attack or change its battle position.", "lock");
        }
        public override void Resolve(EffectContext ctx)
        {
            DuelMonsterState m = ctx.Engine.FindMonster(ctx.Target);
            if (m != null && ctx.Source != null)
            {
                ctx.Source.LinkedMonster = m;
                m.CannotChangePosition = true;
            }
            ctx.Finish();
        }
        public override void OnLeaveField(DuelEngine engine, DuelBackrowState source)
        {
            if (source.LinkedMonster != null) source.LinkedMonster.CannotChangePosition = false;
        }
        public override int AiValue(EffectContext ctx) => ctx.Opp.MonstersOnField.Any(m => m.IsFaceUp && ctx.Engine.GetAttack(m) >= 1900) ? 55 : 0;
    }

    internal sealed class Revive : CardEffect
    {
        private readonly int _lifeCost;
        private readonly bool _equip;
        public Revive(int lifeCost, bool equip) { _lifeCost = lifeCost; _equip = equip; }
        public override bool StaysOnField => true;

        private static IEnumerable<DuelCard> Candidates(EffectContext ctx) =>
            ctx.Me.Graveyard.Where(c => c.IsMonster && !c.IsExtraDeckCard && DuelRules.CanEverBeNormalSummoned(c.Data));

        public override bool CanActivate(EffectContext ctx) =>
            ctx.Me.HasFreeMonsterZone && ctx.Me.LifePoints > _lifeCost && Candidates(ctx).Any();
        public override bool CanRespond(EffectContext ctx) =>
            !_equip && ctx.Trigger != null && ctx.Trigger.Kind == DuelTriggerKind.AttackDeclared && ctx.Trigger.Player != ctx.Player &&
            ctx.Me.HasFreeMonsterZone && Candidates(ctx).Any();

        public override TargetRequest Targets(EffectContext ctx) =>
            CardEffects.Request(Candidates(ctx), "Target 1 monster in your Graveyard to Special Summon in Attack Position.", "revive");

        public override void PayCost(EffectContext ctx, Action paid)
        {
            if (_lifeCost > 0) ctx.Engine.PayLife(ctx.Player, _lifeCost, ctx.Card);
            paid();
        }

        public override void Resolve(EffectContext ctx)
        {
            DuelCard target = ctx.Target;
            if (target != null && target.Zone == DuelZone.Graveyard && ctx.Source != null && ctx.Card.OnField)
            {
                DuelMonsterState m = ctx.Engine.SpecialSummon(ctx.Player, target, DuelMonsterPosition.FaceUpAttack, ctx.Card);
                if (m != null)
                {
                    ctx.Source.LinkedMonster = m;
                    if (_equip)
                    {
                        ctx.Source.EquippedTo = m;
                        m.Equips.Add(ctx.Source);
                    }
                }
            }
            ctx.Finish();
        }

        public override bool ShouldLeaveAfterResolve(EffectContext ctx) => ctx.Source == null || ctx.Source.LinkedMonster == null;

        public override void OnLeaveField(DuelEngine engine, DuelBackrowState source)
        {
            DuelMonsterState linked = source.LinkedMonster;
            source.LinkedMonster = null;
            if (linked != null && linked.Card.Zone == DuelZone.Monster)
                engine.Destroy(linked.Card, source.Card);
        }

        public override int AiValue(EffectContext ctx) =>
            Candidates(ctx).Any(c => c.Data.attack >= 1500) ? 75 : 0;
    }

    internal sealed class TempBoost : CardEffect
    {
        private readonly int _amount;
        public TempBoost(int amount) { _amount = amount; }
        private static IEnumerable<DuelCard> Candidates(EffectContext ctx) => ctx.Me.MonstersOnField.Where(m => m.IsFaceUp).Select(m => m.Card);
        public override bool CanActivate(EffectContext ctx) => Candidates(ctx).Any();
        public override bool CanRespond(EffectContext ctx) =>
            ctx.Trigger != null && ctx.Trigger.Kind == DuelTriggerKind.AttackDeclared && ctx.Trigger.Defender != null &&
            ctx.Trigger.Defender.Card.Controller == ctx.Player && ctx.Trigger.Defender.IsFaceUp;
        public override TargetRequest Targets(EffectContext ctx)
        {
            if (ctx.Trigger?.Defender != null && ctx.Trigger.Defender.Card.Controller == ctx.Player)
                return CardEffects.Request(new[] { ctx.Trigger.Defender.Card }, $"Your monster gains {_amount} ATK.", "boost");
            return CardEffects.Request(Candidates(ctx), $"Target 1 face-up monster you control: it gains {_amount} ATK until the end of this turn.", "boost");
        }
        public override void Resolve(EffectContext ctx)
        {
            DuelMonsterState m = ctx.Engine.FindMonster(ctx.Target);
            if (m != null) m.TempAttack += _amount;
            ctx.Finish();
        }
        public override int AiValue(EffectContext ctx) => ctx.Engine.Phase == DuelPhase.Battle ? 40 : 0;
    }

    internal sealed class EquipBoost : CardEffect
    {
        private readonly int _atk;
        private readonly int _def;
        private readonly int _burnOnGraveyard;
        private readonly string _restriction;

        public EquipBoost(int atk, int def, int burnOnGraveyard = 0, string restriction = null)
        {
            _atk = atk;
            _def = def;
            _burnOnGraveyard = burnOnGraveyard;
            _restriction = restriction;
        }

        public override bool StaysOnField => true;

        private bool Allowed(DuelMonsterState m)
        {
            if (m == null || m.IsFaceDown) return false;
            if (string.IsNullOrEmpty(_restriction)) return true;
            string type = m.Card.Data.typeLine ?? string.Empty;
            string attribute = m.Card.Data.attribute ?? string.Empty;
            return type.IndexOf(_restriction, StringComparison.OrdinalIgnoreCase) >= 0 ||
                   attribute.Equals(_restriction, StringComparison.OrdinalIgnoreCase) ||
                   m.Name.IndexOf(_restriction, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private IEnumerable<DuelCard> Candidates(EffectContext ctx) => ctx.AllMonsters.Where(Allowed).Select(m => m.Card);
        public override bool CanActivate(EffectContext ctx) => Candidates(ctx).Any();
        public override TargetRequest Targets(EffectContext ctx)
        {
            string what = _atk >= 0 ? $"+{_atk} ATK" : $"{_atk} ATK";
            if (_def != 0) what += _def >= 0 ? $" / +{_def} DEF" : $" / {_def} DEF";
            return CardEffects.Request(Candidates(ctx), $"Equip to 1 face-up monster ({what}).", _atk >= 0 ? "boost" : "weaken");
        }

        public override void Resolve(EffectContext ctx)
        {
            DuelMonsterState m = ctx.Engine.FindMonster(ctx.Target);
            if (m != null && ctx.Source != null && Allowed(m))
            {
                ctx.Source.EquippedTo = m;
                m.Equips.Add(ctx.Source);
            }
            ctx.Finish();
        }

        public override bool ShouldLeaveAfterResolve(EffectContext ctx) => ctx.Source == null || ctx.Source.EquippedTo == null;
        public override int AttackModifier(DuelEngine engine, DuelBackrowState source, DuelMonsterState monster) => source.EquippedTo == monster ? _atk : 0;
        public override int DefenseModifier(DuelEngine engine, DuelBackrowState source, DuelMonsterState monster) => source.EquippedTo == monster ? _def : 0;

        public override void OnSentToGraveyardFromField(DuelEngine engine, DuelCard card)
        {
            if (_burnOnGraveyard > 0) engine.DealDamage(1 - card.Controller, _burnOnGraveyard, card, battle: false);
        }

        public override int AiValue(EffectContext ctx) => _atk > 0 && ctx.Me.MonstersOnField.Any(Allowed) ? 55 : _atk < 0 && ctx.Opp.MonstersOnField.Any(Allowed) ? 45 : 0;
    }

    internal sealed class FieldBoostSpell : CardEffect
    {
        private readonly List<(string type, int atk, int def)> _boosts;
        public FieldBoostSpell(List<(string type, int atk, int def)> boosts) { _boosts = boosts; }
        public override bool StaysOnField => true;
        public override void Resolve(EffectContext ctx) => ctx.Finish();

        private int Sum(DuelMonsterState m, bool atk)
        {
            string type = m.Card.Data.typeLine ?? string.Empty;
            string attribute = m.Card.Data.attribute ?? string.Empty;
            int total = 0;
            foreach (var b in _boosts)
            {
                bool match = type.IndexOf(b.type, StringComparison.OrdinalIgnoreCase) >= 0 ||
                             attribute.Equals(b.type, StringComparison.OrdinalIgnoreCase);
                if (match) total += atk ? b.atk : b.def;
            }
            return total;
        }

        public override int AttackModifier(DuelEngine engine, DuelBackrowState source, DuelMonsterState monster) => Sum(monster, true);
        public override int DefenseModifier(DuelEngine engine, DuelBackrowState source, DuelMonsterState monster) => Sum(monster, false);
        public override int AiValue(EffectContext ctx) => ctx.Me.MonstersOnField.Any(m => Sum(m, true) > 0) || ctx.Me.Hand.Any(c => c.IsMonster) ? 50 : 20;
    }

    internal sealed class BurnSpell : CardEffect
    {
        private readonly int _amount;
        public BurnSpell(int amount) { _amount = amount; }
        public override void Resolve(EffectContext ctx) { ctx.Engine.DealDamage(ctx.OpponentIndex, _amount, ctx.Card, false); ctx.Finish(); }
        public override int AiValue(EffectContext ctx) => 50;
    }

    internal sealed class HealSpell : CardEffect
    {
        private readonly int _amount;
        public HealSpell(int amount) { _amount = amount; }
        public override void Resolve(EffectContext ctx) { ctx.Engine.GainLife(ctx.Player, _amount, ctx.Card); ctx.Finish(); }
        public override int AiValue(EffectContext ctx) => ctx.Me.LifePoints < 4000 ? 40 : 5;
    }

    // ---------------------------------------------------------------- attack responses

    internal sealed class MirrorForce : CardEffect
    {
        public override bool CanActivate(EffectContext ctx) => false;
        public override bool CanRespond(EffectContext ctx) => CardEffects.IsOpponentTurnTrigger(ctx, DuelTriggerKind.AttackDeclared);
        public override void Resolve(EffectContext ctx)
        {
            foreach (DuelMonsterState m in ctx.Opp.MonstersOnField.Where(m => m.IsAttackPosition).ToList())
                ctx.Engine.Destroy(m.Card, ctx.Card);
            ctx.Finish();
        }
        public override int AiValue(EffectContext ctx) => ctx.Opp.MonstersOnField.Count(m => m.IsAttackPosition) >= 1 ? 95 : 0;
    }

    internal sealed class MagicCylinder : CardEffect
    {
        public override bool CanActivate(EffectContext ctx) => false;
        public override bool CanRespond(EffectContext ctx) => CardEffects.IsOpponentTurnTrigger(ctx, DuelTriggerKind.AttackDeclared);
        public override void Resolve(EffectContext ctx)
        {
            DuelMonsterState attacker = ctx.Trigger?.Attacker;
            ctx.Trigger.Negated = true;
            if (attacker != null && attacker.Card.Zone == DuelZone.Monster)
                ctx.Engine.DealDamage(ctx.OpponentIndex, ctx.Engine.GetAttack(attacker), ctx.Card, battle: false);
            ctx.Finish();
        }
        public override int AiValue(EffectContext ctx) => ctx.Trigger?.Attacker != null && ctx.Engine.GetAttack(ctx.Trigger.Attacker) >= 1400 ? 90 : 0;
    }

    internal sealed class NegateAttack : CardEffect
    {
        public override bool CanActivate(EffectContext ctx) => false;
        public override bool CanRespond(EffectContext ctx) => CardEffects.IsOpponentTurnTrigger(ctx, DuelTriggerKind.AttackDeclared);
        public override void Resolve(EffectContext ctx)
        {
            ctx.Trigger.Negated = true;
            ctx.Opp.CannotAttackThisTurn = true;   // "then end the Battle Phase"
            ctx.Finish();
        }
        public override int AiValue(EffectContext ctx)
        {
            DuelMonsterState a = ctx.Trigger?.Attacker;
            if (a == null) return 0;
            int threat = ctx.Opp.MonstersOnField.Where(m => m.IsAttackPosition && !m.HasAttacked).Sum(m => ctx.Engine.GetAttack(m)) + ctx.Engine.GetAttack(a);
            return threat >= 2500 || ctx.Trigger.Defender == null && ctx.Engine.GetAttack(a) >= ctx.Me.LifePoints ? 85 : 0;
        }
    }

    /// <summary>
    /// Magical Hats: when the opponent attacks, send 2 Spells/Traps from your Deck to the Graveyard and put 2 "Magical Hat"
    /// decoys (0/0, face-down) on the field, set one of your monsters face-down, then shuffle the three. The attack lands on a
    /// random hat. The decoys are destroyed when the Battle Phase ends. (The 2 Deck cards stand in for the decoys, which is why
    /// they go straight to the Graveyard.)
    /// </summary>
    internal sealed class MagicalHats : CardEffect
    {
        public override bool CanActivate(EffectContext ctx) => false;
        public override bool CanRespond(EffectContext ctx) =>
            CardEffects.IsOpponentTurnTrigger(ctx, DuelTriggerKind.AttackDeclared) &&
            ctx.Me.MonsterCount >= 1 && ctx.Me.Monsters.Count(m => m == null) >= 2 &&
            ctx.Me.Deck.Count(c => c.IsSpell || c.IsTrap) >= 2;

        public override void Resolve(EffectContext ctx)
        {
            var spellsTraps = ctx.Me.Deck.Where(c => c.IsSpell || c.IsTrap).ToList();
            if (spellsTraps.Count < 2 || ctx.Me.MonsterCount == 0) { ctx.Finish(); return; }
            ctx.Engine.Ask(new DuelChoice
            {
                Player = ctx.Player,
                Title = "Magical Hats",
                Prompt = "Choose 2 Spell/Trap Cards from your Deck to hide under the hats.",
                SourceCard = ctx.Card,
                Candidates = spellsTraps,
                MinCount = 2,
                MaxCount = 2,
                Context = "deck-to-graveyard",
                OnCards = picks => Hide(ctx, picks)
            });
        }

        private static void Hide(EffectContext ctx, List<DuelCard> picks)
        {
            DuelEngine e = ctx.Engine;
            foreach (DuelCard c in picks) e.SendToGraveyard(c, destroyed: false, cause: ctx.Card);

            // The monster under the hats: the one being attacked, else our strongest.
            DuelMonsterState hidden = ctx.Trigger?.Defender != null && ctx.Me.FindMonster(ctx.Trigger.Defender.Card) == ctx.Trigger.Defender
                ? ctx.Trigger.Defender
                : ctx.Me.MonstersOnField.OrderByDescending(m => e.GetAttack(m)).FirstOrDefault();
            if (hidden == null) { ctx.Finish(); return; }
            if (hidden.IsFaceUp)
            {
                hidden.Position = DuelMonsterPosition.FaceDownDefense;
                hidden.Card.FaceUp = false;
                hidden.PositionSetTurn = e.TurnNumber;
            }

            var hats = new List<DuelMonsterState> { hidden };
            for (int i = 0; i < 2; i++)
            {
                DuelMonsterState hat = e.SummonToken(ctx.Player, "Magical Hat", "Spellcaster", "DARK", 1, 0, 0, DuelMonsterPosition.FaceDownDefense, ctx.Card);
                if (hat == null) continue;
                hats.Add(hat);
                e.DestroyAtEndOfBattle.Add(hat.Card);
            }

            // Shuffle the three between their zones.
            var slots = hats.Select(h => h.Slot).ToList();
            var order = slots.OrderBy(_ => e.RandomRange(0, 1 << 20)).ToList();
            foreach (DuelMonsterState h in hats) ctx.Me.Monsters[h.Slot] = null;
            for (int i = 0; i < hats.Count; i++)
            {
                hats[i].Slot = order[i];
                hats[i].Card.Slot = order[i];
                ctx.Me.Monsters[order[i]] = hats[i];
            }
            foreach (DuelMonsterState h in hats) e.MagicalHatsCovered.Add(h.Card.Uid);

            // The attacker must guess: the attack now lands on a random hat.
            if (ctx.Trigger != null) ctx.Trigger.Defender = hats[e.RandomRange(0, hats.Count)];
            e.Raise(DuelEventType.Message, ctx.Player, ctx.Card, text: $"{ctx.Me.Name} hid {hidden.Name} under Magical Hats! The attack lands on a random hat.");
            ctx.Finish();
        }

        public override int AiValue(EffectContext ctx)
        {
            DuelMonsterState a = ctx.Trigger?.Attacker;
            if (a == null) return 0;
            DuelMonsterState d = ctx.Trigger.Defender;
            // Worth it when the attack would destroy something we care about or hit us hard directly.
            return d != null && ctx.Engine.GetAttack(a) > (d.IsAttackPosition ? ctx.Engine.GetAttack(d) : ctx.Engine.GetDefense(d)) ? 75 : 0;
        }
    }

    internal sealed class SakuretsuArmor : CardEffect
    {
        public override bool CanActivate(EffectContext ctx) => false;
        public override bool CanRespond(EffectContext ctx) => CardEffects.IsOpponentTurnTrigger(ctx, DuelTriggerKind.AttackDeclared);
        public override void Resolve(EffectContext ctx)
        {
            DuelMonsterState attacker = ctx.Trigger?.Attacker;
            if (attacker != null && attacker.Card.Zone == DuelZone.Monster) ctx.Engine.Destroy(attacker.Card, ctx.Card);
            ctx.Finish();
        }
        public override int AiValue(EffectContext ctx) => ctx.Trigger?.Attacker != null && ctx.Engine.GetAttack(ctx.Trigger.Attacker) >= 1300 ? 90 : 0;
    }

    internal sealed class ThreateningRoar : CardEffect
    {
        public override bool CanActivate(EffectContext ctx) => false;
        public override bool CanRespond(EffectContext ctx) => CardEffects.IsOpponentTurnTrigger(ctx, DuelTriggerKind.AttackDeclared);
        public override void Resolve(EffectContext ctx)
        {
            ctx.Trigger.Negated = true;
            ctx.Opp.CannotAttackThisTurn = true;
            ctx.Finish();
        }
        public override int AiValue(EffectContext ctx) =>
            ctx.Trigger?.Attacker != null && (ctx.Trigger.Defender == null || ctx.Engine.GetAttack(ctx.Trigger.Attacker) >= 2000) ? 80 : 0;
    }

    // ---------------------------------------------------------------- summon responses

    internal sealed class SummonTrap : CardEffect
    {
        private readonly int _minimumAttack;
        private readonly bool _banish;
        private readonly bool _includeSpecial;

        public SummonTrap(int minimumAttack, bool banish, bool includeSpecial)
        {
            _minimumAttack = minimumAttack;
            _banish = banish;
            _includeSpecial = includeSpecial;
        }

        public override bool CanActivate(EffectContext ctx) => false;

        public override bool CanRespond(EffectContext ctx)
        {
            DuelTrigger t = ctx.Trigger;
            if (t == null || t.Player == ctx.Player || t.Card == null) return false;
            bool kind = t.Kind == DuelTriggerKind.NormalSummoned || t.Kind == DuelTriggerKind.FlipSummoned ||
                        (_includeSpecial && t.Kind == DuelTriggerKind.SpecialSummoned);
            DuelMonsterState m = ctx.Engine.FindMonster(t.Card);
            return kind && m != null && m.IsFaceUp && ctx.Engine.GetAttack(m) >= _minimumAttack;
        }

        public override void Resolve(EffectContext ctx)
        {
            DuelCard target = ctx.Trigger?.Card;
            if (target != null && target.Zone == DuelZone.Monster)
            {
                if (_banish)
                {
                    ctx.Engine.Destroy(target, ctx.Card);
                    ctx.Engine.Banish(target, ctx.Card);
                }
                else ctx.Engine.Destroy(target, ctx.Card);
            }
            ctx.Finish();
        }

        public override int AiValue(EffectContext ctx) => 90;
    }

    internal sealed class TorrentialTribute : CardEffect
    {
        public override bool CanActivate(EffectContext ctx) => false;
        public override bool CanRespond(EffectContext ctx)
        {
            DuelTrigger t = ctx.Trigger;
            return t != null && (t.Kind == DuelTriggerKind.NormalSummoned || t.Kind == DuelTriggerKind.FlipSummoned || t.Kind == DuelTriggerKind.SpecialSummoned);
        }
        public override void Resolve(EffectContext ctx)
        {
            foreach (DuelMonsterState m in ctx.AllMonsters.ToList()) ctx.Engine.Destroy(m.Card, ctx.Card);
            ctx.Finish();
        }
        public override int AiValue(EffectContext ctx)
        {
            int theirs = ctx.Opp.MonstersOnField.Sum(m => 800 + ctx.Engine.GetAttack(m));
            int mine = ctx.Me.MonstersOnField.Sum(m => 800 + ctx.Engine.GetAttack(m));
            return theirs - mine >= 1200 ? 85 : 0;
        }
    }

    // ---------------------------------------------------------------- counter traps

    internal sealed class SolemnJudgment : CardEffect
    {
        public override bool CanActivate(EffectContext ctx) => false;
        public override bool CanRespond(EffectContext ctx)
        {
            DuelTrigger t = ctx.Trigger;
            if (t == null || t.Player == ctx.Player) return false;
            return t.Kind == DuelTriggerKind.NormalSummoned || t.Kind == DuelTriggerKind.FlipSummoned || t.Kind == DuelTriggerKind.SpecialSummoned ||
                   t.Kind == DuelTriggerKind.SpellActivated || t.Kind == DuelTriggerKind.TrapActivated;
        }
        public override void PayCost(EffectContext ctx, Action paid)
        {
            int half = ctx.Me.LifePoints / 2;
            if (half > 0) ctx.Engine.PayLife(ctx.Player, half, ctx.Card);
            paid();
        }
        public override void Resolve(EffectContext ctx)
        {
            DuelTrigger t = ctx.Trigger;
            if (t.Kind == DuelTriggerKind.SpellActivated || t.Kind == DuelTriggerKind.TrapActivated)
            {
                t.Negated = true;
            }
            else if (t.Card != null && t.Card.Zone == DuelZone.Monster)
            {
                ctx.Engine.Raise(DuelEventType.SummonNegated, t.Player, t.Card, text: $"The summon of {t.Card.Name} was negated.");
                ctx.Engine.Destroy(t.Card, ctx.Card);
            }
            ctx.Finish();
        }
        public override int AiValue(EffectContext ctx)
        {
            DuelTrigger t = ctx.Trigger;
            if (t == null || ctx.Me.LifePoints < 2000) return 0;
            if (t.Card != null && t.Card.IsMonster) return t.Card.Data.attack >= 2300 ? 85 : 0;
            if (t.Card != null)
            {
                string n = t.Card.Name;
                return n is "Dark Hole" or "Heavy Storm" or "Harpie's Feather Duster" or "Mirror Force" or "Change of Heart" or "Torrential Tribute" ? 85 : 0;
            }
            return 0;
        }
    }

    internal sealed class MagicJammer : CardEffect
    {
        public override bool CanActivate(EffectContext ctx) => false;
        public override bool CanRespond(EffectContext ctx) =>
            ctx.Trigger != null && ctx.Trigger.Kind == DuelTriggerKind.SpellActivated && ctx.Trigger.Player != ctx.Player && ctx.Me.Hand.Count > 0;
        public override void PayCost(EffectContext ctx, Action paid)
        {
            ctx.Engine.Ask(new DuelChoice
            {
                Player = ctx.Player,
                Title = "Magic Jammer",
                Prompt = "Discard 1 card to negate the Spell Card.",
                SourceCard = ctx.Card,
                Candidates = ctx.Me.Hand.ToList(),
                MinCount = 1,
                MaxCount = 1,
                Context = "discard",
                OnCards = picks =>
                {
                    foreach (DuelCard c in picks) ctx.Engine.Discard(c);
                    paid();
                }
            });
        }
        public override void Resolve(EffectContext ctx) { ctx.Trigger.Negated = true; ctx.Finish(); }
        public override int AiValue(EffectContext ctx)
        {
            string n = ctx.Trigger?.Card?.Name ?? string.Empty;
            return n is "Dark Hole" or "Heavy Storm" or "Harpie's Feather Duster" or "Change of Heart" or "Brain Control" or "Swords of Revealing Light" or "Pot of Greed" or "Graceful Charity" ? 80 : 0;
        }
    }

    internal sealed class SevenTools : CardEffect
    {
        public override bool CanActivate(EffectContext ctx) => false;
        public override bool CanRespond(EffectContext ctx) =>
            ctx.Trigger != null && ctx.Trigger.Kind == DuelTriggerKind.TrapActivated && ctx.Trigger.Player != ctx.Player && ctx.Me.LifePoints > 1000;
        public override void PayCost(EffectContext ctx, Action paid) { ctx.Engine.PayLife(ctx.Player, 1000, ctx.Card); paid(); }
        public override void Resolve(EffectContext ctx) { ctx.Trigger.Negated = true; ctx.Finish(); }
        public override int AiValue(EffectContext ctx) => 85;
    }

    internal sealed class JustDesserts : CardEffect
    {
        public override bool CanActivate(EffectContext ctx) => ctx.Opp.MonsterCount > 0;
        public override bool CanRespond(EffectContext ctx) => ctx.Trigger != null && ctx.Trigger.Player != ctx.Player && ctx.Opp.MonsterCount >= 2;
        public override void Resolve(EffectContext ctx)
        {
            ctx.Engine.DealDamage(ctx.OpponentIndex, 500 * ctx.Opp.MonsterCount, ctx.Card, false);
            ctx.Finish();
        }
        public override int AiValue(EffectContext ctx) => ctx.Opp.MonsterCount >= 2 ? 60 : 0;
    }

    // ---------------------------------------------------------------- monster passives

    internal sealed class BusterBlader : MonsterEffect
    {
        public override int SelfAttackModifier(DuelEngine engine, DuelMonsterState self)
        {
            DuelistState opp = engine.Opponent(self.Card.Controller);
            int dragons = opp.MonstersOnField.Count(m => m.IsFaceUp && (m.Card.Data.typeLine ?? "").Contains("Dragon")) +
                          opp.Graveyard.Count(c => c.IsMonster && (c.Data.typeLine ?? "").Contains("Dragon"));
            return 500 * dragons;
        }
    }
}
