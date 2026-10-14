"""はるかわーるど ver3.0 Phase 2.1: a TEMPORARY low-poly stand-in for ﾊﾙｶﾁｬﾝ.

This is not the official character model. The user will provide the real model later; this
placeholder only carries the features readable in the shipped 2D sprites so the 2D/3D tests
look like ﾊﾙｶﾁｬﾝ. Colors were sampled from Assets/Content/Character/*_idle/00.png.

  blender.exe --background --factory-startup --python blender/scripts/build_haruka_placeholder.py

Outputs:
  blender/sources/phase2_1/haruka_placeholder.blend
  unity-project/Assets/POC2D3D/Models/Phase2_1/haruka_placeholder.fbx
  blender/exports/phase2_1_haruka_placeholder_manifest.json

Hierarchy (pivots at the joints so Unity can swing them; no Rig, no Gameplay dependency):
  haruka_placeholder  (body, head, hair, skirt; origin = ground centre of the foot cell)
    ArmL / ArmR       (shoulder pivots; L = the character's left = +X while facing South)
    LegL / LegR       (hip pivots)
Front faces South (Blender -Y), like every asset in this project.
"""

import json
import os
import sys

import bpy

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import halka_lowpoly as hl  # noqa: E402

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
SOURCE = os.path.join(ROOT, "blender", "sources", "phase2_1", "haruka_placeholder.blend")
FBX = os.path.join(ROOT, "unity-project", "Assets", "POC2D3D", "Models", "Phase2_1", "haruka_placeholder.fbx")
MANIFEST = os.path.join(ROOT, "blender", "exports", "phase2_1_haruka_placeholder_manifest.json")

# Sprite palette.
HAIR, HAIR_SHADE, HAIR_DEEP = "#f1e2bc", "#c8baa0", "#9d8c78"
BLUE, BLUE_DARK, BLUE_DEEP = "#4b7ae8", "#3c5fbe", "#283c87"
INK, ORNAMENT = "#12121e", "#24242e"
SKIN, BLUSH, WHITE = "#f9f6f4", "#fe1b6d", "#ffffff"
JACKET, JACKET_LIGHT, SKIRT = "#3a3a40", "#5f5a5c", "#24242e"


def hair_color(normal, center):
    if normal.z > 0.55:
        return HAIR
    if normal.z > -0.1:
        return HAIR if normal.y < 0.3 else HAIR_SHADE
    return HAIR_DEEP


def blue_color(normal, center):
    return BLUE if normal.z > -0.2 and normal.y < 0.5 else BLUE_DARK


def build_body():
    b = hl.MeshBuilder()
    # Head and face (face skin shows at the front; hair is shifted back over it).
    b.blob((0.0, -0.012, 0.64), 0.158, SKIN, SKIN, SKIN, subdivisions=2, squash=0.96)
    b.jitter_blob((0.0, 0.028, 0.675), (0.205, 0.198, 0.19), 2, 21, 0.006, hair_color)
    # Bangs: square-ish locks across the forehead, one strand crossing like the sprite.
    for x0, x1, z0 in ((-0.15, -0.075, 0.69), (-0.08, 0.0, 0.705), (0.0, 0.07, 0.7), (0.065, 0.15, 0.685)):
        b.box((x0, -0.185, z0), (x1, -0.12, 0.82), HAIR)
    b.box((0.015, -0.19, 0.6), (0.04, -0.16, 0.72), HAIR_SHADE)
    # Blue under-layer hanging at both sides and the back (the sprite's lower hair), tucked under
    # the cream hair so it reads as hair, with the cream side locks in front of it.
    for side in (-1, 1):
        x0, x1 = sorted((side * 0.15, side * 0.2))
        b.box((x0, -0.08, 0.46), (x1, 0.1, 0.62), BLUE)
        b.box((x0, -0.08, 0.46), (x1, 0.1, 0.48), BLUE_DARK)
        lx0, lx1 = sorted((side * 0.145, side * 0.2))
        b.box((lx0, -0.15, 0.5), (lx1, -0.07, 0.72), HAIR)
    b.slab([(-0.17, 0.1), (0.17, 0.1), (0.15, 0.19), (-0.15, 0.19)], 0.46, 0.6, BLUE, BLUE_DARK)
    b.box((-0.16, 0.1, 0.46), (0.16, 0.18, 0.475), BLUE_DEEP)
    # Back of the hair: one thin dark zigzag parting, as in the back sprite.
    for (x0, z0, x1, z1) in ((0.0, 0.8, 0.014, 0.87), (0.0, 0.79, 0.05, 0.804), (0.036, 0.72, 0.05, 0.8),
                             (-0.02, 0.716, 0.05, 0.73), (-0.02, 0.64, -0.006, 0.73)):
        b.box((x0, 0.2, z0), (x1, 0.222, z1), INK)
    # Black horn-like ornaments on top.
    for side in (-1, 1):
        b.cylinder_between((side * 0.1, 0.0, 0.8), (side * 0.25, -0.01, 0.95), 0.06, 0.008, 4, ORNAMENT)
    # White square hair clip on the character's right (-X while facing South).
    b.box((-0.16, -0.2, 0.735), (-0.095, -0.18, 0.8), INK)
    b.box((-0.153, -0.207, 0.742), (-0.102, -0.19, 0.793), WHITE)
    b.box((-0.135, -0.21, 0.75), (-0.12, -0.2, 0.785), INK)
    # Curled lock on the character's left, sticking out to the side and back.
    b.cylinder_between((0.17, 0.03, 0.74), (0.27, 0.09, 0.7), 0.04, 0.012, 5, HAIR)
    b.cylinder_between((0.26, 0.09, 0.705), (0.3, 0.12, 0.64), 0.014, 0.004, 4, HAIR_DEEP)
    # Face: blue eyes with dark upper lids and a highlight, pink marks under the eyes.
    for side in (-1, 1):
        x0, x1 = sorted((side * 0.03, side * 0.08))
        b.box((x0, -0.172, 0.585), (x1, -0.158, 0.655), BLUE_DARK)
        b.box((x0, -0.174, 0.62), (x1, -0.16, 0.655), BLUE)
        b.box((x0 - 0.004, -0.175, 0.652), (x1 + 0.004, -0.16, 0.668), INK)
        hx = side * 0.065
        b.box((hx - 0.009, -0.176, 0.63), (hx + 0.006, -0.162, 0.645), WHITE)
        b.box((side * 0.07 - 0.016, -0.168, 0.565), (side * 0.07 + 0.016, -0.154, 0.578), BLUSH)
    # Neck, jacket, blue collar and chest emblem.
    b.cylinder((0.0, 0.0, 0.44), 0.04, 0.04, 0.06, 6, SKIN)
    b.cylinder((0.0, 0.0, 0.29), 0.115, 0.1, 0.17, 8, JACKET)
    b.cylinder((0.0, 0.0, 0.44), 0.11, 0.09, 0.025, 8, BLUE_DARK)
    b.box((-0.04, -0.118, 0.37), (0.04, -0.1, 0.41), WHITE)
    b.box((-0.04, -0.12, 0.35), (0.04, -0.1, 0.375), BLUE)
    b.box((-0.09, -0.11, 0.3), (-0.07, -0.09, 0.44), JACKET_LIGHT)
    # Pleated black skirt with a blue hem.
    b.cylinder((0.0, 0.0, 0.18), 0.165, 0.11, 0.14, 10, SKIRT)
    b.cylinder((0.0, 0.0, 0.175), 0.168, 0.16, 0.02, 10, BLUE_DARK)
    return b.to_object("haruka_placeholder")


