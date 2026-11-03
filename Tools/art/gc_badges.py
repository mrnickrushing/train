#!/usr/bin/env python3
"""Game Center achievement badges (512x512 PNG): a painted game icon on a brass medallion.
  python3 Tools/art/gc_badges.py   -> store/gamecenter/<achievement>.png
"""
import os
from PIL import Image, ImageDraw, ImageFilter

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
ICONS = os.path.join(ROOT, "Assets", "Resources", "UI", "Icons")
OUT = os.path.join(ROOT, "store", "gamecenter")
BADGES = {  # achievement key -> (icon, medallion tint)
    "first_dispatch": ("coal", (120, 40, 34)), "dispatch_100": ("rods", (120, 40, 34)),
    "contracts_10": ("crate", (46, 74, 58)), "contracts_100": ("gold_bars", (46, 74, 58)),
    "level_5": ("xp", (44, 62, 96)), "level_10": ("people", (44, 62, 96)), "level_15": ("nugget", (44, 62, 96)),
    "locos_4": ("gauges", (78, 52, 36)), "chapter_first": ("mail", (78, 52, 36)),
    "golden_spike": ("gold_ore", (92, 64, 20)), "coins_100k": ("coin", (92, 64, 20)),
}


def badge(icon, tint, size=512):
    s = size * 2  # supersample
    im = Image.new("RGB", (s, s), (24, 18, 14))
    d = ImageDraw.Draw(im)
    c = s // 2
    for r, col in [(c - 8, (120, 86, 30)), (c - 40, (214, 170, 82)), (c - 64, (150, 108, 40)), (c - 84, tint)]:
        d.ellipse((c - r, c - r, c + r, c + r), fill=col)
    # soft highlight on the inner field
    hl = Image.new("L", (s, s), 0)
    ImageDraw.Draw(hl).ellipse((c - 300, c - 420, c + 300, c - 40), fill=60)
    im.paste((255, 255, 255), (0, 0), hl.filter(ImageFilter.GaussianBlur(60)))
    ic = Image.open(os.path.join(ICONS, icon + ".png")).convert("RGBA")
    ic = ic.crop(ic.getchannel("A").point(lambda a: 255 if a > 16 else 0).getbbox())  # icons carry wide margins
    k = (s * 0.56) / max(ic.size)
    ic = ic.resize((int(ic.width * k), int(ic.height * k)), Image.LANCZOS)
    shadow = Image.new("RGBA", ic.size, (0, 0, 0, 0))
    shadow.putalpha(ic.getchannel("A").point(lambda a: a * 0.55))
    pos = (c - ic.width // 2, c - ic.height // 2)
    im.paste((0, 0, 0), (pos[0] + 10, pos[1] + 14), shadow.getchannel("A").filter(ImageFilter.GaussianBlur(10)))
    im.paste(ic, pos, ic)
    return im.resize((size, size), Image.LANCZOS)


if __name__ == "__main__":
    os.makedirs(OUT, exist_ok=True)
    for k, (icon, tint) in BADGES.items():
        badge(icon, tint).save(os.path.join(OUT, k + ".png"), optimize=True)
    print("wrote", len(BADGES), "badges to", OUT)
