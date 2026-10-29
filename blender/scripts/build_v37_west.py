"""はるかわーるど ver3.7 (the West area, 「にしのこもれびのもり」): the forest floor and the clearing of the
first West region — a big hollow stump (the clearing's 「きりかぶにふれる」 spot), a low forest bush, ferns,
a few fallen leaves, and a sign with a green board. The trees are the field's own tree_basic; the
sunlight through the leaves (木漏れ日) is drawn by the game.

Run from the repository root:
  "C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" --background --factory-startup \
      --python blender/scripts/build_v37_west.py

The green is not written here: it is the "west" colour of
unity-project/Assets/Content/Maps/World/direction_themes.json (as build_v36_north.py reads "north").
Conventions as build_v36_north.py: +X East, +Y North, +Z up, 1 cell = 0.5, origin = ground centre
of the root cell, fronts face South. New designs: candidates until the user approves them.

Outputs:
  blender/sources/v37/<family>.blend
  unity-project/Assets/POC2D3D/Models/V37/<model>.fbx
  blender/exports/v37_assets_manifest.json
"""

import json
import math
import os
import sys

import bpy

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import halka_lowpoly as hl  # noqa: E402
import build_phase2_environment as p2  # noqa: E402  (stem helper, rotate helper; its main() does not run)
import build_phase3_buildings as p3  # noqa: E402  (Geo, sign palette, world_bounds)
import build_v33_east as v33  # noqa: E402  (theme())

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
SOURCES = os.path.join(ROOT, "blender", "sources", "v37")
EXPORTS = os.path.join(ROOT, "blender", "exports")
UNITY_MODELS = os.path.join(ROOT, "unity-project", "Assets", "POC2D3D", "Models", "V37")

GREEN, GREEN_SHADE = v33.theme("west")
# The field tree's bark (build_phase2_environment.build_tree): the stump is one of those trees, cut.
BARK, BARK_DARK = "#a8735a", "#79504d"
WOOD, WOOD_RING, HOLLOW = "#e2c08e", "#bf9662", "#4a3324"
MOSS, MOSS_DARK = "#86b04e", "#5a8236"
BUSH_TOP, BUSH_SIDE, BUSH_LOW, BUSH_SHADOW = "#88b867", "#679a58", "#4f7f4f", "#3c6346"
FERN, FERN_RIM = "#74b04a", "#3d7a2c"
LEAF_COLOURS = (("#e0a03c", "#9c6522"), ("#e9c94e", "#a98a24"), ("#b8703c", "#7a4524"), ("#9cc552", "#5f8a2c"))


def build_stump_hollow():
    """stump_hollow: a big tree stump (wider than a tree's trunk) cut flat, with year rings and a dark
    hollow in its heart, a few roots, and moss on its North side. The clearing's examine spot."""
    b = hl.MeshBuilder()
    before = b._snapshot()
    b.cylinder((0, 0, 0), 0.215, 0.185, 0.24, 10, BARK)
    for angle in (15, 85, 160, 230, 300):
        a = math.radians(angle)
        p2._cylinder_between(b, (0.12 * math.cos(a), 0.12 * math.sin(a), 0.07),
                             (0.27 * math.cos(a), 0.27 * math.sin(a), -0.01), 0.06, 0.03, 5, BARK_DARK)
    p2._shade_by_normal(b, before, BARK, BARK_DARK)
    # The cut: a pale disc with two rings and the hollow (the rims of the slabs draw the rings' lines).
    top = 0.24

    def disc(r, n=10, start=0.0):
        return [(r * math.cos(start + i * 2 * math.pi / n), r * math.sin(start + i * 2 * math.pi / n)) for i in range(n)]

    b.slab(disc(0.18), top, top + 0.006, WOOD, WOOD_RING)
    b.slab(disc(0.13, 10, 0.2), top + 0.006, top + 0.009, WOOD, WOOD_RING)
    b.slab(disc(0.085, 9, 0.5), top + 0.009, top + 0.012, WOOD, WOOD_RING)
    b.slab([(x + 0.01, y + 0.015) for x, y in disc(0.05, 8, 0.3)], top + 0.012, top + 0.013, HOLLOW, HOLLOW)
    # Moss over the North rim and down the back.
    b.slab([(-0.13, 0.11), (0.13, 0.11), (0.17, 0.15), (0.0, 0.205), (-0.17, 0.15)], top + 0.006, top + 0.02, MOSS, MOSS_DARK)
    b.box((-0.12, 0.17, 0.1), (0.12, 0.205, top + 0.01), MOSS_DARK)
    obj = b.to_object("stump_hollow", recalc_normals=False)
    hl.add_cell_guides("stump_hollow", [(0, 0)])
    return [obj], [], {"stump_hollow": ["new design for ver3.7 (the clearing's examine spot)"]}


