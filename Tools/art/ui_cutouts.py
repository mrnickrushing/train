#!/usr/bin/env python3
"""UI cut-outs of the AI concept renders (free, no Higgsfield calls).

  python3 Tools/art/ui_cutouts.py

Every locomotive, wagon and building concept in art-src/<id>.png sits on a white studio backdrop.
This removes the backdrop (light, grey pixels connected to the image border, so a soft floor shadow
goes too), trims, and writes Assets/Resources/UI/{Locos,Wagons,Buildings}/<id>.png for the UI.
"""
import os
import sys

import numpy as np
from PIL import Image, ImageFilter
from scipy import ndimage

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from ai_assets import ROOT  # noqa: E402

SRC = os.path.join(ROOT, "art-src")
OUT = os.path.join(ROOT, "Assets", "Resources", "UI")

LOCOS = ["dusty", "lark", "redmesa", "copperqueen", "ironbison", "sundown", "silverarrow", "thunderhead"]
WAGONS = ["flatcar", "hopper", "boxcar", "stockcar", "mailcoach", "longflat", "steelhopper", "reefer", "tankcar", "pullman"]
BUILDINGS = ["cabin", "clapboard", "boarding", "rowhouse", "grandhotel", "sawmill", "gristmill", "packing", "smelter",
             "toolworks", "wireworks", "assay", "refinery", "watertower", "saloon", "telegraph", "schoolhouse", "bank",
             "clocktower", "lamppost", "garden", "fountain"]

# (folder, ids, longest side, lightness floor, max greyness) — vehicles drop their grey floor shadow too.
SETS = [("Locos", LOCOS, 640, 150, 28), ("Wagons", WAGONS, 480, 150, 28), ("Buildings", BUILDINGS, 420, 205, 22)]


def cutout(src, side, floor, grey):
    im = Image.open(src).convert("RGB")
    im.thumbnail((1100, 1100))
    a = np.asarray(im).astype(int)
    light = (a.min(2) > floor) & ((a.max(2) - a.min(2)) < grey)
    lab, _ = ndimage.label(light)
    edge = set(np.unique(np.concatenate([lab[0], lab[-1], lab[:, 0], lab[:, -1]]))) - {0}
    alpha = np.where(np.isin(lab, list(edge)), 0, 255).astype(np.uint8)
    mask = Image.fromarray(alpha).filter(ImageFilter.MinFilter(3)).filter(ImageFilter.GaussianBlur(0.8))
    out = im.convert("RGBA")
    out.putalpha(mask)
    out = out.crop(mask.point(lambda v: 255 if v > 20 else 0).getbbox())
    out.thumbnail((side, side), Image.LANCZOS)
    return out


def main():
    for folder, ids, side, floor, grey in SETS:
        dst = os.path.join(OUT, folder)
        os.makedirs(dst, exist_ok=True)
        for i in ids:
            src = os.path.join(SRC, i + ".png")
            if not os.path.exists(src):
                print("missing", src)
                continue
            cutout(src, side, floor, grey).save(os.path.join(dst, i + ".png"), optimize=True)
        print(folder, len(ids))


if __name__ == "__main__":
    main()
