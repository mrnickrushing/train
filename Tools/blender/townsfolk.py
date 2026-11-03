"""Turns a rigged AI character GLB (24-bone humanoid) into a game townsperson with Walk and Idle loops.

  blender -b -P Tools/blender/townsfolk.py -- <in.glb> <id> [--height 1.75]

Writes Assets/Resources/Models/People/<id>.fbx (mesh + armature + clips "Walk" and "Idle") and
<id>_albedo.jpg. The walk is authored procedurally: hip bob, leg swing with knee bend, opposite arm
swing and a little spine counter-twist, one stride per second at 24 fps. Characters face +Z in Unity.
"""
import math
import os
import subprocess
import sys

import bpy
from mathutils import Matrix, Vector

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
a = sys.argv[sys.argv.index("--") + 1:]
glb, pid = a[0], a[1]
height = float(a[a.index("--height") + 1]) if "--height" in a else 1.75
out_dir = os.path.join(ROOT, "Assets", "Resources", "Models", "People")
os.makedirs(out_dir, exist_ok=True)

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=glb)
for o in list(bpy.context.scene.objects):
    if o.type == "MESH" and o.name.lower().startswith("icosphere"):
        bpy.data.objects.remove(o)
for act in list(bpy.data.actions):
    bpy.data.actions.remove(act)
arm = next(o for o in bpy.context.scene.objects if o.type == "ARMATURE")
mesh = next(o for o in bpy.context.scene.objects if o.type == "MESH")

# Albedo texture out of the GLB material.
img = next((n.image for n in mesh.data.materials[0].node_tree.nodes if n.type == "TEX_IMAGE" and n.image), None)
if img is not None:
    png = os.path.join(ROOT, "Temp", pid + "_albedo.png")
    img.filepath_raw = png
    img.file_format = "PNG"
    img.save()
    subprocess.run(["python3", "-c", "from PIL import Image;im=Image.open(%r).convert('RGB');im.thumbnail((1024,1024));im.save(%r,quality=90)"
                    % (png, os.path.join(out_dir, pid + "_albedo.jpg"))], check=True)

# Scale the whole rig so the character is `height` tall, feet on z = 0.
bpy.context.view_layer.update()
ws = [mesh.matrix_world @ v.co for v in mesh.data.vertices]
zmin, zmax = min(w.z for w in ws), max(w.z for w in ws)
k = height / (zmax - zmin)
arm.scale = arm.scale * k
arm.location = arm.location * k
bpy.context.view_layer.update()
ws = [mesh.matrix_world @ v.co for v in mesh.data.vertices]
arm.location.z -= min(w.z for w in ws)
bpy.context.view_layer.update()

bpy.context.view_layer.objects.active = arm
bpy.ops.object.mode_set(mode="POSE")
pbs = arm.pose.bones
for pb in pbs:
    pb.rotation_mode = "QUATERNION"

# Armature-space axes: characters face -Y here, X points to their left side.
LAT = Vector((1, 0, 0))   # pitch axis (swing forward/back)
UP = Vector((0, 0, 1))

ORDER = [b.name for b in arm.data.bones]  # parents come before children


def pose(rots, hip_lift=0.0):
    """rots: {bone: [(axis, degrees), ...]} applied in armature space about each bone's head."""
    for pb in pbs:
        pb.rotation_quaternion = (1, 0, 0, 0)
        pb.location = (0, 0, 0)
    bpy.context.view_layer.update()
    for name in ORDER:
        if name not in rots and not (name == "Hips" and hip_lift):
            continue
        pb = pbs[name]
        m = pb.matrix.copy()
        head = m.translation.copy()
        for axis, deg in rots.get(name, []):
            r = Matrix.Rotation(math.radians(deg), 4, axis)
            m = Matrix.Translation(head) @ r @ Matrix.Translation(-head) @ m
        if name == "Hips" and hip_lift:
            m = Matrix.Translation(UP * hip_lift / arm.scale.z) @ m
        pb.matrix = m
        bpy.context.view_layer.update()


