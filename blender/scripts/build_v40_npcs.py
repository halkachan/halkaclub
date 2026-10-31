"""はるかわーるど ver4.0: the four passers-by (fans and antis of ﾊﾙｶﾁｬﾝ's channel), blocky like the game.

Made the way ﾊﾙｶﾁｬﾝ's official model is (boxes, one vertex-coloured material, pixel units, front =
Blender -Y = South, pivots at the joints), but plainly ordinary people so they are never mistaken for
her, Rin or Len: brown / black hair, everyday clothes, no blue hair, no ribbons, no headphones.
  fan_male     short brown hair, green T-shirt with a white heart, jeans, a yellow penlight
  fan_female   long chestnut hair tied at the side, pink top with a white heart, white skirt
  anti_male    grey hoodie with the hood up, black trousers, arms by his sides, a flat mouth
  anti_female  black bob under a dark cap, purple jacket, grey skirt, a flat mouth
Fans smile; antis have flat mouths and lowered brows (nothing aggressive).

  blender.exe --background --factory-startup --python blender/scripts/build_v40_npcs.py

Outputs per character:
  blender/sources/v40/npc_<id>.blend   (with a small walk action: frames 0 / 10 / 20, for the sprites)
  unity-project/Assets/POC2D3D/Models/V40/npc_<id>.fbx  (no animation: the game swings the limbs)
  blender/exports/v40_npcs_manifest.json
Hierarchy: npc_<id> (head, hair, body; origin = ground centre) with ArmL / ArmR / LegL / LegR.
"""
import json
import math
import os
import sys

import bpy
from mathutils import Vector

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import halka_lowpoly as hl  # noqa: E402

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
SOURCES = os.path.join(ROOT, "blender", "sources", "v40")
MODELS = os.path.join(ROOT, "unity-project", "Assets", "POC2D3D", "Models", "V40")
MANIFEST = os.path.join(ROOT, "blender", "exports", "v40_npcs_manifest.json")
PIXEL = 0.044  # metres per pixel, as ﾊﾙｶﾁｬﾝ

SKIN = "#f3d6c0"
EYE = "#2a2a36"
BLUSH = "#f2a0a0"

LOOKS = {
    "fan_male": dict(hair="#6b4a2e", hair_dark="#4f3420", shirt="#5fb36b", shirt_dark="#478c52", mark="#ffffff",
                     legs="#3f5f9a", shoes="#2e2e34", hairstyle="short", smile=True, prop="penlight"),
    "fan_female": dict(hair="#8a5a3a", hair_dark="#6a4229", shirt="#f19ab8", shirt_dark="#d77c9c", mark="#ffffff",
                       legs="#f4f1ea", shoes="#b05a6a", hairstyle="long_side", smile=True, prop=None, skirt=True),
    "anti_male": dict(hair="#2c2a2a", hair_dark="#1d1c1c", shirt="#7c8088", shirt_dark="#5f636b", mark=None,
                      legs="#2b2b30", shoes="#1c1c20", hairstyle="hood", smile=False, prop=None),
    "anti_female": dict(hair="#1f1d22", hair_dark="#131215", shirt="#6d5a86", shirt_dark="#54456a", mark=None,
                        legs="#8a8a92", shoes="#2a2a30", hairstyle="bob_cap", smile=False, prop=None, skirt=True,
                        cap="#2f3340"),
}


def P(x, y, z):
    return (x * PIXEL, y * PIXEL, z * PIXEL)


