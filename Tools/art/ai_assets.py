#!/usr/bin/env python3
"""AI art pipeline for Ironhorse Frontier (Higgsfield: GPT Image 2.5 concept -> Tripo H3.1 3D -> Blender cleanup).

  python3 Tools/art/ai_assets.py concept <id>...   # concept image (skips ids that already have one)
  python3 Tools/art/ai_assets.py model <id>...     # image-to-3D GLB from the approved concept (skips existing)
  python3 Tools/art/ai_assets.py import <id>...    # Blender cleanup -> Assets/Resources/Models/...
  python3 Tools/art/ai_assets.py list

Work files live in art-src/ (gitignored, large). Never re-run a paid step for an id that already has its
output: delete the file first if a redo is really wanted. All designs are original (no real railroads,
liveries or brands).
"""
import json
import os
import subprocess
import sys
import urllib.request

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
SRC = os.path.join(ROOT, "art-src")

VEHICLE = ("Game asset reference render of {what}. Slightly elevated three-quarter view of its left side with the front "
           "pointing to the LEFT, isolated on a plain flat white background, the entire vehicle visible and nothing cropped, "
           "no rails, no ground. Semi-realistic, richly detailed like a premium mobile railroad tycoon game, clean studio "
           "lighting with soft shadows. No text, no numbers, no logos, no people.")
BUILDING = ("Game asset reference render of {what}. Isometric three-quarter view from the front-right, the main entrance "
            "facing the viewer, the whole building visible on a thin square base of {base}, isolated on a plain flat white "
            "background, nothing cropped. Semi-realistic 1880s American frontier town, richly detailed like a premium "
            "mobile city-builder, warm clean studio lighting with soft shadows. No text, no signs with letters, no logos, no people.")

A = {}


def loco(i, length, what, **kw):
    A[i] = dict(cat="Locomotives", length=length, prompt=VEHICLE.format(what=what), **kw)


def wagon(i, length, what, **kw):
    A[i] = dict(cat="Wagons", length=length, prompt=VEHICLE.format(what=what), **kw)


def bldg(i, what, base="packed earth", footprint=11.0, **kw):
    A[i] = dict(cat="Buildings", footprint=footprint, prompt=BUILDING.format(what=what, base=base), **kw)


# ----------------------------------------------------------------------------------- locomotives
loco("lark", 15.5, "an original American 4-4-0 steam locomotive with its separate coal tender: iron cowcatcher, square oil headlamp, black smokebox, tall flared diamond smokestack, long deep crimson (#7A1F1F) boiler with polished brass bands, brass sand and steam domes, brass bell, crimson wooden cab with windows at the rear, crimson tender with coal; a four-wheel leading truck and two large crimson spoked driving wheels with steel side rods, fine gold (#D4AF37) lining")
loco("dusty", 8.5, "an original small 0-4-0 saddle-tank steam yard switcher with NO tender: a short boiler with a rounded saddle water tank over it, painted dark forest green (#3B5D3A) with brass (#C9A227) trim and lining, short straight smokestack with a brass cap, brass dome and bell, compact cab with a coal bunker at the back, four small green spoked driving wheels with steel side rods, wooden pilot beam with footboards and a small oil headlamp")
loco("redmesa", 15.9, "an original American 2-6-0 Mogul freight steam locomotive with its coal tender: slate blue (#2F3E59) boiler and cab with burnished copper (#B87333) bands and trim, black smokebox, flared diamond smokestack, copper domes and bell, iron cowcatcher, oil headlamp, a two-wheel leading truck and three pairs of large blue spoked driving wheels with steel rods, slate blue tender with coal")
loco("copperqueen", 17.0, "an original American 4-6-0 ten-wheeler steam locomotive with its coal tender: deep emerald green (#14532D) boiler, cab and tender with gleaming copper-gold (#E0A458) bands, domes and lining, black smokebox, straight capped smokestack, electric headlamp, iron pilot, a four-wheel leading truck and three pairs of green spoked driving wheels with steel rods, ornate wooden cab")
loco("ironbison", 19.6, "an original heavy 2-8-2 freight steam locomotive with a large coal tender: massive black (#1C1C1C) boiler with red-oxide (#A33B20) running boards, wheel rims and trim, air pumps on the side, big headlamp, short capped stack, steel pilot, a two-wheel leading truck, four pairs of black driving wheels with red rims and heavy steel rods, a trailing truck under the cab, rugged and powerful")
loco("sundown", 19.4, "an original 1930s art-deco streamlined 4-6-4 steam locomotive with a matching streamlined tender: a smooth bullet-nosed shroud in deep navy blue (#1F3A68) with flowing cream-gold (#E8C26A) speed stripes, side skirts with the large spoked driving wheels and rods visible below, a chrome nose headlamp, enclosed cab, tender shrouded to match")
loco("silverarrow", 15.0, "an original 1940s streamlined diesel passenger locomotive, a single cab unit: fluted brushed stainless steel (#C0C6CC) body with a rounded slanted nose and wraparound cab windows, bold red (#B22222) nose stripes and side band, round porthole windows along the side, two three-axle trucks, chrome headlights", flip=True)
loco("thunderhead", 15.1, "an original 1940s streamlined diesel freight-passenger locomotive cab unit: deep royal purple (#5B2A86) body with bright gold (#F2C14E) chevron stripes sweeping back from a rounded bulldog nose, gold side band, wraparound cab windows, louvered side grilles, two three-axle trucks, chrome headlights", twin=True)

