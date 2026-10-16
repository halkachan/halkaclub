"""はるかわーるど ver3.0 Phase 3: the house, outdoor fixtures, interior pieces and furniture.

Run from the repository root:
  "C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" --background --factory-startup \
      --python blender/scripts/build_phase3_buildings.py

Blender owns shapes, colors, pivots and anchors (Source of Truth for assets). Where things go
is owned by Map JSON v4 and the Object Catalog and is assembled in Unity; no map coordinate
appears here.

Conventions (shared with every earlier phase):
  Blender +X = East, +Y = North, +Z = Up, 1 unit = 1 Unity unit, 1 cell = 0.5.
  The origin is the ground centre of the Definition's ROOT cell. "bottom-left" Definitions
  (bed, desk, bench, easel, shelf) extend toward +X/+Y from it; "bottom-center" ones (house,
  mic) are centred on it. Fronts face South (-Y) like the 2D sprites.
  Colors are face-corner vertex colors ("Col"), one material slot (HALKA_VertexColor).

Colors were sampled from the shipped sprites in Assets/Content/World (read only). Parts that
the sprites do not show (the house's side and back walls, the back of furniture) are filled in
with the smallest plain design that reads from every side; they are listed per model in the
manifest under "supplemented" and are not decided designs.

Outputs:
  blender/sources/phase3/<family>.blend
  unity-project/Assets/POC2D3D/Models/Phase3/<model>.fbx
  blender/exports/phase3_assets_manifest.json
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
SOURCES = os.path.join(ROOT, "blender", "sources", "phase3")
EXPORTS = os.path.join(ROOT, "blender", "exports")
UNITY_MODELS = os.path.join(ROOT, "unity-project", "Assets", "POC2D3D", "Models", "Phase3")
HALF = hl.CELL * 0.5

# ---------------------------------------------------------------- palettes (from the sprites)

# house_exterior.png
WALL, WALL_SHADE = "#f2eee6", "#d6d2c8"
TIMBER, TIMBER_DARK = "#64646a", "#47474d"
ROOF_A, ROOF_B, ROOF_C, RIDGE = "#3c3c42", "#46464c", "#55555b", "#2c2c32"
DOOR, DOOR_DARK = "#4f4f55", "#2e2e34"
GOLD, GOLD_DARK = "#f6d35c", "#b08a2a"
STONE, STONE_LIGHT = "#6c6c72", "#86868c"
GLASS = "#9fb2c2"  # supplemented: the sprite shows no windows
# bench.png
BENCH_LIGHT, BENCH_MID, BENCH_DARK, BENCH_LEG = "#897266", "#715d53", "#4f413a", "#3a2e29"
# sign.png
SIGN_LIGHT, SIGN_MID, SIGN_DARK = "#94715f", "#816155", "#5e4742"
# well.png
WELL_A, WELL_B, WELL_C, WELL_TOP, WELL_IN, WELL_WATER = "#676181", "#8f8ca8", "#aba9c0", "#bfbcd5", "#363248", "#12121e"
# house_bed.png
BED_FRAME, BED_FRAME_LIGHT, BED_KNOB = "#2a282a", "#5a585c", "#6e6b6e"
SHEET, SHEET_SHADE = "#fcf9f5", "#dfdcdc"
BLANKET, BLANKET_SIDE, BLANKET_DARK = "#fdc217", "#eeaa01", "#d39200"
# desk.png
DESK_TOP, DESK_EDGE, DESK_LEG = "#563b2d", "#4d362b", "#31231e"
MONITOR, SCREEN, SCREEN_TEXT = "#19171a", "#272b32", "#48616d"
KEYS, KEYS_DARK, MOUSE = "#bec2bf", "#22252b", "#22252b"
# cushion.png
CUSHION_TOP, CUSHION_SIDE, CUSHION_DARK, CUSHION_LIGHT, CUSHION_STITCH = "#edb732", "#c7951f", "#996b14", "#f2c13f", "#5a3c10"
# recording_mic.png
MIC_BASE, MIC_BASE_TOP, MIC_POLE, MIC_JOINT = "#30282a", "#403433", "#2b2426", "#594844"
MIC_HEAD, MIC_HEAD_LIGHT, MIC_GRILLE = "#81736b", "#a99b95", "#4f403d"
# art_easel.png
EASEL_WOOD, EASEL_LIGHT, EASEL_DARK = "#6d442f", "#8c553b", "#40281e"
CANVAS, CANVAS_EDGE = "#f6e5cb", "#cec2b0"
PAINT_BLUE, PAINT_YELLOW, PAINT_PINK = "#81a8db", "#f1d492", "#f6b7a2"
# studio_shelf.png
SHELF_WOOD, SHELF_EDGE, SHELF_INSIDE = "#533628", "#452b21", "#2c1d18"
BOOK_RED, BOOK_GRAY, BOOK_TAN, BOOK_NAVY, BOOK_DARK, BOOK_BROWN = "#693a2b", "#444848", "#766350", "#30383c", "#282a31", "#6d4a3b"
JAR, JAR_LID, RADIO, RADIO_DIAL = "#b09676", "#362b29", "#282a31", "#b09676"
# house_floor.png / house_wall.png
FLOOR_A, FLOOR_B = "#bbb198", "#b8ae95"
IWALL_TOP, IWALL_FACE, IWALL_BASE = "#26262d", "#2f2f37", "#3b3b44"

INTERIOR_WALL_HEIGHT = 1.2   # the north wall face fills the two dark rows of the 2D room
INTERIOR_WALL_BASE = 0.14    # what stays visible when a wall is cut away for the camera


# ---------------------------------------------------------------- geometry helpers

class Geo:
    """A MeshBuilder with explicit faces: every face is oriented outward when it is created,
    so the exported normals are right without Blender's recalculation guesses."""

    def __init__(self):
        self.mb = hl.MeshBuilder()
        self.bm = self.mb.bm
        self.layer = self.mb.layer

    def paint(self, faces, color):
        rgba = hl.hex_color(color)
        for face in faces:
            for loop in face.loops:
                loop[self.layer] = rgba

    def _new_faces(self, before):
        faces = [f for f in self.bm.faces if f not in before]
        for f in faces:
            f.normal_update()
        return faces

    def box(self, lo, hi, color, top=None, bottom=None, front=None):
        before = set(self.bm.faces)
        self.mb.box(lo, hi, color)
        faces = self._new_faces(before)
        if top:
            self.paint([f for f in faces if f.normal.z > 0.5], top)
        if bottom:
            self.paint([f for f in faces if f.normal.z < -0.5], bottom)
        if front:
            self.paint([f for f in faces if f.normal.y < -0.5], front)
        return faces

    def face(self, points, color, outward):
        verts = [self.bm.verts.new(Vector(p)) for p in points]
        f = self.bm.faces.new(verts)
        f.normal_update()
        if f.normal.dot(Vector(outward)) < 0:
            f.normal_flip()
        self.paint([f], color)
        return f

    def prism(self, points, offset, top_color, side_color=None, bottom_color=None):
        """Convex planar polygon and its copy moved by offset, closed; faces point away from
        the solid's centre. The polygon itself is painted top_color, the copy bottom_color."""
        offset = Vector(offset)
        a = [self.bm.verts.new(Vector(p)) for p in points]
        b = [self.bm.verts.new(Vector(p) + offset) for p in points]
        faces = [self.bm.faces.new(a), self.bm.faces.new(b)]
        n = len(points)
        for i in range(n):
            j = (i + 1) % n
            faces.append(self.bm.faces.new((a[i], a[j], b[j], b[i])))
        centre = sum((v.co for v in a + b), Vector()) / (2 * n)
        for f in faces:
            f.normal_update()
            if f.normal.dot(f.calc_center_median() - centre) < 0:
                f.normal_flip()
        self.paint(faces[2:], side_color or top_color)
        self.paint([faces[0]], top_color)
        self.paint([faces[1]], bottom_color or side_color or top_color)
        return faces

    def rod(self, p0, p1, radius, color, segments=4):
        """A square (or n-gon) bar between two points: legs, rails, poles."""
        before = set(self.bm.faces)
        self.mb.cylinder_between(p0, p1, radius, radius, segments, color)
        return self._new_faces(before)

    def loft(self, rings, color_top, color_side, color_bottom):
        """Closed convex loft through horizontal rings (lists of (x, y, z)), bottom to top."""
        verts = [[self.bm.verts.new(Vector(p)) for p in ring] for ring in rings]
        faces = [self.bm.faces.new(list(reversed(verts[0]))), self.bm.faces.new(verts[-1])]
        for lower, upper in zip(verts, verts[1:]):
            n = len(lower)
            for i in range(n):
                j = (i + 1) % n
                faces.append(self.bm.faces.new((lower[i], lower[j], upper[j], upper[i])))
        everything = [v for ring in verts for v in ring]
        centre = sum((v.co for v in everything), Vector()) / len(everything)
        for f in faces:
            f.normal_update()
            if f.normal.dot(f.calc_center_median() - centre) < 0:
                f.normal_flip()
        self.paint([faces[0]], color_bottom)
        self.paint([faces[1]], color_top)
        self.paint(faces[2:], color_side)
        return faces

    def obj(self, name):
        return self.mb.to_object(name, recalc_normals=False)


