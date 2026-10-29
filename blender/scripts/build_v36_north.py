"""はるかわーるど ver3.6 (the North area, 「きたのみずべ」): the blue accents of the first North region and
the small things at its pond — a blue flower, a sign with a blue board, a water lily (the pond's
「みずをのぞく」 spot) and waterside reeds. The pond's water itself is drawn by the game (a Surface).

Run from the repository root:
  "C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" --background --factory-startup \
      --python blender/scripts/build_v36_north.py

The blue is not written here: it is the "north" colour of
unity-project/Assets/Content/Maps/World/direction_themes.json (as build_v33_east.py reads "east").
Conventions as build_v33_east.py: +X East, +Y North, +Z up, 1 cell = 0.5, origin = ground centre
of the root cell, fronts face South. New designs: candidates until the user approves them.

Outputs:
  blender/sources/v36/<family>.blend
  unity-project/Assets/POC2D3D/Models/V36/<model>.fbx
  blender/exports/v36_assets_manifest.json
"""

import json
import math
import os
import sys

import bpy

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import halka_lowpoly as hl  # noqa: E402
import build_phase2_environment as p2  # noqa: E402  (stem helper; its main() does not run)
import build_phase3_buildings as p3  # noqa: E402  (Geo, sign palette, world_bounds)
import build_v33_east as v33  # noqa: E402  (theme(), the flower face helper)

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
SOURCES = os.path.join(ROOT, "blender", "sources", "v36")
EXPORTS = os.path.join(ROOT, "blender", "exports")
UNITY_MODELS = os.path.join(ROOT, "unity-project", "Assets", "POC2D3D", "Models", "V36")

BLUE, BLUE_SHADE = v33.theme("north")
PALE_BLUE, PALE_RIM = "#a9c8ea", "#6f93bf"
STEM, LEAF, LEAF_RIM = v33.STEM, v33.LEAF, v33.LEAF_RIM
DEEP_LEAF, DEEP_LEAF_RIM = "#3f8f3a", "#24601f"
CENTRE, CENTRE_RIM = "#f4e27a", "#c9a93a"
PAD, PAD_RIM = "#5aa64a", "#2f6b2a"
REED, REED_RIM = "#7fae4a", "#4d7a2a"
CATTAIL, CATTAIL_RIM = "#7a5130", "#4d3119"
# The game draws the water at this height (PocWorldPresenter.WaterHeight): the pad floats on it.
WATER = 0.012


def build_flower_blue():
    """flower_blue: not the yellow flower recoloured — a low clump of three small five-petal blue
    flowers (forget-me-not-like) with pale yellow eyes on short stems, and long narrow leaves."""
    b = hl.MeshBuilder()
    for side, (x, y, z) in zip((-1, 1, -1, 1), ((-0.02, 0.02, 0.03), (0.03, -0.01, 0.05), (0.0, 0.04, 0.07), (-0.03, -0.03, 0.02))):
        leaf = [(x, y), (x + side * 0.07, y - 0.02), (x + side * 0.17, y + 0.01), (x + side * 0.08, y + 0.025)]
        b.slab(leaf if side > 0 else list(reversed(leaf)), z, z + 0.01, DEEP_LEAF, DEEP_LEAF_RIM)
    for (x, y, h, r, tilt) in ((-0.06, 0.02, 0.2, 0.07, 24), (0.07, 0.0, 0.17, 0.065, 30), (0.0, -0.06, 0.13, 0.06, 36)):
        p2._cylinder_between(b, (x * 0.4, y * 0.4, 0.0), (x, y, h), 0.009, 0.007, 4, STEM)
        v33._flower_head(b, (x, y - 0.005, h + 0.005), r, 5, BLUE, BLUE_SHADE, CENTRE, CENTRE_RIM, tilt)
    return [b.to_object("flower_blue", recalc_normals=False)], [], {"flower_blue": ["new design for ver3.6"]}


def build_sign_blue():
    """sign_blue: the field's sign with a North-blue top board and an arrow pointing North (up). Its
    message is its definition's (a fixed Object, like signpost_yellow; not "sign_north": the importer
    and WORLD EDITOR read the old id sign_north as sign_basic)."""
    g = p3.Geo()
    g.box((-0.03, -0.03, 0.0), (0.03, 0.03, 0.56), p3.SIGN_DARK, top=p3.SIGN_MID)
    g.box((-0.22, -0.065, 0.36), (0.22, -0.03, 0.47), BLUE_SHADE, top=BLUE, front=BLUE)
    g.box((-0.215, -0.065, 0.245), (0.215, -0.03, 0.355), p3.SIGN_MID, front=p3.SIGN_MID)
    for z in (0.355, 0.245):
        g.box((-0.215, -0.07, z - 0.006), (0.215, -0.064, z + 0.006), p3.SIGN_DARK)
    # An arrow pointing North (up) on the blue board: a direction sign, colour AND arrow.
    y = -0.068
    g.face([(-0.012, y, 0.375), (0.012, y, 0.375), (0.012, y, 0.43), (-0.012, y, 0.43)], "#f3f6fb", (0, -1, 0))
    g.face([(-0.045, y, 0.425), (0.045, y, 0.425), (0.0, y, 0.462)], "#f3f6fb", (0, -1, 0))
    sign = g.obj("sign_blue")
    hl.add_cell_guides("sign_blue", [(0, 0)])
    return [sign], [], {"sign_blue": ["new design for ver3.6", "back of the board"]}