A["dusty"]["flip"] = True        # saddle tank: the dome/cab outrank the stack in height
A["thunderhead"]["flip"] = True  # diesel: no stack to find; cab noses must face outward

# ----------------------------------------------------------------------------------- wagons
wagon("flatcar", 8.0, "an original 1880s wooden railroad flatcar, empty: weathered brown (#6B4226) plank deck with stake pockets and a few wooden stakes, steel truss rods underneath, two four-wheel trucks, link-and-pin couplers")
wagon("hopper", 8.0, "an original 1880s open-top ore hopper railroad car: riveted dark iron grey (#3A3A3A) steel body with sloped bottom chutes, side ribs, two four-wheel trucks, empty")
wagon("boxcar", 8.0, "an original 1880s wooden railroad boxcar: oxide red (#8C2F1B) vertical board siding, sliding side door, roof walk with a brake wheel, ladders at the corners, two four-wheel trucks")
wagon("stockcar", 8.0, "an original 1880s wooden livestock stock car: sienna (#A0522D) slatted sides with gaps, sliding door, roof walk, ladders, two four-wheel trucks, empty")
wagon("mailcoach", 8.0, "an original 1880s wooden railway mail and express car: dark green (#1F4E3D) varnished wood sides with fine gold lining, clerestory roof, a mail catcher arm, small windows, open platforms at the ends, two four-wheel trucks")
wagon("longflat", 10.0, "an original long heavy-duty wooden railroad flatcar, empty: dark brown (#5A3A1E) plank deck, steel side sills with stake pockets and stakes, fishbelly steel center sill, two four-wheel trucks")
wagon("steelhopper", 10.0, "an original 1920s twin-bay steel hopper railroad car: riveted slate grey (#55606B) steel with outside ribs, twin bottom discharge bays, ladders, two four-wheel trucks, empty")
wagon("reefer", 10.0, "an original 1920s insulated refrigerator boxcar (reefer): cream (#E8E2D0) wood sides with dark brown roof and ends, ice hatches on the roof, plug doors, roof walk, two four-wheel trucks")
wagon("tankcar", 8.0, "an original 1900s railroad tank car: black (#202020) riveted cylindrical tank on a steel frame with a top dome and safety walkway rails, ladders, two four-wheel trucks")
wagon("pullman", 10.0, "an original 1920s heavyweight Pullman passenger coach: dark midnight blue (#2E2A5A) steel sides with thin gold lining, a row of large windows, clerestory roof, vestibules at both ends, two six-wheel trucks")

