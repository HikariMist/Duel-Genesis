#!/usr/bin/env python3
"""
Duel: Genesis card rarities.

Every card in StreamingAssets/duel_genesis_cards.json used to be Common, so booster packs had no chase
cards. This script gives each card a rarity:

  1. Hand-picked tiers: the legendary cards of the era are Secret Rare, the staples and bosses Ultra Rare.
  2. Everything else is scored from its stats and effect text (how strong / how swingy it is) and ranked
     within its kind, so each of Monster / Spell / Trap gets the same shape:
        Super Rare ~13%, Rare ~27%, Common the rest.

Output:
  Assets/StreamingAssets/duel_genesis_rarities.json  (id -> rarity; applied by the catalog loader, so a
                                                      rebuilt catalog keeps these rarities)
  and the rarity fields inside duel_genesis_cards.json are updated to match.

Run from the repo root:  python3 Tools/assign_rarities.py
"""
import collections
import json
import os
import re

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
CATALOG = os.path.join(ROOT, "Assets", "StreamingAssets", "duel_genesis_cards.json")
OUT = os.path.join(ROOT, "Assets", "StreamingAssets", "duel_genesis_rarities.json")

SECRET = {
    # Monsters
    "Blue-Eyes White Dragon", "Blue-Eyes Ultimate Dragon", "Dark Magician", "Dark Magician Girl",
    "Exodia the Forbidden One", "Red-Eyes Black Dragon", "Black Luster Soldier - Envoy of the Beginning",
    "Dark Magician of Chaos", "Divine Dragon Ragnarok", "Gandora the Dragon of Destruction",
    "Dark Master - Zorc", "Five-Headed Dragon", "Perfectly Ultimate Great Moth", "Arcana Knight Joker",
    "Silent Magician LV8", "Horus the Black Flame Dragon LV8", "Gorz the Emissary of Darkness",
    "Chaos Sorcerer", "Toon Dark Magician Girl",
    # Spells
    "Pot of Greed", "Harpie's Feather Duster", "Change of Heart", "Dark Hole", "Graceful Charity",
    "Heavy Storm", "The Seal of Orichalcos", "Snatch Steal", "Chaos End",
    # Traps
    "Mirror Force", "Torrential Tribute", "Solemn Judgment", "Magic Cylinder", "Ring of Destruction",
    "Destiny Board",
}

ULTRA = {
    # Monsters
    "Summoned Skull", "Buster Blader", "Dark Paladin", "Magician of Black Chaos", "Gaia the Dragon Champion",
    "Black Luster Soldier", "Valkyrion the Magna Warrior", "XYZ-Dragon Cannon", "Airknight Parshath",
    "Tyrant Dragon", "Dark Necrofear", "Cyber Jar", "Sinister Serpent", "Breaker the Magical Warrior",
    "Caius the Shadow Monarch", "Zaborg the Thunder Monarch", "Mobius the Frost Monarch",
    "Thestalos the Firestorm Monarch", "Raiza the Storm Monarch", "Granmarg the Rock Monarch",
    "Kuraz the Light Monarch", "Harpie Lady Sisters", "Thousand Dragon", "Barrel Dragon", "Lord of D.",
    "Injection Fairy Lily", "Beast King Barbaros", "Elemental HERO Flame Wingman", "Ultimate Insect LV7",
    "Silent Swordsman LV7", "Horus the Black Flame Dragon LV6", "Dark Ruler Ha Des", "Legendary Fiend",
    "Twin-Headed Behemoth", "Left Arm of the Forbidden One", "Right Arm of the Forbidden One",
    "Left Leg of the Forbidden One", "Right Leg of the Forbidden One", "Jinzo", "Dark Magician Knight",
    "Cosmo Queen", "Gearfried The Iron Knight", "Tribe-Infecting Virus", "Magician of Faith",
    "Morphing Jar", "D.D. Warrior Lady", "Blade Knight", "Freed the Matchless General",
    "Gatling Dragon", "Twin-Headed Thunder Dragon", "Masaki the Legendary Swordsman",
    "Different Dimension Dragon", "Machine King", "Ojama King", "Doomcaliber Knight",
    "Mokey Mokey King", "Sacred Phoenix of Nephthys", "Divine Knight Ishzark", "Dark Eradicator Warlock",
    "Skull Archfiend of Lightning", "Chimera the Flying Mythical Beast",
    # Spells
    "Premature Burial", "Mystical Space Typhoon", "Swords of Revealing Light", "Brain Control",
    "Card Destruction", "Enemy Controller", "Lightning Vortex", "Limiter Removal", "Nobleman of Crossout",
    "Painful Choice", "Book of Moon", "Dimension Fusion", "Chaos Greed", "Giant Trunade", "Toon World",
    "Contract with Exodia", "Black Luster Ritual", "Black Magic Ritual", "Level Up!", "Delinquent Duo",
    "Scapegoat", "Megamorph", "Monster Reborn", "Raigeki", "Magical Mallet", "Dark Magic Attack",
    "Thousand Knives", "Burst Stream of Destruction", "Soul Exchange", "Creature Swap",
    "Mage Power", "Fusion Gate", "Magicians Unite",
    # Traps
    "Call of the Haunted", "Bottomless Trap Hole", "Compulsory Evacuation Device", "Royal Decree",
    "Gravity Bind", "Magic Jammer", "Trap Dustshoot", "Mind Crush", "Deck Devastation Virus",
    "Return from the Different Dimension", "Skull Lair", "Imperial Order", "Wall of Revealing Light",
    "Judgment of Anubis", "Light of Intervention", "Seven Tools of the Bandit", "Horn of Heaven",
    "Metal Reflect Slime", "Magical Hats", "Divine Wrath", "Raigeki Break",
}

