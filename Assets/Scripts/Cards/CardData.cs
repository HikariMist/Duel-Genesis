using System;

namespace DuelGenesis.Cards
{
    public enum CardKind
    {
        Monster,
        Spell,
        Trap
    }

    public enum CardFrameKind
    {
        Auto = 0,
        NormalMonster,
        EffectMonster,
        FusionMonster,
        RitualMonster,
        SynchroMonster,
        XyzMonster,
        Spell,
        Trap,
        Token,
        Unknown
    }

    public enum CardRarity
    {
        Common = 0,
        Rare = 1,
        SuperRare = 2,
        UltraRare = 3
    }

    [Serializable]
    public class CardData
    {
        public string id;
        public string cardName;
        public CardKind kind;
        public CardRarity rarity;
        public string attribute;
        public string typeLine;
        public int level;
        public int attack;
        public int defense;
        public string effectText;
        public CardFrameKind frameKind = CardFrameKind.Auto;

        public CardData(
            string id,
            string cardName,
            CardKind kind,
            CardRarity rarity,
            string attribute,
            string typeLine,
            int level,
            int attack,
            int defense,
            string effectText,
            CardFrameKind frameKind = CardFrameKind.Auto)
        {
            this.id = id;
            this.cardName = cardName;
            this.kind = kind;
            this.rarity = rarity;
            this.attribute = attribute;
            this.typeLine = typeLine;
            this.level = level;
            this.attack = attack;
            this.defense = defense;
            this.effectText = effectText;
            this.frameKind = frameKind;
        }

        public CardFrameKind ResolvedFrameKind
        {
            get
            {
                if (frameKind != CardFrameKind.Auto && frameKind != CardFrameKind.Unknown)
                    return frameKind;

                if (kind == CardKind.Spell) return CardFrameKind.Spell;
                if (kind == CardKind.Trap) return CardFrameKind.Trap;

                string line = typeLine ?? string.Empty;
                if (line.IndexOf("Token", StringComparison.OrdinalIgnoreCase) >= 0) return CardFrameKind.Token;
                if (line.IndexOf("Fusion", StringComparison.OrdinalIgnoreCase) >= 0) return CardFrameKind.FusionMonster;
                if (line.IndexOf("Ritual", StringComparison.OrdinalIgnoreCase) >= 0) return CardFrameKind.RitualMonster;
                if (line.IndexOf("Synchro", StringComparison.OrdinalIgnoreCase) >= 0) return CardFrameKind.SynchroMonster;
                if (line.IndexOf("Xyz", StringComparison.OrdinalIgnoreCase) >= 0) return CardFrameKind.XyzMonster;
                if (line.IndexOf("Normal", StringComparison.OrdinalIgnoreCase) >= 0 &&
                    line.IndexOf("Effect", StringComparison.OrdinalIgnoreCase) < 0)
                    return CardFrameKind.NormalMonster;

                return CardFrameKind.EffectMonster;
            }
        }

        public string RarityLabel => rarity switch
        {
            CardRarity.Common => "COMMON",
            CardRarity.Rare => "RARE",
            CardRarity.SuperRare => "SUPER RARE",
            CardRarity.UltraRare => "ULTRA RARE",
            _ => rarity.ToString().ToUpperInvariant()
        };

        public string ShortStats
        {
            get
            {
                if (kind != CardKind.Monster)
                    return typeLine;

                return $"LV {level}  ATK {attack} / DEF {defense}";
            }
        }
    }
}