def key(frame):
    for pb in pbs:
        pb.keyframe_insert("rotation_quaternion", frame=frame)
        if pb.name == "Hips":
            pb.keyframe_insert("location", frame=frame)


def make(name, frames, fn):
    act = bpy.data.actions.new(name)
    arm.animation_data_create()
    arm.animation_data.action = act
    for f in range(frames + 1):
        rots, lift = fn(f / frames * 2 * math.pi)
        pose(rots, lift)
        key(f)
    for fc in act.fcurves:
        for kp in fc.keyframe_points:
            kp.interpolation = "LINEAR"
    act.use_fake_user = True
    track = arm.animation_data.nla_tracks.new()
    track.name = name
    track.strips.new(name, 0, act)
    arm.animation_data.action = None
    return act


def walk(t):
    s = math.sin(t)
    # Negative angles about +X swing a leg forward (toward -Y).
    # Knee bends mid-swing (legs crossing) and is nearly straight at full reach.
    lk = 4 + 32 * max(0.0, math.cos(t))
    rk = 4 + 32 * max(0.0, -math.cos(t))
    return {
        "LeftUpLeg": [(LAT, -22 * s - 6 * max(0.0, math.cos(t)))],
        "RightUpLeg": [(LAT, 22 * s - 6 * max(0.0, -math.cos(t)))],
        "LeftLeg": [(LAT, lk)],
        "RightLeg": [(LAT, rk)],
        "LeftFoot": [(LAT, -12 * max(0.0, math.cos(t)))],
        "RightFoot": [(LAT, -12 * max(0.0, -math.cos(t)))],
        "LeftArm": [(LAT, 18 * s)],
        "RightArm": [(LAT, -18 * s)],
        "LeftForeArm": [(LAT, -10 - 6 * s)],
        "RightForeArm": [(LAT, -10 + 6 * s)],
        "Spine02": [(UP, 5 * s), (LAT, -3)],
        "neck": [(UP, -4 * s)],
    }, 0.025 * abs(math.cos(t))


def idle(t):
    b = math.sin(t)
    return {
        "Spine02": [(LAT, -1.5 * b)],
        "neck": [(UP, 6 * math.sin(t * 0.5))],
        "LeftArm": [(LAT, 2 * b)],
        "RightArm": [(LAT, -2 * b)],
        "LeftForeArm": [(LAT, -6)],
        "RightForeArm": [(LAT, -6)],
    }, 0.005 * b


bpy.context.scene.render.fps = 24
make("Walk", 24, walk)
make("Idle", 72, idle)
bpy.ops.object.mode_set(mode="OBJECT")
for pb in pbs:
    pb.rotation_quaternion = (1, 0, 0, 0)
    pb.location = (0, 0, 0)

# Face Unity +Z: Unity reads the exporter's +Y forward as -Z, and the character faces -Y here.
mesh.data.materials.clear()
mesh.data.materials.append(bpy.data.materials.new(pid))
bpy.ops.object.select_all(action="DESELECT")
arm.select_set(True)
mesh.select_set(True)
fbx = os.path.join(out_dir, pid + ".fbx")
bpy.ops.export_scene.fbx(filepath=fbx, use_selection=True, apply_unit_scale=True, apply_scale_options="FBX_SCALE_ALL",
                         axis_forward="-Y", axis_up="Z", object_types={"MESH", "ARMATURE"}, add_leaf_bones=False,
                         bake_anim=True, bake_anim_use_all_actions=False, bake_anim_use_nla_strips=True,
                         bake_anim_force_startend_keying=True, bake_anim_simplify_factor=0.0,
                         path_mode="STRIP", embed_textures=False, mesh_smooth_type="FACE")
print("[folk] exported", fbx, "tris", sum(len(p.vertices) - 2 for p in mesh.data.polygons))