def _cut(a, b, axis, value):
    t = (value - a[axis]) / (b[axis] - a[axis])
    return a + (b - a) * t


def clip(poly, axis, lo, hi):
    """Sutherland-Hodgman clip of a planar polygon (Vectors) to lo <= p[axis] <= hi."""
    for value, keep_above in ((lo, True), (hi, False)):
        out = []
        for i, cur in enumerate(poly):
            prv = poly[i - 1]
            cin = cur[axis] >= value if keep_above else cur[axis] <= value
            pin = prv[axis] >= value if keep_above else prv[axis] <= value
            if cin:
                if not pin:
                    out.append(_cut(prv, cur, axis, value))
                out.append(cur)
            elif pin:
                out.append(_cut(prv, cur, axis, value))
        poly = out
        if len(poly) < 3:
            return []
    return poly


def area(poly):
    total = Vector()
    for i in range(len(poly)):
        total += poly[i - 1].cross(poly[i])
    return total.length * 0.5


def roof_slope(g, points, thickness, column_axis, rows, column_width, lift=0.004):
    """A roof slope: a thick slab plus staggered shingle tiles in three close grays, like the
    slate pattern of house_exterior.png (close enough that the 2D inner lines stay quiet)."""
    pts = [Vector(p) for p in points]
    normal = (pts[1] - pts[0]).cross(pts[2] - pts[0]).normalized()
    if normal.z < 0:
        normal = -normal
    g.prism([tuple(p) for p in pts], tuple(-normal * thickness), ROOF_A, RIDGE, RIDGE)
    zmin, zmax = min(p.z for p in pts), max(p.z for p in pts)
    cmin, cmax = min(p[column_axis] for p in pts), max(p[column_axis] for p in pts)
    shades = (ROOF_A, ROOF_B, ROOF_A, ROOF_C, ROOF_B)
    for r in range(rows):
        band = clip(pts, 2, zmin + (zmax - zmin) * r / rows, zmin + (zmax - zmin) * (r + 1) / rows)
        if not band:
            continue
        start = cmin - (column_width * 0.5 if r % 2 else 0.0)
        c = 0
        x = start
        while x < cmax:
            tile = clip(band, column_axis, x, x + column_width)
            if tile and area(tile) > 1e-5:
                g.face([tuple(p + normal * lift) for p in tile], shades[(r * 2 + c) % len(shades)], normal)
            x += column_width
            c += 1


def anchor(name, parent, location):
    return hl.add_anchor(name, parent, location)


# ---------------------------------------------------------------- house_main (5 x 2 cells)

HOUSE_FRONT, HOUSE_BACK, HOUSE_X = -0.2, 0.72, 1.06
HOUSE_WALL_TOP = 1.06  # the 2D view sees the whole wall under the eaves, as in the sprite
DOOR_PLANE = 0.22  # back of the walkable alcove: the root (door) cell's north edge is 0.25