def build_water_lily():
    """water_lily: two round lily pads (with their notch) floating on the water, one small pale-blue
    flower. It marks the place to look into the pond from the bank (みずをのぞく)."""
    b = hl.MeshBuilder()
    for (cx, cy, r, notch) in ((-0.05, 0.02, 0.15, 30), (0.11, -0.07, 0.09, 200)):
        pts = []
        for i in range(14):
            a = math.radians(notch + 20 + i * (320 / 13))
            pts.append((cx + math.cos(a) * r, cy + math.sin(a) * r))
        pts.append((cx, cy))
        b.slab(pts, WATER + 0.002, WATER + 0.01, PAD, PAD_RIM)
    v33._flower_head(b, (-0.05, 0.0, WATER + 0.05), 0.06, 6, PALE_BLUE, PALE_RIM, CENTRE, CENTRE_RIM, 15)
    obj = b.to_object("water_lily", recalc_normals=False)
    hl.add_cell_guides("water_lily", [(0, 0)])
    return [obj], [], {"water_lily": ["new design for ver3.6 (the pond's examine spot)"]}


def build_pond_reeds():
    """pond_reeds: a clump of waterside reeds, two with a brown cattail head."""
    b = hl.MeshBuilder()
    for i, (x, y, h, lean) in enumerate(((-0.09, 0.03, 0.42, -0.04), (-0.03, -0.04, 0.5, 0.0), (0.04, 0.05, 0.36, 0.03),
                                        (0.1, -0.02, 0.45, 0.05), (0.0, 0.08, 0.3, -0.02))):
        top = (x + lean, y, h)
        p2._cylinder_between(b, (x, y, 0.0), top, 0.012, 0.006, 4, REED)
        if i in (1, 3):
            p2._cylinder_between(b, (x + lean * 0.8, y, h * 0.78), (x + lean * 0.95, y, h * 0.94), 0.026, 0.024, 6, CATTAIL)
    for side, (x, y) in zip((-1, 1, 1), ((-0.05, 0.0), (0.05, 0.01), (0.0, -0.05))):
        blade = [(x - 0.012, y), (x + 0.012, y), (x + side * 0.09, y + 0.01)]
        b.slab(blade, 0.02, 0.03, REED, REED_RIM)
    obj = b.to_object("pond_reeds", recalc_normals=False)
    hl.add_cell_guides("pond_reeds", [(0, 0)])
    return [obj], [], {"pond_reeds": ["new design for ver3.6 (waterside plants)"]}


FAMILIES = (
    ("flower_blue", build_flower_blue),
    ("sign_blue", build_sign_blue),
    ("water_lily", build_water_lily),
    ("pond_reeds", build_pond_reeds),
)


def main():
    for folder in (SOURCES, EXPORTS, UNITY_MODELS):
        os.makedirs(folder, exist_ok=True)
    manifest = {
        "generator": "blender/scripts/build_v36_north.py",
        "blenderVersion": bpy.app.version_string,
        "units": "1 Blender unit = 1 Unity unit, 1 cell = 0.5, origin = ground centre of the root cell",
        "theme": {"source": "unity-project/Assets/Content/Maps/World/direction_themes.json", "north": [BLUE, BLUE_SHADE]},
        "approval": "pending: new ver3.6 designs, not approved by the user yet",
        "models": [],
    }
    for family, build in FAMILIES:
        hl.reset_scene()
        objects, anchors, supplemented = build()
        bpy.ops.wm.save_as_mainfile(filepath=os.path.join(SOURCES, family + ".blend"), compress=False)
        for obj in [o for o in objects if o.parent is None]:
            fbx = os.path.join(UNITY_MODELS, obj.name + ".fbx")
            hl.export_fbx(obj, fbx, [])
            lo, hi = p3.world_bounds([obj])
            manifest["models"].append({
                "family": family, "name": obj.name, "blend": f"blender/sources/v36/{family}.blend",
                "fbx": os.path.relpath(fbx, ROOT).replace("\\", "/"),
                "triangles": hl.triangle_count(obj), "vertices": len(obj.data.vertices), "materials": hl.material_count(obj),
                "boundsMin": lo, "boundsMax": hi, "anchors": {},
                "supplemented": supplemented.get(obj.name, []),
            })
            print(f"HARUKA_V36_ASSET {obj.name}: {hl.triangle_count(obj)} tris, bounds {lo} {hi}")
    with open(os.path.join(EXPORTS, "v36_assets_manifest.json"), "w", encoding="utf-8") as f:
        json.dump(manifest, f, indent=2, ensure_ascii=False)
        f.write("\n")


if __name__ == "__main__":
    main()
