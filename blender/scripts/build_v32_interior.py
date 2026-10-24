"""はるかわーるど ver3.2 (interior update): the house's kitchen, rug and low table, the booth's guitar on
its stand, and the two booth desks made one cell wider each so they have a middle cell (the
player can stand exactly in front: pen-tablet desk 2 -> 3 cells, DTM desk 4 -> 5 cells).

Run from the repository root:
  "C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" --background --factory-startup \
      --python blender/scripts/build_v32_interior.py

The same conventions as build_phase3_buildings.py / build_v31_studio.py:
  Blender +X = East, +Y = North, +Z = Up, 1 unit = 1 Unity unit, 1 cell = 0.5.
  The origin is the ground centre of the Definition's ROOT cell. Fronts face South (-Y), except the
  kitchen, which stands against the house's East wall and faces West (-X).
  Colors are face-corner vertex colors ("Col"), one material slot (HALKA_VertexColor).
The ver3.1 desk models (Models/V31, blender/sources/v31) are kept unchanged; the wider desks are new
files with the same Definition IDs. New designs: candidates until the user approves them.

Outputs:
  blender/sources/v32/<family>.blend
  unity-project/Assets/POC2D3D/Models/V32/<model>.fbx
  blender/exports/v32_assets_manifest.json
"""

import json
import math
import os
import sys

import bpy
from mathutils import Vector

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import halka_lowpoly as hl  # noqa: E402
import build_phase3_buildings as p3  # noqa: E402  (Geo, anchor, palettes, world_bounds)
import build_v31_studio as v31  # noqa: E402  (studio palettes, the monitor speaker)

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
SOURCES = os.path.join(ROOT, "blender", "sources", "v32")
EXPORTS = os.path.join(ROOT, "blender", "exports")
UNITY_MODELS = os.path.join(ROOT, "unity-project", "Assets", "POC2D3D", "Models", "V32")
Geo = p3.Geo
anchor = p3.anchor

# ---------------------------------------------------------------- palettes (ver3.2 interior)

DESK_TOP, DESK_EDGE, DESK_LEG = p3.DESK_TOP, p3.DESK_EDGE, p3.DESK_LEG
# Kitchen: cream cabinets with a light wood worktop (the desks' wood), steel sink, black hob.
CAB, CAB_SHADE, CAB_LINE, HANDLE = "#ece6da", "#d8d1c3", "#c4bcad", "#8f9399"
WORKTOP, WORKTOP_EDGE = DESK_TOP, DESK_EDGE
STEEL, STEEL_DARK, WATER = "#b7bdc3", "#8d949b", "#9fc3d6"
HOB, BURNER, BURNER_RING = "#2b2a2f", "#45444b", "#5c5b63"
KETTLE, KETTLE_DARK = "#d65a4f", "#a8443b"
TILE, TILE_LINE = "#d6e6e6", "#b9cfcf"
FRIDGE, FRIDGE_SHADE, FRIDGE_LINE = "#f3f1ec", "#dcd9d2", "#c7c3bb"
NOTE_YELLOW, NOTE_PINK = "#f6d35c", "#f2a7b8"
# Rug: a soft rose rug with a cream border and stripes (a warm note next to the blue sofa).
RUG, RUG_BORDER, RUG_STRIPE, RUG_EDGE = "#d9a9b4", "#efe2cf", "#c88f9c", "#b98593"
# Low table: lighter wood, a cocoa mug and a plate of cookies.
TABLE_TOP, TABLE_EDGE, TABLE_LEG = "#d5ab7d", "#b78b5e", "#8e6948"
MUG, MUG_SHADE, COCOA = "#eae4d8", "#cfc8ba", "#6b4226"
PLATE, PLATE_RIM, COOKIE, COOKIE_DARK = "#f5f2ea", "#ddd8cc", "#d39a57", "#a8713a"
# Guitar: a natural-wood acoustic guitar on a black A-frame stand.
GUITAR_TOP, GUITAR_SIDE, GUITAR_BACK = "#e2b46a", "#a86f3a", "#8a5a2e"
SOUNDHOLE, NECK, FRET_BOARD, HEAD, PEG = "#3a2a1e", "#7a5232", "#3b2b22", "#2f2420", "#c9c3b8"
STAND, STAND_PAD = "#26252a", "#3c3b42"
SKETCH_COVER, SKETCH_PAPER = "#5a7fb3", "#f4ede0"