def build_house():
    """Root cell (0, 0) is the walkable entrance alcove in the middle of the front row.
    Walls, roof and door are separate objects so Unity can show or hide each part."""
    walls = Geo()
    # Wall body around the alcove (x -0.25..0.25 is open from the front to the door).
    walls.box((-HOUSE_X, 0.25, 0.0), (HOUSE_X, HOUSE_BACK, HOUSE_WALL_TOP), WALL, top=WALL_SHADE)
    walls.box((-HOUSE_X, HOUSE_FRONT, 0.0), (-0.25, 0.25, HOUSE_WALL_TOP), WALL, top=WALL_SHADE)
    walls.box((0.25, HOUSE_FRONT, 0.0), (HOUSE_X, 0.25, HOUSE_WALL_TOP), WALL, top=WALL_SHADE)
    # Stepped (pixel-art) arch over the alcove and the door.
    for x0, x1, z in ((-0.25, -0.17, 0.58), (-0.17, -0.09, 0.66), (-0.09, 0.09, 0.71),
                      (0.09, 0.17, 0.66), (0.17, 0.25, 0.58)):
        walls.box((x0, HOUSE_FRONT, z), (x1, DOOR_PLANE, HOUSE_WALL_TOP), WALL, bottom=WALL_SHADE)
    # Arch trim on the front face (dark band following the steps).
    for x0, x1, z in ((-0.27, -0.17, 0.58), (-0.17, -0.09, 0.66), (-0.09, 0.09, 0.71),
                      (0.09, 0.17, 0.66), (0.17, 0.27, 0.58)):
        walls.box((x0, HOUSE_FRONT - 0.02, z), (x1, HOUSE_FRONT + 0.005, z + 0.045), TIMBER_DARK)
    for x in (-0.27, 0.235):
        walls.box((x, HOUSE_FRONT - 0.02, 0.0), (x + 0.035, HOUSE_FRONT + 0.005, 0.6), TIMBER_DARK)
    # Timber frame: door-section posts, corner posts, top beam, posts on the sides and back.
    for x0, x1 in ((-0.5, -0.34), (0.34, 0.5)):
        walls.box((x0, HOUSE_FRONT - 0.035, 0.0), (x1, HOUSE_FRONT + 0.01, HOUSE_WALL_TOP), TIMBER, front=TIMBER)
        walls.box((x0 - 0.02, HOUSE_FRONT - 0.05, 0.0), (x1 + 0.02, HOUSE_FRONT + 0.01, 0.05), STONE, top=STONE_LIGHT)
    for x in (-HOUSE_X, HOUSE_X):
        for y in (HOUSE_FRONT, HOUSE_BACK):
            walls.box((x - 0.05, y - 0.05, 0.0), (x + 0.05, y + 0.05, HOUSE_WALL_TOP + 0.01), TIMBER)
            walls.box((x - 0.065, y - 0.065, 0.0), (x + 0.065, y + 0.065, 0.05), STONE, top=STONE_LIGHT)
    beam = HOUSE_WALL_TOP - 0.13
    walls.box((-HOUSE_X - 0.05, HOUSE_FRONT - 0.045, beam), (HOUSE_X + 0.05, HOUSE_FRONT + 0.005, HOUSE_WALL_TOP),
              TIMBER_DARK, top=TIMBER)
    walls.box((-HOUSE_X - 0.05, HOUSE_BACK - 0.005, beam), (HOUSE_X + 0.05, HOUSE_BACK + 0.045, HOUSE_WALL_TOP),
              TIMBER_DARK, top=TIMBER)
    for x in (-HOUSE_X - 0.045, HOUSE_X - 0.005):
        walls.box((x, HOUSE_FRONT, beam), (x + 0.05, HOUSE_BACK, HOUSE_WALL_TOP), TIMBER_DARK, top=TIMBER)
    for x in (-0.42, 0.42):  # gold bolts on the beam above the door posts
        walls.box((x - 0.035, HOUSE_FRONT - 0.06, beam + 0.035), (x + 0.035, HOUSE_FRONT - 0.04, beam + 0.105), GOLD,
                  front=GOLD)
    # Front cross-gable wall (white triangle) with the gold diamond and dark verge boards.
    walls.prism([(-0.44, HOUSE_FRONT - 0.02, HOUSE_WALL_TOP), (0.44, HOUSE_FRONT - 0.02, HOUSE_WALL_TOP),
                 (0.0, HOUSE_FRONT - 0.02, HOUSE_WALL_TOP + 0.5)], (0.0, 0.42, 0.0), WALL, WALL, WALL_SHADE)
    dz = HOUSE_WALL_TOP + 0.2  # diamond centre
    walls.prism([(0.0, HOUSE_FRONT - 0.045, dz - 0.08), (0.075, HOUSE_FRONT - 0.045, dz),
                 (0.0, HOUSE_FRONT - 0.045, dz + 0.08), (-0.075, HOUSE_FRONT - 0.045, dz)], (0.0, 0.03, 0.0),
                GOLD, GOLD_DARK, GOLD)
    walls.prism([(0.0, HOUSE_FRONT - 0.052, dz - 0.035), (0.035, HOUSE_FRONT - 0.052, dz),
                 (0.0, HOUSE_FRONT - 0.052, dz + 0.035), (-0.035, HOUSE_FRONT - 0.052, dz)], (0.0, 0.01, 0.0),
                GOLD_DARK, GOLD_DARK, GOLD_DARK)
    # Entrance step: a stone slab on the alcove floor (the walkable door cell).
    walls.box((-0.24, HOUSE_FRONT - 0.04, 0.0), (0.24, DOOR_PLANE, 0.025), STONE_LIGHT, top=STEP_COLOR)
    # The alcove is in shadow: its inner sides, ceiling and the strips beside the door are dark,
    # so the 2D view (which looks down into it) reads as the dark arched door of the sprite.
    for f in walls.bm.faces:
        c = f.calc_center_median()
        if not (-0.26 <= c.x <= 0.26 and HOUSE_FRONT - 0.001 <= c.y <= 0.251 and c.z < 0.75):
            continue
        n = f.normal
        if (abs(n.x) > 0.5 and abs(c.x) > 0.2) or n.z < -0.5 or (n.y < -0.5 and c.y > DOOR_PLANE - 0.001):
            walls.paint([f], ALCOVE)
    # Supplemented (not in the sprite): plain windows on the sides and the back.
    for x, outward in ((-HOUSE_X, -1), (HOUSE_X, 1)):
        add_window(walls, Vector((x + outward * 0.005, 0.26, 0.56)), axis="x", outward=outward)
    for x in (-0.6, 0.6):
        add_window(walls, Vector((x, HOUSE_BACK + 0.005, 0.56)), axis="y", outward=1)
    house = walls.obj("house_main")

    roof = Geo()
    # Eaves just above the wall top with a short overhang, so the 2D view shows the whole wall
    # (about 53 px) under a roof of about 75 px, the proportions of house_exterior.png.
    eave_z, ridge_z = HOUSE_WALL_TOP + 0.04, HOUSE_WALL_TOP + 0.77
    x0, x1, y0, y1 = -1.18, 1.18, HOUSE_FRONT - 0.1, HOUSE_BACK + 0.1
    yc = (y0 + y1) * 0.5
    r0, r1 = -0.08, 0.08
    roof_slope(roof, [(x0, y0, eave_z), (x1, y0, eave_z), (r1, yc, ridge_z), (r0, yc, ridge_z)], 0.05, 0, 6, 0.2)
    roof_slope(roof, [(x1, y1, eave_z), (x0, y1, eave_z), (r0, yc, ridge_z), (r1, yc, ridge_z)], 0.05, 0, 6, 0.2)
    roof_slope(roof, [(x0, y1, eave_z), (x0, y0, eave_z), (r0, yc, ridge_z)], 0.05, 1, 6, 0.2)
    roof_slope(roof, [(x1, y0, eave_z), (x1, y1, eave_z), (r1, yc, ridge_z)], 0.05, 1, 6, 0.2)
    # Ridge and hip caps.
    roof.rod((r0 - 0.04, yc, ridge_z + 0.02), (r1 + 0.04, yc, ridge_z + 0.02), 0.035, RIDGE)
    for corner, end in (((x0, y0), r0), ((x1, y0), r1), ((x0, y1), r0), ((x1, y1), r1)):
        roof.rod((corner[0], corner[1], eave_z + 0.02), (end, yc, ridge_z + 0.02), 0.025, RIDGE)
    # Cross gable over the door: two small slopes running back into the main roof.
    gz_eave, gz_peak, gy0, gy1 = HOUSE_WALL_TOP + 0.02, HOUSE_WALL_TOP + 0.56, HOUSE_FRONT - 0.12, 0.16
    roof_slope(roof, [(-0.53, gy0, gz_eave), (0.0, gy0, gz_peak), (0.0, gy1, gz_peak), (-0.53, gy1, gz_eave)],
               0.045, 1, 4, 0.16)
    roof_slope(roof, [(0.0, gy0, gz_peak), (0.53, gy0, gz_eave), (0.53, gy1, gz_eave), (0.0, gy1, gz_peak)],
               0.045, 1, 4, 0.16)
    roof.rod((0.0, gy0 - 0.02, gz_peak + 0.02), (0.0, gy1, gz_peak + 0.02), 0.03, RIDGE)
    for x in (-0.53, 0.53):  # verge boards along the gable's front edge
        roof.rod((x, gy0 - 0.01, gz_eave - 0.01), (0.0, gy0 - 0.01, gz_peak - 0.01), 0.03, TIMBER_DARK)
    roof_obj = roof.obj("HouseRoof")

    door = Geo()  # pivot on the hinge side so a future opening animation can rotate it
    for x0, x1, z in ((-0.21, -0.17, 0.58), (-0.17, -0.09, 0.66), (-0.09, 0.09, 0.71),
                      (0.09, 0.17, 0.66), (0.17, 0.21, 0.58)):
        door.box((x0 + 0.21, -0.03, 0.0), (x1 + 0.21, 0.0, z), DOOR, front=DOOR)
    for x in (-0.11, -0.035, 0.04, 0.115):  # plank gaps
        door.box((x + 0.21 - 0.006, -0.036, 0.02), (x + 0.21 + 0.006, -0.028, 0.6), DOOR_DARK)
    door.box((0.12 + 0.21, -0.05, 0.27), (0.17 + 0.21, -0.03, 0.32), GOLD, front=GOLD)
    door.box((0.135 + 0.21, -0.056, 0.285), (0.155 + 0.21, -0.05, 0.305), GOLD_DARK)
    door_obj = door.obj("HouseDoor")

    for part in (roof_obj, door_obj):
        part.parent = house
    door_obj.location = (-0.21, DOOR_PLANE + 0.03, 0.0)
    anchors = [
        anchor("DoorAnchor", house, (0.0, DOOR_PLANE, 0.0)),        # door plane, ground level
        anchor("EntranceAnchor", house, (0.0, 0.0, 0.025)),         # where the player stands (step top)
    ]
    hl.add_cell_guides("house_main", [(x, y) for x in range(-2, 3) for y in range(0, 2)])
    return [house, roof_obj, door_obj], anchors, {
        "house_main": ["side and back walls", "windows on the sides and the back", "roof back and side slopes",
                       "door inside face", "entrance step"]}


