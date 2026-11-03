"""Cuts the square ground pad off an AI building so it stands straight on the terrain.

Tripo models come on an 11 m slab of dirt and grass, which in game reads as a building on a tray.
This finds the slab's top (the largest upward-facing area near the bottom), cuts just above it,
drops leftover flat fragments (bumps of the slab surface), and lowers everything onto z = 0.

  blender -b -P Tools/blender/trim_base.py -- <game.fbx> [<game.fbx> ...]

Edits the FBX in place, keeping its UVs, textures and anchors (re-exported like rebake.py).
"""
import sys

import bmesh
import bpy
from mathutils import Vector

for path in sys.argv[sys.argv.index("--") + 1:]:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=path)
    objs = list(bpy.context.scene.objects)
    mesh = next(o for o in objs if o.type == "MESH")
    mw = mesh.matrix_world.copy()
    bm = bmesh.new()
    bm.from_mesh(mesh.data)
    bm.transform(mw)
    z0 = min(v.co.z for v in bm.verts)

    # Slab top: area-weighted histogram (2 cm bins) of upward faces in the bottom 0.9 m.
    hist = {}
    for f in bm.faces:
        if f.normal.z > 0.85:
            c = f.calc_center_median().z
            if c < z0 + 0.9:
                k = round((c - z0) / 0.02)
                hist[k] = hist.get(k, 0) + f.calc_area()
    top = z0 + max(hist, key=hist.get) * 0.02 if hist else z0
    total = sum(f.calc_area() for f in bm.faces if f.normal.z > 0.85 and f.calc_center_median().z < z0 + 0.9)
    near = sum(a for k, a in hist.items() if abs(z0 + k * 0.02 - top) < 0.08)
    cut = top + 0.06

    # Cut and delete everything below.
    geom = bm.verts[:] + bm.edges[:] + bm.faces[:]
    res = bmesh.ops.bisect_plane(bm, geom=geom, plane_co=(0, 0, cut), plane_no=(0, 0, 1), clear_inner=True)
    # Drop loose flat fragments left from the slab surface.
    seen, flat = set(), []
    for v in bm.verts:
        if v in seen:
            continue
        stack, part = [v], []
        seen.add(v)
        while stack:
            u = stack.pop()
            part.append(u)
            for e in u.link_edges:
                w = e.other_vert(u)
                if w not in seen:
                    seen.add(w)
                    stack.append(w)
        if max(p.co.z for p in part) - cut < 0.14:
            flat.append(part)
    for part in flat:
        bmesh.ops.delete(bm, geom=part, context="VERTS")
    dropped = len(flat)
    bm.verts.ensure_lookup_table()
    for v in bm.verts:
        v.co.z -= cut
    bm.transform(mw.inverted())
    bm.to_mesh(mesh.data)
    bm.free()
    mesh.data.update()
    for o in objs:
        if o is not mesh:
            m = o.matrix_world.copy()
            m.translation.z -= cut
            o.matrix_world = m

    print("[trim] %s slab top %.2f (%.0f%% of low flat area) cut %.2f, dropped %d fragments" % (path.split("/")[-1], top - z0, 100 * near / max(total, 1e-6), cut - z0, dropped))
    bpy.ops.object.select_all(action="DESELECT")
    for o in objs:
        o.select_set(True)
    bpy.ops.export_scene.fbx(filepath=path, use_selection=True, apply_unit_scale=True, apply_scale_options="FBX_SCALE_UNITS",
                             axis_forward="Y", axis_up="Z", object_types={"MESH", "EMPTY"}, mesh_smooth_type="OFF",
                             add_leaf_bones=False, path_mode="STRIP", embed_textures=False)
