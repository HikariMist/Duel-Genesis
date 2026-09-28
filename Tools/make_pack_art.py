#!/usr/bin/env python3
"""
Duel: Genesis booster wrappers, built from the game's own card art.

Each pack shows its signature card: Blue-Eyes on the Monster pack, Change of Heart on Spells, Magical Hats on
Traps, and a staple of each Attribute on the Attribute packs. The wrapper has foil gradients, crimped ends,
the Duel Genesis logo, a framed art window and the pack name.

Writes Assets/Resources/DuelGenesis/Packs/pack_<id>.jpg (768 x 1152), which GenesisPacks loads.
Run from the repo root:  python3 Tools/make_pack_art.py
"""
import math
import os

from PIL import Image, ImageDraw, ImageFilter, ImageFont

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
ART = os.path.join(ROOT, "Cards", "DMO_card_art", "cards")
OUT = os.path.join(ROOT, "Assets", "Resources", "DuelGenesis", "Packs")
LOGO = os.path.join(ROOT, "Assets", "Resources", "DuelGenesis", "DuelGenesisLogo.png")
FONT = "/usr/share/fonts/truetype/google-fonts/Poppins-Bold.ttf"
W, H = 768, 1152

# id: (title, featured card, main colour, second colour)
PACKS = {
    "monster": ("MONSTER PACK", "Blue-Eyes White Dragon", (40, 110, 230), (10, 20, 60)),
    "spell": ("SPELL PACK", "Change of Heart", (20, 170, 140), (5, 40, 45)),
    "trap": ("TRAP PACK", "Magical Hats", (190, 40, 140), (45, 8, 40)),
    "dark": ("DARK PACK", "Dark Magician", (120, 50, 200), (18, 6, 40)),
    "light": ("LIGHT PACK", "Black Luster Soldier - Envoy of the Beginning", (235, 190, 70), (70, 45, 10)),
    "earth": ("EARTH PACK", "Gaia The Fierce Knight", (150, 110, 60), (35, 25, 12)),
    "fire": ("FIRE PACK", "Horus the Black Flame Dragon LV8", (235, 80, 20), (60, 10, 5)),
    "water": ("WATER PACK", "Mobius the Frost Monarch", (40, 150, 235), (5, 25, 60)),
    "wind": ("WIND PACK", "Harpie Lady Sisters", (60, 200, 120), (8, 45, 30)),
}


def lerp(a, b, t):
    return tuple(int(a[i] + (b[i] - a[i]) * t) for i in range(3))


def background(c1, c2):
    img = Image.new("RGB", (W, H))
    px = img.load()
    for y in range(H):
        for x in range(W):
            # Radial glow behind the art window plus a vertical fade to the dark colour.
            dx, dy = (x - W / 2) / W, (y - H * 0.47) / H
            r = math.sqrt(dx * dx * 1.6 + dy * dy)
            t = min(1.0, r * 1.9)
            base = lerp(c1, c2, t)
            # Diagonal foil streaks.
            s = math.sin((x * 0.8 + y) / 38.0) * 0.5 + 0.5
            streak = max(0.0, s - 0.82) * 3.2
            px[x, y] = tuple(min(255, int(v + 120 * streak)) for v in base)
    return img


def crimp(draw, top, colour):
    """Metallic crimped band with a zig-zag edge."""
    y0, y1 = (0, 70) if top else (H - 70, H)
    for y in range(y0, y1):
        t = (y - y0) / 70
        shade = int(150 + 80 * math.sin(t * math.pi))
        draw.line([(0, y), (W, y)], fill=lerp(colour, (shade, shade, shade), 0.55))
    for x in range(0, W, 8):
        draw.line([(x, y0), (x, y1)], fill=(255, 255, 255, 40) if x % 16 else (0, 0, 0, 40))
    edge = y1 if top else y0
    pts = []
    for i in range(0, W + 16, 16):
        pts += [(i, edge), (i + 8, edge + (10 if top else -10))]
    draw.line(pts, fill=(230, 230, 235), width=3)


def main():
    font_big = ImageFont.truetype(FONT, 78)
    font_small = ImageFont.truetype(FONT, 26)
    logo = Image.open(LOGO).convert("RGBA")
    logo.thumbnail((430, 230))
    os.makedirs(OUT, exist_ok=True)
    for pid, (title, card, c1, c2) in PACKS.items():
        img = background(c1, c2).convert("RGBA")
        d = ImageDraw.Draw(img, "RGBA")

        # Art window: framed card art with a glow.
        art = Image.open(os.path.join(ART, card + ".png")).convert("RGBA").resize((540, 540), Image.LANCZOS)
        ax, ay = (W - 540) // 2, 300
        glow = Image.new("RGBA", (W, H), (0, 0, 0, 0))
        ImageDraw.Draw(glow).rectangle([ax - 26, ay - 26, ax + 566, ay + 566], fill=c1 + (255,))
        img = Image.alpha_composite(img, glow.filter(ImageFilter.GaussianBlur(28)))
        d = ImageDraw.Draw(img, "RGBA")
        d.rectangle([ax - 12, ay - 12, ax + 552, ay + 552], fill=(20, 20, 26, 255))
        d.rectangle([ax - 9, ay - 9, ax + 549, ay + 549], outline=(235, 200, 110, 255), width=5)
        img.alpha_composite(art, (ax, ay))
        # Foil sheen across the art.
        sheen = Image.new("RGBA", (540, 540), (0, 0, 0, 0))
        sd = ImageDraw.Draw(sheen)
        for i in range(-540, 540, 4):
            a = max(0, 60 - abs(i - 60) // 3)
            sd.line([(i, 540), (i + 540, 0)], fill=(255, 255, 255, a), width=4)
        img.alpha_composite(sheen, (ax, ay))

        # Logo and titles.
        img.alpha_composite(logo, ((W - logo.width) // 2, 72))
        d = ImageDraw.Draw(img, "RGBA")
        tw = d.textlength(title, font=font_big)
        for ox, oy in ((4, 4), (-2, 0), (2, 0), (0, -2), (0, 2)):
            d.text(((W - tw) / 2 + ox, 872 + oy), title, font=font_big, fill=(0, 0, 0, 200))
        d.text(((W - tw) / 2, 872), title, font=font_big, fill=(255, 255, 255, 255))
        sub = "9 CARDS  •  BOOSTER"
        sw = d.textlength(sub, font=font_small)
        d.text(((W - sw) / 2, 972), sub, font=font_small, fill=(255, 225, 140, 255))
        feat = "FEATURING " + card.upper()
        fs = font_small
        if d.textlength(feat, font=fs) > W - 80:
            fs = ImageFont.truetype(FONT, 20)
        fw = d.textlength(feat, font=fs)
        d.text(((W - fw) / 2, 1012), feat, font=fs, fill=(230, 230, 240, 230))

        crimp(d, True, c1)
        crimp(d, False, c1)
        img.convert("RGB").save(os.path.join(OUT, f"pack_{pid}.jpg"), quality=90)
        print("wrote pack_" + pid + ".jpg featuring", card)


if __name__ == "__main__":
    main()
