"""はるかわーるど ver3.0 Phase 2: ground, road, grass and nature assets from code.

Run from the repository root:
  "C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" --background --factory-startup \
      --python blender/scripts/build_phase2_environment.py

Blender owns the shapes and colors (Source of Truth for assets). Where things go is owned
by Map JSON v4 and is assembled in Unity; nothing here contains a map coordinate.

Outputs per family:
  blender/sources/phase2/<family>.blend
  unity-project/Assets/POC2D3D/Models/Phase2/<model>.fbx
  blender/exports/phase2_assets_manifest.json

Colors follow the shipped 2D sprites (grass.png, dirt.png, tree.png, flower.png, stone.png)
and the ground tint (0.85, 0.91, 0.72) the 2D runtime applies to the base surface.
"""

import json
import math
import os
import sys

import bmesh
import bpy
from mathutils import Matrix, Vector

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import halka_lowpoly as hl  # noqa: E402

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
SOURCES = os.path.join(ROOT, "blender", "sources", "phase2")
EXPORTS = os.path.join(ROOT, "blender", "exports")
UNITY_MODELS = os.path.join(ROOT, "unity-project", "Assets", "POC2D3D", "Models", "Phase2")
HALF = hl.CELL * 0.5
DIRT_TOP = 0.004  # Road surface sits 4 mm above the grass so the two never z-fight.
EDGE_INSET = 0.04  # Every road edge meets the cell border at the same inset (seamless joins).


# ---------------------------------------------------------------- ground

def build_ground_grass():
    """Three near-identical square tiles; Unity picks one per cell from a fixed hash."""
    models = []
    for suffix, color in (("a", "#d9e8b8"), ("b", "#d5e5b2"), ("c", "#dcebbe")):
        b = hl.MeshBuilder()
        b.flat_polygon([(-HALF, -HALF), (HALF, -HALF), (HALF, HALF), (-HALF, HALF)], 0.0, color)
        models.append(b.to_object("ground_grass_" + suffix, recalc_normals=False))
    return models


def _road_shades(builder, before, shades):
    """Alternate a few close browns per triangle, like the noise in dirt.png."""
    index = 0
    for face in builder.bm.faces:
        if face in before:
            continue
        rgba = hl.hex_color(shades[index % len(shades)])
        for loop in face.loops:
            loop[builder.layer] = rgba
        index += 1


def build_ground_dirt():
    """Quarter-cell road pieces for the north-east quadrant of a road cell (cell center at 0).
    Unity rotates or mirrors them per quadrant from the four neighbouring Surface cells:
      full   : both neighbours on this corner are road
      edge   : the East neighbour is grass (wavy edge along +X)
      corner : both neighbours are grass (rounded outer corner)"""
    shades = ("#93663f", "#8e6141", "#966942", "#8b5f3f")
    q = HALF
    inner = q - EDGE_INSET
    wave = [(t, inner - 0.018 * math.sin(2.0 * math.pi * t / q)) for t in (0.0, q * 0.25, q * 0.5, q * 0.75, q)]
    outlines = {
        "full": [(0.0, 0.0), (q, 0.0), (q, q), (0.0, q)],
        "edge": [(0.0, 0.0)] + [(x, t) for t, x in wave] + [(0.0, q)],
        "corner": [(0.0, 0.0)] + [((r * math.cos(math.radians(a))), (r * math.sin(math.radians(a))))
                                   for a, r in ((0, inner), (22.5, inner - 0.016), (45, inner - 0.036),
                                                (67.5, inner - 0.02), (90, inner))],
    }
    models = []
    for name, outline in outlines.items():
        b = hl.MeshBuilder()
        before = b._snapshot()
        b.flat_polygon(outline, DIRT_TOP, shades[0])
        _road_shades(b, before, shades)
        models.append(b.to_object("ground_dirt_" + name, recalc_normals=False))
    return models


# ---------------------------------------------------------------- grass tufts

def build_grass_tufts():
    """Small fans of blades in the grass.png greens; three shapes, colliders never."""
    dark, light, pale = "#86a27c", "#b5ce8f", "#d4e6a8"
    layouts = {
        "a": [(-0.06, 0.0, 0.12, -25), (-0.025, 0.01, 0.16, -10), (0.0, -0.01, 0.18, 0),
              (0.03, 0.01, 0.15, 12), (0.065, 0.0, 0.11, 28)],
        "b": [(-0.05, 0.02, 0.1, -30), (-0.02, -0.01, 0.14, -12), (0.015, 0.0, 0.15, 8),
              (0.05, 0.015, 0.11, 30)],
        "c": [(-0.07, -0.01, 0.1, -32), (-0.04, 0.015, 0.13, -18), (-0.01, -0.005, 0.16, -4),
              (0.02, 0.012, 0.14, 10), (0.05, -0.008, 0.12, 22), (0.075, 0.01, 0.09, 36)],
    }
    models = []
    for suffix, blades in layouts.items():
        b = hl.MeshBuilder()
        for i, (x, y, h, lean_deg) in enumerate(blades):
            y = y * 2.0 + (0.03 if i % 2 else -0.025)  # A shallow cluster, not a flat fan.
            yaw = math.radians(lean_deg * 0.6 + (35 if i % 2 else -30))
            lean = math.sin(math.radians(lean_deg)) * h * 0.7
            b.blade((x, y), 0.055, h * 1.1, 0.0, yaw, dark, pale if i % 2 == 0 else light)
            # Lean outward in X by moving the tip just created.
            b.bm.verts.ensure_lookup_table()
            tip = b.bm.verts[len(b.bm.verts) - 1]
            tip.co.x += lean
        models.append(b.to_object("grass_tuft_" + suffix, recalc_normals=False))
    return models


