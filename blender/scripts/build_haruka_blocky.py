"""はるかわーるど ver3.0 Phase 2.1: a TEMPORARY blocky (Minecraft-like) ﾊﾙｶﾁｬﾝ for the 3D view.

Not the official character model. Boxes like a Minecraft character, each face painted pixel by
pixel (vertex colors, one shared material) after the shipped 2D sprites
Assets/Content/Character/{front,back,left,right}_idle/00.png (read only). A second, slightly
larger "hair layer" box around the head carries the bangs, side hair and the blue under-layer,
like a Minecraft skin's hat layer.

  blender.exe --background --factory-startup --python blender/scripts/build_haruka_blocky.py

Outputs:
  blender/sources/phase2_1/haruka_blocky_placeholder.blend
  unity-project/Assets/POC2D3D/Models/Phase2_1/haruka_blocky_placeholder.fbx
  blender/exports/phase2_1_haruka_blocky_placeholder_manifest.json

Hierarchy (pivots at the joints so Unity can swing them; no Rig, no Gameplay dependency):
  haruka_blocky_placeholder  (head, hair layer, body, skirt, ornaments; origin = ground centre)
    ArmL / ArmR              (shoulder pivots; L = the character's left = +X while facing South)
    LegL / LegR              (hip pivots)
Front faces South (Blender -Y). Units below are "pixels" of PIXEL metres. The black ornaments sit
on the ponytail's hair tie at the back of the head (user feedback, 2026-10-15).
"""

import json
import os
import sys

import bpy
from mathutils import Vector

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import halka_lowpoly as hl  # noqa: E402

NAME = "haruka_blocky_placeholder"
ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
SOURCE = os.path.join(ROOT, "blender", "sources", "phase2_1", NAME + ".blend")
FBX = os.path.join(ROOT, "unity-project", "Assets", "POC2D3D", "Models", "Phase2_1", NAME + ".fbx")
MANIFEST = os.path.join(ROOT, "blender", "exports", f"phase2_1_{NAME}_manifest.json")

PIXEL = 0.044  # metres per pixel: 22 px tall with the ornaments = 0.97 (about two cells)

# Sprite palette (sampled from the idle sprites).
PALETTE = {
    "Y": "#f1e2bc",  # hair
    "y": "#c8baa0",  # hair shade
    "B": "#4b7ae8",  # blue (hair under-layer, eyes, trims)
    "b": "#3c5fbe",  # dark blue
    "K": "#12121e",  # ink: lids, the clip's "H", boots
    "D": "#24242e",  # ornaments, skirt
    "J": "#3a3a40",  # jacket
    "j": "#5f5a5c",  # jacket light
    "S": "#f9f6f4",  # skin / white legs
    "P": "#fe1b6d",  # blush under the eyes (also used by the checks to find the face)
    "W": "#ffffff",  # hair clip (also used by the checks to find the clip)
}


class Painter:
    """Pixel-painted faces on one bmesh; '.' leaves a pixel out (see-through hair layer)."""

    def __init__(self):
        self.builder = hl.MeshBuilder()
        self.bm = self.builder.bm
        self.layer = self.builder.layer

    def quad(self, p0, u, v, color):
        verts = [self.bm.verts.new(p) for p in (p0, p0 + u, p0 + u + v, p0 + v)]
        face = self.bm.faces.new(verts)
        rgba = hl.hex_color(PALETTE[color])
        for loop in face.loops:
            loop[self.layer] = rgba

    def face(self, origin, u_axis, v_axis, rows, pixel_u=1.0, pixel_v=1.0):
        """rows[0] is the top row seen from outside; u = right, v = up (normal = u x v)."""
        height = len(rows)
        for r, row in enumerate(rows):
            c = 0
            while c < len(row):
                ch = row[c]
                run = 1
                while c + run < len(row) and row[c + run] == ch:
                    run += 1
                if ch != ".":
                    p0 = origin + u_axis * (c * pixel_u) + v_axis * ((height - 1 - r) * pixel_v)
                    self.quad(p0 * PIXEL, u_axis * (run * pixel_u * PIXEL), v_axis * (pixel_v * PIXEL), ch)
                c += run

    def box(self, lo, hi, faces):
        """Axis box in pixel units. faces: front/back/left/right/top/bottom -> rows or one letter.
        'right' is the character's right (-X), 'left' the character's left (+X)."""
        x0, y0, z0 = lo
        x1, y1, z1 = hi
        sx, sy, sz = x1 - x0, y1 - y0, z1 - z0
        X, Y, Z = Vector((1, 0, 0)), Vector((0, 1, 0)), Vector((0, 0, 1))

        def rows_for(key, width, height):
            value = faces.get(key)
            if value is None:
                return None
            if isinstance(value, str):
                return [value * max(1, int(round(width)))] * max(1, int(round(height)))
            return value

        specs = {
            "front": (Vector((x0, y0, z0)), X, Z, sx, sz),
            "back": (Vector((x1, y1, z0)), -X, Z, sx, sz),
            "left": (Vector((x1, y0, z0)), Y, Z, sy, sz),
            "right": (Vector((x0, y1, z0)), -Y, Z, sy, sz),
            "top": (Vector((x0, y0, z1)), X, Y, sx, sy),
            "bottom": (Vector((x0, y1, z0)), X, -Y, sx, sy),
        }
        for key, (origin, u, v, width, height) in specs.items():
            rows = rows_for(key, width, height)
            if rows is None:
                continue
            self.face(origin, u, v, rows, width / len(rows[0]), height / len(rows))

    def to_object(self, name):
        return self.builder.to_object(name, recalc_normals=False)


