"""はるかわーるど ver3.0 RC1: the sofa in ﾊﾙｶﾁｬﾝの家, with the yellow cushion on it.

Run from the repository root:
  "C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" --background --factory-startup \
      --python blender/scripts/build_phase5_sofa.py

Same conventions as build_phase3_buildings.py (whose geometry helpers are reused): +X East,
+Y North, +Z Up, 1 cell = 0.5, origin = ground centre of the ROOT (left) cell, the front faces
South. sofa_basic is 2 x 1 cells like the bench, with SeatAnchor_L / SeatAnchor_R over both
cells on the seat surface; the 2D game's bench_sit pose sits on it (catalog poseKey bench_sit).
Colors follow Assets/Content/World/sofa.png; the cushion keeps cushion.png's colors and shape.

Outputs:
  blender/sources/phase5/sofa_basic.blend
  unity-project/Assets/POC2D3D/Models/Phase5/sofa_basic.fbx
  blender/exports/phase5_assets_manifest.json
"""

import json
import math
import os
import sys

import bpy

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import halka_lowpoly as hl  # noqa: E402
import build_phase3_buildings as p3  # noqa: E402

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
SOURCES = os.path.join(ROOT, "blender", "sources", "phase5")
EXPORTS = os.path.join(ROOT, "blender", "exports")
UNITY_MODELS = os.path.join(ROOT, "unity-project", "Assets", "POC2D3D", "Models", "Phase5")

# sofa.png
SOFA_LIGHT, SOFA_MID, SOFA_DARK, SOFA_SHADE, SOFA_LEG = "#8b9cbc", "#6e7fa2", "#526080", "#3c4662", "#342622"
SEAT_TOP = 0.22  # the seat surface: SeatAnchor height


def build_sofa():
    g = p3.Geo()
    # legs
    for x in (-0.19, 0.64):
        for y in (-0.17, 0.14):
            g.box((x, y, 0.0), (x + 0.05, y + 0.04, 0.05), SOFA_LEG)
    # base under the seat
    g.box((-0.17, -0.19, 0.05), (0.67, 0.12, 0.17), SOFA_DARK, top=SOFA_MID, front=SOFA_SHADE)
    # two seat cushions
    for x0, x1 in ((-0.165, 0.245), (0.255, 0.665)):
        g.box((x0, -0.2, 0.17), (x1, 0.1, SEAT_TOP), SOFA_MID, top=SOFA_LIGHT, front=SOFA_DARK)
    # backrest, two back cushions
    g.box((-0.17, 0.1, 0.05), (0.67, 0.2, 0.3), SOFA_DARK, top=SOFA_MID)
    for x0, x1 in ((-0.165, 0.245), (0.255, 0.665)):
        g.box((x0, 0.06, 0.22), (x1, 0.16, 0.48), SOFA_MID, top=SOFA_LIGHT, front=SOFA_MID)
    # arms
    for x0 in (-0.23, 0.63):
        g.box((x0, -0.2, 0.05), (x0 + 0.1, 0.2, 0.33), SOFA_DARK, top=SOFA_LIGHT, front=SOFA_SHADE)

    # The cushion (cushion.png's shape and colors, 0.75 size) leaning on the right back cushion.
    tilt = math.radians(62)  # from lying flat toward standing against the back

    def place(p):
        x, y, z = p
        # rotate about the X axis (top edge falls back toward North), then move onto the seat
        yr = y * math.cos(tilt) - z * math.sin(tilt)
        zr = y * math.sin(tilt) + z * math.cos(tilt)
        return (x + 0.5, yr + 0.035, zr + SEAT_TOP + 0.1)

    def ring(r, corner, z):
        pts = []
        for i in range(12):
            a = (i + 0.5) / 12 * math.tau
            c, s = math.cos(a), math.sin(a)
            k = max(abs(c), abs(s))
            radius = r / k * (1.0 - corner) + r * corner
            pts.append(place((radius * c, radius * s, z)))
        return pts

    scale = 0.75
    g.loft([ring(0.15 * scale, 0.25, 0.0), ring(0.175 * scale, 0.1, 0.045 * scale), ring(0.14 * scale, 0.35, 0.1 * scale)],
           p3.CUSHION_TOP, p3.CUSHION_SIDE, p3.CUSHION_DARK)
    sofa = g.obj("sofa_basic")
    anchors = [p3.anchor("SeatAnchor_L", sofa, (0.0, -0.04, SEAT_TOP)), p3.anchor("SeatAnchor_R", sofa, (0.5, -0.04, SEAT_TOP))]
    hl.add_cell_guides("sofa_basic", [(0, 0), (1, 0)])
    return [sofa], anchors, {"sofa_basic": ["underside", "back of the backrest"]}


def main():
    for folder in (SOURCES, EXPORTS, UNITY_MODELS):
        os.makedirs(folder, exist_ok=True)
    hl.reset_scene()
    objects, anchors, supplemented = build_sofa()
    sofa = objects[0]
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(SOURCES, "sofa_basic.blend"), compress=False)
    fbx = os.path.join(UNITY_MODELS, "sofa_basic.fbx")
    hl.export_fbx(sofa, fbx, anchors)
    lo, hi = p3.world_bounds([sofa])
    manifest = {
        "generator": "blender/scripts/build_phase5_sofa.py",
        "blenderVersion": bpy.app.version_string,
        "units": "1 Blender unit = 1 Unity unit, 1 cell = 0.5, origin = ground centre of the root cell",
        "models": [{
            "name": "sofa_basic", "blend": "blender/sources/phase5/sofa_basic.blend",
            "fbx": os.path.relpath(fbx, ROOT).replace("\\", "/"),
            "triangles": hl.triangle_count(sofa), "vertices": len(sofa.data.vertices),
            "materials": hl.material_count(sofa), "boundsMin": lo, "boundsMax": hi,
            "anchors": {a.name: [round(v, 4) for v in a.location] for a in anchors},
            "supplemented": supplemented["sofa_basic"],
        }],
    }
    with open(os.path.join(EXPORTS, "phase5_assets_manifest.json"), "w", encoding="utf-8") as f:
        json.dump(manifest, f, indent=2, ensure_ascii=False)
        f.write("\n")
    print(f"HARUKA_PHASE5_ASSET sofa_basic: {manifest['models'][0]['triangles']} tris, bounds {lo} {hi}")


if __name__ == "__main__":
    main()