# ----------------------------------------------------------------------------------- buildings
bldg("cabin", "an original settler's log cabin: notched log walls, a wood shingle roof, a stone chimney, a small porch with posts, a wood pile and a rain barrel")
bldg("clapboard", "an original two-storey clapboard family house: pale blue-grey painted clapboard siding with white trim, a front porch with railings, gabled shingle roof, brick chimney, a little picket fence")
bldg("boarding", "an original three-storey wooden boarding house: tan clapboard with green shutters, a long balcony along the upper floor, many windows, a false-front parapet, wooden boardwalk in front")
bldg("rowhouse", "an original row of three attached red-brick townhouses: arched windows, stone lintels, stoops with iron railings, slate mansard roof with dormers, chimneys")
bldg("grandhotel", "an original grand Victorian frontier hotel: four storeys of cream stone and red brick, a corner turret with a copper dome, wraparound verandah with iron columns, many windows with awnings, ornate cornice")
bldg("sawmill", "an original water-powered frontier sawmill: a timber mill shed with an open side showing a big circular saw blade, a log ramp with stacked logs, piles of fresh planks, a small smokestack", spin=True, smoke=True)
bldg("gristmill", "an original frontier grist mill: a tall timber mill house with a stone base and a large wooden windmill with four sails on top, sacks of grain by the door", spin=True)
bldg("packing", "an original frontier meat packing house: a long red-painted wooden building with a loading dock, cattle pens with rails beside it, a water tank on the roof, a small chimney", smoke=True)
bldg("smelter", "an original frontier ore smelter: a stone and iron furnace building with a tall brick smokestack, ore carts, a heap of dark ore, glowing furnace doors, corrugated iron roof", smoke=True)
bldg("toolworks", "an original 1890s tool works factory: a brick workshop with big arched windows, a sawtooth skylight roof, a forge chimney, crates of tools and an anvil outside", smoke=True)
bldg("wireworks", "an original 1900s copper wire works factory: a brick and corrugated-iron factory hall with large spools of copper wire stacked outside, a chimney, rooftop ventilators", smoke=True)
bldg("assay", "an original frontier assay office: a sturdy stone building with iron-barred windows, a heavy door, gold-trimmed cornice, a small balance-scale sign shape, crates and a strongbox on the porch")
bldg("refinery", "an original 1910s oil refinery: steel distillation towers, round storage tanks, pipes and catwalks, a tall flare stack, a small brick control house", smoke=True)
bldg("watertower", "an original wooden railroad water tower: a large round wooden tank with iron hoops and a conical shingled roof on a tall timber trestle with cross bracing, a swinging spout", footprint=7.0)
bldg("saloon", "an original frontier saloon: a two-storey false-front wooden building painted oxblood red with cream trim, swinging half doors, a balcony on the upper floor, hitching rail and water trough out front")
bldg("telegraph", "an original frontier telegraph office: a small wooden office with a bay window, a tall telegraph pole with many glass insulators and wires beside it, a bench on the porch")
bldg("schoolhouse", "an original one-room frontier schoolhouse: white-painted clapboard, a small bell tower with a bell on the roof, tall windows, front steps, a little fenced yard")
bldg("bank", "an original frontier territorial bank: a solid two-storey stone and brick building with columns at the entrance, arched windows with iron grilles, a gilded cornice, a heavy vault door visible inside the doorway")
bldg("clocktower", "an original town clock tower: a tall brick and stone tower with a large clock face on each side, a copper-green pointed roof with a weathervane, an arched base")
bldg("lamppost", "an original Victorian cast-iron gas street lamp: a fluted black iron post on a round base with a four-sided glass lantern on top and a small ladder bar", base="cobblestones", footprint=2.2, height=5.5)
bldg("garden", "an original small town flower garden: neat flower beds with red, yellow and purple blooms, low hedges, a white wooden picket fence, a gravel path and a wooden bench", base="grass", footprint=11.0)
bldg("fountain", "an original town square fountain: a round stone basin with a tiered cast-iron fountain spraying water, a ring of cobblestones and four flower planters", base="cobblestones", footprint=9.0)

