"""Rigs the AI draft horse and authors its walk: a four-legged armature with weights assigned from the
body plan (legs, neck/head, body), a four-beat walk loop (left hind, left fore, right hind, right fore)
with knee lift, head nod and a little body bob.

  blender -b -P Tools/blender/horse.py -- <Props/horse.fbx> <out_dir>

Reads the imported, rebaked game mesh (faces -Y here, which Unity shows as +Z) and writes
<out_dir>/horse.fbx with one Legacy clip "Walk" (24 frames = one stride of about 1.4 m).
"""
import math
import os
import sys

import bpy
from mathutils import Matrix, Vector

a = sys.argv[sys.argv.index("--") + 1:]
src, out_dir = a[0], a[1]
os.makedirs(out_dir, exist_ok=True)

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=src)
mesh = next(o for o in bpy.context.scene.objects if o.type == "MESH")
ws = [mesh.matrix_world @ v.co for v in mesh.data.vertices]
H = max(w.z for w in ws)
ymin, ymax = min(w.y for w in ws), max(w.y for w in ws)
L = ymax - ymin

# Body plan from the mesh: leg columns are what reaches the ground; front legs are on the head (-Y) side.
low = [w for w in ws if w.z < 0.12 * H]
mid = (ymin + ymax) / 2
front = [w.y for w in low if w.y < mid]
hind = [w.y for w in low if w.y >= mid and w.y < ymax - 0.12 * L]  # skip the tail tip
fy, hy = sum(front) / len(front), sum(hind) / len(hind)
knee, top = 0.3 * H, 0.55 * H        # knee/hock height, where legs meet the body
spine = 0.68 * H
print("[horse] H %.2f front legs y %.2f hind legs y %.2f" % (H, fy, hy))

arm_data = bpy.data.armatures.new("HorseRig")
arm = bpy.data.objects.new("HorseRig", arm_data)
bpy.context.scene.collection.objects.link(arm)
bpy.context.view_layer.objects.active = arm
bpy.ops.object.mode_set(mode="EDIT")


def bone(name, head, tail, parent=None):
    b = arm_data.edit_bones.new(name)
    b.head, b.tail = Vector(head), Vector(tail)
    b.roll = 0
    if parent:
        b.parent = arm_data.edit_bones[parent]
    return b


bone("Body", (0, hy, spine), (0, fy, spine))
bone("Neck", (0, fy - 0.05 * L, 0.78 * H), (0, fy - 0.2 * L, 0.92 * H), "Body")
bone("Tail", (0, ymax - 0.06 * L, 0.66 * H), (0, ymax, 0.3 * H), "Body")
LEGS = {}
for side, sx in (("L", 1), ("R", -1)):
    for end, y in (("F", fy), ("H", hy)):
        x = sx * 0.2 * (max(w.x for w in ws) - min(w.x for w in ws))
        n = end + side
        bone("Up" + n, (x, y, top), (x, y, knee), "Body")
        bone("Low" + n, (x, y, knee), (x, y, 0.02), "Up" + n)
        LEGS[n] = (x, y)
bpy.ops.object.mode_set(mode="OBJECT")

# Weights from the body plan (smooth blends at the knee, the shoulder/hip and the neck).
def sm(e0, e1, v):
    t = min(max((v - e0) / (e1 - e0), 0), 1)
    return t * t * (3 - 2 * t)


groups = {b.name: mesh.vertex_groups.new(name=b.name) for b in arm_data.bones}
for v, w in zip(mesh.data.vertices, ws):
    wt = {}
    # nearest leg column on this side and end
    end = "F" if abs(w.y - fy) < abs(w.y - hy) else "H"
    side = "L" if w.x > 0 else "R"
    n = end + side
    dy = abs(w.y - LEGS[n][1])
    in_leg = w.z < top + 0.08 * H and dy < 0.17 * L and not (end == "H" and w.y > hy + 0.1 * L and abs(w.x) < 0.12)
    if in_leg:
        body = sm(top - 0.1 * H, top + 0.08 * H, w.z)
        low_w = 1 - sm(knee - 0.05 * H, knee + 0.05 * H, w.z)
        leg = 1 - body
        wt["Low" + n] = leg * low_w
        wt["Up" + n] = leg * (1 - low_w)
        wt["Body"] = body
    elif w.y < fy - 0.02 * L and w.z > 0.55 * H:
        neck = sm(fy + 0.02 * L, fy - 0.12 * L, w.y)
        wt["Neck"], wt["Body"] = neck, 1 - neck
    elif w.y > ymax - 0.1 * L and abs(w.x) < 0.15:
        t = sm(ymax - 0.12 * L, ymax - 0.04 * L, w.y)
        wt["Tail"], wt["Body"] = t, 1 - t
    else:
        wt["Body"] = 1
    for g, val in wt.items():
        if val > 0.001:
            groups[g].add([v.index], val, "REPLACE")

