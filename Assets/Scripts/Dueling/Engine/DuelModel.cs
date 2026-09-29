using System;
using System.Collections.Generic;
using System.Linq;
using DuelGenesis.Cards;

namespace DuelGenesis.Dueling
{
    // =====================================================================================
    //  Official Yu-Gi-Oh! TCG constants (current Master Rule).
    // =====================================================================================
    public static class DuelRules
    {
        public const int StartingLifePoints = 8000;
        public const int OpeningHandSize = 5;
        public const int HandSizeLimit = 6;          // checked in the End Phase
        public const int MonsterZoneCount = 5;       // Main Monster Zones
        public const int SpellTrapZoneCount = 5;
        public const int MainDeckMinimum = 40;
        public const int MainDeckMaximum = 60;
        public const int ExtraDeckMaximum = 15;
        public const int MaxCopies = 3;

        /// <summary>Tributes needed to Normal Summon/Set: Lv 5-6 = 1, Lv 7+ = 2.</summary>
        public static int TributesRequired(CardData card)
        {
            if (card == null || card.kind != CardKind.Monster) return 0;
            if (card.level >= 7) return 2;
            if (card.level >= 5) return 1;
            return 0;
        }

        public static bool IsExtraDeckMonster(CardData card)
        {
            if (card == null || card.kind != CardKind.Monster) return false;
            CardFrameKind frame = card.ResolvedFrameKind;
            return frame == CardFrameKind.FusionMonster || frame == CardFrameKind.SynchroMonster || frame == CardFrameKind.XyzMonster;
        }

        public static bool IsToken(CardData card) => card != null && card.ResolvedFrameKind == CardFrameKind.Token;

        /// <summary>Monsters that can never be Normal Summoned or Set (Extra Deck, Ritual,
        /// Tokens and "Cannot be Normal Summoned/Set" / "Must be Special Summoned" cards).</summary>
        public static bool CanEverBeNormalSummoned(CardData card)
        {
            if (card == null || card.kind != CardKind.Monster) return false;
            if (IsExtraDeckMonster(card) || IsToken(card)) return false;
            if (card.ResolvedFrameKind == CardFrameKind.RitualMonster) return false;
            string text = card.effectText ?? string.Empty;
            if (text.IndexOf("Cannot be Normal Summoned", StringComparison.OrdinalIgnoreCase) >= 0) return false;
            if (text.IndexOf("Must be Special Summoned", StringComparison.OrdinalIgnoreCase) >= 0) return false;
            if (text.IndexOf("Must first be Special Summoned", StringComparison.OrdinalIgnoreCase) >= 0) return false;
            return true;
        }

        public static bool IsQuickPlay(CardData card) => card != null && card.kind == CardKind.Spell && card.typeLine == "Quick-Play";
        public static bool IsFieldSpell(CardData card) => card != null && card.kind == CardKind.Spell && card.typeLine == "Field";
        public static bool IsContinuous(CardData card) => card != null && card.typeLine == "Continuous";
        public static bool IsEquip(CardData card) => card != null && card.kind == CardKind.Spell && card.typeLine == "Equip";
        public static bool IsCounterTrap(CardData card) => card != null && card.kind == CardKind.Trap && card.typeLine == "Counter";
    }

    public enum DuelPhase { Draw, Standby, Main1, Battle, Main2, End }

    public enum BattleStep { None, Start, Battle, Damage, End }

    public enum DuelZone { None, Deck, Hand, Monster, SpellTrap, FieldSpell, Graveyard, Banished, ExtraDeck }

    public enum DuelMonsterPosition { FaceUpAttack, FaceUpDefense, FaceDownDefense }

    // Kept for code written against the previous controller.
    public enum DuelTurnPhase { Main, Battle, Opponent, Finished }

    // =====================================================================================
    //  Physical cards and field state
    // =====================================================================================

    /// <summary>One physical card in the duel. Copies of the same card are separate objects.</summary>
    public sealed class DuelCard
    {
        public readonly int Uid;
        public readonly CardData Data;
        public readonly int Owner;

        public DuelZone Zone = DuelZone.None;
        public int Controller;
        public int Slot = -1;
        public bool FaceUp;

        public DuelCard(int uid, CardData data, int owner)
        {
            Uid = uid;
            Data = data;
            Owner = owner;
            Controller = owner;
        }

