"""はるかわーるど ver3.8 (the South area, 「みなみのあかねのかぜのおか」): the red-brown rocks and the windy
hill of the first South region — the red pinwheel on the hilltop (its 「かざぐるまをまわす」 spot; the game
turns its blades), a red-brown boulder, a low rock ledge (the hill's edge), a few small stones and short
grass (walkable), a patch of reddish soil on the ground (walkable), and a sign with a red board. The
trees are the field's own tree_basic.

Run from the repository root:
  "C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" --background --factory-startup \
      --python blender/scripts/build_v38_south.py

The red is not written here: it is the "south" colour of
unity-project/Assets/Content/Maps/World/direction_themes.json (as build_v37_west.py reads "west").
Conventions as build_v37_west.py: +X East, +Y North, +Z up, 1 cell = 0.5, origin = ground centre
of the root cell, fronts face South. New designs: candidates until the user approves them.

The pinwheel is two objects: pinwheel_red (the pole and the pin's head; static) and its child
pinwheel_blades (the four blades and their cap), whose origin is the pin: the game turns that child
about its own South-facing axis (PocWorldPresenter.Pinwheel.cs), never the pole.

Outputs:
  blender/sources/v38/<family>.blend
  unity-project/Assets/POC2D3D/Models/V38/<model>.fbx
  blender/exports/v38_assets_manifest.json
"""

import json
import math
import os
import sys

import bpy

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import halka_lowpoly as hl  # noqa: E402
import build_phase2_environment as p2  # noqa: E402  (rotate helper; its main() does not run)
import build_phase3_buildings as p3  # noqa: E402  (Geo, sign palette, world_bounds)
import build_v33_east as v33  # noqa: E402  (theme())

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
SOURCES = os.path.join(ROOT, "blender", "sources", "v38")
EXPORTS = os.path.join(ROOT, "blender", "exports")
UNITY_MODELS = os.path.join(ROOT, "unity-project", "Assets", "POC2D3D", "Models", "V38")

RED, RED_SHADE = v33.theme("south")
BLADE_LIGHT, BLADE_BACK = "#e48a76", "#f2d9cc"   # the paler blades (so the turning shows) and the paper's back
CAP, CAP_RIM = "#f3ead8", "#c9b99c"
# Red-brown rock: lit top, sides, the shaded lower band, underside.
ROCK_TOP, ROCK_SIDE, ROCK_LOW, ROCK_SHADOW = "#c98466", "#a9604a", "#8a4a3b", "#663630"
# The ledge's turf lip: the field's pale grass (build_phase2_environment ground_grass), a little deeper.
TURF, TURF_RIM = "#cfe1a8", "#a9c47e"
SOIL, SOIL_RIM = "#dba383", "#bb7f5f"           # reddish soil, lighter than the road
GRASS_DARK, GRASS_LIGHT = "#6f9a3c", "#b4c85a"  # short, sun-dried grass

PIN_HEIGHT = 1.2       # the pin (the blades' axis) above the ground: over her head when she stands South of it
                       # (0.78 at first: in 3D her head hid the blades from the camera behind her)
PIN_FRONT = -0.045     # the blades stand just South of the pole
BLADE_RADIUS = 0.26


def rock_colour(normal, center):
    if normal.z > 0.65:
        return ROCK_TOP
    if normal.z > 0.15:
        return ROCK_SIDE
    if normal.z > -0.3:
        return ROCK_LOW
    return ROCK_SHADOW