# ---------------------------------------------------------------- head and hair

HEAD_FRONT = [  # 8 x 8, column 0 = the character's right (-X)
    "YYYYYYYY",
    "YYYYYYYY",
    "YYYYYYYY",
    "SSSSSSSS",
    "SKKSSKKS",
    "SBWSSBWS",
    "SBBSSBBS",
    "SPSSSSPS",
]
HEAD_SIDE_RIGHT = [  # seen from -X: column 0 = back, column 7 = front
    "YYYYYYYY", "YYYYYYYY", "YYYYYYYY", "YYYYYYYY",
    "YYYYYYYY", "YYYYYYSS", "YYYYYSSS", "YYYYYSSS",
]
HEAD_SIDE_LEFT = [row[::-1] for row in HEAD_SIDE_RIGHT]  # seen from +X: column 0 = front

HAIR_FRONT = [  # 9 x 10 hair layer, transparent over the face
    "YYYYYYYYY",
    "YYYYYYYYY",
    "YYYYyYYYY",
    "YyYY.YYyY",
    "Y...y...Y",
    "Y.......Y",
    "b.......b",
    "B.......B",
    "B.......B",
    "b.......b",
]
HAIR_SIDE_RIGHT = [  # seen from -X: column 0 = back, column 8 = front
    "YYYYYYYYY",
    "YYYYYYYYY",
    "YYYYYYYYY",
    "YYYYYYYYY",
    "YYYYYYYYy",
    "BBYYYYYYy",
    "BBBYYYYY.",
    "BBBBBYY..",
    "bBBBBb...",
    "bbbb.....",
]
HAIR_SIDE_LEFT = [row[::-1] for row in HAIR_SIDE_RIGHT]
HAIR_BACK = [  # seen from +Y: blond above, the blue under-layer below
    "YYYYYYYYY",
    "YYYYYYYYY",
    "YYYYYYYYY",
    "YYYYYYYYY",
    "YYYYYYYYY",
    "YYYYYYYYY",
    "BBBBBBBBB",
    "BBBBBBBBB",
    "bBBBBBBBb",
    "bbbbbbbbb",
]
HAIR_TOP = "Y"

BODY_FRONT = [  # 8 x 6
    "JJbBBbJJ",
    "JJJWBJJJ",
    "JJJBWJJJ",
    "JJJJJJJJ",
    "JjJJJJjJ",
    "JJJJJJJJ",
]
BODY_BACK = ["JJbbbbJJ", "JJJJJJJJ", "JJJJJJJJ", "JjJJJJjJ", "JJJJJJJJ", "JJJJJJJJ"]
SKIRT_FRONT = ["DDDDDDDDD", "DJDJDJDJD", "bBbBbBbBb"]
SKIRT_SIDE = ["DDDDD", "DJDJD", "bBbBb"]