STEP_COLOR = "#5e5e64"
ALCOVE = "#3e3e44"


def add_window(g, centre, axis, outward):
    """Small four-pane window (supplemented design): timber frame, pale glass, cross bars.
    axis is the wall's normal axis ("x" for side walls, "y" for the back wall)."""
    w, h, t = 0.26, 0.26, 0.03

    def bx(a0, b0, a1, b1, depth, color):
        d0, d1 = sorted((0.0, outward * depth))
        if axis == "x":
            g.box((centre.x + d0, centre.y + a0, centre.z + b0), (centre.x + d1, centre.y + a1, centre.z + b1), color)
        else:
            g.box((centre.x + a0, centre.y + d0, centre.z + b0), (centre.x + a1, centre.y + d1, centre.z + b1), color)

    bx(-w / 2, -h / 2, w / 2, h / 2, t * 0.5, GLASS)
    for a0, a1, b0, b1 in ((-w / 2 - 0.03, -w / 2, -h / 2 - 0.03, h / 2 + 0.03),
                           (w / 2, w / 2 + 0.03, -h / 2 - 0.03, h / 2 + 0.03),
                           (-w / 2, w / 2, -h / 2 - 0.03, -h / 2), (-w / 2, w / 2, h / 2, h / 2 + 0.03),
                           (-0.012, 0.012, -h / 2, h / 2), (-w / 2, w / 2, -0.012, 0.012)):
        bx(a0, b0, a1, b1, t, TIMBER_DARK)


# ---------------------------------------------------------------- outdoor fixtures