# ---------------------------------------------------------------- tree

def _cylinder_between(b, p0, p1, r0, r1, segments, color):
    before = b._snapshot()
    p0, p1 = Vector(p0), Vector(p1)
    axis = p1 - p0
    rotation = Vector((0, 0, 1)).rotation_difference(axis.normalized()).to_matrix().to_4x4()
    matrix = Matrix.Translation((p0 + p1) * 0.5) @ rotation
    bmesh.ops.create_cone(b.bm, cap_ends=True, cap_tris=False, segments=segments,
                          radius1=r0, radius2=r1, depth=axis.length, matrix=matrix)
    b._paint_new(before, color)


def _shade_by_normal(b, before, light, dark, axis=Vector((-0.4, -0.5, 0.75))):
    axis = axis.normalized()
    b.bm.normal_update()
    for face in b.bm.faces:
        if face in before:
            continue
        rgba = hl.hex_color(light if face.normal.dot(axis) > 0.15 else dark)
        for loop in face.loops:
            loop[b.layer] = rgba


def build_tree():
    """tree_basic: one blocked root cell, a crown about three cells wide (like tree.png)."""
    b = hl.MeshBuilder()
    bark_light, bark_dark = "#a8735a", "#79504d"
    before = b._snapshot()
    b.cylinder((0, 0, 0), 0.2, 0.12, 0.12, 7, bark_dark)
    b.cylinder((0, 0, 0.08), 0.12, 0.085, 0.9, 7, bark_light, rotation=hl.deg(8))
    for angle in (20, 110, 200, 290):
        a = math.radians(angle)
        _cylinder_between(b, (0.05 * math.cos(a), 0.05 * math.sin(a), 0.02),
                          (0.2 * math.cos(a), 0.2 * math.sin(a), -0.01), 0.05, 0.03, 5, bark_dark)
    _cylinder_between(b, (0.0, 0.0, 0.6), (-0.24, -0.04, 0.9), 0.05, 0.03, 5, bark_light)
    _cylinder_between(b, (0.02, 0.0, 0.66), (0.26, 0.02, 0.92), 0.045, 0.028, 5, bark_light)
    _shade_by_normal(b, before, bark_light, bark_dark)

    def crown_color(normal, center):
        if normal.z > 0.8:
            return "#b3d27f"
        if normal.z > 0.25:
            return "#93bd72"
        if normal.z > -0.3:
            return "#6a9a6c"
        return "#4f7d63"

    # The crown sits high enough that the trunk shows below it in the 2D view, like tree.png.
    clumps = [((0.0, 0.02, 1.22), (0.46, 0.42, 0.38)), ((-0.42, -0.02, 1.02), (0.3, 0.3, 0.27)),
              ((0.43, 0.0, 1.04), (0.31, 0.3, 0.28)), ((-0.2, -0.2, 0.96), (0.28, 0.26, 0.24)),
              ((0.22, -0.18, 0.98), (0.28, 0.26, 0.24)), ((0.0, 0.33, 1.08), (0.3, 0.28, 0.27)),
              ((-0.06, 0.04, 1.5), (0.29, 0.28, 0.25)), ((-0.3, 0.22, 1.28), (0.25, 0.24, 0.22)),
              ((0.3, 0.2, 1.3), (0.25, 0.24, 0.22))]
    for i, (center, radii) in enumerate(clumps):
        b.jitter_blob(center, radii, 2 if i < 3 else 1, 100 + i, 0.025, crown_color)
    tree = b.to_object("tree_basic")
    anchor = hl.add_anchor("PerchAnchor", tree, (-0.17, 0.0, 1.7))
    return [tree], [anchor]


# ---------------------------------------------------------------- flower

def _rotate_new(b, before_verts, pivot, angle, axis):
    rotation = Matrix.Rotation(angle, 4, axis)
    pivot = Vector(pivot)
    for vert in b.bm.verts:
        if vert not in before_verts:
            vert.co = pivot + rotation @ (vert.co - pivot)