def build_pinwheel_red():
    """pinwheel_red: a small paper pinwheel on a wooden pole (not a wind turbine): four curled blades,
    red and a paler red in turn, round a cream cap; the pole stands in a little heap of soil. The
    blades are their own child object (pinwheel_blades) so the game can turn them alone."""
    g = p3.Geo()
    # The pole and its foot.
    g.box((-0.022, -0.022, 0.0), (0.022, 0.022, PIN_HEIGHT + 0.03), p3.SIGN_MID, top=p3.SIGN_LIGHT)
    g.mb.slab([(0.07 * math.cos(a), 0.06 * math.sin(a)) for a in [i * math.pi / 4 for i in range(8)]], 0.0, 0.03, SOIL, SOIL_RIM)
    # The pin through the pole (the blades turn on it).
    g.box((-0.012, PIN_FRONT + 0.005, PIN_HEIGHT - 0.012), (0.012, 0.03, PIN_HEIGHT + 0.012), CAP_RIM)
    pole = g.obj("pinwheel_red")

    b = hl.MeshBuilder()
    for i in range(4):
        theta = i * math.pi / 2 + math.radians(20)
        tip = (BLADE_RADIUS * math.cos(theta), BLADE_RADIUS * math.sin(theta))
        inner = (BLADE_RADIUS * 0.66 * math.cos(theta + math.radians(52)), BLADE_RADIUS * 0.66 * math.sin(theta + math.radians(52)))
        hub = (0.025 * math.cos(theta - math.radians(60)), 0.025 * math.sin(theta - math.radians(60)))
        front = RED if i % 2 == 0 else BLADE_LIGHT
        verts = set(b.bm.verts)
        # A thin sail: front colour on its face, the paper's pale back and red edges.
        b.slab([hub, tip, inner, (0.0, 0.0)], 0.0, 0.006, front, RED_SHADE)
        # The curl: the tip's side bends forward (South) a little.
        for vert in b.bm.verts:
            if vert not in verts:
                d = math.hypot(vert.co.x, vert.co.y)
                vert.co.z += 0.03 * (d / BLADE_RADIUS) ** 2
        # From the blades' plane (XY, facing +Z) to the pinwheel's (XZ, facing South = -Y).
        # (+90 degrees about X: the slab's top, +Z, turns to -Y: the coloured face looks South)
        p2._rotate_new(b, verts, (0.0, 0.0, 0.0), math.radians(90), "X")
    # The back of the paper: under each sail's coloured face, a pale face just North of it.
    b.bm.normal_update()
    for face in b.bm.faces:
        if face.normal.y > 0.5:
            rgba = hl.hex_color(BLADE_BACK)
            for loop in face.loops:
                loop[b.layer] = rgba
    # The cap on the pin.
    before = set(b.bm.verts)
    b.cylinder((0.0, 0.0, 0.0), 0.03, 0.022, 0.03, 8, CAP)
    p2._rotate_new(b, before, (0.0, 0.0, 0.0), math.radians(90), "X")   # along -Y: in front of the blades
    blades = b.to_object("pinwheel_blades")
    blades.parent = pole
    blades.location = (0.0, PIN_FRONT, PIN_HEIGHT)
    hl.add_cell_guides("pinwheel_red", [(0, 0)])
    return [pole, blades], [blades], {"pinwheel_red": ["new design for ver3.8 (the hill's examine spot; its blades turn)"]}


def build_rock_red():
    """rock_red: a chunky red-brown boulder, about knee high, flat-topped and cracked into facets."""
    b = hl.MeshBuilder()
    b.jitter_blob((0.0, 0.01, 0.1), (0.22, 0.19, 0.15), 1, 811, 0.035, rock_colour)
    b.jitter_blob((0.12, -0.08, 0.05), (0.1, 0.08, 0.07), 1, 812, 0.015, rock_colour)
    # cut flat on top (a slab of rock, not a round pebble): the top facets lit
    for vert in b.bm.verts:
        vert.co.z = min(max(0.0, vert.co.z), 0.2 + 0.04 * vert.co.x - 0.03 * vert.co.y)
    b.bm.normal_update()
    obj = b.to_object("rock_red")
    hl.add_cell_guides("rock_red", [(0, 0)])
    return [obj], [], {"rock_red": ["new design for ver3.8"]}


