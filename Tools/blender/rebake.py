"""Rebuilds an AI model's textures by baking the full-detail GLB onto the decimated game mesh.

Decimating 1.5M triangles to a mobile budget warps the original UVs (sliver triangles that span
several texture islands -> wrong-colour patches and specks). This gives the low-poly mesh a fresh,
clean UV layout and bakes base colour (and a tangent normal map) from the original onto it.

  blender -b -P Tools/blender/rebake.py -- <in.glb> <game.fbx> <out_prefix> [--size 1024] [--normal]

Writes <out_prefix>_albedo.png (and _normal.png), and re-exports <game.fbx> with the new UVs.
"""
import math
import os
import sys

import bpy
from mathutils import Vector

a = sys.argv[sys.argv.index("--") + 1:]
glb, fbx, prefix = a[0], a[1], a[2]
size = int(a[a.index("--size") + 1]) if "--size" in a else 1024
want_normal = "--normal" in a
twin = "--twin" in a
force_deg = int(a[a.index("--deg") + 1]) if "--deg" in a else None  # override the orientation search

bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene
scene.render.engine = "CYCLES"
scene.cycles.device = "CPU"
scene.cycles.samples = 1
scene.render.bake.margin = 6

# Game mesh (already oriented, scaled and decimated by import_ai.py).
bpy.ops.import_scene.fbx(filepath=fbx)
fbx_objs = list(scene.objects)
low = [o for o in fbx_objs if o.type == "MESH"][0]
low_world = low.matrix_world.copy()

# Full-detail source, moved onto the game mesh: match bounds (import_ai.py only rotated, scaled, translated).
bpy.ops.import_scene.gltf(filepath=glb)
high_parts = [o for o in scene.objects if o.type == "MESH" and o not in fbx_objs]
bpy.ops.object.select_all(action="DESELECT")
for o in high_parts:
    o.select_set(True)
bpy.context.view_layer.objects.active = high_parts[0]
if len(high_parts) > 1:
    bpy.ops.object.join()
high = bpy.context.object
bpy.ops.object.parent_clear(type="CLEAR_KEEP_TRANSFORM")
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)


def bounds(o):
    ws = [o.matrix_world @ Vector(c) for c in o.bound_box]
    return Vector((min(v.x for v in ws), min(v.y for v in ws), min(v.z for v in ws))), Vector((max(v.x for v in ws), max(v.y for v in ws), max(v.z for v in ws)))


def size_of(o):
    mn, mx = bounds(o)
    return mx - mn


# Try the 8 quarter-turn/flip orientations about Z that import_ai could have applied; pick the one
# whose vertex cloud best matches the low mesh (compare bounds, then a coarse point sample).
from mathutils import Matrix
import random
random.seed(1)
low_pts = [low.matrix_world @ v.co for v in random.sample(list(low.data.vertices), min(600, len(low.data.vertices)))]
best = None
hv = [v.co.copy() for v in random.sample(list(high.data.vertices), min(4000, len(high.data.vertices)))]
lmn, lmx = bounds(low)
if twin:
    # Two identical units back to back: fit the source to the front half (+Y), then mirror a copy.
    ws = [low.matrix_world @ v.co for v in low.data.vertices]
    cy = (lmn.y + lmx.y) / 2
    front = [w for w in ws if w.y > cy]
    lmn = Vector((min(w.x for w in front), min(w.y for w in front), min(w.z for w in front)))
    lmx = Vector((max(w.x for w in front), max(w.y for w in front), max(w.z for w in front)))
    low_pts = [q for q in low_pts if q.y > cy]
# Rotations, then the same four mirrored (a source that import_ai mirrored can't be matched by turning it).
for deg in ((force_deg,) if force_deg is not None else (0, 90, 180, 270, 1000, 1090, 1180, 1270)):
    R = Matrix.Rotation(math.radians(deg % 1000), 4, "Z")
    if deg >= 1000: R = R @ Matrix.Scale(-1, 4, Vector((1, 0, 0)))
    pts = [R @ p for p in hv]
    mn = Vector((min(p.x for p in pts), min(p.y for p in pts), min(p.z for p in pts)))
    mx = Vector((max(p.x for p in pts), max(p.y for p in pts), max(p.z for p in pts)))
    sz = mx - mn
    lsz = lmx - lmn
    k = lsz.z / max(sz.z, 1e-6)
    T = Matrix.Translation(Vector(((lmn.x + lmx.x) / 2, (lmn.y + lmx.y) / 2, lmn.z))) @ Matrix.Scale(k, 4) @ Matrix.Translation(-Vector(((mn.x + mx.x) / 2, (mn.y + mx.y) / 2, mn.z))) @ R
    tp = [T @ p for p in hv[:800]]
    # mean distance from low samples to nearest transformed high sample (coarse)
    err = 0
    for q in low_pts[:200]:
        err += min((q - p).length_squared for p in tp)
    if best is None or err < best[0]:
        best = (err, T, deg)
