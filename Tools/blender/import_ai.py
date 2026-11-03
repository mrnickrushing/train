"""Turns an AI-generated GLB (Tripo via Higgsfield) into game-ready drop-in art.

  blender -b -P Tools/blender/import_ai.py -- <in.glb> <Category> <id> --length 15.5 [--front -y] [--faces 60000]
  blender -b -P Tools/blender/import_ai.py -- <in.glb> Buildings <id> --footprint 11.5 [--front -y]

Steps: join, orient (vehicles: long axis to +Y, front at +Y; buildings: door toward -Y), scale to game
size, sit on y = 0 (rail top / ground), decimate to a mobile budget, downsample the PBR maps and pack
them for URP (albedo with AO folded in, metallic+smoothness, normal), add Smoke anchors, export FBX.
"""
import math
import os
import sys

import bpy
import numpy as np
from mathutils import Matrix, Vector

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))


def arg(flag, default=None, cast=str):
    a = sys.argv[sys.argv.index("--") + 1:]
    return cast(a[a.index(flag) + 1]) if flag in a else default


def bounds(o):
    ws = [o.matrix_world @ Vector(c) for c in o.bound_box]
    mn = Vector((min(v.x for v in ws), min(v.y for v in ws), min(v.z for v in ws)))
    mx = Vector((max(v.x for v in ws), max(v.y for v in ws), max(v.z for v in ws)))
    return mn, mx


def image_px(img):
    w, h = img.size
    a = np.empty(w * h * 4, dtype=np.float32)
    img.pixels.foreach_get(a)
    return a.reshape(h, w, 4)


def save_png(arr, path, colorspace):
    h, w, _ = arr.shape
    img = bpy.data.images.new(os.path.basename(path), w, h, alpha=True)
    img.colorspace_settings.name = colorspace
    img.pixels.foreach_set(np.ascontiguousarray(arr, dtype=np.float32).ravel())
    img.filepath_raw = path
    img.file_format = "PNG"
    img.save()


def to_jpg(png):
    """Albedo and normal maps ship as quality-90 JPG (Unity recompresses to ASTC anyway); keeps the repo small."""
    import subprocess
    jpg = png[:-4] + ".jpg"
    subprocess.run(["python3", "-c",
                    "from PIL import Image;Image.open(%r).convert('RGB').save(%r,quality=90)" % (png, jpg)], check=True)
    os.remove(png)


def resize(img, size):
    if img.size[0] > size:
        img.scale(size, size)
    return img