# Station pieces (wide footprints: the long side runs along the tracks)
bldg("station", "an original long frontier railway depot station, about four and a half times as long as it is deep: a central two-storey block with a clock tower and a pointed copper-green roof, two long single-storey wings with red hip roofs, cream board-and-batten walls with brown trim, a wooden platform canopy with brackets along the front, ticket windows, benches, luggage carts", base="a stone platform edge", footprint=36.0, wide=True)
A["station"]["hero"] = True  # the centrepiece, seen up close: locomotive-grade mesh and 2K maps
bldg("freightshed", "an original wooden railway freight shed, about twice as long as deep: red-painted plank walls, a raised wooden loading dock with sliding doors along the front, a corrugated tin roof with a deep overhang, crates and barrels on the dock", footprint=14.0, wide=True)

# Town life props (vehicle-style side views; the horse is rigged and animated by Tools/blender/horse.py)
# Construction site shown on a plot while a building goes up (replaces the old procedural scaffold).
bldg("construction_site", "an original 1880s frontier building construction site: the bare timber framing of a two-storey house "
     "half built (raw pine studs, joists and rafters, no walls yet on the upper floor), wooden scaffolding with plank walkways and "
     "ladders around two sides, a tall wooden gin-pole hoist with a rope and pulley lifting a beam, neat stacks of fresh lumber, "
     "a pile of red bricks, a sawhorse with a hand saw, nail kegs and a wheelbarrow", base="bare packed dirt with sawdust", footprint=11.0)
# Scattered desert boulders (replace the procedural rocks).
A["boulders"] = dict(cat="Props", length=6.0, faces=4000, front="none", prompt=(
    "Game asset reference render of a natural cluster of three weathered desert boulders of different sizes: rounded red-brown "
    "and buff sandstone with cracks, layered strata, wind-worn hollows and a few small loose stones, slightly elevated three-quarter "
    "view, isolated on a plain flat white background, the whole cluster visible and nothing cropped, no ground plane. Semi-realistic, "
    "richly detailed like a premium mobile tycoon game, clean studio lighting with soft shadows. No text, no plants, no people."))
# ----------------------------------------------------------------------------------- haulers (road and river freight)
def hauler(i, length, what, faces=15000):
    A[i] = dict(cat="Haulers", length=length, faces=faces, front="auto", prompt=VEHICLE.format(what=what).replace("no rails, no ground", "no rails, no water, no ground"))


hauler("sternwheeler", 14.0, "an original 1880s American river sternwheeler paddle steamer: a shallow white wooden hull, two decks of white cabins with fine gingerbread railings and rows of small windows, a small pilot house on the top deck, two tall black smokestacks with crown tops side by side near the front, a big red paddle wheel at the stern, wooden crates and barrels stacked on the low fore deck")
hauler("steamer", 20.0, "an original 1890s lake cargo-passenger steamer: a long white hull with a dark red waterline band, a long white deck house with portholes and windows, a single tall buff funnel with a black top amidships, a pilot house with big windows forward, two thin masts, small lifeboats on davits, a cargo hatch with crates on the fore deck")
hauler("motortruck", 5.4, "an original 1920s American motor freight truck: a dark green cab with black fenders and running boards, a long bonnet with an upright radiator grille, an open wooden stake bed loaded with wooden crates and sacks, wooden spoked wheels with solid rubber tyres, brass headlamps", faces=12000)
hauler("expressvan", 5.8, "an original 1930s express delivery van: a deep maroon (#7A1F1F) enclosed box body with polished brass trim and fine gold pinstriping but NO lettering, a rounded bonnet with a chrome grille, black fenders, white-wall tyres, round headlamps", faces=12000)