high.data.transform(best[1])
high.data.update()
if twin:
    import bmesh
    cmn, cmx = bounds(low)
    c = (cmn + cmx) / 2
    bm = bmesh.new()
    bm.from_mesh(high.data)
    dup = bmesh.ops.duplicate(bm, geom=bm.verts[:] + bm.edges[:] + bm.faces[:])
    vs = [g for g in dup["geom"] if isinstance(g, bmesh.types.BMVert)]
    bmesh.ops.rotate(bm, verts=vs, cent=(c.x, c.y, 0), matrix=Matrix.Rotation(math.radians(180), 3, "Z"))
    bm.to_mesh(high.data)
    bm.free()
    high.data.update()
print("[bake] orientation", best[2], "err", round(best[0], 3))

# Fresh UVs on the low mesh.
bpy.ops.object.select_all(action="DESELECT")
low.select_set(True)
bpy.context.view_layer.objects.active = low
bpy.ops.object.mode_set(mode="EDIT")
bpy.ops.mesh.select_all(action="SELECT")
bpy.ops.uv.smart_project(angle_limit=math.radians(66), island_margin=0.004, area_weight=1.0)
bpy.ops.uv.pack_islands(margin=0.003, rotate=True)
print("[bake] uv islands", len(low.data.polygons))
bpy.ops.object.mode_set(mode="OBJECT")

# High material: emit its base colour (and keep normals for the normal bake).
hm = high.data.materials[0]
nt = hm.node_tree
bsdf = next(n for n in nt.nodes if n.type == "BSDF_PRINCIPLED")
out = next(n for n in nt.nodes if n.type == "OUTPUT_MATERIAL")
col_src = bsdf.inputs["Base Color"].links[0].from_socket if bsdf.inputs["Base Color"].links else None
emit = nt.nodes.new("ShaderNodeEmission")
if col_src is not None:
    nt.links.new(col_src, emit.inputs["Color"])
else:
    emit.inputs["Color"].default_value = bsdf.inputs["Base Color"].default_value

# Low material with the bake target.
lm = bpy.data.materials.new("bake")
lm.use_nodes = True
low.data.materials.clear()
low.data.materials.append(lm)
tex = lm.node_tree.nodes.new("ShaderNodeTexImage")


def bake(kind, colorspace, setup):
    img = bpy.data.images.new(os.path.basename(prefix) + "_" + kind, size, size, alpha=False)
    img.colorspace_settings.name = colorspace
    tex.image = img
    lm.node_tree.nodes.active = tex
    setup()
    bpy.ops.object.select_all(action="DESELECT")
    high.select_set(True)
    low.select_set(True)
    bpy.context.view_layer.objects.active = low
    ext = max(size_of(low)) * 0.02 * float(os.environ.get("REBAKE_REACH", "1"))  # >1 for meshes that fit their source loosely
    t = "EMIT" if kind == "albedo" else "NORMAL"
    bpy.ops.object.bake(type=t, use_selected_to_active=True, cage_extrusion=ext, max_ray_distance=ext * 4, margin=6, use_clear=True)
    img.filepath_raw = prefix + "_" + kind + ".png"
    img.file_format = "PNG"
    img.save()
    print("[bake]", kind, img.filepath_raw)


def use_emit():
    for l in list(out.inputs[0].links):
        nt.links.remove(l)
    nt.links.new(emit.outputs[0], out.inputs[0])


def use_bsdf():
    for l in list(out.inputs[0].links):
        nt.links.remove(l)
    nt.links.new(bsdf.outputs[0], out.inputs[0])


bake("albedo", "sRGB", use_emit)
if want_normal:
    scene.render.bake.normal_space = "TANGENT"
    bake("normal", "Non-Color", use_bsdf)

# Re-export the game mesh with its new UVs (anchors and transforms unchanged).
bpy.data.objects.remove(high)
low.data.materials.clear()
low.data.materials.append(bpy.data.materials.new(os.path.basename(prefix)))
bpy.ops.object.select_all(action="DESELECT")
for o in fbx_objs:
    o.select_set(True)
bpy.ops.export_scene.fbx(filepath=fbx, use_selection=True, apply_unit_scale=True, apply_scale_options="FBX_SCALE_UNITS",
                         axis_forward="Y", axis_up="Z", object_types={"MESH", "EMPTY"}, mesh_smooth_type="OFF",
                         add_leaf_bones=False, path_mode="STRIP", embed_textures=False)
print("[bake] exported", fbx)
