"""Side-view filmstrip of a townsperson FBX's Walk clip: blender -b -P walk_strip.py -- in.fbx out_prefix"""
import math, sys, bpy
from mathutils import Vector
a = sys.argv[sys.argv.index("--") + 1:]
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=a[0])
arm = next(o for o in bpy.context.scene.objects if o.type == "ARMATURE")
acts = {x.name.split("|")[-1]: x for x in bpy.data.actions}
print("[strip] actions", list(acts))
walk = next(v for k, v in acts.items() if "Walk" in k)
arm.animation_data_create(); arm.animation_data.action = walk
s = bpy.context.scene
s.render.engine = "BLENDER_EEVEE_NEXT"; s.render.resolution_x, s.render.resolution_y = 300, 420
w = bpy.data.worlds.new("w"); w.use_nodes = True; w.node_tree.nodes["Background"].inputs[1].default_value = 2; s.world = w
cam = bpy.data.objects.new("c", bpy.data.cameras.new("c")); cam.data.type = "ORTHO"; cam.data.ortho_scale = 2.2
s.collection.objects.link(cam); cam.location = (4, 0, 0.95); cam.rotation_euler = (math.pi / 2, 0, math.pi / 2); s.camera = cam
for f in (0, 6, 12, 18):
    s.frame_set(f); s.render.filepath = "%s_%02d.png" % (a[1], f); bpy.ops.render.render(write_still=True)