def build_rock_ledge():
    """rock_ledge: a low ridge of red-brown rock as wide as a cell (pieces side by side make the hill's
    rocky rim): three flat, broken slabs of uneven height leaning together, a little moss on top. (A
    first version, a box with a turf lid, looked like a garden bed wall.)"""
    b = hl.MeshBuilder()
    for i, (x, y, rx, ry, h) in enumerate(((-0.17, 0.02, 0.15, 0.2, 0.2), (0.0, -0.02, 0.16, 0.21, 0.27), (0.17, 0.03, 0.15, 0.19, 0.17))):
        b.jitter_blob((x, y, h * 0.45), (rx, ry, h * 0.62), 1, 821 + i, 0.03, rock_colour)
    for vert in b.bm.verts:
        vert.co.z = max(0.0, vert.co.z)
    b.bm.normal_update()
    for i, (x, y) in enumerate(((-0.12, 0.1), (0.06, 0.08))):
        b.slab([(x + 0.05 * math.cos(a), y + 0.04 * math.sin(a)) for a in [k * math.pi / 3 for k in range(6)]],
               0.21 + 0.04 * i, 0.225 + 0.04 * i, TURF_RIM, TURF_RIM)
    obj = b.to_object("rock_ledge")
    hl.add_cell_guides("rock_ledge", [(0, 0)])
    return [obj], [], {"rock_ledge": ["new design for ver3.8 (the hill's edge)"]}


def build_rock_small():
    """rock_small: four small red-brown stones lying in the grass. Walkable (decoration only)."""
    b = hl.MeshBuilder()
    for i, (x, y, r) in enumerate(((-0.12, 0.08, 0.05), (0.08, 0.12, 0.04), (0.11, -0.09, 0.055), (-0.06, -0.12, 0.035))):
        b.jitter_blob((x, y, r * 0.5), (r, r * 0.85, r * 0.6), 1, 830 + i, 0.008, rock_colour)
    for vert in b.bm.verts:
        vert.co.z = max(0.0, vert.co.z)
    b.bm.normal_update()
    obj = b.to_object("rock_small")
    hl.add_cell_guides("rock_small", [(0, 0)])
    return [obj], [], {"rock_small": ["new design for ver3.8 (walkable)"]}


def build_grass_short():
    """grass_short: a low clump of short, sun-dried grass blades bending one way (the wind on the
    hill). Walkable (decoration only)."""
    b = hl.MeshBuilder()
    blades = ((-0.09, 0.04, 0.12), (-0.05, -0.02, 0.15), (0.0, 0.05, 0.13), (0.04, -0.03, 0.16), (0.09, 0.02, 0.11),
              (-0.02, -0.08, 0.1), (0.07, 0.08, 0.1), (-0.11, -0.06, 0.09), (0.12, -0.07, 0.1))
    for i, (x, y, height) in enumerate(blades):
        b.blade((x, y), 0.03, height, 0.05, math.radians(-15 + i * 7), GRASS_DARK, GRASS_LIGHT)
    obj = b.to_object("grass_short", recalc_normals=False)
    hl.add_cell_guides("grass_short", [(0, 0)])
    return [obj], [], {"grass_short": ["new design for ver3.8 (walkable)"]}


def build_ground_accent():
    """ground_accent: a flat, uneven patch of reddish soil on the ground (the hill's bare top). Walkable;
    no grass grows on it."""
    b = hl.MeshBuilder()
    outline = []
    for i in range(12):
        a = i * math.pi * 2 / 12
        r = 0.21 + (0.03 if i % 3 == 0 else -0.02 if i % 3 == 1 else 0.0)
        outline.append((r * math.cos(a), r * 0.92 * math.sin(a)))
    b.slab(outline, 0.002, 0.006, SOIL, SOIL_RIM)
    for x, y, r in ((0.07, 0.04, 0.018), (-0.09, -0.05, 0.014), (0.02, -0.11, 0.012)):
        b.slab([(x + r * math.cos(a), y + r * math.sin(a)) for a in [k * math.pi / 3 for k in range(6)]], 0.006, 0.014,
               ROCK_TOP, ROCK_SIDE)
    obj = b.to_object("ground_accent", recalc_normals=False)
    hl.add_cell_guides("ground_accent", [(0, 0)])
    return [obj], [], {"ground_accent": ["new design for ver3.8 (walkable)"]}


