"""Renders a GLB from four sides (-Y, +X, +Y, -X) into one strip, to pick a building's front.
  blender -b -P Tools/blender/orient_sheet.py -- in.glb out.png"""
import math, sys, bpy
from mathutils import Vector, Euler
a = sys.argv[sys.argv.index("--") + 1:]
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=a[0])
objs = [o for o in bpy.context.scene.objects if o.type == "MESH"]
mn = Vector((1e9,) * 3); mx = Vector((-1e9,) * 3)
for o in objs:
    for c in o.bound_box:
        w = o.matrix_world @ Vector(c); mn = Vector(map(min, mn, w)); mx = Vector(map(max, mx, w))
ctr, size = (mn + mx) / 2, (mx - mn).length
s = bpy.context.scene
s.render.engine = "BLENDER_EEVEE_NEXT"
s.render.resolution_x, s.render.resolution_y = 1024, 256
w = bpy.data.worlds.new("w"); w.use_nodes = True; w.node_tree.nodes["Background"].inputs[1].default_value = 1.5; s.world = w
cams = []
for i, (lbl, yaw) in enumerate((("-Y", 0), ("+X", 90), ("+Y", 180), ("-X", 270))):
    cam = bpy.data.objects.new(lbl, bpy.data.cameras.new(lbl)); cam.data.type = "ORTHO"; cam.data.ortho_scale = size
    s.collection.objects.link(cam)
    d = Vector((math.sin(math.radians(yaw)), -math.cos(math.radians(yaw)), 0.45)).normalized()
    cam.location = ctr + d * size * 2
    cam.rotation_euler = (-d).to_track_quat("-Z", "Y").to_euler()
    cams.append(cam)
import os
paths = []
s.render.resolution_x = 256
for cam in cams:
    s.camera = cam; p = a[1] + "_" + cam.name + ".png"; s.render.filepath = p; bpy.ops.render.render(write_still=True); paths.append(p)