A["mule"] = dict(cat="Props", length=2.4, faces=6000, front="auto", animal=True, prompt=VEHICLE.format(what="a single sturdy grey-brown pack mule with long ears, carrying a wooden pack saddle loaded with two lashed wooden crates and a rolled canvas bundle, standing square and calm with all four legs straight and clearly separated, head forward, tail hanging down").replace("vehicle", "animal").replace("no rails, no ground", "no rider, no ground").replace("No text, no numbers, no logos, no people.", "No text, no people."))
# Haulage yard, wharf and the finished Eagle Lake projects.
bldg("freightbarn", "an original 1880s teamsters' freight barn and stable: a long weathered red board-and-batten barn with a gambrel roof, big sliding wagon doors, a hay loft door with a hoist beam, a lean-to shed with feed sacks, a hitching rail and a water trough", footprint=16.0)
bldg("wharf", "an original 1880s river wharf warehouse: a weathered grey timber freight shed on heavy pilings with a wide loading platform, a timber cargo crane with a hook, stacked crates and barrels, a lantern on a post", base="timber planks", footprint=14.0)
bldg("lighthouse_done", "an original 1880s lake lighthouse station on a rocky knoll: a tall white conical brick tower with broad red bands, a black iron lantern room with glass and a gallery railing, a small white keeper's cottage with a red roof attached by a covered walkway, an oil store shed", base="rocky grass", footprint=18.0, height=24.0)
bldg("lodge_done", "an original 1890s grand log lakeshore lodge: two storeys of dark honey-brown logs, a steep green metal roof with three dormers, two massive fieldstone chimneys, a long covered veranda with log posts and rocking chairs across the front, a stone foundation", base="grass and gravel", footprint=22.0)

# Storyline landmarks (finished art for later territory projects).
bldg("winery_done", "an original 1890s valley winery estate: a two-storey stone winery hall with arched doors and a red tile roof, a round stone fermenting tower, wooden barrels stacked outside, a small vineyard of green vine rows beside it", base="grass and gravel", footprint=22.0)
bldg("mission_done", "an original 1880s adobe frontier mission church: white-washed adobe walls, a tall bell tower with a bronze bell, an arched wooden doorway, a red clay tile roof, a walled garden courtyard with a well and a cactus", base="dry earth", footprint=20.0)
bldg("opera_done", "an original 1890s frontier opera house: a grand three-storey red brick theatre with tall arched windows, white stone trim, a columned entrance portico, a copper-green mansard roof with a small cupola, gas lamps by the steps", base="cobblestones", footprint=20.0)
bldg("capitol_done", "an original 1890s territorial capitol building: a symmetrical grey stone government hall with two wings, a columned portico with a pediment, a tall copper-green dome topped with a lantern, broad front steps, flag poles", base="cobblestones and lawn", footprint=26.0)

A["horse"] = dict(cat="Props", length=2.8, faces=6000, front="auto", animal=True, prompt=VEHICLE.format(what="a single sturdy chestnut-brown draft horse in a dark leather work harness with a collar and blinkers, standing square and calm with all four legs straight and clearly separated, head forward, tail hanging down").replace("vehicle", "animal").replace("no rails, no ground", "no cart, no rider, no ground").replace("No text, no numbers, no logos, no people.", "No text, no people."))
A["covered_wagon"] = dict(cat="Props", length=6.2, faces=6000, front="auto", prompt=VEHICLE.format(what="an original 1870s prairie covered wagon with NO horses: a white weathered canvas bonnet stretched over wooden hoops, a blue-grey painted wooden box body, red spoked wooden wheels with iron tyres (larger at the back), a wooden tongue pole sticking out at the front, a water barrel and a toolbox strapped to the side"))

# Vegetation (instanced thousands of times: tiny triangle budgets)
TREE = ("Game asset reference render of {what}, the whole plant visible from the ground to the top, standing alone, "
        "slightly elevated three-quarter view, isolated on a plain flat white background, nothing cropped, no ground "
        "patch. Semi-realistic stylized like a premium mobile tycoon game: clear chunky foliage masses, rich natural color, "
        "soft studio light. No text.")
for _i, _w in {
    "pine": "a tall dense ponderosa pine tree with layered drooping dark green branches and a straight reddish-brown trunk",
    "pine2": "a slender young Douglas fir with a narrow pointed crown of blue-green needles",
    "cottonwood": "a big rounded cottonwood tree with a thick gnarled trunk and bright yellow-green leafy crown",
    "saguaro": "a tall saguaro cactus with two upturned arms and ribbed green skin",
    "sage": "a low rounded silvery sagebrush shrub",
}.items():
    A[_i] = dict(cat="Trees", footprint=1.0, prompt=TREE.format(what=_w), tree=True)