def build_sign_red():
    """sign_red: the field's sign with a South-red top board and an arrow pointing South (down). Its
    message is its definition's (a fixed Object, like signpost_green; not "sign_south": the importer
    and WORLD EDITOR read the old id sign_south as sign_basic)."""
    g = p3.Geo()
    g.box((-0.03, -0.03, 0.0), (0.03, 0.03, 0.56), p3.SIGN_DARK, top=p3.SIGN_MID)
    g.box((-0.22, -0.065, 0.36), (0.22, -0.03, 0.47), RED_SHADE, top=RED, front=RED)
    g.box((-0.215, -0.065, 0.245), (0.215, -0.03, 0.355), p3.SIGN_MID, front=p3.SIGN_MID)
    for z in (0.355, 0.245):
        g.box((-0.215, -0.07, z - 0.006), (0.215, -0.064, z + 0.006), p3.SIGN_DARK)
    # An arrow pointing South (down) on the red board: a direction sign, colour AND arrow.
    y = -0.068
    g.face([(-0.012, y, 0.4), (0.012, y, 0.4), (0.012, y, 0.455), (-0.012, y, 0.455)], "#f3f6fb", (0, -1, 0))
    g.face([(-0.035, y, 0.4), (0.035, y, 0.4), (0.0, y, 0.372)], "#f3f6fb", (0, -1, 0))
    sign = g.obj("sign_red")
    hl.add_cell_guides("sign_red", [(0, 0)])
    return [sign], [], {"sign_red": ["new design for ver3.8", "back of the board"]}


FAMILIES = (
    ("pinwheel_red", build_pinwheel_red),
    ("rock_red", build_rock_red),
    ("rock_ledge", build_rock_ledge),
    ("rock_small", build_rock_small),
    ("grass_short", build_grass_short),
    ("ground_accent", build_ground_accent),
    ("sign_red", build_sign_red),
)


def main():
    for folder in (SOURCES, EXPORTS, UNITY_MODELS):
        os.makedirs(folder, exist_ok=True)
    manifest = {
        "generator": "blender/scripts/build_v38_south.py",
        "blenderVersion": bpy.app.version_string,
        "units": "1 Blender unit = 1 Unity unit, 1 cell = 0.5, origin = ground centre of the root cell",
        "theme": {"source": "unity-project/Assets/Content/Maps/World/direction_themes.json", "south": [RED, RED_SHADE]},
        "approval": "pending: new ver3.8 designs, not approved by the user yet",
        "models": [],
    }
    for family, build in FAMILIES:
        hl.reset_scene()
        objects, children, supplemented = build()
        bpy.ops.wm.save_as_mainfile(filepath=os.path.join(SOURCES, family + ".blend"), compress=False)
        for obj in [o for o in objects if o.parent is None]:
            fbx = os.path.join(UNITY_MODELS, obj.name + ".fbx")
            hl.export_fbx(obj, fbx, children)
            lo, hi = p3.world_bounds([obj] + list(children))
            manifest["models"].append({
                "family": family, "name": obj.name, "blend": f"blender/sources/v38/{family}.blend",
                "fbx": os.path.relpath(fbx, ROOT).replace("\\", "/"),
                "triangles": hl.triangle_count(obj) + sum(hl.triangle_count(c) for c in children),
                "vertices": len(obj.data.vertices) + sum(len(c.data.vertices) for c in children),
                "materials": hl.material_count(obj),
                "boundsMin": lo, "boundsMax": hi,
                "children": {c.name: {"origin": [round(v, 4) for v in c.location]} for c in children},
                "supplemented": supplemented.get(obj.name, []),
            })
            print(f"HARUKA_V38_ASSET {obj.name}: {manifest['models'][-1]['triangles']} tris, bounds {lo} {hi}")
    with open(os.path.join(EXPORTS, "v38_assets_manifest.json"), "w", encoding="utf-8") as f:
        json.dump(manifest, f, indent=2, ensure_ascii=False)
        f.write("\n")


if __name__ == "__main__":
    main()