mesh.parent = arm
mod = mesh.modifiers.new("Armature", "ARMATURE")
mod.object = arm

# ------------------------------------------------------------------ walk
bpy.context.view_layer.objects.active = arm
bpy.ops.object.mode_set(mode="POSE")
pbs = arm.pose.bones
for pb in pbs:
    pb.rotation_mode = "XYZ"

PHASE = {"HL": 0.0, "FL": 0.25, "HR": 0.5, "FR": 0.75}  # four-beat walk


def rot(name, deg):
    pbs[name].rotation_euler = (math.radians(deg), 0, 0)


def leg_pose(n, t):
    # t in 0..1 within this leg's cycle: stance 0..0.62 (hoof moves back), swing 0.62..1 (lifts and reaches).
    stance = 0.62
    if t < stance:
        u = t / stance
        swing = 22 - 44 * u                         # +22 (reaching forward) -> -22 (pushed back): ~1.4 m per stride
        bend = 0
    else:
        u = (t - stance) / (1 - stance)
        swing = -22 + 44 * (0.5 - 0.5 * math.cos(u * math.pi))
        bend = 55 * math.sin(u * math.pi)           # fold the lower leg to clear the ground
    front = n[0] == "F"
    return swing, (bend if front else -bend * 0.8)


# Signs: find which rotation sense moves a hoof forward (toward -Y) for these bone axes.
rot("UpFL", 10)
bpy.context.view_layer.update()
fwd_sign = 1 if (arm.matrix_world @ pbs["UpFL"].tail).y < LEGS["FL"][1] else -1
rot("UpFL", 0)
print("[horse] forward sign", fwd_sign)

act = bpy.data.actions.new("Walk")
arm.animation_data_create()
arm.animation_data.action = act
FR = 24
for f in range(FR + 1):
    t = f / FR
    for n, ph in PHASE.items():
        s, b = leg_pose(n, (t + ph) % 1.0)
        rot("Up" + n, fwd_sign * s)
        rot("Low" + n, -fwd_sign * b)
    rot("Neck", 4 * math.sin(t * 4 * math.pi))      # head nods twice per stride
    rot("Tail", 5 * math.sin(t * 2 * math.pi))
    pbs["Body"].location = (0, 0, 0.012 * math.cos(t * 4 * math.pi))
    for pb in pbs:
        pb.keyframe_insert("rotation_euler", frame=f)
    pbs["Body"].keyframe_insert("location", frame=f)
for fc in act.fcurves:
    for kp in fc.keyframe_points:
        kp.interpolation = "LINEAR"
act.use_fake_user = True
track = arm.animation_data.nla_tracks.new()
track.name = "Walk"
track.strips.new("Walk", 0, act)
arm.animation_data.action = None
bpy.context.scene.frame_end = FR
bpy.context.scene.render.fps = 24
bpy.ops.object.mode_set(mode="OBJECT")
for pb in pbs:
    pb.rotation_euler = (0, 0, 0)
    pb.location = (0, 0, 0)

bpy.ops.object.select_all(action="DESELECT")
arm.select_set(True)
mesh.select_set(True)
out = os.path.join(out_dir, "horse.fbx")
bpy.ops.export_scene.fbx(filepath=out, use_selection=True, apply_unit_scale=True, apply_scale_options="FBX_SCALE_UNITS",
                         axis_forward="Y", axis_up="Z", object_types={"MESH", "ARMATURE"}, add_leaf_bones=False,
                         bake_anim=True, bake_anim_use_all_actions=False, bake_anim_use_nla_strips=True,
                         bake_anim_force_startend_keying=True, bake_anim_simplify_factor=0.0,
                         path_mode="STRIP", embed_textures=False, mesh_smooth_type="OFF")
print("[horse] exported", out)