def octagon(cx, cy, r, z, n=8, phase=0.5):
    return [(cx + r * math.cos((i + phase) / n * math.tau), cy + r * math.sin((i + phase) / n * math.tau), z)
            for i in range(n)]


def disc(g, cx, cy, r, z0, z1, top, side, n=8):
    g.prism(octagon(cx, cy, r, z1, n), (0.0, 0.0, z0 - z1), top, side, side)


# ---------------------------------------------------------------- the booth's desks, one cell wider

def build_dtm_desk_large():
    """dtm_desk_large, 5 x 1 cells from the root (left) cell (ver3.1: 4). The monitor, the keyboard and
    the space between the two speakers are centred on the MIDDLE cell (root + 2), so standing South of
    that cell is standing exactly in front of the monitor. All five cells are blocked."""
    g = Geo()
    left, right, front, back, top_lo, top_hi = -0.22, 2.22, -0.2, 0.2, 0.36, 0.41
    cx = 1.0  # the middle cell's centre
    g.box((left, front, top_lo), (right, back, top_hi), DESK_EDGE, top=DESK_TOP)
    for x in (left + 0.02, cx - 0.025, right - 0.07):
        for y in (front + 0.02, back - 0.07):
            g.box((x, y, 0.0), (x + 0.05, y + 0.05, top_lo), DESK_LEG)
    g.box((left + 0.02, back - 0.05, 0.18), (right - 0.02, back - 0.02, top_lo), DESK_EDGE)
    # A low shelf under the right part for the audio interface (as in ver3.1, moved with the desk).
    g.box((1.45, front + 0.04, 0.1), (right - 0.07, back - 0.05, 0.13), DESK_EDGE, top=DESK_TOP)
    g.box((1.6, front + 0.07, 0.13), (2.0, back - 0.08, 0.21), v31.INTERFACE, front=v31.INTERFACE)
    for i in range(4):
        g.box((1.65 + i * 0.08, front + 0.066, 0.165), (1.68 + i * 0.08, front + 0.07, 0.185), v31.INTERFACE_KNOB)
    g.box((1.92, front + 0.066, 0.19), (1.94, front + 0.07, 0.2), v31.LED_GREEN)
    # The wide monitor on its stand, the DAW timeline on the screen.
    g.box((cx - 0.08, 0.04, top_hi), (cx + 0.08, 0.14, top_hi + 0.012), v31.MONITOR)
    g.box((cx - 0.02, 0.08, top_hi + 0.012), (cx + 0.02, 0.1, top_hi + 0.08), v31.MONITOR)
    g.box((cx - 0.3, 0.05, top_hi + 0.07), (cx + 0.3, 0.09, top_hi + 0.33), v31.MONITOR)
    g.box((cx - 0.285, 0.042, top_hi + 0.085), (cx + 0.285, 0.05, top_hi + 0.315), v31.SCREEN, front=v31.SCREEN)
    for row, (a, b) in enumerate(((0.0, 0.55), (0.1, 0.38), (0.22, 0.48), (0.05, 0.3))):
        z = top_hi + 0.27 - row * 0.05
        g.box((cx - 0.26 + a * 0.95, 0.036, z), (cx - 0.26 + b * 0.95, 0.042, z + 0.022), v31.SCREEN_TEXT)
    # The keyboard controller centred under the monitor, the mouse to its right.
    kx0, kx1, ky0, ky1 = cx - 0.27, cx + 0.27, front + 0.03, front + 0.15
    g.box((kx0, ky0, top_hi), (kx1, ky1, top_hi + 0.03), v31.PIANO_BODY)
    keys = 14
    kw = (kx1 - kx0 - 0.02) / keys
    for i in range(keys):
        a = kx0 + 0.01 + i * kw
        g.box((a + 0.002, ky0 + 0.005, top_hi + 0.03), (a + kw - 0.002, ky1 - 0.04, top_hi + 0.036), v31.PIANO_WHITE)
        if i % 7 not in (2, 6) and i < keys - 1:
            g.box((a + kw * 0.65, ky0 + 0.035, top_hi + 0.036), (a + kw * 1.35, ky1 - 0.045, top_hi + 0.044), v31.PIANO_BLACK)
    g.box((cx + 0.36, front + 0.06, top_hi), (cx + 0.42, front + 0.13, top_hi + 0.03), v31.KEYS_DARK, top=v31.KEYS_DARK)
    # Headphones lying at the left end (the wider desk has room for them).
    g.rod((0.05, -0.02, top_hi + 0.02), (0.25, -0.02, top_hi + 0.02), 0.012, v31.SPEAKER, 6)
    for x in (0.05, 0.25):
        disc(g, x, -0.02, 0.045, top_hi, top_hi + 0.04, v31.SPEAKER_SIDE, v31.SPEAKER)
    desk = g.obj("dtm_desk_large")
    spk_l = v31._speaker("monitor_speaker_L", cx - 0.7, 0.04, -12.0)
    spk_r = v31._speaker("monitor_speaker_R", cx + 0.7, 0.04, 12.0)
    for spk in (spk_l, spk_r):
        spk.parent = desk
    hl.add_cell_guides("dtm_desk_large", [(x, 0) for x in range(5)])
    anchors = [anchor("WorkAnchor", desk, (cx, -0.45, 0.0))]
    return [desk, spk_l, spk_r], anchors, {"dtm_desk_large": ["ver3.2: 5 cells, centred on the middle cell",
                                                               "headphones added", "speaker backs plain"]}