def build_bench():
    """bench_basic, 2 x 1 cells from the root (left) cell; backrest on the North side.
    Seat anchors over both cells: the 2D game's bench_sit pose faces South."""
    g = Geo()
    for i, (y0, y1) in enumerate(((-0.11, -0.04), (-0.035, 0.035), (0.04, 0.11))):
        g.box((-0.21, y0, 0.18), (0.71, y1, 0.22), BENCH_MID, top=BENCH_LIGHT if i != 1 else BENCH_MID)
    for x in (-0.16, 0.62):
        for y in (-0.085, 0.07):
            g.box((x, y, 0.0), (x + 0.055, y + 0.05, 0.18), BENCH_LEG)
    for x in (-0.2, 0.655):
        g.box((x, 0.1, 0.0), (x + 0.055, 0.15, 0.5), BENCH_DARK, top=BENCH_MID)
    g.box((-0.22, 0.105, 0.39), (0.72, 0.145, 0.47), BENCH_MID, top=BENCH_LIGHT, front=BENCH_LIGHT)
    g.box((-0.22, 0.105, 0.27), (0.72, 0.145, 0.33), BENCH_MID, top=BENCH_LIGHT)
    bench = g.obj("bench_basic")
    anchors = [anchor("SeatAnchor_L", bench, (0.0, 0.0, 0.22)), anchor("SeatAnchor_R", bench, (0.5, 0.0, 0.22))]
    hl.add_cell_guides("bench_basic", [(0, 0), (1, 0)])
    return [bench], anchors, {"bench_basic": ["back of the backrest", "seat underside"]}


def build_sign():
    """sign_basic: a two-plank board on a post, facing South. The text stays in the Map's
    signText and the Interaction message; nothing is baked on the board."""
    g = Geo()
    g.box((-0.03, -0.03, 0.0), (0.03, 0.03, 0.56), SIGN_DARK, top=SIGN_MID)
    g.box((-0.22, -0.065, 0.36), (0.22, -0.03, 0.47), SIGN_MID, top=SIGN_LIGHT, front=SIGN_LIGHT)
    g.box((-0.215, -0.065, 0.245), (0.215, -0.03, 0.355), SIGN_MID, front=SIGN_MID)
    for z in (0.355, 0.245):
        g.box((-0.215, -0.07, z - 0.006), (0.215, -0.064, z + 0.006), SIGN_DARK)
    sign = g.obj("sign_basic")
    hl.add_cell_guides("sign_basic", [(0, 0)])
    return [sign], [], {"sign_basic": ["back of the board"]}


def build_well():
    """well_basic: a ring of lavender-grey stones in two staggered courses around dark water."""
    g = Geo()
    outer, inner, count = 0.22, 0.14, 10
    colors = (WELL_A, WELL_B, WELL_C, WELL_A, WELL_B)
    for course, (z0, z1, shift) in enumerate(((0.0, 0.075, 0.0), (0.075, 0.15, 0.5))):
        for i in range(count):
            a0 = (i + shift) / count * math.tau
            a1 = (i + shift + 0.92) / count * math.tau
            ring = [(inner * math.cos(a0), inner * math.sin(a0), z1), (outer * math.cos(a0), outer * math.sin(a0), z1),
                    (outer * math.cos(a1), outer * math.sin(a1), z1), (inner * math.cos(a1), inner * math.sin(a1), z1)]
            faces = g.prism(ring, (0.0, 0.0, -(z1 - z0)), WELL_TOP if course else colors[i % 5],
                            colors[(i + course) % 5], WELL_IN)
            g.paint([f for f in faces[2:] if f.normal.dot(Vector((ring[0][0], ring[0][1], 0))) < 0], WELL_IN)
    disc = [(0.145 * math.cos(i / 12 * math.tau), 0.145 * math.sin(i / 12 * math.tau), 0.05) for i in range(12)]
    g.prism(disc, (0.0, 0.0, -0.05), WELL_WATER, WELL_IN, WELL_IN)
    well = g.obj("well_basic")
    hl.add_cell_guides("well_basic", [(0, 0)])
    return [well], [], {"well_basic": ["stone course split (the sprite shows one ring from above)"]}


# ---------------------------------------------------------------- interior pieces

def build_interior():
    """Floor tiles and wall pieces for interior maps. A wall cell is two objects: the base
    (always shown) and the upper part (hidden when it stands between the 3D camera and the
    room, and always on the camera side of the 2D view). The doorway marks the exit cell."""
    models = []
    for suffix, color in (("a", FLOOR_A), ("b", FLOOR_B)):
        g = Geo()
        g.face([(-HALF, -HALF, 0.0), (HALF, -HALF, 0.0), (HALF, HALF, 0.0), (-HALF, HALF, 0.0)], color, (0, 0, 1))
        models.append(g.obj("interior_floor_" + suffix))
    g = Geo()
    g.box((-HALF, -HALF, 0.0), (HALF, HALF, INTERIOR_WALL_BASE), IWALL_BASE, top=IWALL_TOP)
    models.append(g.obj("interior_wall_base"))
    g = Geo()
    g.box((-HALF, -HALF, INTERIOR_WALL_BASE), (HALF, HALF, INTERIOR_WALL_HEIGHT), IWALL_FACE, top=IWALL_TOP,
          bottom=IWALL_FACE)
    models.append(g.obj("interior_wall_upper"))
    # Doorway (the interior_exit cell): threshold always shown; frame, lintel and the closed
    # door on the outer (South) edge are the upper part.
    g = Geo()
    g.face([(-HALF, -HALF, 0.0), (HALF, -HALF, 0.0), (HALF, HALF, 0.0), (-HALF, HALF, 0.0)], FLOOR_A, (0, 0, 1))
    g.box((-0.22, -HALF, 0.0), (0.22, -0.16, 0.02), STONE_LIGHT, top=STEP_COLOR)
    for x0, x1 in ((-HALF, -0.2), (0.2, HALF)):
        g.box((x0, -HALF, 0.0), (x1, HALF, INTERIOR_WALL_BASE), IWALL_BASE, top=IWALL_TOP)
    models.append(g.obj("interior_doorway_base"))
    g = Geo()
    for x0, x1 in ((-HALF, -0.2), (0.2, HALF)):
        g.box((x0, -HALF, INTERIOR_WALL_BASE), (x1, HALF, INTERIOR_WALL_HEIGHT), IWALL_FACE, top=IWALL_TOP)
        g.box((x0 + (0.0 if x0 < 0 else -0.02), -HALF + 0.02, INTERIOR_WALL_BASE),
              (x1 + (0.02 if x0 < 0 else 0.0), HALF - 0.02, 0.78), TIMBER)
    g.box((-0.2, -HALF, 0.78), (0.2, HALF, INTERIOR_WALL_HEIGHT), IWALL_FACE, top=IWALL_TOP, bottom=TIMBER)
    g.box((-0.2, -HALF, 0.0), (0.2, -HALF + 0.03, 0.78), DOOR)
    for x in (-0.1, 0.0, 0.1):
        g.box((x - 0.006, -HALF + 0.03, 0.02), (x + 0.006, -HALF + 0.036, 0.74), DOOR_DARK)
    g.box((-0.15, -HALF + 0.03, 0.34), (-0.11, -HALF + 0.05, 0.38), GOLD)
    models.append(g.obj("interior_doorway_upper"))
    return models, [], {"interior": ["wall faces (the 2D room shows wall tops only)", "doorway frame and door",
                                     "baseboard"]}


