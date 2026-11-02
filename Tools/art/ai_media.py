#!/usr/bin/env python3
"""Key art and celebration videos (Higgsfield): GPT Image 2.5 stills, Seedance 2.0 image-to-video.

  python3 Tools/art/ai_media.py still <id>...   # still frame (skips existing)
  python3 Tools/art/ai_media.py video <id>...   # animate the approved still (skips existing)
  python3 Tools/art/ai_media.py install         # copy finished media into the Unity project

Work files go to art-src/media/. Never re-run a paid step for an id that already has its output.
Everything is original: no real railroads, brands, logos or text in frame.
"""
import os
import shutil
import subprocess
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from ai_assets import ROOT, fetch, hf  # noqa: E402

SRC = os.path.join(ROOT, "art-src", "media")
STYLE = ("Cinematic semi-realistic illustration in the style of a premium mobile railroad tycoon game, 1880s American "
         "frontier, warm golden light, rich color, crisp detail, wide 16:9 composition. No text, no letters, no logos, "
         "no watermarks.")

M = {
    "loading": dict(still="Epic key art: a crimson and brass 4-4-0 steam locomotive with a coal tender charging toward the viewer "
                          "along a curving track through a sunlit prairie valley, billowing white steam, a small frontier town with "
                          "a clock-tower depot in the middle distance, red mesas and pine-covered mountains under a dramatic sky. " + STYLE),
    "contract": dict(still="A crimson and brass steam locomotive pulling into a busy wooden frontier station at golden hour, townsfolk "
                           "on the platform waving hats, freight wagons loaded with timber and ore, steam and dust in the warm light. " + STYLE,
                     motion="The locomotive rolls slowly into the station and stops with a big puff of steam; people on the platform "
                            "cheer and wave their hats; steam drifts through the golden light; gentle camera push-in."),
    "levelup": dict(still="A frontier railroad town at sunrise seen from a low hill: a clock-tower depot, wooden and brick buildings, "
                          "a freight yard with trains, pine forests and red mesas beyond, a few fireworks bursting in the pastel sky. " + STYLE,
                    motion="Slow crane shot rising over the town as the sun comes up; fireworks burst and sparkle in the sky; smoke "
                           "curls from chimneys; a train moves through the yard."),
    "newloco": dict(still="A gleaming emerald-green and copper 4-6-0 steam locomotive standing in the open doorway of a brick roundhouse, "
                          "dramatic shafts of morning light through the smoke, a turntable in front, railroad workers admiring it. " + STYLE,
                    motion="The locomotive slowly rolls forward out of the roundhouse into the light, steam hissing from its cylinders "
                           "and smoke rising from the stack; light rays sweep through the haze; camera slowly orbits."),
    "project": dict(still="A newly finished steel truss railroad bridge spanning a red-rock canyon river at golden hour, workers and "
                          "a crowd celebrating on the bank, bunting on the bridge, a steam train approaching across it. " + STYLE,
                    motion="The steam train crosses the new bridge as the crowd cheers and throws hats in the air; bunting flutters; "
                           "steam trails behind the train; slow lateral tracking shot."),
    "goldenage": dict(still="An art-deco streamlined navy-blue and gold steam locomotive racing across a desert plain at sunset, red "
                            "mesas behind, long golden light, a sense of speed and triumph. " + STYLE,
                      motion="The streamlined locomotive races past the camera at speed, steam streaming back over its shroud; dust "
                             "swirls; the sun flares on its gold stripes; fast dramatic tracking shot."),
}

# Full-body characters (transparent background), shown beside panels and in story scenes.
CHAR = ("Full-body portrait of {what}, standing in a relaxed confident pose, facing three-quarters toward the viewer, "
        "head to boots visible, on a fully transparent background. Semi-realistic 3D rendered game character like a premium "
        "mobile tycoon game, soft studio lighting, crisp detail. Nothing else in the frame. No text.")
CHARACTERS = {
    "char_foreman": "Hank, a weathered 1880s railroad foreman in his fifties with a big grey moustache, brown bowler hat, red "
                    "neckerchief, rolled shirt sleeves, leather vest, canvas work trousers and boots, holding a coal shovel",
    "char_engineer": "Clara Whitlock, a sharp 1880s territory surveyor-engineer woman in her thirties, auburn hair pinned up, "
                     "white blouse with a brass brooch, brown tweed vest and long plaid skirt, holding rolled blue blueprints",
    "char_investor": "Silas Pemberton, a portly 1880s railroad investor in his sixties, grey mutton-chop whiskers, black top "
                     "hat, pinstripe waistcoat with a gold watch chain, dark frock coat, hands resting on a silver-topped cane",
    "char_rival": "Cyrus Vane, a lean, smug rival railroad baron in his forties, slicked-back black hair and a thin black "
                  "moustache, black flat-brimmed hat, charcoal three-piece suit with a crimson cravat and silver stickpin, "
                  "black leather gloves, one hand on his hip, a cold confident smirk",
}

# Territory project finale backdrops (one per DepthContent project id).
SCENE = ("Wide cinematic establishing shot for a mobile railroad tycoon game story scene: {what}. 1880s American frontier, "
         "golden-hour light, semi-realistic painted 3D style, rich color, empty foreground space at the left and right "
         "edges for characters. No people in the foreground, no text, no letters, no logos.")
SCENES = {
    "scene_telegraph": "a brand-new telegraph line of tall wooden poles marching across red badlands toward a distant wooden cavalry fort, a steam train passing beside it",
    "scene_trestle": "a tall new timber railroad trestle crossing a winding river gorge, a steam train crossing it with a plume of steam",
    "scene_mineshaft": "a big new timbered mine headframe over a deep shaft on a rocky hillside, ore carts and a rail spur, lamps glowing",
    "scene_resupply": "a wooden cavalry fort in the snow at dusk receiving a supply train, warm lit windows, flags flying",
    "scene_dam": "a massive new curved stone dam holding back a blue lake in a red rock canyon, a railroad bridge across the canyon below, the valley town lights coming on",
    "scene_goldenspike": "two steam locomotives meeting nose to nose on a desert plain where the rails join, flags and bunting, a crowd's tents and wagons in the distance",
}


