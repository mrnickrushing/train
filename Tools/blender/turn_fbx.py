"""Turns exported models 180 degrees about the vertical axis and re-exports them in place.
Unity reads the exporter's +Y forward as -Z, so models made before the fix in import_ai.py
came in backwards.   blender -b -P Tools/blender/turn_fbx.py -- a.fbx b.fbx ..."""
import math, sys, bpy
from mathutils import Matrix
R = Matrix.Rotation(math.radians(180), 4, "Z")
for path in sys.argv[sys.argv.index("--") + 1:]:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=path)
    objs = list(bpy.context.scene.objects)
    for o in [o for o in objs if o.parent is None]:  # children (Smoke anchors) follow their parent
        o.matrix_world = R @ o.matrix_world
    bpy.ops.object.select_all(action="DESELECT")
    for o in objs:
        o.select_set(True)
    bpy.ops.export_scene.fbx(filepath=path, use_selection=True, apply_unit_scale=True, apply_scale_options="FBX_SCALE_UNITS",
                             axis_forward="Y", axis_up="Z", object_types={"MESH", "EMPTY"}, mesh_smooth_type="OFF",
                             add_leaf_bones=False, path_mode="STRIP", embed_textures=False)
    print("[turn]", path, len(objs))