def build_flower():
    """flower_basic: a white five-petal flower with a yellow centre and two leaves.
    Petals are thin slabs whose brown rims stand in for the sprite outline."""
    b = hl.MeshBuilder()
    _cylinder_between(b, (0.0, 0.0, 0.0), (0.0, -0.01, 0.27), 0.014, 0.011, 5, "#49a125")
    for side, z in ((-1, 0.07), (1, 0.11)):
        leaf = [(0.0, 0.0), (side * 0.06, -0.025), (side * 0.13, 0.0), (side * 0.06, 0.028)]
        b.slab([(x, y) for x, y in (leaf if side > 0 else list(reversed(leaf)))], z, z + 0.012,
               "#94cd34", "#2f7a1a")
    head = set(b.bm.verts)
    center = (0.0, -0.01, 0.28)
    for i in range(5):
        a = math.radians(90 + i * 72)
        c, s = math.cos(a), math.sin(a)
        petal = []
        for px, py in ((0.02, 0.0), (0.055, -0.034), (0.1, -0.03), (0.125, 0.0), (0.1, 0.03), (0.055, 0.034)):
            petal.append((center[0] + px * c - py * s, center[1] + px * s + py * c))
        b.slab(petal, center[2] - 0.009, center[2] + 0.009, "#fbf6ea", "#8c7656")
    disc = [(center[0] + 0.045 * math.cos(math.radians(a)), center[1] + 0.045 * math.sin(math.radians(a)))
            for a in range(0, 360, 45)]
    b.slab(disc, center[2] + 0.002, center[2] + 0.022, "#f6bc03", "#a66002")
    # Tilt the head toward the South so the 2D camera and the 3D camera both see the face.
    _rotate_new(b, head, center, math.radians(28), "X")
    return [b.to_object("flower_basic", recalc_normals=False)]


# ---------------------------------------------------------------- stone

def build_stones():
    """stone_basic: three rounded grey shapes; the instance ID picks one in Unity."""
    def stone_color(normal, center):
        if normal.z > 0.65:
            return "#c4c1c7"
        if normal.z > 0.2:
            return "#aeabb4"
        if normal.z > -0.25:
            return "#8a889a"
        return "#5c5e77"

    models = []
    for i, (suffix, radii) in enumerate((("a", (0.21, 0.16, 0.15)), ("b", (0.18, 0.17, 0.16)),
                                         ("c", (0.23, 0.14, 0.12)))):
        b = hl.MeshBuilder()
        b.jitter_blob((0.0, 0.0, radii[2] * 0.85), radii, 2, 300 + i, 0.022, stone_color)
        for vert in b.bm.verts:
            vert.co.z = max(0.0, vert.co.z)
        b.bm.normal_update()
        models.append(b.to_object("stone_basic_" + suffix))
    return models


FAMILIES = (
    ("ground_grass", build_ground_grass),
    ("ground_dirt", build_ground_dirt),
    ("grass_tuft", build_grass_tufts),
    ("tree_basic", build_tree),
    ("flower_basic", build_flower),
    ("stone_basic", build_stones),
)


def main():
    for folder in (SOURCES, EXPORTS, UNITY_MODELS):
        os.makedirs(folder, exist_ok=True)
    manifest = {
        "generator": "blender/scripts/build_phase2_environment.py",
        "blenderVersion": bpy.app.version_string,
        "units": "1 Blender unit = 1 Unity unit, 1 cell = 0.5, origin = ground centre of the root cell",
        "export": {"format": "FBX 7.4 binary", "axisForward": "-Z", "axisUp": "Y", "bakeSpaceTransform": True,
                   "colors": "SRGB face-corner attribute 'Col'", "material": "HALKA_VertexColor (1 slot)"},
        "dirtTopHeight": DIRT_TOP,
        "dirtEdgeInset": EDGE_INSET,
        "models": [],
    }
    for family, build in FAMILIES:
        hl.reset_scene()
        result = build()
        objects, anchors = result if isinstance(result, tuple) else (result, [])
        spacing = 0.0
        for obj in objects:
            obj.location.x = spacing  # Spread variants in the .blend only for editing.
            spacing += 0.8
        bpy.ops.wm.save_as_mainfile(filepath=os.path.join(SOURCES, family + ".blend"), compress=False)
        for obj in objects:
            obj.location.x = 0.0
            fbx = os.path.join(UNITY_MODELS, obj.name + ".fbx")
            own = [a for a in anchors if a.parent == obj]
            hl.export_fbx(obj, fbx, own)
            xs, ys, zs = zip(*[tuple(round(v, 4) for v in corner) for corner in obj.bound_box])
            manifest["models"].append({
                "family": family, "name": obj.name,
                "blend": f"blender/sources/phase2/{family}.blend",
                "fbx": os.path.relpath(fbx, ROOT).replace("\\", "/"),
                "triangles": hl.triangle_count(obj), "vertices": len(obj.data.vertices),
                "materials": hl.material_count(obj),
                "boundsMin": [min(xs), min(ys), min(zs)], "boundsMax": [max(xs), max(ys), max(zs)],
                "anchors": {a.name: [round(v, 4) for v in a.location] for a in own},
            })
            print(f"HARUKA_PHASE2_ASSET {obj.name}: {hl.triangle_count(obj)} tris")
    with open(os.path.join(EXPORTS, "phase2_assets_manifest.json"), "w", encoding="utf-8") as f:
        json.dump(manifest, f, indent=2, ensure_ascii=False)
        f.write("\n")


if __name__ == "__main__":
    main()