def hf(*args):
    print("+ higgsfield", " ".join(a if len(a) < 60 else a[:57] + "..." for a in args))
    r = subprocess.run(["higgsfield", *args], capture_output=True, text=True)
    out = (r.stdout + r.stderr).strip().splitlines()
    url = next((l for l in reversed(out) if l.startswith("http")), None)
    if not url:
        raise RuntimeError("\n".join(out[-5:]))
    return url


def fetch(url, path):
    req = urllib.request.Request(url, headers={"User-Agent": "Mozilla/5.0"})
    with urllib.request.urlopen(req, timeout=300) as r, open(path + ".part", "wb") as f:
        f.write(r.read())
    os.replace(path + ".part", path)  # never expose a half-written file to the importer


def concept(i):
    p = os.path.join(SRC, i + ".png")
    if os.path.exists(p):
        return print("have", p)
    a = A[i]
    ar = "16:9" if a["cat"] == "Props" else "21:9" if a["cat"] != "Buildings" else "16:9" if a.get("wide") else "1:1"
    url = hf("generate", "create", "gpt_image_2_5", "--aspect_ratio", ar, "--quality", "high", "--resolution", "2k",
             "--background", "opaque", "--prompt", a["prompt"], "--wait")
    fetch(url, p)
    print("concept", i, url)


def model(i):
    p, g = os.path.join(SRC, i + ".png"), os.path.join(SRC, i + ".glb")
    if os.path.exists(g):
        return print("have", g)
    if not os.path.exists(p):
        raise SystemExit("no concept for " + i)
    url = hf("generate", "create", "tripo_h3_1_image_to_3d", "--image", p, "--texture_quality", "detailed",
             "--geometry_quality", "standard", "--pbr", "true", "--wait", "--wait-timeout", "25m")
    fetch(url, g)
    print("model", i, url)


TREE_HEIGHT = {"pine": 11, "pine2": 9, "cottonwood": 9, "saguaro": 6, "sage": 1.4}
# (near, far) triangle budgets: near cells draw the detailed mesh, distant cells the light one.
TREE_VOXELS = {"sage": (22, 12)}  # voxels across the plant's height for the solid canopy remesh
TREE_FACES = {"pine": (1500, 250), "pine2": (1200, 200), "cottonwood": (1800, 300), "saguaro": (500, 150), "sage": (300, 90)}


def do_import(i, out_id=None, faces=None):
    a = A[i]
    if a.get("tree") and out_id is None:
        near, far = TREE_FACES[i]
        do_import(i, i, near)
        do_import(i, i + "_far", far)
        return
    out_id = out_id or i
    g = os.path.join(SRC, i + ".glb")
    cmd = ["blender", "-b", "--factory-startup", "-P", os.path.join(ROOT, "Tools", "blender", "import_ai.py"), "--", g, a["cat"], out_id]
    if a.get("tree"):
        cmd += ["--height", str(TREE_HEIGHT[i]), "--front", "none", "--faces", str(faces), "--tex", "512",
                "--remesh", str(TREE_VOXELS.get(i, (70, 36))[0 if out_id == i else 1])]
    elif a["cat"] == "Buildings":
        small = a["footprint"] < 4
        faces = "6000" if small else "40000" if a.get("hero") else "20000"
        cmd += ["--footprint", str(a["footprint"]), "--faces", faces, "--tex", "512" if small else "2048" if a.get("hero") else "1024"]
        if "height" in a:
            cmd += ["--height", str(a["height"])]
        if a.get("smoke"):
            cmd += ["--smoke"]
        if a.get("wide"):
            cmd += ["--wide"]
    else:
        cmd += ["--length", str(a["length"]), "--front", a.get("front", "auto"),
                "--faces", str(a.get("faces", 40000 if a["cat"] == "Locomotives" else 15000)),
                "--tex", "2048" if a["cat"] == "Locomotives" else "1024"]
        if a.get("twin"):
            cmd += ["--twin"]
        if a.get("flip"):
            cmd += ["--flip"]
    r = subprocess.run(cmd, capture_output=True, text=True)
    print("\n".join(l for l in r.stdout.splitlines() if "[ai]" in l) or r.stdout[-2000:] + r.stderr[-2000:])
    rebake(out_id, a, src=i)
    if a.get("animal"):
        # Rig + walk cycle, then move the result (and its textures) to Models/Animals.
        props, animals = (os.path.join(ROOT, "Assets", "Resources", "Models", c) for c in ("Props", "Animals"))
        r = subprocess.run(["blender", "-b", "--factory-startup", "-P", os.path.join(ROOT, "Tools", "blender", "horse.py"), "--",
                            os.path.join(props, i + ".fbx"), animals], capture_output=True, text=True)
        print("\n".join(l for l in r.stdout.splitlines() if "[horse]" in l) or r.stdout[-2000:] + r.stderr[-2000:])
        for kind in ("_albedo.jpg", "_normal.jpg"):
            os.replace(os.path.join(props, i + kind), os.path.join(animals, i + kind))
        for f in (i + ".fbx", i + ".fbx.meta", i + "_albedo.jpg.meta", i + "_normal.jpg.meta"):
            if os.path.exists(os.path.join(props, f)):
                os.remove(os.path.join(props, f))


