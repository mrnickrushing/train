#!/usr/bin/env python3
"""UI kit sprites (procedural, free): glossy 3D buttons, framed panels, cards, pills, ribbons.
  python3 Tools/art/ui_kit.py   -> Assets/Resources/UI/Kit/*.png
UIKit.Kit(name) loads them as 9-sliced sprites; the slice borders live in UIKit.KitBorders.
"""
import os
import random
from PIL import Image, ImageDraw, ImageFilter, ImageChops

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
OUT = os.path.join(ROOT, "Assets", "Resources", "UI", "Kit")
SS = 4  # supersampling


def hexc(h): return tuple(int(h[i:i + 2], 16) for i in (0, 2, 4))
def mul(c, k): return tuple(max(0, min(255, int(v * k))) for v in c)
def mix(a, b, t): return tuple(int(a[i] + (b[i] - a[i]) * t) for i in range(3))


def rrect(size, box, r, fill):
    m = Image.new("L", size, 0)
    ImageDraw.Draw(m).rounded_rectangle(box, r, fill=fill)
    return m


def vgrad(w, h, top, bottom):
    g = Image.new("RGB", (1, h))
    for y in range(h):
        g.putpixel((0, y), mix(top, bottom, y / max(1, h - 1)))
    return g.resize((w, h))


def finish(im, w, h, name):
    im.resize((w, h), Image.LANCZOS).save(os.path.join(OUT, name + ".png"), optimize=True)