def main():
    a = sys.argv[sys.argv.index("--") + 1:]
    glb, category, model_id = a[0], a[1], a[2]
    length = arg("--length", None, float)
    footprint = arg("--footprint", None, float)
    height = arg("--height", None, float)
    twin = "--twin" in sys.argv
    front = arg("--front", "+y")
    faces = arg("--faces", 60000, int)
    tex = arg("--tex", 2048, int)
    out_dir = os.path.join(ROOT, "Assets", "Resources", "Models", category)
    os.makedirs(out_dir, exist_ok=True)

    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=glb)
    meshes = [o for o in bpy.context.scene.objects if o.type == "MESH"]
    bpy.ops.object.select_all(action="DESELECT")
    for o in meshes:
        o.select_set(True)
    bpy.context.view_layer.objects.active = meshes[0]
    if len(meshes) > 1:
        bpy.ops.object.join()
    o = bpy.context.object
    # Unparent and bake every transform into the mesh.
    bpy.ops.object.parent_clear(type="CLEAR_KEEP_TRANSFORM")
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    for e in [x for x in bpy.context.scene.objects if x.type != "MESH"]:
        bpy.data.objects.remove(e)

    # Orientation.
    mn, mx = bounds(o)
    size = mx - mn
    rot = Matrix.Identity(4)
    if category == "Buildings":
        # Tripo puts the side that faced the viewer in the concept (the entrance) at +X; turn it to
        # -Y (Unity -Z, the street). Wide buildings' long side then runs along X (the tracks).
        rot = Matrix.Rotation(math.radians(-90), 4, "Z")
        front = "flip" if "--flip" in sys.argv else "none"
    elif size.x > size.y:
        rot = Matrix.Rotation(math.radians(90), 4, "Z")  # long axis onto Y
    o.data.transform(rot)
    if front == "flip":
        o.data.transform(Matrix.Rotation(math.radians(180), 4, "Z"))
    if front == "auto":
        # Steam locomotives: the smokestack top is the highest point and sits at the front.
        o.data.update()
        top = max(o.data.vertices, key=lambda v: v.co.z)
        mn, mx = bounds(o)
        front = "-y" if top.co.y < (mn.y + mx.y) / 2 else "+y"
        print("[ai] auto front:", front)
    if "--flip" in sys.argv and category != "Buildings":
        front = {"-y": "+y", "+y": "-y"}.get(front, front)
    if front == "-y":
        o.data.transform(Matrix.Rotation(math.radians(180), 4, "Z"))
    elif front == "+x":
        o.data.transform(Matrix.Rotation(math.radians(-90), 4, "Z"))
    elif front == "-x":
        o.data.transform(Matrix.Rotation(math.radians(90), 4, "Z"))
    o.data.update()

    # Scale and place: centered on X/Y, bottom at z = 0.
    mn, mx = bounds(o)
    size = mx - mn
    k = (length / size.y) if length else (height / size.z) if height else (footprint / max(size.x, size.y))
    c = (mn + mx) / 2
    o.data.transform(Matrix.Scale(k, 4) @ Matrix.Translation(Vector((-c.x, -c.y, -mn.z))))
    o.data.update()

    if twin:
        # Two identical cab units coupled back to back: duplicate, turn around, put behind.
        import bmesh
        L_ = length or (bounds(o)[1].y - bounds(o)[0].y)
        bm = bmesh.new()
        bm.from_mesh(o.data)
        dup = bmesh.ops.duplicate(bm, geom=bm.verts[:] + bm.edges[:] + bm.faces[:])
        verts = [g for g in dup["geom"] if isinstance(g, bmesh.types.BMVert)]
        bmesh.ops.rotate(bm, verts=verts, cent=(0, 0, 0), matrix=Matrix.Rotation(math.radians(180), 3, "Z"))
        bmesh.ops.translate(bm, verts=verts, vec=(0, -(L_ + 0.4), 0))
        bmesh.ops.translate(bm, verts=bm.verts[:], vec=(0, (L_ + 0.4) / 2, 0))
        bm.to_mesh(o.data)
        bm.free()
        o.data.update()
        faces = faces * 2

    # Weld the surface first: the GLB splits vertices at every texture seam, and decimating split
    # geometry pulls the pieces apart (see-through cracks). UVs are per-corner, so welding keeps them.
    import bmesh
    bm = bmesh.new()
    bm.from_mesh(o.data)
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-4)
    bm.to_mesh(o.data)
    bm.free()
    o.data.update()

    if "--remesh" in sys.argv:
        # Foliage is thousands of separate leaf clumps that decimation can't merge: rebuild it as one
        # solid canopy shape first (rebake.py paints the leafy look back on from the original).
        mn_, mx_ = bounds(o)
        md = o.modifiers.new("vox", "REMESH")
        md.mode = "VOXEL"
        md.voxel_size = (mx_.z - mn_.z) / float(arg("--remesh", 60, float))
        md.adaptivity = 0
        bpy.context.view_layer.objects.active = o
        bpy.ops.object.modifier_apply(modifier="vox")

    # Decimate to the mobile budget.
    n = len(o.data.polygons)
    if n > faces:
        md = o.modifiers.new("dec", "DECIMATE")
        md.ratio = faces / n
        md.use_collapse_triangulate = True
        bpy.context.view_layer.objects.active = o
        bpy.ops.object.modifier_apply(modifier="dec")
    bpy.ops.object.shade_smooth_by_angle(angle=math.radians(40))
    print("[ai] faces %d -> %d, size %s" % (n, len(o.data.polygons), tuple(round(v, 2) for v in (bounds(o)[1] - bounds(o)[0]))))

    # Textures: Tripo ships Color, NormalGL and ORM (R = AO, G = roughness, B = metallic).
    mat = o.data.materials[0]
    imgs = {nd.image.name: nd.image for nd in mat.node_tree.nodes if nd.type == "TEX_IMAGE" and nd.image}
    def find(tag):
        for name, im in imgs.items():
            if tag.lower() in name.lower():
                return im
        return None
    col, nrm, orm = find("Color") or find("base"), find("Normal"), find("ORM") or find("metal")
    if col is None and imgs:
        col = list(imgs.values())[0]
    alb = image_px(resize(col, tex)).copy()
    if orm is not None:
        ormp = image_px(resize(orm, tex))  # same size as albedo for the AO fold; ms is halved below
        ao = ormp[..., 0:1]
        alb[..., :3] *= (0.72 + 0.28 * ao)  # light AO fold: Tripo albedo already carries some shading
        ms = np.zeros_like(ormp)
        # Tripo's PBR maps read as polished chrome under URP: tone metal and gloss down to painted iron/wood.
        ms[..., 0] = ormp[..., 2] * 0.5      # metallic
        ms[..., 1] = ms[..., 0]
        ms[..., 2] = ms[..., 0]
        ms[..., 3] = (1.0 - ormp[..., 1]) * 0.55  # smoothness in alpha (URP)
        if tex >= 1024:
            ms = ms[::2, ::2]
        save_png(ms, os.path.join(out_dir, model_id + "_ms.png"), "Non-Color")
    alb[..., 3] = 1.0
    save_png(alb, os.path.join(out_dir, model_id + "_albedo.png"), "sRGB")
    to_jpg(os.path.join(out_dir, model_id + "_albedo.png"))
    if nrm is not None:
        npx = image_px(resize(nrm, max(512, tex // 2))).copy()
        npx[..., 3] = 1.0
        save_png(npx, os.path.join(out_dir, model_id + "_normal.png"), "Non-Color")
        to_jpg(os.path.join(out_dir, model_id + "_normal.png"))

    o.name = model_id
    o.data.name = model_id
    final = bpy.data.materials.new(model_id)
    o.data.materials.clear()
    o.data.materials.append(final)

    # Smoke anchor(s): the highest point in the front quarter (stack) for vehicles.
    anchors = []
    if category == "Buildings" and "--smoke" in sys.argv:
        top = max(o.data.vertices, key=lambda v: v.co.z)
        e = bpy.data.objects.new("Smoke", None)
        e.location = top.co.copy()
        bpy.context.collection.objects.link(e)
        e.parent = o
        anchors.append(e)
    if category == "Locomotives":
        vs = [v.co for v in o.data.vertices]
        mn, mx = bounds(o)
        front_vs = [v for v in vs if v.y > mx.y - (mx.y - mn.y) * 0.3 and abs(v.x) < 0.6]
        if front_vs:
            top = max(front_vs, key=lambda v: v.z)
            e = bpy.data.objects.new("Smoke", None)
            e.location = (0, top.y, top.z)
            bpy.context.collection.objects.link(e)
            e.parent = o
            anchors.append(e)

    # Unity's FBX import reads the exporter's +Y forward as -Z: turn the model so its front
    # (+Y in Blender, where everything above was oriented) ends up facing Unity's +Z.
    turn = Matrix.Rotation(math.radians(180), 4, "Z")
    o.data.transform(turn)
    for e in anchors:
        e.location = turn @ e.location

    bpy.ops.object.select_all(action="DESELECT")
    o.select_set(True)
    for e in anchors:
        e.select_set(True)
    fbx = os.path.join(out_dir, model_id + ".fbx")
    bpy.ops.export_scene.fbx(filepath=fbx, use_selection=True, apply_unit_scale=True, apply_scale_options="FBX_SCALE_UNITS",
                             axis_forward="Y", axis_up="Z", object_types={"MESH", "EMPTY"}, mesh_smooth_type="OFF",
                             add_leaf_bones=False, path_mode="STRIP", embed_textures=False)
    print("[ai] exported", fbx)


main()