def rebake(i, a, src=None):
    """Bakes the full-detail colours onto the decimated mesh with clean UVs (see Tools/blender/rebake.py)."""
    from PIL import Image
    d = os.path.join(ROOT, "Assets", "Resources", "Models", a["cat"])
    fbx = os.path.join(d, i + ".fbx")
    tmp = os.path.join(ROOT, "Temp", "bake", i)
    os.makedirs(os.path.dirname(tmp), exist_ok=True)
    size = "2048" if a["cat"] == "Locomotives" or a.get("hero") else "512" if a.get("tree") or a.get("footprint", 9) < 4 else "1024"
    cmd = ["blender", "-b", "--factory-startup", "-P", os.path.join(ROOT, "Tools", "blender", "rebake.py"), "--",
           os.path.join(SRC, (src or i) + ".glb"), fbx, tmp, "--size", size, "--normal"] + (["--twin"] if a.get("twin") else [])
    r = subprocess.run(cmd, capture_output=True, text=True)
    lines = [l for l in r.stdout.splitlines() if "[bake]" in l]
    if not any("exported" in l for l in lines):
        print("BAKE FAILED", i, (r.stdout + r.stderr)[-1500:])
        return
    for kind in ("albedo", "normal"):
        im = Image.open(tmp + "_" + kind + ".png").convert("RGB")
        if kind == "albedo" and (src or i) == "cottonwood":
            # The concept's leaves came out lime: deepen them toward the grass.
            import numpy as np
            from PIL import ImageEnhance
            im = ImageEnhance.Color(Image.fromarray((np.asarray(im) * [0.82, 0.92, 0.78]).clip(0, 255).astype("uint8"))).enhance(0.85)
        im.save(os.path.join(d, i + "_" + kind + ".jpg"), quality=90)
    ms = os.path.join(d, i + "_ms.png")  # old-UV metal map no longer lines up
    for f in (ms, ms + ".meta"):
        if os.path.exists(f):
            os.remove(f)
    print("[bake]", i, lines[0])


if __name__ == "__main__":
    os.makedirs(SRC, exist_ok=True)
    cmd, ids = sys.argv[1], sys.argv[2:]
    if cmd == "list":
        for k, v in A.items():
            print(k, v["cat"], "concept" if os.path.exists(os.path.join(SRC, k + ".png")) else "-", "glb" if os.path.exists(os.path.join(SRC, k + ".glb")) else "-")
        sys.exit()
    fn = {"concept": concept, "model": model, "import": do_import}[cmd]
    for i in ids or list(A):
        try:
            fn(i)
        except Exception as e:  # keep going; report at the end
            print("FAILED", i, e)
