"""Writes a light distant version of a game model: same UVs and textures, fewer triangles.

  blender -b -P Tools/blender/lod.py -- <game.fbx> <out_far.fbx> <triangles>

Used for parked yard wagons, which are seen from 100+ m away. Round-trips the FBX with the same
export settings as rebake.py, so orientation and anchors are unchanged.
"""
import sys

import bpy

a = sys.argv[sys.argv.index("--") + 1:]
src, dst, target = a[0], a[1], int(a[2])

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=src)
objs = list(bpy.context.scene.objects)
for o in objs:
    if o.type != "MESH":
        continue
    tris = sum(len(p.vertices) - 2 for p in o.data.polygons)
    if tris > target:
        m = o.modifiers.new("lod", "DECIMATE")
        m.decimate_type = "COLLAPSE"
        m.ratio = target / tris
        m.use_collapse_triangulate = True
        bpy.context.view_layer.objects.active = o
        bpy.ops.object.modifier_apply(modifier=m.name)
    print("[lod]", o.name, tris, "->", sum(len(p.vertices) - 2 for p in o.data.polygons))
bpy.ops.object.select_all(action="DESELECT")
for o in objs:
    o.select_set(True)
bpy.ops.export_scene.fbx(filepath=dst, use_selection=True, apply_unit_scale=True, apply_scale_options="FBX_SCALE_UNITS",
                         axis_forward="Y", axis_up="Z", object_types={"MESH", "EMPTY"}, mesh_smooth_type="OFF",
                         add_leaf_bones=False, path_mode="STRIP", embed_textures=False)