def build(npc_id, look):
    hl.reset_scene()
    # Body (y = depth; -y is the front). Legs 0..5, body 5..11, head 11..19.
    body = hl.MeshBuilder()
    # torso
    body.box(P(-4, -2, 5), P(4, 2, 11), look["shirt"])
    body.box(P(-4.05, -2.05, 5), P(4.05, 2.05, 6), look["shirt_dark"])           # hem
    if look.get("skirt"):
        body.box(P(-4.4, -2.4, 3.6), P(4.4, 2.4, 5.6), look["legs"] if npc_id == "fan_female" else "#5b5b63")
    if look.get("mark"):
        body.box(P(-1.4, -2.12, 8.2), P(-0.2, -2.05, 9.0), look["mark"])        # a little heart
        body.box(P(0.2, -2.12, 8.2), P(1.4, -2.05, 9.0), look["mark"])
        body.box(P(-1.4, -2.12, 7.6), P(1.4, -2.05, 8.2), look["mark"])
        body.box(P(-0.8, -2.12, 7.0), P(0.8, -2.05, 7.6), look["mark"])
        body.box(P(-0.3, -2.12, 6.5), P(0.3, -2.05, 7.0), look["mark"])
    # head
    body.box(P(-4, -4, 11), P(4, 4, 19), SKIN)
    # face (front = -y): eyes, mouth, blush / brows
    body.box(P(-2.6, -4.08, 14.2), P(-1.4, -4.0, 15.8), EYE)
    body.box(P(1.4, -4.08, 14.2), P(2.6, -4.0, 15.8), EYE)
    if look["smile"]:
        body.box(P(-1.2, -4.08, 12.6), P(1.2, -4.0, 13.2), "#c0505a")
        body.box(P(-1.6, -4.08, 13.0), P(-1.0, -4.0, 13.4), "#c0505a")
        body.box(P(1.0, -4.08, 13.0), P(1.6, -4.0, 13.4), "#c0505a")
        body.box(P(-3.4, -4.06, 13.4), P(-2.6, -4.0, 14.0), BLUSH)
        body.box(P(2.6, -4.06, 13.4), P(3.4, -4.0, 14.0), BLUSH)
    else:
        body.box(P(-1.0, -4.08, 12.8), P(1.0, -4.0, 13.2), "#6a4a4a")
        body.box(P(-2.9, -4.08, 16.0), P(-1.2, -4.0, 16.4), look["hair_dark"])   # lowered brows
        body.box(P(1.2, -4.08, 16.0), P(2.9, -4.0, 16.4), look["hair_dark"])
    # hair
    style = look["hairstyle"]
    hair, dark = look["hair"], look["hair_dark"]
    if style == "short":
        body.box(P(-4.4, -4.4, 17.2), P(4.4, 4.4, 19.6), hair)
        body.box(P(-4.4, -3.0, 13.6), P(-3.8, 4.4, 17.2), hair)
        body.box(P(3.8, -3.0, 13.6), P(4.4, 4.4, 17.2), hair)
        body.box(P(-4.4, 3.8, 12.4), P(4.4, 4.4, 17.2), dark)
        body.box(P(-4.4, -4.45, 16.6), P(1.6, -3.8, 17.4), hair)                 # fringe
    elif style == "long_side":
        body.box(P(-4.5, -4.5, 17.0), P(4.5, 4.5, 19.7), hair)
        body.box(P(-4.5, -3.2, 11.0), P(-3.8, 4.5, 17.0), hair)
        body.box(P(3.8, -3.2, 11.0), P(4.5, 4.5, 17.0), hair)
        body.box(P(-4.5, 3.8, 9.0), P(4.5, 4.5, 17.0), dark)
        body.box(P(-4.5, -4.55, 16.4), P(4.5, -3.8, 17.2), hair)                 # straight fringe
        body.box(P(4.5, 0.5, 13.0), P(6.0, 2.5, 17.0), hair)                     # side tail
        body.box(P(4.7, 0.8, 10.0), P(5.8, 2.2, 13.0), dark)
        body.box(P(4.4, 0.4, 16.4), P(5.0, 2.6, 17.4), "#f6f2e8")                # hair tie
    elif style == "hood":
        body.box(P(-4.6, -3.6, 11.4), P(4.6, 4.8, 20.2), look["shirt"])          # the hood
        body.box(P(-4.0, -4.6, 18.6), P(4.0, -3.6, 20.2), look["shirt_dark"])   # hood rim above the face
        body.box(P(-4.6, -4.6, 11.4), P(-3.8, -3.6, 18.6), look["shirt_dark"])
        body.box(P(3.8, -4.6, 11.4), P(4.6, -3.6, 18.6), look["shirt_dark"])
        body.box(P(-3.6, -4.2, 16.8), P(3.6, -4.0, 18.6), hair)                  # fringe under the hood
        body.box(P(-0.6, -2.2, 9.0), P(-0.2, -2.1, 10.8), "#e8e8e8")             # hood strings
        body.box(P(0.2, -2.2, 9.0), P(0.6, -2.1, 10.8), "#e8e8e8")
    elif style == "bob_cap":
        body.box(P(-4.5, -3.4, 12.6), P(4.5, 4.5, 18.6), hair)                   # bob
        body.box(P(-4.5, -4.5, 16.4), P(4.5, -3.4, 17.6), hair)                  # fringe
        body.box(P(-4.7, -4.7, 18.0), P(4.7, 4.7, 20.4), look["cap"])           # cap crown
        body.box(P(-3.6, -7.2, 18.0), P(3.6, -4.7, 18.6), look["cap"])          # visor
    body_obj = body.to_object("npc_" + npc_id)

    def limb(name, lo, hi, color, end_color, pivot):
        builder = hl.MeshBuilder()
        builder.box(P(*lo), P(*hi), color)
        end_lo = (lo[0], lo[1], lo[2])
        end_hi = (hi[0], hi[1], lo[2] + 1.6)
        builder.box(P(end_lo[0] - 0.05, end_lo[1] - 0.05, end_lo[2] - 0.05), P(end_hi[0] + 0.05, end_hi[1] + 0.05, end_hi[2]), end_color)
        obj = builder.to_object(name)
        # pivot at the joint
        pv = Vector(P(*pivot))
        obj.data.transform(__import__("mathutils").Matrix.Translation(-pv))
        obj.location = pv
        obj.parent = body_obj
        return obj

    sleeve = look["shirt"]
    arm_l = limb("ArmL", (4, -1.5, 5.4), (6, 1.5, 11), sleeve, SKIN, (5, 0, 10.6))
    arm_r = limb("ArmR", (-6, -1.5, 5.4), (-4, 1.5, 11), sleeve, SKIN, (-5, 0, 10.6))
    leg_l = limb("LegL", (0.3, -1.5, 0), (3.3, 1.5, 5), look["legs"], look["shoes"], (1.8, 0, 5))
    leg_r = limb("LegR", (-3.3, -1.5, 0), (-0.3, 1.5, 5), look["legs"], look["shoes"], (-1.8, 0, 5))
    if look.get("prop") == "penlight":
        stick = hl.MeshBuilder()
        stick.box(P(-5.6, -3.4, 5.6), P(-4.4, -2.2, 6.6), "#2e2e34")
        stick.box(P(-5.5, -4.2, 6.6), P(-4.5, -3.2, 10.6), "#fff176")
        prop = stick.to_object("Penlight")
        pv = Vector(P(-5, 0, 10.6))
        prop.data.transform(__import__("mathutils").Matrix.Translation(-pv))
        prop.location = Vector((0, 0, 0))
        prop.parent = arm_r
    # a small walk (sprites only; the game swings the pivots itself)
    for obj, sign in ((arm_l, 1), (arm_r, -1), (leg_l, -1), (leg_r, 1)):
        obj.rotation_mode = "XYZ"
        for frame, angle in ((0, 0.0), (10, 24.0), (20, -24.0)):
            obj.rotation_euler = (math.radians(angle * sign), 0.0, 0.0)
            obj.keyframe_insert("rotation_euler", frame=frame)
    os.makedirs(SOURCES, exist_ok=True)
    os.makedirs(MODELS, exist_ok=True)
    bpy.context.scene.frame_set(0)
    blend = os.path.join(SOURCES, "npc_" + npc_id + ".blend")
    bpy.ops.wm.save_as_mainfile(filepath=blend)
    parts = [arm_l, arm_r, leg_l, leg_r] + ([o for o in bpy.data.objects if o.name == "Penlight"])
    for obj in [arm_l, arm_r, leg_l, leg_r]:
        obj.rotation_euler = (0.0, 0.0, 0.0)
    fbx = os.path.join(MODELS, "npc_" + npc_id + ".fbx")
    hl.export_fbx(body_obj, fbx, extra=parts)
    tris = hl.triangle_count(body_obj) + sum(hl.triangle_count(o) for o in parts)
    height = max((body_obj.matrix_world @ Vector(c)).z for c in body_obj.bound_box)
    return {"id": npc_id, "blend": os.path.relpath(blend, ROOT).replace("\\", "/"),
            "fbx": os.path.relpath(fbx, ROOT).replace("\\", "/"), "triangles": tris, "height_m": round(height, 3),
            "parts": [o.name for o in [body_obj] + parts], "look": look}


report = [build(npc_id, look) for npc_id, look in LOOKS.items()]
os.makedirs(os.path.dirname(MANIFEST), exist_ok=True)
with open(MANIFEST, "w", encoding="utf-8") as f:
    json.dump({"pixel_m": PIXEL, "npcs": report}, f, ensure_ascii=False, indent=2)
print("HALKA_V40_NPCS", json.dumps([(r["id"], r["triangles"], r["height_m"]) for r in report]))