def build_illust_desk_tablet():
    """illust_desk_tablet, 3 x 1 cells from the root (left) cell (ver3.1: 2). The pen display is
    centred on the MIDDLE cell (root + 1); a sketchbook on the left, the pen on the right."""
    g = Geo()
    cx = 0.5
    g.box((-0.22, -0.17, 0.36), (1.22, 0.17, 0.41), DESK_EDGE, top=DESK_TOP)
    for x in (-0.2, 1.15):
        for y in (-0.15, 0.1):
            g.box((x, y, 0.0), (x + 0.05, y + 0.05, 0.36), DESK_LEG)
    g.box((-0.2, 0.12, 0.2), (1.2, 0.15, 0.36), DESK_EDGE)
    top = 0.41
    tilt = math.radians(25.0)
    w, hgt, t = 0.46, 0.3, 0.022
    up = Vector((0.0, math.cos(tilt), math.sin(tilt)))
    right = Vector((1.0, 0.0, 0.0))
    normal = right.cross(up)
    origin = Vector((cx, -0.12, top + 0.025))

    def on_slab(u, v, lift=0.0):
        return tuple(origin + right * u + up * v + normal * lift)

    corners = [on_slab(-w / 2, 0.0), on_slab(w / 2, 0.0), on_slab(w / 2, hgt), on_slab(-w / 2, hgt)]
    g.prism(corners, tuple(-normal * t), v31.TABLET, v31.TABLET_EDGE, v31.TABLET_EDGE)
    g.face([on_slab(-w / 2 + 0.025, 0.025, 0.002), on_slab(w / 2 - 0.025, 0.025, 0.002),
            on_slab(w / 2 - 0.025, hgt - 0.025, 0.002), on_slab(-w / 2 + 0.025, hgt - 0.025, 0.002)], v31.CANVAS, normal)
    for u0, u1, v0, v1, color in ((-0.17, -0.02, 0.06, 0.2, v31.PAINT_BLUE), (0.0, 0.12, 0.14, 0.24, v31.PAINT_YELLOW),
                                  (-0.06, 0.15, 0.04, 0.11, v31.PAINT_PINK)):
        g.face([on_slab(u0, v0, 0.004), on_slab(u1, v0, 0.004), on_slab(u1, v1, 0.004), on_slab(u0, v1, 0.004)],
               color, normal)
    back = -normal * t
    for u in (-w / 2 + 0.06, w / 2 - 0.06):
        knee = Vector(on_slab(u, hgt * 0.7)) + back
        foot = (cx + u, knee.y + 0.04, top)
        g.rod(foot, tuple(knee), 0.012, v31.TABLET_STAND, 4)
        g.box((cx + u - 0.03, knee.y + 0.015, top), (cx + u + 0.03, knee.y + 0.065, top + 0.012), v31.TABLET_STAND)
    g.box((cx - w / 2, -0.14, top), (cx + w / 2, -0.11, top + 0.025), v31.TABLET_STAND)
    # The pen to the right of the display, a sketchbook to its left.
    g.rod((0.84, -0.1, top + 0.012), (0.98, -0.04, top + 0.012), 0.008, v31.PEN, 6)
    g.rod((0.98, -0.04, top + 0.012), (1.0, -0.032, top + 0.012), 0.005, v31.PEN_TIP, 6)
    g.box((-0.12, -0.12, top), (0.16, 0.08, top + 0.018), SKETCH_COVER, top=SKETCH_COVER)
    g.box((-0.105, -0.105, top + 0.018), (0.145, 0.065, top + 0.02), SKETCH_PAPER)
    desk = g.obj("illust_desk_tablet")
    hl.add_cell_guides("illust_desk_tablet", [(0, 0), (1, 0), (2, 0)])
    return [desk], [anchor("WorkAnchor", desk, (cx, -0.45, 0.0))], {
        "illust_desk_tablet": ["ver3.2: 3 cells, the display on the middle cell", "sketchbook added"]}