# ---------------------------------------------------------------- furniture

def build_bed():
    """bed_basic, 2 x 3 cells from the root (bottom-left) cell; the head is North."""
    g = Geo()
    xa, xb, ya, yb = -0.2, 0.7, -0.2, 1.2
    for x in (xa, xb):
        for y, top in ((ya, 0.42), (yb, 0.56)):
            g.box((x - 0.035, y - 0.035, 0.0), (x + 0.035, y + 0.035, top), BED_FRAME_LIGHT, top=BED_KNOB)
            g.box((x - 0.045, y - 0.045, top), (x + 0.045, y + 0.045, top + 0.04), BED_KNOB)
    for x in (xa, xb):
        g.box((x - 0.02, ya, 0.13), (x + 0.02, yb, 0.21), BED_FRAME)
    for y, z0, z1 in ((ya, 0.3, 0.37), (yb, 0.33, 0.41), (yb, 0.46, 0.52)):
        g.box((xa, y - 0.025, z0), (xb, y + 0.025, z1), BED_FRAME_LIGHT, top=BED_KNOB)
    g.box((xa + 0.03, ya + 0.03, 0.18), (xb - 0.03, yb - 0.03, 0.29), SHEET_SHADE, top=SHEET)
    g.box((xa + 0.01, ya + 0.015, 0.22), (xb - 0.01, 0.76, 0.32), BLANKET_SIDE, top=BLANKET)
    g.box((xa + 0.012, ya + 0.012, 0.31), (xb - 0.012, ya + 0.1, 0.325), BLANKET_DARK)
    g.box((xa + 0.012, 0.7, 0.315), (xb - 0.012, 0.76, 0.33), BLANKET_SIDE)
    g.box((xa + 0.07, 0.86, 0.29), (xb - 0.07, 1.13, 0.38), SHEET_SHADE, top=SHEET)
    bed = g.obj("bed_basic")
    anchors = [anchor("SleepAnchor", bed, (0.25, 0.95, 0.33))]
    hl.add_cell_guides("bed_basic", [(x, y) for x in range(2) for y in range(3)])
    return [bed], anchors, {"bed_basic": ["under-bed rails", "back of the headboard"]}


def build_desk():
    """desk_basic, 2 x 1 cells from the root (left) cell: a wooden desk with the monitor,
    keyboard and mouse drawn in desk.png, all facing South."""
    g = Geo()
    g.box((-0.22, -0.17, 0.36), (0.72, 0.17, 0.41), DESK_EDGE, top=DESK_TOP)
    for x in (-0.2, 0.65):
        for y in (-0.15, 0.1):
            g.box((x, y, 0.0), (x + 0.05, y + 0.05, 0.36), DESK_LEG)
    g.box((-0.2, 0.12, 0.2), (0.7, 0.15, 0.36), DESK_EDGE)  # back panel
    g.box((0.19, 0.05, 0.41), (0.31, 0.13, 0.42), MONITOR)
    g.box((0.235, 0.08, 0.42), (0.265, 0.1, 0.48), MONITOR)
    g.box((0.08, 0.06, 0.47), (0.42, 0.1, 0.68), MONITOR)
    g.box((0.095, 0.052, 0.485), (0.405, 0.06, 0.665), SCREEN, front=SCREEN)
    for x0, x1, z in ((0.12, 0.24, 0.62), (0.27, 0.36, 0.62), (0.12, 0.2, 0.58), (0.23, 0.38, 0.58),
                      (0.12, 0.3, 0.54)):
        g.box((x0, 0.046, z), (x1, 0.052, z + 0.015), SCREEN_TEXT)
    g.box((0.1, -0.12, 0.41), (0.4, -0.02, 0.425), KEYS_DARK, top=KEYS)
    for z_row in range(3):
        y = -0.105 + z_row * 0.03
        g.box((0.11, y, 0.425), (0.39, y + 0.004, 0.428), KEYS_DARK)
    g.box((0.47, -0.1, 0.41), (0.53, -0.03, 0.44), MOUSE, top=MOUSE)
    g.box((0.49, -0.08, 0.44), (0.51, -0.06, 0.445), SCREEN_TEXT)
    desk = g.obj("desk_basic")
    hl.add_cell_guides("desk_basic", [(0, 0), (1, 0)])
    return [desk], [anchor("WorkAnchor", desk, (0.25, -0.35, 0.0))], {"desk_basic": ["back panel", "monitor back"]}


def build_cushion():
    """cushion_basic (walkable): a soft square cushion with a stitched tuft."""
    g = Geo()

    def ring(r, corner, z):
        pts = []
        for i in range(12):
            a = (i + 0.5) / 12 * math.tau
            c, s = math.cos(a), math.sin(a)
            k = max(abs(c), abs(s))
            square = r / k  # square-ish ring with slightly pointed corners like the sprite
            radius = square * (1.0 - corner) + r * corner
            pts.append((radius * c, radius * s, z))
        return pts

    g.loft([ring(0.15, 0.25, 0.0), ring(0.175, 0.1, 0.045), ring(0.14, 0.35, 0.1)],
           CUSHION_TOP, CUSHION_SIDE, CUSHION_DARK)
    for angle, length in ((45, 0.05), (-45, 0.05), (0, 0.025)):  # the tuft's stitches
        d = Vector((math.cos(math.radians(angle)), math.sin(math.radians(angle)), 0.0))
        n = Vector((-d.y, d.x, 0.0))
        lift = Vector((0.0, 0.0, 0.102))
        g.face([tuple(lift - d * length - n * 0.007), tuple(lift + d * length - n * 0.007),
                tuple(lift + d * length + n * 0.007), tuple(lift - d * length + n * 0.007)], CUSHION_STITCH, (0, 0, 1))
    cushion = g.obj("cushion_basic")
    hl.add_cell_guides("cushion_basic", [(0, 0)])
    return [cushion], [anchor("SitAnchor", cushion, (0.0, 0.0, 0.1))], {"cushion_basic": ["underside"]}