def portrait(i):
    p = os.path.join(SRC, i + ".png")
    if os.path.exists(p):
        return print("have", p)
    if i in CHARACTERS:
        args = ["--aspect_ratio", "2:3", "--background", "transparent", "--prompt", CHAR.format(what=CHARACTERS[i])]
    else:
        args = ["--aspect_ratio", "16:9", "--background", "opaque", "--prompt", SCENE.format(what=SCENES[i])]
    url = hf("generate", "create", "gpt_image_2_5", "--quality", "high", "--resolution", "2k", *args, "--wait")
    fetch(url, p)
    print("art", i, url)


def install_story():
    from PIL import Image
    d = os.path.join(ROOT, "Assets", "Resources", "UI", "Story")
    os.makedirs(d, exist_ok=True)
    for i in list(CHARACTERS) + list(SCENES):
        p = os.path.join(SRC, i + ".png")
        if not os.path.exists(p):
            continue
        im = Image.open(p)
        if i in CHARACTERS:
            im = im.convert("RGBA")
            box = im.getchannel("A").point(lambda a: 255 if a > 12 else 0).getbbox()
            if box:
                im = im.crop(box)
            im.thumbnail((1024, 1024))
            im.save(os.path.join(d, i + ".png"), optimize=True)
        else:
            im = im.convert("RGB")
            im.thumbnail((1920, 1920))
            im.save(os.path.join(d, i + ".jpg"), quality=86)
    print("installed story art")


def still(i):
    p = os.path.join(SRC, i + ".png")
    if os.path.exists(p):
        return print("have", p)
    url = hf("generate", "create", "gpt_image_2_5", "--aspect_ratio", "16:9", "--quality", "high", "--resolution", "2k",
             "--background", "opaque", "--prompt", M[i]["still"], "--wait")
    fetch(url, p)
    print("still", i, url)


def video(i):
    if "motion" not in M[i]:
        return print("no video for", i)
    p, v = os.path.join(SRC, i + ".png"), os.path.join(SRC, i + ".mp4")
    if os.path.exists(v):
        return print("have", v)
    url = hf("generate", "create", "seedance_2_0", "--start-image", p, "--prompt", M[i]["motion"], "--duration", "5",
             "--resolution", "720p", "--aspect_ratio", "16:9", "--wait", "--wait-timeout", "25m")
    fetch(url, v)
    print("video", i, url)


def install():
    """Videos -> StreamingAssets (VideoPlayer streams them); loading art -> Resources."""
    vid_dir = os.path.join(ROOT, "Assets", "StreamingAssets", "Videos")
    os.makedirs(vid_dir, exist_ok=True)
    for i in M:
        v = os.path.join(SRC, i + ".mp4")
        if os.path.exists(v):
            # Re-encode small for phones if ffmpeg is available; otherwise copy as is.
            out = os.path.join(vid_dir, i + ".mp4")
            if shutil.which("ffmpeg"):
                subprocess.run(["ffmpeg", "-y", "-loglevel", "error", "-i", v, "-vf", "scale=1280:-2", "-c:v", "libx264",
                                "-profile:v", "main", "-crf", "25", "-preset", "slow", "-pix_fmt", "yuv420p", "-c:a", "aac",
                                "-b:a", "96k", "-movflags", "+faststart", out], check=True)
            else:
                shutil.copy(v, out)
            print("installed", out)
            # Linux Editors can't decode H.264: keep a VP8 copy in Library/ (not shipped) for testing there.
            if shutil.which("ffmpeg"):
                lib = os.path.join(ROOT, "Library", "EditorVideos")
                os.makedirs(lib, exist_ok=True)
                subprocess.run(["ffmpeg", "-y", "-loglevel", "error", "-i", v, "-vf", "scale=1280:-2", "-c:v", "libvpx", "-b:v", "2M",
                                "-c:a", "libvorbis", os.path.join(lib, i + ".webm")], check=True)
        p = os.path.join(SRC, i + ".png")
        if os.path.exists(p) and i != "loading":
            from PIL import Image
            d = os.path.join(ROOT, "Assets", "Resources", "UI", "Cinematics")
            os.makedirs(d, exist_ok=True)
            im = Image.open(p).convert("RGB")
            im.thumbnail((1280, 1280))
            im.save(os.path.join(d, i + ".jpg"), quality=85)  # shown if the clip can't play
    art = os.path.join(SRC, "loading.png")
    if os.path.exists(art):
        dst = os.path.join(ROOT, "Assets", "Resources", "UI")
        os.makedirs(dst, exist_ok=True)
        from PIL import Image
        im = Image.open(art).convert("RGB")
        im.thumbnail((2048, 2048))
        im.save(os.path.join(dst, "LoadingArt.jpg"), quality=88)
        print("installed loading art")


if __name__ == "__main__":
    os.makedirs(SRC, exist_ok=True)
    cmd, ids = sys.argv[1], sys.argv[2:] or list(M)
    if cmd == "install":
        install()
        install_story()
    elif cmd == "story":
        for i in ids if sys.argv[2:] else list(CHARACTERS) + list(SCENES):
            try:
                portrait(i)
            except Exception as e:
                print("FAILED", i, e)
    else:
        for i in ids:
            try:
                {"still": still, "video": video}[cmd](i)
            except Exception as e:
                print("FAILED", i, e)