# ---------------------------------------------------------------- the house: kitchen, rug, low table

def build_kitchen_counter():
    """kitchen_counter, footprint 1 x 3 from the root (South) cell, against the East wall, facing
    West: hob on the root cell, sink on the middle cell (root + (0,1)), fridge on the North cell.
    The player stands West of a cell (facing East); the middle cell is the kitchen's centre."""
    g = Geo()
    front_x, back_x = -0.21, 0.24       # the West face (front) and the wall side
    y0, y1, yf = -0.235, 0.735, 0.765   # counter South / North ends, fridge South end
    top = 0.42
    # Cabinets (two cells) with a light worktop that overhangs the front a little.
    g.box((front_x, y0, 0.03), (back_x, y1, top - 0.03), CAB_SHADE, top=CAB)
    g.box((front_x + 0.02, y0 + 0.01, 0.0), (back_x, y1 - 0.01, 0.03), CAB_LINE)  # plinth
    g.box((front_x - 0.02, y0 - 0.005, top - 0.03), (back_x, y1, top), WORKTOP_EDGE, top=WORKTOP)
    # Door lines and handles on the West face (drawn as thin boxes just in front of it).
    for yc in (0.0, 0.5):
        g.box((front_x - 0.006, yc - 0.004, 0.06), (front_x, yc + 0.004, top - 0.06), CAB_LINE)
        g.box((front_x - 0.006, yc - 0.21, 0.29), (front_x, yc + 0.21, 0.297), CAB_LINE)  # drawer line
        g.box((front_x - 0.02, yc - 0.06, 0.32), (front_x - 0.006, yc + 0.06, 0.335), HANDLE)
        g.box((front_x - 0.02, yc - 0.05, 0.2), (front_x - 0.006, yc - 0.035, 0.26), HANDLE)
        g.box((front_x - 0.02, yc + 0.035, 0.2), (front_x - 0.006, yc + 0.05, 0.26), HANDLE)
    # Tiles on the wall behind the worktop.
    g.box((back_x - 0.03, y0, top), (back_x, y1, top + 0.22), TILE_LINE, top=TILE)
    for k in range(1, 6):
        g.box((back_x - 0.034, y0 + 0.01, top + k * 0.037), (back_x - 0.03, y1 - 0.01, top + k * 0.037 + 0.004), TILE)
    # The hob on the root cell: black plate, two burners, a red kettle on the front-left one.
    g.box((-0.16, -0.19, top), (0.16, 0.19, top + 0.012), HOB, top=HOB)
    for (bx, by) in ((-0.06, -0.09), (0.07, 0.08)):
        disc(g, bx, by, 0.07, top + 0.012, top + 0.018, BURNER, BURNER_RING)
    g.loft([octagon(-0.06, -0.09, 0.065, top + 0.018), octagon(-0.06, -0.09, 0.07, top + 0.07),
            octagon(-0.06, -0.09, 0.045, top + 0.11)], KETTLE, KETTLE, KETTLE_DARK)
    g.rod((-0.06, -0.09, top + 0.11), (-0.06, -0.09, top + 0.13), 0.012, KETTLE_DARK, 6)  # lid knob
    g.rod((-0.115, -0.09, top + 0.06), (-0.16, -0.09, top + 0.1), 0.01, KETTLE_DARK, 4)   # spout
    for kx in (-0.1, -0.04, 0.02):  # hob knobs on the front edge
        g.box((front_x - 0.03, kx + 0.0 - 0.01 + 0.0, top - 0.025), (front_x - 0.018, kx + 0.01, top - 0.01), BURNER_RING)
    # The sink on the middle cell: a steel basin set into the worktop, a faucet from the back.
    g.box((-0.15, 0.32, top), (0.15, 0.68, top + 0.006), STEEL, top=STEEL)
    g.box((-0.12, 0.35, top - 0.004), (0.12, 0.65, top + 0.008), STEEL_DARK, top=STEEL_DARK)
    g.box((-0.1, 0.38, top + 0.008), (0.1, 0.62, top + 0.009), WATER)
    g.rod((0.18, 0.5, top), (0.18, 0.5, top + 0.16), 0.014, STEEL, 6)
    g.rod((0.18, 0.5, top + 0.16), (0.07, 0.5, top + 0.15), 0.012, STEEL, 6)
    g.box((0.16, 0.56, top), (0.2, 0.6, top + 0.04), STEEL_DARK)  # tap handle
    # The fridge on the North cell: tall, two doors, handles on the West face, two notes on it.
    fz = 0.82
    g.box((front_x + 0.01, yf, 0.0), (back_x, 1.235, fz), FRIDGE_SHADE, top=FRIDGE)
    g.box((front_x + 0.004, yf + 0.01, 0.02), (front_x + 0.01, 1.225, fz - 0.02), FRIDGE)
    g.box((front_x - 0.002, yf + 0.01, 0.52), (front_x + 0.004, 1.225, 0.53), FRIDGE_LINE)  # freezer line
    for z0, z1 in ((0.56, 0.74), (0.12, 0.46)):
        g.box((front_x - 0.02, 1.14, z0), (front_x - 0.002, 1.165, z1), HANDLE)
    g.box((front_x - 0.004, 0.86, 0.6), (front_x, 0.95, 0.69), NOTE_YELLOW)
    g.box((front_x - 0.004, 0.97, 0.3), (front_x, 1.06, 0.38), NOTE_PINK)
    kitchen = g.obj("kitchen_counter")
    hl.add_cell_guides("kitchen_counter", [(0, 0), (0, 1), (0, 2)])
    anchors = [anchor("WorkAnchor", kitchen, (-0.45, 0.5, 0.0))]
    return [kitchen], anchors, {"kitchen_counter": ["new design for ver3.2", "cabinet backs plain", "fridge back plain"]}


