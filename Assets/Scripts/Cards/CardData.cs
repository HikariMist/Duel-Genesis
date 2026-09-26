using System;

namespace DuelGenesis.Cards
{
    public enum CardKind
    {
        Monster,
        Spell,
        Trap
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
            string effectText)
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