def build_recording_mic():
    """recording_mic_basic: footprint 1 x 2 (the stand on the root cell, the North cell is
    blocked space). The pole is 0.05 wide so it stays at least one pixel in the 2D view."""
    g = Geo()
    base = [(0.11 * math.cos(i / 10 * math.tau), 0.11 * math.sin(i / 10 * math.tau), 0.035) for i in range(10)]
    g.prism(base, (0.0, 0.0, -0.035), MIC_BASE_TOP, MIC_BASE, MIC_BASE)
    g.rod((0.0, 0.0, 0.03), (0.0, 0.0, 0.8), 0.025, MIC_POLE, 6)
    g.box((-0.035, -0.035, 0.5), (0.035, 0.035, 0.56), MIC_JOINT)
    g.rod((0.0, 0.0, 0.8), (0.0, -0.05, 0.86), 0.02, MIC_POLE, 6)
    for z in (0.84, 0.98):  # shock-mount rings
        g.box((-0.065, -0.115, z), (0.065, 0.015, z + 0.018), MIC_POLE)
    g.rod((0.0, -0.05, 0.82), (0.0, -0.05, 1.03), 0.048, MIC_HEAD, 8)
    for z in (0.88, 0.93):
        g.rod((0.0, -0.05, z), (0.0, -0.05, z + 0.012), 0.051, MIC_GRILLE, 8)
    g.rod((0.0, -0.05, 1.03), (0.0, -0.05, 1.05), 0.03, MIC_HEAD_LIGHT, 8)
    mic = g.obj("recording_mic_basic")
    hl.add_cell_guides("recording_mic_basic", [(0, 0), (0, 1)])
    return [mic], [], {"recording_mic_basic": ["back of the capsule", "shock-mount detail simplified"]}


def build_art_easel():
    """art_easel_basic, 2 x 2 cells from the root (bottom-left) cell: an A-frame easel holding
    the painted canvas of art_easel.png (blue bolt, yellow and pink patches)."""
    g = Geo()
    cx, cy = 0.25, 0.22
    g.rod((cx - 0.17, cy - 0.12, 0.0), (cx - 0.05, cy + 0.02, 0.98), 0.022, EASEL_WOOD)
    g.rod((cx + 0.17, cy - 0.12, 0.0), (cx + 0.05, cy + 0.02, 0.98), 0.022, EASEL_WOOD)
    g.rod((cx, cy + 0.32, 0.0), (cx, cy + 0.05, 0.86), 0.02, EASEL_DARK)
    g.rod((cx, cy + 0.0, 0.55), (cx, cy + 0.03, 1.08), 0.022, EASEL_LIGHT)
    g.box((cx - 0.22, cy - 0.16, 0.4), (cx + 0.22, cy - 0.08, 0.44), EASEL_LIGHT, top=EASEL_WOOD)
    g.box((cx - 0.08, cy - 0.02, 0.86), (cx + 0.08, cy + 0.03, 0.9), EASEL_LIGHT)
    tilt = math.radians(14)
    up = Vector((0.0, math.sin(tilt), math.cos(tilt)))
    right = Vector((1.0, 0.0, 0.0))
    normal = right.cross(up)  # toward South and up
    origin = Vector((cx, cy - 0.11, 0.44))

    def on_canvas(u, v, lift=0.0):
        return tuple(origin + right * u + up * v + normal * lift)

    corners = [on_canvas(-0.21, 0.0), on_canvas(0.21, 0.0), on_canvas(0.21, 0.4), on_canvas(-0.21, 0.4)]
    g.prism(corners, tuple(-normal * 0.025), CANVAS_EDGE, CANVAS_EDGE, CANVAS_EDGE)
    g.face([on_canvas(-0.195, 0.015, 0.002), on_canvas(0.195, 0.015, 0.002), on_canvas(0.195, 0.385, 0.002),
            on_canvas(-0.195, 0.385, 0.002)], CANVAS, normal)
    paint = [  # (u0, v0, u1, v1, color) patches in the sprite's layout
        (0.02, 0.22, 0.17, 0.33, PAINT_YELLOW), (-0.12, 0.05, 0.06, 0.16, PAINT_PINK),
        (-0.16, 0.24, -0.08, 0.33, PAINT_BLUE), (-0.12, 0.18, -0.03, 0.26, PAINT_BLUE),
        (-0.09, 0.12, -0.01, 0.19, PAINT_BLUE)]
    for u0, v0, u1, v1, color in paint:
        g.face([on_canvas(u0, v0, 0.004), on_canvas(u1, v0, 0.004), on_canvas(u1, v1, 0.004),
                on_canvas(u0, v1, 0.004)], color, normal)
    for x, color in ((cx - 0.12, PAINT_BLUE), (cx - 0.07, PAINT_YELLOW)):
        g.box((x, cy - 0.15, 0.44), (x + 0.025, cy - 0.125, 0.455), color)
    easel = g.obj("art_easel_basic")
    hl.add_cell_guides("art_easel_basic", [(x, y) for x in range(2) for y in range(2)])
    return [easel], [], {"art_easel_basic": ["back leg and the back of the canvas"]}