def build_living_rug():
    """living_rug, 3 x 2 cells from the root (South-West) cell, walkable (blocks nothing): a thin rose
    rug with a cream border and two stripes."""
    g = Geo()
    x0, x1, y0, y1, h = -0.23, 1.23, -0.23, 0.73, 0.012
    g.box((x0, y0, 0.0), (x1, y1, h), RUG_EDGE, top=RUG_BORDER)
    g.face([(x0 + 0.05, y0 + 0.05, h + 0.001), (x1 - 0.05, y0 + 0.05, h + 0.001),
            (x1 - 0.05, y1 - 0.05, h + 0.001), (x0 + 0.05, y1 - 0.05, h + 0.001)], RUG, (0, 0, 1))
    for ya, yb in ((y0 + 0.14, y0 + 0.17), (y1 - 0.17, y1 - 0.14)):
        g.face([(x0 + 0.05, ya, h + 0.002), (x1 - 0.05, ya, h + 0.002), (x1 - 0.05, yb, h + 0.002),
                (x0 + 0.05, yb, h + 0.002)], RUG_STRIPE, (0, 0, 1))
    for i in range(7):  # a row of small diamonds down the middle
        cx, cy, r = x0 + 0.2 + i * (x1 - x0 - 0.4) / 6, (y0 + y1) / 2, 0.04
        g.face([(cx, cy - r, h + 0.002), (cx + r, cy, h + 0.002), (cx, cy + r, h + 0.002), (cx - r, cy, h + 0.002)],
               RUG_BORDER, (0, 0, 1))
    rug = g.obj("living_rug")
    hl.add_cell_guides("living_rug", [(x, y) for x in range(3) for y in range(2)])
    return [rug], [], {"living_rug": ["new design for ver3.2"]}