def build_body():
    p = Painter()
    # Body z 5..11 (the skirt covers its lower part and the top of the legs).
    p.box((-4, -2, 5), (4, 2, 11), {"front": BODY_FRONT, "back": BODY_BACK, "left": "J", "right": "J",
                                    "top": "J", "bottom": "J"})
    p.box((-4.5, -2.5, 4.5), (4.5, 2.5, 7.5), {"front": SKIRT_FRONT, "back": SKIRT_FRONT, "left": SKIRT_SIDE,
                                               "right": SKIRT_SIDE, "top": "D", "bottom": "D"})
    # Head 8 x 8 x 8 and the hair layer (open where the face shows, longer at the back).
    p.box((-4, -4, 11), (4, 4, 19), {"front": HEAD_FRONT, "back": "Y", "left": HEAD_SIDE_LEFT,
                                     "right": HEAD_SIDE_RIGHT, "top": "Y", "bottom": "S"})
    p.box((-4.5, -4.5, 9.5), (4.5, 4.5, 19.5), {"front": HAIR_FRONT, "back": HAIR_BACK, "left": HAIR_SIDE_LEFT,
                                                "right": HAIR_SIDE_RIGHT, "top": HAIR_TOP})
    # Ponytail at the back of the head: the hair tie, a short tuft below it, and the black
    # ornaments on the tie that rise up and outward behind the head (from the front they show
    # above both sides of the head, as in the sprite).
    solid = ("front", "back", "left", "right", "top", "bottom")
    p.box((-1, 4.5, 14), (1, 5.5, 17.5), {k: "Y" for k in solid})
    p.box((-1, 4.5, 17.5), (1, 5.8, 19), {k: "D" for k in solid})
    for side in (-1, 1):
        x_in, x_out = sorted((side * 1.0, side * 3.0))
        p.box((x_in, 4.5, 18.5), (x_out, 6.0, 20.5), {k: "D" for k in solid})
        x_in, x_out = sorted((side * 2.5, side * 4.5))
        p.box((x_in, 5.0, 20.5), (x_out, 6.5, 22), {k: "D" for k in solid})
    # White clip with a black "H" on the character's right of the forehead.
    p.box((-4.4, -4.9, 15.5), (-1.6, -4.5, 18.5), {"front": ["WKKW", "WWWW", "WKKW"],
                                                   "left": "K", "right": "K", "top": "K", "bottom": "K"})
    return p.to_object(NAME)


def build_arm(name, side):
    """2 x 6 x 3 around the shoulder pivot: black sleeve, blue cuff, white hand."""
    p = Painter()
    rows = ["JJ", "JJ", "JJ", "JJ", "bb", "SS"]
    side_rows = ["JJJ", "JJJ", "JJJ", "JJJ", "bbb", "SSS"]
    p.box((-1, -1.5, -5.5), (1, 1.5, 0.5), {"front": rows, "back": rows, "left": side_rows, "right": side_rows,
                                          "top": "J", "bottom": "S"})
    arm = p.to_object(name)
    arm.location = Vector((side * 5.0, 0.0, 10.5)) * PIXEL
    return arm


def build_leg(name, side):
    """3 x 5 x 3 around the hip pivot: white leg, black boot, blue sole."""
    p = Painter()
    rows = ["SSS", "SSS", "SSS", "KKK", "bbb"]
    p.box((-1.5, -1.5, -5), (1.5, 1.5, 0), {"front": rows, "back": rows, "left": rows, "right": rows,
                                          "top": "S", "bottom": "b"})
    leg = p.to_object(name)
    leg.location = Vector((side * 1.5, 0.0, 5.0)) * PIXEL
    return leg


def main():
    for path in (SOURCE, FBX, MANIFEST):
        os.makedirs(os.path.dirname(path), exist_ok=True)
    hl.reset_scene()
    body = build_body()
    parts = [build_arm("ArmL", 1), build_arm("ArmR", -1), build_leg("LegL", 1), build_leg("LegR", -1)]
    for part in parts:
        part.parent = body
    hl.add_cell_guides(NAME, [(0, 0)])
    bpy.ops.wm.save_as_mainfile(filepath=SOURCE, compress=False)
    hl.export_fbx(body, FBX, parts)
    corners = []
    for obj in [body] + parts:
        corners += [obj.matrix_world @ Vector(c) for c in obj.bound_box]
    xs, ys, zs = zip(*[tuple(c) for c in corners])
    manifest = {
        "generator": "blender/scripts/build_haruka_blocky.py",
        "status": "TEMPORARY placeholder (Minecraft-like blocks), not the official character model",
        "style": "boxes painted per pixel from the 2D sprites; head 8x8x8 + hair layer, body 8x6x4, "
                 "arms 2x6x3, legs 3x5x3 (pixels)",
        "pixelMetres": PIXEL,
        "blenderVersion": bpy.app.version_string,
        "blend": f"blender/sources/phase2_1/{NAME}.blend",
        "fbx": f"unity-project/Assets/POC2D3D/Models/Phase2_1/{NAME}.fbx",
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
    print("HARUKA_BLOCKY", manifest["triangles"], "tris", manifest["boundsMin"], manifest["boundsMax"])


if __name__ == "__main__":
    main()