def build_studio_shelf():
    """studio_shelf_basic, 2 x 1 cells from the root (left) cell: a low wooden shelf with the
    books and things drawn in studio_shelf.png (nothing beyond what the sprite shows)."""
    g = Geo()
    x0, x1, y0, y1, top = -0.22, 0.72, -0.08, 0.2, 0.38
    for x in (x0, x1 - 0.04):
        g.box((x, y0, 0.0), (x + 0.04, y1, top), SHELF_EDGE, front=SHELF_WOOD)
    g.box((x0, y0, top - 0.04), (x1, y1, top), SHELF_EDGE, top=SHELF_WOOD, front=SHELF_WOOD)
    g.box((x0, y0, 0.0), (x1, y1, 0.05), SHELF_EDGE, front=SHELF_WOOD)
    g.box((x0 + 0.04, y1 - 0.02, 0.05), (x1 - 0.04, y1, top - 0.04), SHELF_INSIDE)
    g.box((0.23, y0, 0.05), (0.27, y1 - 0.02, top - 0.04), SHELF_EDGE, front=SHELF_WOOD)
    # Lower compartments: standing books (left), a lying stack and standing books (right).
    x = x0 + 0.05
    for width, height, color in ((0.05, 0.25, BOOK_RED), (0.04, 0.22, BOOK_GRAY), (0.05, 0.26, BOOK_NAVY),
                                 (0.045, 0.2, BOOK_TAN), (0.05, 0.24, BOOK_RED), (0.04, 0.23, BOOK_GRAY)):
        g.box((x, y0 + 0.03, 0.05), (x + width - 0.005, y1 - 0.04, 0.05 + height), color, top=color)
        x += width
    for i, color in enumerate((BOOK_GRAY, BOOK_RED, BOOK_GRAY)):
        g.box((0.3, y0 + 0.035, 0.05 + i * 0.045), (0.5, y1 - 0.04, 0.05 + (i + 1) * 0.045 - 0.004), color)
    x = 0.54
    for width, height, color in ((0.045, 0.24, BOOK_DARK), (0.045, 0.22, BOOK_RED), (0.05, 0.25, BOOK_TAN)):
        g.box((x, y0 + 0.03, 0.05), (x + width - 0.005, y1 - 0.04, 0.05 + height), color, top=color)
        x += width
    # Things on top (left to right as drawn).
    g.box((-0.18, -0.02, top), (0.02, 0.14, top + 0.045), BOOK_BROWN, top="#8a5a46")
    x = 0.06
    for width, height, color in ((0.05, 0.2, BOOK_NAVY), (0.045, 0.22, BOOK_GRAY), (0.05, 0.19, BOOK_NAVY)):
        g.box((x, 0.0, top), (x + width - 0.005, 0.15, top + height), color, top=color)
        x += width
    jar = [(0.3 + 0.04 * math.cos(i / 8 * math.tau), 0.08 + 0.04 * math.sin(i / 8 * math.tau), top + 0.12)
           for i in range(8)]
    g.prism(jar, (0.0, 0.0, -0.12), JAR, JAR, JAR)
    for dx in (-0.015, 0.012):
        g.box((0.3 + dx - 0.008, 0.075, top + 0.12), (0.3 + dx + 0.008, 0.09, top + 0.2), JAR_LID)
    g.box((0.38, 0.01, top), (0.55, 0.15, top + 0.13), RADIO, top="#3a3c44")
    dial = [(0.465 + 0.035 * math.cos(i / 8 * math.tau), -0.004, top + 0.065 + 0.035 * math.sin(i / 8 * math.tau))
            for i in range(8)]
    g.prism(dial, (0.0, 0.014, 0.0), RADIO_DIAL, RADIO_DIAL, RADIO_DIAL)
    g.box((0.6, 0.0, top), (0.66, 0.15, top + 0.15), BOOK_DARK, top=BOOK_DARK)
    shelf = g.obj("studio_shelf_basic")
    hl.add_cell_guides("studio_shelf_basic", [(0, 0), (1, 0)])
    return [shelf], [], {"studio_shelf_basic": ["back panel", "sides of the top items"]}


FAMILIES = (
    ("house_main", build_house),
    ("bench_basic", build_bench),
    ("sign_basic", build_sign),
    ("well_basic", build_well),
    ("interior", build_interior),
    ("bed_basic", build_bed),
    ("desk_basic", build_desk),
    ("cushion_basic", build_cushion),
    ("recording_mic_basic", build_recording_mic),
    ("art_easel_basic", build_art_easel),
    ("studio_shelf_basic", build_studio_shelf),
)


def world_bounds(objects):
    corners = [o.matrix_world @ Vector(c) for o in objects for c in o.bound_box]
    xs, ys, zs = zip(*[tuple(c) for c in corners])
    return [round(min(xs), 4), round(min(ys), 4), round(min(zs), 4)], [round(max(xs), 4), round(max(ys), 4), round(max(zs), 4)]


def main():
    for folder in (SOURCES, EXPORTS, UNITY_MODELS):
        os.makedirs(folder, exist_ok=True)
    manifest = {
        "generator": "blender/scripts/build_phase3_buildings.py",
        "blenderVersion": bpy.app.version_string,
        "units": "1 Blender unit = 1 Unity unit, 1 cell = 0.5, origin = ground centre of the root cell",
        "export": {"format": "FBX 7.4 binary", "axisForward": "-Z", "axisUp": "Y", "bakeSpaceTransform": True,
                   "colors": "SRGB face-corner attribute 'Col'", "material": "HALKA_VertexColor (1 slot)"},
        "interiorWallHeight": INTERIOR_WALL_HEIGHT,
        "interiorWallBase": INTERIOR_WALL_BASE,
        "models": [],
    }
    for family, build in FAMILIES:
        hl.reset_scene()
        objects, anchors, supplemented = build()
        roots = [o for o in objects if o.parent is None]
        spacing = 0.0
        for obj in roots:
            obj.location.x = spacing  # spread separate models in the .blend only for editing
            spacing += 1.4
        bpy.ops.wm.save_as_mainfile(filepath=os.path.join(SOURCES, family + ".blend"), compress=False)
        for obj in roots:
            obj.location.x = 0.0
        bpy.context.view_layer.update()
        for obj in roots:
            parts = [o for o in objects if o.parent == obj]
            own = [a for a in anchors if a.parent == obj]
            fbx = os.path.join(UNITY_MODELS, obj.name + ".fbx")
            hl.export_fbx(obj, fbx, parts + own)
            lo, hi = world_bounds([obj] + parts)
            entry = {
                "family": family, "name": obj.name,
                "blend": f"blender/sources/phase3/{family}.blend",
                "fbx": os.path.relpath(fbx, ROOT).replace("\\", "/"),
                "triangles": sum(hl.triangle_count(o) for o in [obj] + parts),
                "vertices": sum(len(o.data.vertices) for o in [obj] + parts),
                "materials": max(hl.material_count(o) for o in [obj] + parts),
                "parts": {o.name: {"triangles": hl.triangle_count(o), "materials": hl.material_count(o),
                                   "pivot": [round(v, 4) for v in o.location]} for o in [obj] + parts},
                "boundsMin": lo, "boundsMax": hi,
                "anchors": {a.name: [round(v, 4) for v in a.location] for a in own},
                "supplemented": supplemented.get(obj.name, supplemented.get(family, [])),
            }
            manifest["models"].append(entry)
            print(f"HARUKA_PHASE3_ASSET {obj.name}: {entry['triangles']} tris, parts {list(entry['parts'])}")
    with open(os.path.join(EXPORTS, "phase3_assets_manifest.json"), "w", encoding="utf-8") as f:
        json.dump(manifest, f, indent=2, ensure_ascii=False)
        f.write("\n")


if __name__ == "__main__":
    main()
