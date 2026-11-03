#!/usr/bin/env python3
"""Game icons (Higgsfield GPT Image 2.5, transparent background), one consistent style.

  python3 Tools/art/ai_icons.py gen [<id>...]   # generate (skips existing)
  python3 Tools/art/ai_icons.py install [<id>...] # trim, resize to 256 px -> Assets/Resources/UI/Icons/<id>.png

Raw files: art-src/icons/. Never regenerate an id that already exists unless you delete it first.
"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from ai_assets import ROOT, fetch, hf  # noqa: E402

SRC = os.path.join(ROOT, "art-src", "icons")
DST = os.path.join(ROOT, "Assets", "Resources", "UI", "Icons")
STYLE = ("A single mobile game inventory icon of {what}, centered, filling most of the frame, on a fully transparent "
         "background. Semi-realistic painted style like a premium mobile tycoon game: chunky readable shapes, rich "
         "saturated color, soft top-left lighting, subtle dark outline, gentle drop shadow under the object. "
         "Nothing else in the frame. No text, no letters, no numbers, no border, no background shape.")

I = {
    # resources
    "timber": "a neat stack of three fresh-cut logs with pale ends showing tree rings",
    "coal": "a pile of glossy black coal lumps",
    "grain": "a tied bundle of golden wheat sheaves",
    "cattle": "the head of a brown longhorn steer with curved horns",
    "iron_ore": "a chunk of rusty red-brown iron ore rock with metallic flecks",
    "mail": "a canvas mail sack tied with rope with a couple of sealed envelopes peeking out",
    "copper_ore": "a chunk of rock with bright orange copper and blue-green malachite veins",
    "gold_ore": "a chunk of grey quartz rock with shiny gold veins",
    "oil": "a dark wooden oil barrel with iron hoops and a drip of black oil",
    "planks": "a stack of smooth sawn wooden planks tied with twine",
    "flour": "a white cloth flour sack tied at the top, a little flour dust",
    "provisions": "a wooden crate of provisions with a ham, a loaf of bread and tin cans",
    "steel": "a stack of shiny blue-grey steel ingots",
    "tools": "a crossed hammer and wrench with wooden handles",
    "copper_wire": "a spool of gleaming copper wire",
    "gold_bars": "a small stack of three gleaming gold bars",
    "fuel": "a coal bucket full of coal next to a small steel water can",
    # workshop parts
    "rivets": "a small pile of steel rivets with round heads",
    "bearings": "a polished steel ball bearing ring with visible balls",
    "rods": "two crossed steel locomotive drive rods with brass bushings",
    "gauges": "a round brass steam pressure gauge with a white dial and red needle",
    # UI
    "lock": "a sturdy closed brass padlock with an iron shackle",
    # HUD
    "coin": "a single shiny gold coin with an embossed steam locomotive, seen slightly from the front",
    "nugget": "a big shiny gold nugget",
    "xp": "a glowing golden five-pointed sheriff's star badge",
    "people": "a cowboy hat on top of a small group silhouette of three frontier townsfolk",
    "crate": "a sturdy wooden shipping crate with iron corners",
    # HUD buttons
    "nav_trains": "the front of a red and brass steam locomotive in three-quarter view, with a glowing headlamp and a puff of white steam",
    "nav_territory": "a partly unrolled old parchment map with a dotted railroad route and two red pins",
    "nav_contracts": "a paper order sheet pinned to a small wooden board, with a red wax seal and a gold coin",
    "nav_build": "a claw hammer crossed over a carpenter's hand saw with wooden handles",
    "nav_depot": "a big red spoked steam locomotive driving wheel with a brass hub",
    "nav_settings": "a polished brass cogwheel gear",
    # Store art (big, on offer cards)
    "shop_nuggets_1": "a small handful of shiny gold nuggets",
    "shop_nuggets_2": "a small brown leather drawstring pouch overflowing with shiny gold nuggets",
    "shop_nuggets_3": "a burlap sack tied with rope, open at the top and overflowing with shiny gold nuggets",
    "shop_nuggets_4": "an open iron-banded wooden strongbox heaped high with shiny gold nuggets",
    "shop_nuggets_5": "an open heavy black iron bank safe with a brass dial, overflowing with a mountain of shiny gold nuggets",
    "shop_coins": "a big heap of shiny gold coins embossed with steam locomotives, a few coins standing on edge",
    "shop_fuel": "a large wooden railway coal bin heaped with glossy black coal next to two steel water cans",
    "shop_supplies": "a stack of three wooden supply crates with rope handles, one open showing steel ingots, tools and planks",
    # Daily hooks (medium quality: 0.5 credits each)
    "payday": "a brown leather money pouch tied with a cord, spilling a few shiny gold coins, with a small paper pay envelope behind it",
    "telegram": "a folded yellowed telegram paper slip with a red urgent wax stamp, lying over a small brass telegraph key",
    "crate_mystery": "a wooden treasure crate with iron corners, its lid slightly open with bright golden light glowing out",
    "bandit": "the head and shoulders of a frontier outlaw wearing a black cowboy hat and a red bandana mask over his face, narrowed eyes",
    "tumbleweed": "a round dry tan tumbleweed ball of tangled twigs with a couple of gold coins caught in it",
    "traveler": "a worn brown leather travel suitcase with brass clasps and a cowboy hat resting on top",
    "album": "a thick brown leather-bound collector's album with brass corners and a few old locomotive photographs sticking out of the pages",
    "foreman": "a friendly portrait of a weathered railroad foreman with a grey moustache, a brown bowler hat and a red neckerchief, head and shoulders",
}

MEDIUM = {"payday", "telegram", "crate_mystery", "bandit", "tumbleweed", "traveler", "album"}


def gen(i):
    p = os.path.join(SRC, i + ".png")
    if os.path.exists(p):
        return print("have", p)
    quality = "medium" if i in MEDIUM else "high"
    url = hf("generate", "create", "gpt_image_2_5", "--aspect_ratio", "1:1", "--quality", quality, "--resolution", "1k",
             "--background", "transparent", "--prompt", STYLE.format(what=I[i]), "--wait")
    fetch(url, p)
    print("icon", i, url)


def install(ids=None):
    from PIL import Image
    os.makedirs(DST, exist_ok=True)
    for i in ids or I:
        p = os.path.join(SRC, i + ".png")
        if not os.path.exists(p):
            continue
        im = Image.open(p).convert("RGBA")
        box = im.getchannel("A").point(lambda a: 255 if a > 12 else 0).getbbox()
        if box:
            im = im.crop(box)
        side = max(im.size)
        sq = Image.new("RGBA", (side, side), (0, 0, 0, 0))
        sq.paste(im, ((side - im.width) // 2, (side - im.height) // 2))
        sq.resize((256, 256), Image.LANCZOS).save(os.path.join(DST, i + ".png"), optimize=True)
    print("installed", len(os.listdir(DST)), "icons")


if __name__ == "__main__":
    os.makedirs(SRC, exist_ok=True)
    if sys.argv[1] == "install":
        install(sys.argv[2:])
    else:
        for i in sys.argv[2:] or list(I):
            try:
                gen(i)
            except Exception as e:
                print("FAILED", i, e)
