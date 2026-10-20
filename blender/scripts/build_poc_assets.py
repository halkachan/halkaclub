"""Rebuilds the HALKA WORLD ver3.0 Phase 1 placeholder assets from code.

Run from the repository root:
  "C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" --background --factory-startup \
      --python blender/scripts/build_poc_assets.py

Outputs:
  blender/sources/poc_<name>.blend                     editable Blender source (Source of Truth)
  unity-project/Assets/POC2D3D/Models/poc_<name>.fbx   Unity export
  blender/exports/poc_assets_manifest.json             triangle counts and export settings

These are technical placeholders only. They are not the official HALKA character,
tree or house designs.
"""

import json
import os
import sys

import bpy

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import halka_lowpoly as hl  # noqa: E402

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
SOURCES = os.path.join(ROOT, "blender", "sources")
EXPORTS = os.path.join(ROOT, "blender", "exports")
UNITY_MODELS = os.path.join(ROOT, "unity-project", "Assets", "POC2D3D", "Models")


def build_player():
    """Neutral mannequin. Front (-Y): face, eyes, blue badge. Back: orange pack.
    Character's right side (-X when facing south): yellow shoulder mark."""
    b = hl.MeshBuilder()
    body, dark, skin, hair = "#3a3d52", "#22232b", "#f4e4d4", "#c9c3b8"
    for side in (-1, 1):
        x0, x1 = sorted((side * 0.025, side * 0.085))
        b.box((x0, -0.035, 0.04), (x1, 0.035, 0.24), "#3b3d4a")
        b.box((x0 - 0.005, -0.065, 0.0), (x1 + 0.005, 0.04, 0.05), dark)
        ax0, ax1 = sorted((side * 0.1, side * 0.15))
        b.box((ax0, -0.035, 0.31), (ax1, 0.035, 0.52), body)
        b.box((ax0 + 0.005, -0.03, 0.27), (ax1 - 0.005, 0.03, 0.31), skin)
    b.cylinder((0, 0, 0.18), 0.15, 0.11, 0.15, 8, "#2f3140")
    b.box((-0.1, -0.07, 0.33), (0.1, 0.07, 0.55), body)
    b.box((-0.045, -0.088, 0.42), (0.045, -0.066, 0.49), "#3a7be0")
    b.box((-0.07, 0.068, 0.37), (0.07, 0.12, 0.52), "#d98a3a")
    b.box((-0.165, -0.045, 0.49), (-0.095, 0.045, 0.55), "#f2c94c")
    b.box((-0.04, -0.04, 0.55), (0.04, 0.04, 0.6), skin)
    b.blob((0, 0.0, 0.75), 0.2, hair, hair, "#b8b2a8", subdivisions=2, squash=0.92)
    b.box((-0.13, -0.205, 0.63), (0.13, -0.145, 0.78), skin)
    b.box((-0.155, -0.2, 0.775), (0.155, -0.11, 0.86), hair)
    for side in (-1, 1):
        x0, x1 = sorted((side * 0.035, side * 0.085))
        b.box((x0, -0.215, 0.68), (x1, -0.2, 0.745), "#2c56c8")
    obj = b.to_object("POC_Player")
    hl.add_cell_guides("POC_Player", [(0, 0)])
    return obj


def build_tree():
    """Placeholder for tree_basic: 1x1 blocked root cell, crown spans about 3 cells."""
    b = hl.MeshBuilder()
    b.cylinder((0, 0, 0), 0.15, 0.09, 0.08, 7, "#6b4528")
    b.cylinder((0, 0, 0.06), 0.09, 0.065, 0.62, 7, "#7d5434", rotation=hl.deg(10))
    top, side, bottom = "#9ccc58", "#6fa63e", "#4b7f2e"
    for center, radius in (((0.0, 0.02, 1.0), 0.46), ((-0.32, -0.08, 0.86), 0.3),
                           ((0.33, -0.05, 0.87), 0.31), ((0.0, -0.25, 0.83), 0.3),
                           ((0.1, 0.28, 0.96), 0.3), ((-0.12, 0.05, 1.3), 0.3)):
        b.blob(center, radius, top, side, bottom, subdivisions=2, squash=0.85)
    obj = b.to_object("POC_Tree")
    hl.add_cell_guides("POC_Tree", [(0, 0)])
    return obj


