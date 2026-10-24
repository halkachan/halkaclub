"""はるかわーるど ver3.3 (the East area): the yellow accents of the first East region — a yellow
flower, the landmark of its small plaza (a cluster of sunflowers) and a sign with a yellow board.

Run from the repository root:
  "C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" --background --factory-startup \
      --python blender/scripts/build_v33_east.py

The yellow is not written here: it is the "east" colour of
unity-project/Assets/Content/Maps/World/direction_themes.json (North blue, East yellow, South red,
West green), so a later region of another direction can be built the same way with its colour.
Conventions as build_phase2_environment.py / build_phase3_buildings.py: +X East, +Y North, +Z up,
1 cell = 0.5, origin = ground centre of the root cell, fronts face South. New designs: candidates
until the user approves them.

Outputs:
  blender/sources/v33/<family>.blend
  unity-project/Assets/POC2D3D/Models/V33/<model>.fbx
  blender/exports/v33_assets_manifest.json
"""

import json
import math
import os
import sys

import bpy

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import halka_lowpoly as hl  # noqa: E402
import build_phase2_environment as p2  # noqa: E402  (flower helpers; its main() does not run)
import build_phase3_buildings as p3  # noqa: E402  (Geo, sign palette, world_bounds)

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
SOURCES = os.path.join(ROOT, "blender", "sources", "v33")
EXPORTS = os.path.join(ROOT, "blender", "exports")
UNITY_MODELS = os.path.join(ROOT, "unity-project", "Assets", "POC2D3D", "Models", "V33")
THEMES = os.path.join(ROOT, "unity-project", "Assets", "Content", "Maps", "World", "direction_themes.json")


def theme(direction):
    with open(THEMES, encoding="utf-8") as f:
        for t in json.load(f)["themes"]:
            if t["direction"] == direction:
                return t["color"], t["shade"]
    raise KeyError(direction)


YELLOW, YELLOW_SHADE = theme("east")
STEM, LEAF, LEAF_RIM = "#49a125", "#94cd34", "#2f7a1a"
SEED, SEED_RIM = "#7a4f1d", "#4e3110"


def _flower_head(b, center, radius, petals, color, rim, seed, seed_rim, tilt):
    """A flat flower face (petal slabs around a seed disc), tilted toward the South."""
    before = set(b.bm.verts)
    for i in range(petals):
        a = math.radians(90 + i * 360 / petals)
        c, s = math.cos(a), math.sin(a)
        petal = []
        for px, py in ((0.02, 0.0), (0.45, -0.3), (0.8, -0.26), (1.0, 0.0), (0.8, 0.26), (0.45, 0.3)):
            px, py = px * radius, py * radius * 0.9
            petal.append((center[0] + px * c - py * s, center[1] + px * s + py * c))
        b.slab(petal, center[2] - 0.008, center[2] + 0.008, color, rim)
    disc = [(center[0] + radius * 0.38 * math.cos(math.radians(a)), center[1] + radius * 0.38 * math.sin(math.radians(a)))
            for a in range(0, 360, 45)]
    b.slab(disc, center[2] + 0.002, center[2] + 0.022, seed, seed_rim)
    p2._rotate_new(b, before, center, math.radians(tilt), "X")


def build_flower_yellow():
    """flower_yellow: the field's flower (flower_basic) with East-yellow petals and a brown centre."""
    b = hl.MeshBuilder()
    p2._cylinder_between(b, (0.0, 0.0, 0.0), (0.0, -0.01, 0.27), 0.014, 0.011, 5, STEM)
    for side, z in ((-1, 0.07), (1, 0.11)):
        leaf = [(0.0, 0.0), (side * 0.06, -0.025), (side * 0.13, 0.0), (side * 0.06, 0.028)]
        b.slab([(x, y) for x, y in (leaf if side > 0 else list(reversed(leaf)))], z, z + 0.012, LEAF, LEAF_RIM)
    _flower_head(b, (0.0, -0.01, 0.28), 0.125, 5, YELLOW, YELLOW_SHADE, SEED, SEED_RIM, 28)
    return [b.to_object("flower_yellow", recalc_normals=False)], [], {"flower_yellow": ["new design for ver3.3"]}