        public string Name => Data?.cardName ?? "?";
        public bool IsMonster => Data != null && Data.kind == CardKind.Monster;
        public bool IsSpell => Data != null && Data.kind == CardKind.Spell;
        public bool IsTrap => Data != null && Data.kind == CardKind.Trap;
        public bool IsExtraDeckCard => DuelRules.IsExtraDeckMonster(Data);
        public bool OnField => Zone == DuelZone.Monster || Zone == DuelZone.SpellTrap || Zone == DuelZone.FieldSpell;
        public override string ToString() => $"{Name}#{Uid}";
    }

    public sealed class DuelMonsterState
    {
        public DuelCard Card;
        public DuelMonsterPosition Position;
        public int Slot;
        public int SummonedTurn;
        public int PositionSetTurn;
        public bool HasAttacked;
        public bool HasChangedPosition;
        public int TempAttack;         // "until the end of this turn" modifiers
        public int TempDefense;
        public int ReturnControlTo = -1;  // Change of Heart / Brain Control
        public bool CannotAttackThisTurn;
        public bool CannotChangePosition;
        public int PermAttack;         // lasting changes from effects (Slate Warrior, Rigorous Reaver)
        public int PermDefense;
        public DuelCard ControlHeldBy;   // Charmers: control returns when this card leaves the field or turns face-down
        public bool WasFlipped;          // has been flipped face-up since it came to the field
        public bool SpecialSummoned;
        public bool CanAttackDirectly;   // Jowls of Dark Demise
        public readonly List<DuelBackrowState> Equips = new();

        public bool IsFaceDown => Position == DuelMonsterPosition.FaceDownDefense;
        public bool IsFaceUp => !IsFaceDown;
        public bool IsAttackPosition => Position == DuelMonsterPosition.FaceUpAttack;
        public bool IsDefensePosition => !IsAttackPosition;
        public int Level => Card.Data.level;
        public string Name => Card.Name;

        public DuelMonsterState(DuelCard card, DuelMonsterPosition position, int slot, int turn)
        {
            Card = card;
            Position = position;
            Slot = slot;
            SummonedTurn = turn;
            PositionSetTurn = turn;
        }
    }

    public sealed class DuelBackrowState
    {
        public DuelCard Card;
        public bool FaceDown;
        public int Slot;
        public int SetTurn;
        public DuelMonsterState EquippedTo;
        public DuelMonsterState LinkedMonster;   // Call of the Haunted, Spellbinding Circle, Premature Burial
        public int TurnsRemaining;               // Swords of Revealing Light
        public int Counter;                      // generic per-card counter (turns passed, stored value...)
        public bool Resolving;                   // a Normal Spell/Trap on the field only while it resolves

        public string Name => Card.Name;

        public DuelBackrowState(DuelCard card, bool faceDown, int slot, int turn)
        {
            Card = card;
            FaceDown = faceDown;
            Slot = slot;
            SetTurn = turn;
        }
    }

    public sealed class DuelistState
    {
        public readonly int Index;
        public string Name;
        public int LifePoints = DuelRules.StartingLifePoints;

        /// <summary>Main Deck; the top of the deck is the LAST element.</summary>
        public readonly List<DuelCard> Deck = new();
        public readonly List<DuelCard> Hand = new();
        public readonly List<DuelCard> Graveyard = new();
        public readonly List<DuelCard> Banished = new();
        public readonly List<DuelCard> ExtraDeck = new();
        public readonly DuelMonsterState[] Monsters = new DuelMonsterState[DuelRules.MonsterZoneCount];
        public readonly DuelBackrowState[] SpellTraps = new DuelBackrowState[DuelRules.SpellTrapZoneCount];
        public DuelBackrowState FieldSpell;

        public bool NormalSummonUsed;
        public bool CannotAttackThisTurn;     // Threatening Roar, Negate Attack
        public int SkipDraws;                  // Reckless Greed, Time Seal, Offerings to the Doomed

        public DuelistState(int index, string name)
        {
            Index = index;
            Name = name;
        }

        public IEnumerable<DuelMonsterState> MonstersOnField => Monsters.Where(m => m != null);
        public IEnumerable<DuelBackrowState> SpellTrapsOnField => SpellTraps.Where(s => s != null);
        public IEnumerable<DuelBackrowState> AllBackrow => FieldSpell != null ? SpellTrapsOnField.Append(FieldSpell) : SpellTrapsOnField;
        public int MonsterCount => Monsters.Count(m => m != null);
        public int SpellTrapCount => SpellTraps.Count(s => s != null);
        public bool HasFreeMonsterZone => MonsterCount < DuelRules.MonsterZoneCount;
        public bool HasFreeSpellTrapZone => SpellTrapCount < DuelRules.SpellTrapZoneCount;