def build_house():
    """Placeholder for house_main. Root cell (0,0) is the walkable door alcove.
    Blocked cells: x -2..2 on rows 0 and 1, except the door cell."""
    b = hl.MeshBuilder()
    wall, frame, roof, ridge, gold = "#ece8dc", "#4a4b52", "#3d3e46", "#2d2e35", "#e3b33a"
    b.box((-1.2, 0.25, 0.0), (1.2, 0.7, 0.85), wall)
    b.box((-1.2, -0.2, 0.0), (-0.25, 0.25, 0.85), wall)
    b.box((0.25, -0.2, 0.0), (1.2, 0.25, 0.85), wall)
    b.box((-0.25, -0.2, 0.66), (0.25, 0.25, 0.85), wall)
    for x0, x1, y0, y1 in ((-1.22, 1.22, 0.25, 0.72), (-1.22, -0.25, -0.22, 0.25),
                           (0.25, 1.22, -0.22, 0.25)):
        b.box((x0, y0, 0.0), (x1, y1, 0.06), "#55565e")
    b.prism([(-1.2, 0.85), (1.2, 0.85), (0.0, 1.5)], -0.2, 0.7, wall)
    for x in (-1.2, 1.2):
        for y in (-0.2, 0.7):
            b.box((x - 0.045, y - 0.045, 0.0), (x + 0.045, y + 0.045, 0.86), frame)
    for x in (-0.25, 0.25):
        b.box((x - 0.04, -0.235, 0.0), (x + 0.04, -0.17, 0.86), frame)
    b.box((-1.24, -0.235, 0.82), (1.24, -0.19, 0.89), frame)
    b.box((-1.24, 0.69, 0.82), (1.24, 0.735, 0.89), frame)
    b.prism([(-1.36, 0.78), (0.0, 1.56), (0.0, 1.67), (-1.36, 0.89)], -0.32, 0.82, roof)
    b.prism([(0.0, 1.56), (1.36, 0.78), (1.36, 0.89), (0.0, 1.67)], -0.32, 0.82, roof)
    b.box((-0.06, -0.34, 1.6), (0.06, 0.84, 1.71), ridge)
    b.box((-0.17, 0.22, 0.0), (0.17, 0.26, 0.58), "#34343a")
    b.prism([(-0.17, 0.58), (0.17, 0.58), (0.12, 0.68), (0.0, 0.72), (-0.12, 0.68)],
            0.22, 0.26, "#34343a")
    b.box((0.09, 0.205, 0.28), (0.13, 0.225, 0.32), gold)
    b.prism([(0.0, 1.03), (0.075, 1.12), (0.0, 1.21), (-0.075, 1.12)], -0.235, -0.2, gold)
    for x in (-0.72, 0.72):
        b.box((x - 0.035, -0.235, 0.7), (x + 0.035, -0.2, 0.77), gold)
    obj = b.to_object("POC_House")
    cells = [(x, y) for x in range(-2, 3) for y in range(0, 2)]
    hl.add_cell_guides("POC_House", cells)
    return obj


ASSETS = (("player", build_player), ("tree", build_tree), ("house", build_house))


def main():
    for folder in (SOURCES, EXPORTS, UNITY_MODELS):
        os.makedirs(folder, exist_ok=True)
    manifest = {
        "generator": "blender/scripts/build_poc_assets.py",
        "blenderVersion": bpy.app.version_string,
        "export": {"format": "FBX 7.4 binary", "axisForward": "-Z", "axisUp": "Y",
                   "bakeSpaceTransform": True, "applyScaleOptions": "FBX_SCALE_ALL",
                   "colors": "SRGB face-corner attribute 'Col'"},
        "assets": [],
    }
    for name, build in ASSETS:
        hl.reset_scene()
        obj = build()
        blend_path = os.path.join(SOURCES, f"poc_{name}.blend")
        fbx_path = os.path.join(UNITY_MODELS, f"poc_{name}.fbx")
        bpy.ops.wm.save_as_mainfile(filepath=blend_path, compress=False)
        hl.export_fbx(obj, fbx_path)
        bounds = [tuple(round(v, 4) for v in corner) for corner in obj.bound_box]
        xs, ys, zs = zip(*bounds)
        manifest["assets"].append({
            "name": obj.name, "blend": os.path.relpath(blend_path, ROOT).replace("\\", "/"),
            "fbx": os.path.relpath(fbx_path, ROOT).replace("\\", "/"),
            "triangles": hl.triangle_count(obj), "vertices": len(obj.data.vertices),
            "boundsMin": [min(xs), min(ys), min(zs)], "boundsMax": [max(xs), max(ys), max(zs)],
        })
        print(f"HALKA_POC_ASSET {name}: {hl.triangle_count(obj)} tris -> {fbx_path}")
    with open(os.path.join(EXPORTS, "poc_assets_manifest.json"), "w", encoding="utf-8") as f:
        json.dump(manifest, f, indent=2)
        f.write("\n")


if __name__ == "__main__":
    main()
