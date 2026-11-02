#!/usr/bin/env python3
"""Downloads the CC0 ambientCG materials the game uses and packs them into flipbook atlases that
Unity imports as Texture2DArrays (see Assets/Editor/ArtImport.cs).

  python3 Tools/art/pack_textures.py [--cache DIR]

Outputs (Assets/Resources/Textures/):
  TerrainAlbedo.png  3x2 x 1024px  RGB = albedo (sRGB), A = height
  TerrainNormal.png  3x2 x 1024px  R = AO, G = normal.y, B = roughness, A = normal.x
  SurfaceDetail.png  4x4 x 512px   R = detail (luminance, mean 0.5), G = normal.y, B = AO, A = normal.x
All source materials are CC0 from https://ambientcg.com (no attribution required).
"""
import argparse, io, os, sys, urllib.request, zipfile
from PIL import Image, ImageOps, ImageStat

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
OUT = os.path.join(ROOT, "Assets", "Resources", "Textures")

# Order = array layer index; must match TerrainLayer / SurfaceKind in Materials.cs.
TERRAIN = ["Grass004", "Grass001", "Ground103", "Ground079S", "Rock029", "Gravel041"]
SURFACE = ["Bricks090", "WoodSiding009", "WoodSiding013", "RoofingTiles013A", "CorrugatedSteel005", "Bricks105",
           "Concrete034", "PaintedMetal004", "Metal053B", "Bark014", "Wood049", "Planks037A", "Gravel023", "Ground110"]

def fetch(asset, cache):
    d = os.path.join(cache, asset)
    if not os.path.isdir(d):
        url = "https://ambientcg.com/get?file=%s_1K-JPG.zip" % asset
        print("download", asset)
        req = urllib.request.Request(url, headers={"User-Agent": "Mozilla/5.0 (ironhorse art pipeline)"})
        data = urllib.request.urlopen(req, timeout=60).read()
        zipfile.ZipFile(io.BytesIO(data)).extractall(d)
    def load(kind, mode):
        p = os.path.join(d, "%s_1K-JPG_%s.jpg" % (asset, kind))
        if not os.path.exists(p):
            return None
        return Image.open(p).convert(mode)
    return load

def channel(load, kind, size, default):
    im = load(kind, "L")
    return im.resize((size, size), Image.LANCZOS) if im else Image.new("L", (size, size), default)

def normal(load, size):
    n = load("NormalGL", "RGB")
    n = n.resize((size, size), Image.LANCZOS) if n else Image.new("RGB", (size, size), (128, 128, 255))
    r, g, _ = n.split()
    return r, g

def atlas(tiles, cols, rows, size):
    sheet = Image.new("RGBA", (cols * size, rows * size), (128, 128, 128, 255))
    for i, t in enumerate(tiles):
        # Unity flipbooks count from the top-left.
        sheet.paste(t, ((i % cols) * size, (i // cols) * size))
    return sheet

def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--cache", default=os.path.join(ROOT, "Temp", "ambientcg"))
    a = ap.parse_args()
    os.makedirs(a.cache, exist_ok=True); os.makedirs(OUT, exist_ok=True)

    size = 1024
    alb, nrm = [], []
    for asset in TERRAIN:
        load = fetch(asset, a.cache)
        col = load("Color", "RGB").resize((size, size), Image.LANCZOS)
        height = channel(load, "Displacement", size, 128)
        height = ImageOps.autocontrast(height, cutoff=1)
        alb.append(Image.merge("RGBA", (*col.split(), height)))
        nx, ny = normal(load, size)
        nrm.append(Image.merge("RGBA", (channel(load, "AmbientOcclusion", size, 255), ny, channel(load, "Roughness", size, 200), nx)))
    atlas(alb, 3, 2, size).save(os.path.join(OUT, "TerrainAlbedo.png"), optimize=True)
    atlas(nrm, 3, 2, size).save(os.path.join(OUT, "TerrainNormal.png"), optimize=True)

    size = 512
    det = []
    for asset in SURFACE:
        load = fetch(asset, a.cache)
        lum = load("Color", "L").resize((size, size), Image.LANCZOS)
        st = ImageStat.Stat(lum)
        mean, std = st.mean[0] or 1, st.stddev[0] or 1
        # Mean 0.5 (so x2 in the shader keeps the vertex tint) and a common contrast, so fine
        # textures (gravel, dirt) read as clearly as bold ones (brick) from the game camera.
        gain = max(0.7, min(2.6, 34.0 / std))
        lum = lum.point(lambda v, m=mean, g=gain: max(0, min(255, int(128 + (v - m) * g * 128 / m))))
        nx, ny = normal(load, size)
        det.append(Image.merge("RGBA", (lum, ny, channel(load, "AmbientOcclusion", size, 255), nx)))
    atlas(det, 4, 4, size).save(os.path.join(OUT, "SurfaceDetail.png"), optimize=True)
    print("wrote", OUT)

if __name__ == "__main__":
    sys.exit(main())