        /// <summary>Preferred slot order: centre first, then outward (how most players fill a mat).</summary>
        private static readonly int[] FillOrder = { 2, 1, 3, 0, 4 };

        public int FreeMonsterSlot(int preferred = -1)
        {
            if (preferred >= 0 && preferred < Monsters.Length && Monsters[preferred] == null) return preferred;
            foreach (int i in FillOrder) if (Monsters[i] == null) return i;
            return -1;
        }

        public int FreeSpellTrapSlot(int preferred = -1)
        {
            if (preferred >= 0 && preferred < SpellTraps.Length && SpellTraps[preferred] == null) return preferred;
            foreach (int i in FillOrder) if (SpellTraps[i] == null) return i;
            return -1;
        }

        public DuelMonsterState FindMonster(DuelCard card) => Monsters.FirstOrDefault(m => m != null && m.Card == card);
        public DuelBackrowState FindBackrow(DuelCard card) =>
            FieldSpell != null && FieldSpell.Card == card ? FieldSpell : SpellTraps.FirstOrDefault(s => s != null && s.Card == card);
    }

    // =====================================================================================
    //  Events (what happened) and choices (what the engine is waiting for)
    // =====================================================================================

    public enum DuelEventType
    {
        DuelStarted, TurnStarted, PhaseChanged, CardDrawn,
        NormalSummon, TributeSummon, SpecialSummon, MonsterSet, SpellTrapSet, FlipSummon, Flipped, PositionChanged,
        CardActivated, EffectResolved, ActivationNegated, SummonNegated,
        AttackDeclared, AttackNegated, BattleResolved, Damage, LifeGained, LifePaid,
        Destroyed, SentToGraveyard, Banished, ReturnedToHand, ReturnedToDeck, Discarded, ControlChanged,
        Message, DuelEnded
    }

    public sealed class DuelEvent
    {
        public DuelEventType Type;
        public int Player = -1;
        public DuelCard Card;
        public DuelCard Other;
        public int Amount;
        public string Text;
        public int Turn;
        public DuelPhase Phase;

        public override string ToString() => $"[T{Turn} {Phase}] {Type}: {Text}";
    }

    public enum DuelChoiceKind { SelectCards, SelectOption }

    /// <summary>A decision the engine needs before it can continue (targets, tributes,
    /// discards, "activate a trap?"). The CPU answers instantly; the human answers via the HUD.</summary>
    public sealed class DuelChoice
    {
        public int Player;
        public DuelChoiceKind Kind = DuelChoiceKind.SelectCards;
        public string Title;
        public string Prompt;
        public DuelCard SourceCard;
        public List<DuelCard> Candidates = new();
        public int MinCount = 1;
        public int MaxCount = 1;
        public List<string> Options = new();
        public bool IsResponseWindow;     // "Activate a card in response?" (MinCount 0)
        public string Context;             // AI hint, e.g. "tribute", "discard", "destroy-opponent"
        public DuelTrigger Trigger;        // set for response windows

        internal Action<List<DuelCard>> OnCards;
        internal Action<int> OnOption;
        internal Action OnCancel;

        public bool CanCancel => OnCancel != null;
    }

    /// <summary>Anything that can answer a <see cref="DuelChoice"/> synchronously (the CPU).</summary>
    public interface IDuelDecider
    {
        List<DuelCard> ChooseCards(DuelEngine engine, DuelChoice choice);
        int ChooseOption(DuelEngine engine, DuelChoice choice);
    }

    /// <summary>What a response window is reacting to.</summary>
    public enum DuelTriggerKind { AttackDeclared, NormalSummoned, FlipSummoned, SpecialSummoned, SpellActivated, TrapActivated, MainPhase }

    public sealed class DuelTrigger
    {
        public DuelTriggerKind Kind;
        public int Player;                 // who caused it
        public DuelCard Card;              // summoned / activated card / attacker
        public DuelMonsterState Attacker;
        public DuelMonsterState Defender;  // null for a direct attack
        public bool Negated;
        public int Depth;
    }
}