def build_low_table():
    """low_table_living, 2 x 1 cells from the root (left) cell: a low wooden table with a half-drunk
    mug of cocoa and a plate of cookies. Both cells blocked; examined from the South."""
    g = Geo()
    top = 0.17
    g.box((-0.21, -0.16, top - 0.03), (0.71, 0.16, top), TABLE_EDGE, top=TABLE_TOP)
    for x in (-0.19, 0.64):
        for y in (-0.14, 0.09):
            g.box((x, y, 0.0), (x + 0.05, y + 0.05, top - 0.03), TABLE_LEG)
    # The mug (left) with cocoa inside and a handle.
    g.loft([octagon(0.08, 0.0, 0.04, top), octagon(0.08, 0.0, 0.042, top + 0.075)], MUG, MUG_SHADE, MUG_SHADE)
    g.face(octagon(0.08, 0.0, 0.034, top + 0.076), COCOA, (0, 0, 1))
    g.rod((0.125, 0.0, top + 0.055), (0.15, 0.0, top + 0.04), 0.009, MUG, 4)
    g.rod((0.15, 0.0, top + 0.04), (0.125, 0.0, top + 0.02), 0.009, MUG, 4)
    # The plate (right) with three cookies.
    disc(g, 0.42, 0.0, 0.11, top, top + 0.012, PLATE, PLATE_RIM, 10)
    for (cx, cy) in ((0.38, -0.03), (0.46, 0.02), (0.41, 0.05)):
        disc(g, cx, cy, 0.035, top + 0.012, top + 0.024, COOKIE, COOKIE_DARK, 8)
    table = g.obj("low_table_living")
    hl.add_cell_guides("low_table_living", [(0, 0), (1, 0)])
    return [table], [], {"low_table_living": ["new design for ver3.2", "underside plain"]}


# ---------------------------------------------------------------- the booth: the guitar on its stand