SUPER = {
    "Foolish Burial", "Sakuretsu Armor", "Dust Tornado", "Upstart Goblin", "Alpha the Magnet Warrior",
    "Beta the Magnet Warrior", "Gamma the Magnet Warrior", "Winged Kuriboh", "Spear Dragon", "Marauding Captain",
    "Command Knight", "Mystic Tomato", "Kuriboh", "Harpie Lady", "Celtic Guardian", "Curse of Dragon",
    "Gaia The Fierce Knight", "Swift Gaia the Fierce Knight", "Red Medicine", "Card Ejector", "Magic Reflector",
    "Trap Hole", "Negate Attack", "Book of Secret Arts", "Black Pendant", "Axe of Despair", "Mask of Darkness",
}

# Effect-text phrases and how much they raise a card's power score.
KEYWORDS = [
    (r"destroy all", 3.0), (r"take control", 3.0), (r"negate", 2.0), (r"cannot be destroyed", 1.5),
    (r"special summon", 1.4), (r"\bdraw\b", 1.6), (r"add 1 .* from your deck", 1.6), (r"from your deck", 1.0),
    (r"banish|remove from play|removed from play", 1.0), (r"\bdestroy\b", 1.0), (r"return .* to the hand", 1.0),
    (r"cannot attack", 0.8), (r"inflict .*damage", 0.6), (r"change .* battle position", 0.5),
    (r"both players", -0.4), (r"coin|dice|die\b", -0.6), (r"gains? \d{3} atk", -0.4), (r"loses? \d{3} atk", -0.3),
    (r"equip only to", -0.4), (r"once per turn", 0.2),
]


def score(card):
    text = (card.get("effectText") or "").lower()
    s = sum(w for pat, w in KEYWORDS if re.search(pat, text))
    s += min(len(text), 420) / 420.0 * 0.6          # longer text = more going on
    if card["kind"] == "Monster":
        atk, lvl = card.get("attack", 0), card.get("level", 0)
        frame = card.get("frameKind", "")
        s += atk / 1000.0 + lvl * 0.12
        if frame in ("FusionMonster", "RitualMonster"): s += 1.5
        if frame == "NormalMonster": s -= 1.2 if atk < 2500 else 0.0
        if "Flip" in (card.get("typeLine") or ""): s += 0.5
    return s


def main():
    data = json.load(open(CATALOG, encoding="utf-8"))
    cards = data["cards"]
    result = {}
    for card in cards:
        name = card["cardName"]
        if card.get("frameKind") == "Token" or name.endswith(" Token"):
            result[card["id"]] = "Common"
        elif name in SECRET:
            result[card["id"]] = "Secret Rare"
        elif name in ULTRA:
            result[card["id"]] = "Ultra Rare"
        elif name in SUPER:
            result[card["id"]] = "Super Rare"

    for kind in ("Monster", "Spell", "Trap"):
        rest = [c for c in cards if c["kind"] == kind and c["id"] not in result]
        total = sum(1 for c in cards if c["kind"] == kind)
        rest.sort(key=score, reverse=True)
        n_super = max(0, round(total * 0.13) - sum(1 for c in cards if c["kind"] == kind and result.get(c["id"]) == "Super Rare"))
        n_rare = round(total * 0.27)
        for i, card in enumerate(rest):
            result[card["id"]] = "Super Rare" if i < n_super else "Rare" if i < n_super + n_rare else "Common"

    for card in cards:
        card["rarity"] = result[card["id"]]
    json.dump(data, open(CATALOG, "w", encoding="utf-8"), indent=4, ensure_ascii=False)

    out = {"note": "Generated by Tools/assign_rarities.py. id -> rarity; applied on top of duel_genesis_cards.json.",
           "rarities": [{"id": c["id"], "name": c["cardName"], "rarity": result[c["id"]]} for c in cards]}
    json.dump(out, open(OUT, "w", encoding="utf-8"), indent=1, ensure_ascii=False)

    counts = collections.Counter((c["kind"], c["rarity"]) for c in cards)
    order = ["Common", "Rare", "Super Rare", "Ultra Rare", "Secret Rare"]
    for kind in ("Monster", "Spell", "Trap"):
        print(kind.ljust(8), "  ".join(f"{r}: {counts[(kind, r)]}" for r in order))
    allc = collections.Counter(c["rarity"] for c in cards)
    print("All     ", "  ".join(f"{r}: {allc[r]} ({allc[r] * 100 / len(cards):.1f}%)" for r in order))
    missing = sorted((SECRET | ULTRA | SUPER) - {c["cardName"] for c in cards})
    if missing: print("Hand-picked names not in this catalog (ignored):", ", ".join(missing))


if __name__ == "__main__":
    main()