def build_arm(name, side):
    """Built around the shoulder pivot (0,0,0) and moved there afterwards."""
    b = hl.MeshBuilder()
    b.cylinder_between((0.0, 0.0, 0.0), (side * 0.03, 0.0, -0.14), 0.035, 0.03, 6, JACKET)
    b.cylinder_between((side * 0.03, 0.0, -0.13), (side * 0.035, 0.0, -0.16), 0.032, 0.03, 6, BLUE_DARK)
    b.blob((side * 0.036, 0.0, -0.18), 0.03, SKIN, SKIN, SKIN, subdivisions=1)
    arm = b.to_object(name)
    arm.location = (side * 0.125, 0.0, 0.43)
    return arm


def build_leg(name, side):
    b = hl.MeshBuilder()
    b.cylinder((0.0, 0.0, -0.16), 0.03, 0.032, 0.16, 6, SKIN)
    b.box((-0.035, -0.04, -0.21), (0.035, 0.035, -0.12), JACKET)
    b.box((-0.038, -0.055, -0.235), (0.038, 0.04, -0.205), BLUE_DARK)
    leg = b.to_object(name)
    leg.location = (side * 0.05, 0.0, 0.235)
    return leg


def main():
    for path in (SOURCE, FBX, MANIFEST):
        os.makedirs(os.path.dirname(path), exist_ok=True)
    hl.reset_scene()
    body = build_body()
    parts = [build_arm("ArmL", 1), build_arm("ArmR", -1), build_leg("LegL", 1), build_leg("LegR", -1)]
    for part in parts:
        part.parent = body
    hl.add_cell_guides("haruka_placeholder", [(0, 0)])
    bpy.ops.wm.save_as_mainfile(filepath=SOURCE, compress=False)
    hl.export_fbx(body, FBX, parts)
    corners = []
    for obj in [body] + parts:
        corners += [obj.matrix_world @ __import__("mathutils").Vector(c) for c in obj.bound_box]
    xs, ys, zs = zip(*[tuple(c) for c in corners])
    manifest = {
        "generator": "blender/scripts/build_haruka_placeholder.py",
        "status": "TEMPORARY placeholder, not the official character model",
        "blenderVersion": bpy.app.version_string,
        "blend": "blender/sources/phase2_1/haruka_placeholder.blend",
        "fbx": "unity-project/Assets/POC2D3D/Models/Phase2_1/haruka_placeholder.fbx",
        "front": "South (-Y in Blender, -Z in Unity)",
        "parts": {obj.name: {"triangles": hl.triangle_count(obj), "vertices": len(obj.data.vertices),
                             "materials": hl.material_count(obj),
                             "pivot": [round(v, 4) for v in obj.matrix_world.translation]}
                  for obj in [body] + parts},
        "triangles": sum(hl.triangle_count(o) for o in [body] + parts),
        "boundsMin": [round(min(xs), 4), round(min(ys), 4), round(min(zs), 4)],
        "boundsMax": [round(max(xs), 4), round(max(ys), 4), round(max(zs), 4)],
        "spriteReference": "Assets/Content/Character/{front,back,left,right}_idle/00.png (read only)",
    }
    with open(MANIFEST, "w", encoding="utf-8") as f:
        json.dump(manifest, f, indent=2, ensure_ascii=False)
        f.write("\n")
    print("HARUKA_PLACEHOLDER", manifest["triangles"], "tris", manifest["boundsMin"], manifest["boundsMax"])


if __name__ == "__main__":
    main()