def build_guitar_stand():
    """guitar_stand, 1 x 1: an acoustic guitar standing on a black A-frame stand, leaning back a
    little, its front to the South. Blocked; examined from the South."""
    g = Geo()
    lean = math.radians(12.0)
    up = Vector((0.0, math.sin(lean), math.cos(lean)))       # along the neck, leaning North
    right = Vector((1.0, 0.0, 0.0))
    front = right.cross(up)                                   # the top faces South (and a little up)
    base = Vector((0.0, -0.02, 0.07))
    depth = 0.07

    def at(u, v, lift=0.0):
        return tuple(base + right * u + up * v + front * lift)

    def outline(cv, r, n=12):
        return [at(r * math.cos((i + 0.5) / n * math.tau), cv + r * math.sin((i + 0.5) / n * math.tau)) for i in range(n)]

    # The body: lower bout and upper bout (two overlapping discs), sides and back in darker wood.
    for cv, r in ((0.13, 0.13), (0.3, 0.1)):
        pts = outline(cv, r)
        g.prism(pts, tuple(-front * depth), GUITAR_TOP, GUITAR_SIDE, GUITAR_BACK)
    hole = outline(0.27, 0.04, 10)
    g.face([tuple(Vector(p) + front * 0.002) for p in hole], SOUNDHOLE, tuple(front))
    ba, bb = Vector(at(-0.06, 0.07, 0.001)), Vector(at(0.06, 0.09, 0.012))
    g.box(tuple(min(ba[i], bb[i]) for i in range(3)), tuple(max(ba[i], bb[i]) for i in range(3)), GUITAR_SIDE)  # bridge
    # The neck, fretboard and head.
    neck0, neck1 = Vector(at(0.0, 0.38, -depth * 0.4)), Vector(at(0.0, 0.66, -depth * 0.4))
    g.rod(tuple(neck0), tuple(neck1), 0.026, NECK, 4)
    g.rod(at(0.0, 0.38, 0.0), at(0.0, 0.66, 0.0), 0.02, FRET_BOARD, 4)
    g.rod(at(0.0, 0.66, -depth * 0.3), at(0.0, 0.76, -depth * 0.3), 0.034, HEAD, 4)
    for v in (0.69, 0.73):
        for u in (-0.045, 0.045):
            g.rod(at(u, v, -depth * 0.3), at(u * 1.5, v, -depth * 0.3), 0.008, PEG, 4)
    # The stand: an A-frame behind the guitar, a cradle under the body, a yoke under the neck.
    # The A-frame stands behind the guitar (the body rests on the cradle in front, the neck on the yoke).
    for x in (-0.11, 0.11):
        g.rod((x, 0.02, 0.0), (x * 0.3, 0.17, 0.47), 0.012, STAND, 4)
        g.rod((x, 0.2, 0.0), (x * 0.3, 0.17, 0.47), 0.012, STAND, 4)
        g.box((x - 0.02, 0.0, 0.0), (x + 0.02, 0.22, 0.012), STAND_PAD)
    g.rod((-0.11, 0.03, 0.06), (0.11, 0.03, 0.06), 0.01, STAND, 4)                       # front brace
    g.box((-0.12, -0.1, 0.04), (0.12, -0.03, 0.07), STAND, top=STAND_PAD)                # cradle
    g.rod((0.0, -0.03, 0.05), (0.0, 0.03, 0.06), 0.012, STAND, 4)                        # cradle arm
    g.box((-0.04, 0.12, 0.45), (0.04, 0.18, 0.47), STAND_PAD)                            # yoke
    guitar = g.obj("guitar_stand")
    hl.add_cell_guides("guitar_stand", [(0, 0)])
    return [guitar], [], {"guitar_stand": ["new design for ver3.2", "strings not modelled", "simplified bracing"]}


FAMILIES = (
    ("dtm_desk_large", build_dtm_desk_large),
    ("illust_desk_tablet", build_illust_desk_tablet),
    ("kitchen_counter", build_kitchen_counter),
    ("living_rug", build_living_rug),
    ("low_table_living", build_low_table),
    ("guitar_stand", build_guitar_stand),
)


def main():
    for folder in (SOURCES, EXPORTS, UNITY_MODELS):
        os.makedirs(folder, exist_ok=True)
    manifest = {
        "generator": "blender/scripts/build_v32_interior.py",
        "blenderVersion": bpy.app.version_string,
        "units": "1 Blender unit = 1 Unity unit, 1 cell = 0.5, origin = ground centre of the root cell",
        "export": {"format": "FBX 7.4 binary", "axisForward": "-Z", "axisUp": "Y", "bakeSpaceTransform": True,
                   "colors": "SRGB face-corner attribute 'Col'", "material": "HALKA_VertexColor (1 slot)"},
        "approval": "pending: new ver3.2 interior designs, not approved by the user yet",
        "models": [],
    }
    for family, build in FAMILIES:
        hl.reset_scene()
        objects, anchors, supplemented = build()
        roots = [o for o in objects if o.parent is None]
        bpy.ops.wm.save_as_mainfile(filepath=os.path.join(SOURCES, family + ".blend"), compress=False)
        bpy.context.view_layer.update()
        for obj in roots:
            parts = [o for o in objects if o.parent == obj]
            own = [a for a in anchors if a.parent == obj]
            fbx = os.path.join(UNITY_MODELS, obj.name + ".fbx")
            hl.export_fbx(obj, fbx, parts + own)
            lo, hi = p3.world_bounds([obj] + parts)
            entry = {
                "family": family, "name": obj.name,
                "blend": f"blender/sources/v32/{family}.blend",
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
            print(f"HARUKA_V32_ASSET {obj.name}: {entry['triangles']} tris, bounds {lo} {hi}")
    with open(os.path.join(EXPORTS, "v32_assets_manifest.json"), "w", encoding="utf-8") as f:
        json.dump(manifest, f, indent=2, ensure_ascii=False)
        f.write("\n")


if __name__ == "__main__":
    main()