def build_sunflower_cluster():
    """sunflower_cluster (the landmark of the East plaza): three sunflowers of different heights
    with big yellow faces turned South, a ring of small yellow flowers at their feet. 1 x 1, blocked."""
    b = hl.MeshBuilder()
    for (x, y, h, r) in ((-0.07, 0.05, 0.72, 0.13), (0.1, 0.02, 0.58, 0.115), (0.0, -0.08, 0.44, 0.1)):
        p2._cylinder_between(b, (x, y, 0.0), (x, y - 0.02, h), 0.02, 0.015, 6, STEM)
        for side, z in ((-1, h * 0.35), (1, h * 0.6)):
            leaf = [(x, y), (x + side * 0.08, y - 0.035), (x + side * 0.16, y), (x + side * 0.08, y + 0.04)]
            b.slab(leaf if side > 0 else list(reversed(leaf)), z, z + 0.014, LEAF, LEAF_RIM)
        _flower_head(b, (x, y - 0.03, h + 0.01), r, 12, YELLOW, YELLOW_SHADE, SEED, SEED_RIM, 35)
    for i in range(6):  # small yellow flowers around the stems
        a = i / 6 * math.tau + 0.3
        cx, cy = math.cos(a) * 0.19, math.sin(a) * 0.17
        p2._cylinder_between(b, (cx, cy, 0.0), (cx, cy, 0.09), 0.008, 0.006, 4, STEM)
        _flower_head(b, (cx, cy - 0.005, 0.1), 0.045, 5, YELLOW, YELLOW_SHADE, SEED, SEED_RIM, 25)
    obj = b.to_object("sunflower_cluster", recalc_normals=False)
    hl.add_cell_guides("sunflower_cluster", [(0, 0)])
    return [obj], [], {"sunflower_cluster": ["new design for ver3.3 (landmark of the East plaza)"]}


def build_sign_yellow():
    """sign_yellow: the field's sign (sign_basic) whose top board is East-yellow with a darker rim and
    a small arrow; the text stays in the Map's signText. (Not "sign_east": the game's importer and
    WORLD EDITOR still read the old ids sign_north / sign_east / sign_south / sign_west as sign_basic.)"""
    g = p3.Geo()
    g.box((-0.03, -0.03, 0.0), (0.03, 0.03, 0.56), p3.SIGN_DARK, top=p3.SIGN_MID)
    g.box((-0.22, -0.065, 0.36), (0.22, -0.03, 0.47), YELLOW_SHADE, top=YELLOW, front=YELLOW)
    g.box((-0.215, -0.065, 0.245), (0.215, -0.03, 0.355), p3.SIGN_MID, front=p3.SIGN_MID)
    for z in (0.355, 0.245):
        g.box((-0.215, -0.07, z - 0.006), (0.215, -0.064, z + 0.006), p3.SIGN_DARK)
    # An arrow pointing East on the yellow board (a direction sign: colour AND arrow).
    y = -0.068
    g.face([(-0.12, y, 0.405), (0.06, y, 0.405), (0.06, y, 0.425), (-0.12, y, 0.425)], p3.SIGN_DARK, (0, -1, 0))
    g.face([(0.06, y, 0.385), (0.14, y, 0.415), (0.06, y, 0.445)], p3.SIGN_DARK, (0, -1, 0))
    sign = g.obj("sign_yellow")
    hl.add_cell_guides("sign_yellow", [(0, 0)])
    return [sign], [], {"sign_yellow": ["new design for ver3.3", "back of the board"]}


FAMILIES = (
    ("flower_yellow", build_flower_yellow),
    ("sunflower_cluster", build_sunflower_cluster),
    ("sign_yellow", build_sign_yellow),
)


def main():
    for folder in (SOURCES, EXPORTS, UNITY_MODELS):
        os.makedirs(folder, exist_ok=True)
    manifest = {
        "generator": "blender/scripts/build_v33_east.py",
        "blenderVersion": bpy.app.version_string,
        "units": "1 Blender unit = 1 Unity unit, 1 cell = 0.5, origin = ground centre of the root cell",
        "theme": {"source": "unity-project/Assets/Content/Maps/World/direction_themes.json", "east": [YELLOW, YELLOW_SHADE]},
        "approval": "pending: new ver3.3 designs, not approved by the user yet",
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
                "family": family, "name": obj.name, "blend": f"blender/sources/v33/{family}.blend",
                "fbx": os.path.relpath(fbx, ROOT).replace("\\", "/"),
                "triangles": hl.triangle_count(obj), "vertices": len(obj.data.vertices), "materials": hl.material_count(obj),
                "boundsMin": lo, "boundsMax": hi, "anchors": {},
                "supplemented": supplemented.get(obj.name, []),
            })
            print(f"HARUKA_V33_ASSET {obj.name}: {hl.triangle_count(obj)} tris, bounds {lo} {hi}")
    with open(os.path.join(EXPORTS, "v33_assets_manifest.json"), "w", encoding="utf-8") as f:
        json.dump(manifest, f, indent=2, ensure_ascii=False)
        f.write("\n")


if __name__ == "__main__":
    main()