def button(name, base, w=192, h=104, radius=40):
    W, H, R = w * SS, h * SS, radius * SS
    out = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    o = 4 * SS                     # outline
    lip = 12 * SS                  # 3D base under the face
    dark = mul(base, 0.32)
    # outline + lip
    out.paste(dark + (255,), (0, 0), rrect((W, H), (0, 0, W - 1, H - 1), R, 255))
    lipc = mul(base, 0.58)
    out.paste(lipc + (255,), (0, 0), rrect((W, H), (o, o + lip // 2, W - 1 - o, H - 1 - o), R - o, 255))
    # face with gradient
    face_box = (o, o, W - 1 - o, H - 1 - o - lip)
    face = vgrad(W, H, mix(base, (255, 255, 255), 0.28), mul(base, 0.92))
    out.paste(face, (0, 0), rrect((W, H), face_box, R - o, 255))
    # gloss band on the upper half
    gl = Image.new("L", (W, H), 0)
    ImageDraw.Draw(gl).rounded_rectangle((o + 10 * SS, o + 5 * SS, W - 1 - o - 10 * SS, o + (face_box[3] - o) * 0.45), R - o - 8 * SS, fill=95)
    out.paste((255, 255, 255, 255), (0, 0), gl.filter(ImageFilter.GaussianBlur(2 * SS)))
    # thin bright rim at the top edge of the face
    rim = rrect((W, H), face_box, R - o, 255)
    inner = rrect((W, H), (face_box[0] + 2 * SS, face_box[1] + 3 * SS, face_box[2] - 2 * SS, face_box[3]), R - o - 2 * SS, 255)
    out.paste(mix(base, (255, 255, 255), 0.55) + (255,), (0, 0), ImageChops.subtract(rim, inner).point(lambda a: a * 0.8))
    finish(out, w, h, name)


def round_button(name, base, d=160):
    D = d * SS
    out = Image.new("RGBA", (D, D), (0, 0, 0, 0))
    def circ(pad, fill_rgb, alpha=255):
        m = Image.new("L", (D, D), 0); ImageDraw.Draw(m).ellipse((pad, pad, D - 1 - pad, D - 1 - pad), fill=alpha)
        out.paste(fill_rgb + (255,), (0, 0), m)
    circ(0, (40, 24, 14))                       # dark outline
    circ(4 * SS, (232, 186, 86))                # brass ring
    circ(9 * SS, (150, 104, 36))                # ring shadow
    circ(12 * SS, mul(base, 0.6))               # inner shade
    face = Image.new("RGBA", (D, D), (0, 0, 0, 0))
    g = vgrad(D, D, mix(base, (255, 255, 255), 0.3), mul(base, 0.85))
    m = Image.new("L", (D, D), 0); ImageDraw.Draw(m).ellipse((14 * SS, 13 * SS, D - 1 - 14 * SS, D - 1 - 17 * SS), fill=255)
    out.paste(g, (0, 0), m)
    gl = Image.new("L", (D, D), 0); ImageDraw.Draw(gl).ellipse((28 * SS, 18 * SS, D - 28 * SS, D * 0.5), fill=80)
    out.paste((255, 255, 255, 255), (0, 0), gl.filter(ImageFilter.GaussianBlur(3 * SS)))
    finish(out, d, d, name)


def noise(w, h, amt, seed=1):
    random.seed(seed)
    n = Image.effect_noise((w // 4, h // 4), 40).resize((w, h), Image.BICUBIC)
    return n.point(lambda v: 128 + (v - 128) * amt)


def panel(name, fill, w=256, h=256, radius=34, outline=(44, 26, 14), rim=(214, 170, 82), rim_w=5, grain=0.35, top_light=0.12):
    W, H, R = w * SS, h * SS, radius * SS
    out = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    o = 4 * SS
    out.paste(outline + (255,), (0, 0), rrect((W, H), (0, 0, W - 1, H - 1), R, 255))
    if rim:
        out.paste(rim + (255,), (0, 0), rrect((W, H), (o, o, W - 1 - o, H - 1 - o), R - o, 255))
        out.paste(mul(rim, 0.6) + (255,), (0, 0), rrect((W, H), (o + rim_w * SS // 2, o + rim_w * SS // 2 + SS, W - 1 - o, H - 1 - o), R - o, 255))
    ins = o + (rim_w * SS if rim else 0)
    body = vgrad(W, H, mix(fill, (255, 255, 255), top_light), mul(fill, 0.9))
    if grain:
        n = noise(W, H, grain)
        body = ImageChops.multiply(body, Image.merge("RGB", [n.point(lambda v: min(255, v + 127))] * 3))
    out.paste(body, (0, 0), rrect((W, H), (ins, ins, W - 1 - ins, H - 1 - ins), R - ins, 255))
    # inner vignette
    v = rrect((W, H), (ins, ins, W - 1 - ins, H - 1 - ins), R - ins, 255)
    vi = rrect((W, H), (ins + 10 * SS, ins + 10 * SS, W - 1 - ins - 10 * SS, H - 1 - ins - 10 * SS), R - ins, 255).filter(ImageFilter.GaussianBlur(8 * SS))
    edge = ImageChops.subtract(v, vi).point(lambda a: a * 0.35)
    out.paste(mul(fill, 0.55) + (255,), (0, 0), edge)
    finish(out, w, h, name)


def pill(name, fill=(30, 18, 10), w=160, h=72):
    W, H = w * SS, h * SS; R = H // 2
    out = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    out.paste((20, 12, 6, 255), (0, 0), rrect((W, H), (0, 0, W - 1, H - 1), R, 235))
    out.paste((120, 88, 48, 255), (0, 0), rrect((W, H), (3 * SS, 3 * SS, W - 1 - 3 * SS, H - 1 - 3 * SS), R - 3 * SS, 255))
    out.paste(vgrad(W, H, mul(fill, 0.7), mix(fill, (90, 60, 34), 0.5)), (0, 0), rrect((W, H), (6 * SS, 7 * SS, W - 1 - 6 * SS, H - 1 - 5 * SS), R - 6 * SS, 255))
    finish(out, w, h, name)


def ribbon(name, base=(196, 52, 38), w=512, h=128):
    W, H = w * SS, h * SS
    out = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    d = ImageDraw.Draw(out)
    t = 22 * SS
    dark, mid = mul(base, 0.45), mul(base, 0.7)
    # tails
    for side in (0, 1):
        x0 = 0 if side == 0 else W - 90 * SS
        pts = [(x0, t + 14 * SS), (x0 + 90 * SS, t + 14 * SS), (x0 + 90 * SS, H - 6 * SS), (x0, H - 6 * SS)]
        notch = (x0 + 26 * SS, (t + H) // 2 + 4 * SS) if side == 0 else (x0 + 64 * SS, (t + H) // 2 + 4 * SS)
        poly = [pts[0], pts[1], pts[2], pts[3], notch] if side == 0 else [pts[0], pts[1], notch, pts[2], pts[3]]
        d.polygon(poly, fill=(40, 16, 10, 255))
        inner = [(x + (4 * SS if x == pts[0][0] else -4 * SS if x == pts[1][0] else 0), y + (4 * SS if y < H // 2 else -4 * SS)) for x, y in poly]
        d.polygon(inner, fill=mid + (255,))
    # body
    out.paste((40, 16, 10, 255), (0, 0), rrect((W, H), (40 * SS, 0, W - 1 - 40 * SS, H - 26 * SS), 14 * SS, 255))
    body = vgrad(W, H, mix(base, (255, 255, 255), 0.25), mul(base, 0.85))
    out.paste(body, (0, 0), rrect((W, H), (44 * SS, 4 * SS, W - 1 - 44 * SS, H - 30 * SS), 11 * SS, 255))
    gl = Image.new("L", (W, H), 0); ImageDraw.Draw(gl).rectangle((52 * SS, 9 * SS, W - 52 * SS, 30 * SS), fill=70)
    out.paste((255, 255, 255, 255), (0, 0), gl.filter(ImageFilter.GaussianBlur(3 * SS)))
    # stitched edges
    for y in (12 * SS, H - 40 * SS):
        for x in range(56 * SS, W - 56 * SS, 18 * SS):
            d.rectangle((x, y, x + 9 * SS, y + 2 * SS), fill=mix(base, (255, 230, 200), 0.6) + (200,))
    finish(out, w, h, name)


if __name__ == "__main__":
    os.makedirs(OUT, exist_ok=True)
    for n, c in {"green": "56B030", "red": "D44A30", "blue": "2F84CC", "gold": "F2B22E", "purple": "9348CC",
                 "wood": "8C5C34", "grey": "8E8A84", "orange": "E8822A"}.items():
        button("btn_" + n, hexc(c))
    for n, c in {"red": "D44A30", "blue": "2F84CC", "green": "56B030", "gold": "E4A22A", "wood": "9A6638", "brown": "6E4628", "purple": "9348CC"}.items():
        round_button("round_" + n, hexc(c))
    panel("frame", hexc("F4E7C8"), radius=40, rim_w=7, grain=0.07, top_light=0.25)          # windows
    panel("card", hexc("FBF1DA"), w=128, h=128, radius=22, outline=(120, 86, 50), rim=None, grain=0.12, top_light=0.35)
    panel("card_wood", hexc("4A2E1A"), w=128, h=128, radius=22, outline=(24, 14, 8), rim=(196, 150, 70), rim_w=3, grain=0.6, top_light=0.18)
    panel("tile", hexc("EBDDBB"), w=128, h=128, radius=24, outline=(150, 112, 70), rim=None, grain=0.0, top_light=0.4)
    panel("hero_purple", hexc("6E3AA8"), w=256, h=256, radius=30, outline=(30, 12, 44), rim=(236, 196, 96), rim_w=5, grain=0.15, top_light=0.3)
    panel("hero_blue", hexc("2E6AA6"), w=256, h=256, radius=30, outline=(12, 26, 48), rim=(236, 196, 96), rim_w=5, grain=0.15, top_light=0.3)
    panel("hero_red", hexc("A8402C"), w=256, h=256, radius=30, outline=(48, 14, 8), rim=(236, 196, 96), rim_w=5, grain=0.15, top_light=0.3)
    panel("hero_green", hexc("3E8A3A"), w=256, h=256, radius=30, outline=(12, 40, 12), rim=(236, 196, 96), rim_w=5, grain=0.15, top_light=0.3)
    pill("pill")
    ribbon("ribbon")
    ribbon("ribbon_green", base=hexc("3E9A34"))
    print("wrote", len(os.listdir(OUT)), "kit sprites")