def build_forest_bush():
    """forest_bush: a low round bush of three clumps, darker than the tree crowns (it sits in their
    shade), as wide as a cell and lower than her."""
    b = hl.MeshBuilder()

    def colour(normal, center):
        if normal.z > 0.7:
            return BUSH_TOP
        if normal.z > 0.15:
            return BUSH_SIDE
        if normal.z > -0.3:
            return BUSH_LOW
        return BUSH_SHADOW

    for i, (center, radii) in enumerate((((0.0, 0.03, 0.2), (0.2, 0.17, 0.18)), ((-0.13, -0.04, 0.14), (0.13, 0.12, 0.13)),
                                         ((0.13, -0.05, 0.15), (0.13, 0.12, 0.14)))):
        b.jitter_blob(center, radii, 2 if i == 0 else 1, 700 + i, 0.02, colour)
    for vert in b.bm.verts:
        vert.co.z = max(0.0, vert.co.z)
    b.bm.normal_update()
    obj = b.to_object("forest_bush")
    hl.add_cell_guides("forest_bush", [(0, 0)])
    return [obj], [], {"forest_bush": ["new design for ver3.7"]}


def build_forest_fern():
    """forest_fern: seven fern fronds arching out of one root, each a long leaf with toothed edges,
    tilted up. Walkable (decoration only)."""
    b = hl.MeshBuilder()
    for i in range(7):
        yaw = math.radians(-90 + i * (360 / 7) + 12)
        length = 0.2 if i % 2 == 0 else 0.16
        outline = []
        steps = 6
        for k in range(steps + 1):   # the upper edge, toothed
            t = k / steps
            w = 0.035 * math.sin(math.pi * min(1.0, t * 1.15)) * (1.0 if k % 2 == 0 else 0.55)
            outline.append((t * length, w))
        for k in range(steps, -1, -1):   # back along the lower edge
            t = k / steps
            w = 0.035 * math.sin(math.pi * min(1.0, t * 1.15)) * (1.0 if k % 2 == 1 else 0.55)
            outline.append((t * length, -w))
        outline = outline[:steps + 1] + outline[steps + 2:]
        verts = set(b.bm.verts)
        b.slab(outline, 0.0, 0.008, FERN, FERN_RIM)
        p2._rotate_new(b, verts, (0.0, 0.0, 0.0), math.radians(-38), "Y")   # the frond rises
        p2._rotate_new(b, verts, (0.0, 0.0, 0.0), yaw, "Z")
        for vert in b.bm.verts:
            if vert not in verts:
                vert.co.z += 0.01
    obj = b.to_object("forest_fern", recalc_normals=False)
    hl.add_cell_guides("forest_fern", [(0, 0)])
    return [obj], [], {"forest_fern": ["new design for ver3.7 (walkable)"]}


