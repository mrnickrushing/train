"""Side views of imported FBX models (front = Blender +Y should appear on the RIGHT).
  blender -b -P Tools/blender/side_sheet.py -- out_prefix a.fbx b.fbx ..."""
import sys, bpy, math
from mathutils import Vector
a = sys.argv[sys.argv.index("--") + 1:]
for path in a[1:]:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=path)
    objs = [o for o in bpy.context.scene.objects if o.type == "MESH"]
    mn = Vector((1e9,) * 3); mx = Vector((-1e9,) * 3)
    for o in objs:
        for c in o.bound_box:
            w = o.matrix_world @ Vector(c); mn = Vector(map(min, mn, w)); mx = Vector(map(max, mx, w))
    s = bpy.context.scene; s.render.engine = "BLENDER_EEVEE_NEXT"; s.render.resolution_x, s.render.resolution_y = 640, 200
    w = bpy.data.worlds.new("w"); w.use_nodes = True; w.node_tree.nodes["Background"].inputs[1].default_value = 2; s.world = w
    cam = bpy.data.objects.new("c", bpy.data.cameras.new("c")); cam.data.type = "ORTHO"; cam.data.ortho_scale = (mx - mn).y * 1.05
    s.collection.objects.link(cam); ctr = (mn + mx) / 2
    cam.location = ctr + Vector((40, 0, 0)); cam.rotation_euler = (math.pi / 2, 0, math.pi / 2)
    s.camera = cam; s.render.filepath = a[0] + "_" + path.split("/")[-1].replace(".fbx", ".png"); bpy.ops.render.render(write_still=True)
    print("[side]", path, tuple(round(v, 2) for v in (mx - mn)))