def build_fallen_leaves():
    """fallen_leaves: seven small leaves lying flat on the ground (orange, yellow, brown, a green one),
    a little apart. Walkable (decoration only)."""
    b = hl.MeshBuilder()
    leaves = ((-0.13, 0.09, 30, 0), (0.06, 0.13, 110, 1), (0.15, -0.02, 200, 2), (-0.04, -0.1, 65, 3),
              (-0.17, -0.07, 160, 1), (0.11, -0.15, 15, 0), (0.0, 0.02, 250, 2))
    for i, (x, y, angle, colour) in enumerate(leaves):
        a = math.radians(angle)
        c, s = math.cos(a), math.sin(a)
        shape = [(-0.05, 0.0), (-0.02, -0.022), (0.03, -0.02), (0.055, 0.0), (0.03, 0.02), (-0.02, 0.022)]
        top, rim = LEAF_COLOURS[colour]
        z = 0.006 + i * 0.0015
        b.slab([(x + px * c - py * s, y + px * s + py * c) for px, py in shape], z, z + 0.004, top, rim)
    obj = b.to_object("fallen_leaves", recalc_normals=False)
    hl.add_cell_guides("fallen_leaves", [(0, 0)])
    return [obj], [], {"fallen_leaves": ["new design for ver3.7 (walkable)"]}


def build_sign_green():
    """sign_green: the field's sign with a West-green top board and an arrow pointing West (left). Its
    message is its definition's (a fixed Object, like signpost_blue; not "sign_west": the importer
    and WORLD EDITOR read the old id sign_west as sign_basic)."""
    g = p3.Geo()
    g.box((-0.03, -0.03, 0.0), (0.03, 0.03, 0.56), p3.SIGN_DARK, top=p3.SIGN_MID)
    g.box((-0.22, -0.065, 0.36), (0.22, -0.03, 0.47), GREEN_SHADE, top=GREEN, front=GREEN)
    g.box((-0.215, -0.065, 0.245), (0.215, -0.03, 0.355), p3.SIGN_MID, front=p3.SIGN_MID)
    for z in (0.355, 0.245):
        g.box((-0.215, -0.07, z - 0.006), (0.215, -0.064, z + 0.006), p3.SIGN_DARK)
    # An arrow pointing West (left) on the green board: a direction sign, colour AND arrow.
    y = -0.068
    g.face([(-0.02, y, 0.403), (0.07, y, 0.403), (0.07, y, 0.427), (-0.02, y, 0.427)], "#f3f6fb", (0, -1, 0))
    g.face([(-0.075, y, 0.415), (-0.02, y, 0.385), (-0.02, y, 0.445)], "#f3f6fb", (0, -1, 0))
    sign = g.obj("sign_green")
    hl.add_cell_guides("sign_green", [(0, 0)])
    return [sign], [], {"sign_green": ["new design for ver3.7", "back of the board"]}


FAMILIES = (
    ("stump_hollow", build_stump_hollow),
    ("forest_bush", build_forest_bush),
    ("forest_fern", build_forest_fern),
    ("fallen_leaves", build_fallen_leaves),
    ("sign_green", build_sign_green),
)


def main():
    for folder in (SOURCES, EXPORTS, UNITY_MODELS):
        os.makedirs(folder, exist_ok=True)
    manifest = {
        "generator": "blender/scripts/build_v37_west.py",
        "blenderVersion": bpy.app.version_string,
        "units": "1 Blender unit = 1 Unity unit, 1 cell = 0.5, origin = ground centre of the root cell",
        "theme": {"source": "unity-project/Assets/Content/Maps/World/direction_themes.json", "west": [GREEN, GREEN_SHADE]},
        "approval": "pending: new ver3.7 designs, not approved by the user yet",
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
                "family": family, "name": obj.name, "blend": f"blender/sources/v37/{family}.blend",
                "fbx": os.path.relpath(fbx, ROOT).replace("\\", "/"),
                "triangles": hl.triangle_count(obj), "vertices": len(obj.data.vertices), "materials": hl.material_count(obj),
                "boundsMin": lo, "boundsMax": hi, "anchors": {},
                "supplemented": supplemented.get(obj.name, []),
            })
            print(f"HARUKA_V37_ASSET {obj.name}: {hl.triangle_count(obj)} tris, bounds {lo} {hi}")
    with open(os.path.join(EXPORTS, "v37_assets_manifest.json"), "w", encoding="utf-8") as f:
        json.dump(manifest, f, indent=2, ensure_ascii=False)
        f.write("\n")


if __name__ == "__main__":
    main()
