"""はるかわーるど ver3.1: the studio update (sound booth, its interior, the large DTM desk with monitor
speakers, the illustration desk with a pen tablet).

Run from the repository root:
  "C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" --background --factory-startup \
      --python blender/scripts/build_v31_studio.py

The same conventions as build_phase3_buildings.py (whose Geo helper and palettes this script uses):
  Blender +X = East, +Y = North, +Z = Up, 1 unit = 1 Unity unit, 1 cell = 0.5.
  The origin is the ground centre of the Definition's ROOT cell. Fronts face South (-Y).
  Colors are face-corner vertex colors ("Col"), one material slot (HALKA_VertexColor).
No map coordinate appears here: where things stand is owned by Map JSON v4 and the Object Catalog.

These are new designs made for ver3.1 (not derived from approved sprites). They are candidates
until the user approves them; the manifest says so ("approval": "pending").

Outputs:
  blender/sources/v31/<family>.blend
  unity-project/Assets/POC2D3D/Models/V31/<model>.fbx
  blender/exports/v31_assets_manifest.json
"""

import json
import math
import os
import sys

import bpy
from mathutils import Vector

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import halka_lowpoly as hl  # noqa: E402
import build_phase3_buildings as p3  # noqa: E402  (Geo, anchor, palettes, world_bounds; its main() does not run)

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
SOURCES = os.path.join(ROOT, "blender", "sources", "v31")
EXPORTS = os.path.join(ROOT, "blender", "exports")
UNITY_MODELS = os.path.join(ROOT, "unity-project", "Assets", "POC2D3D", "Models", "V31")
HALF = hl.CELL * 0.5
Geo = p3.Geo

# ---------------------------------------------------------------- palettes (ver3.1)

# The booth: black / dark grey like the room's walls (#26262d family), the house's yellow accent.
BOOTH_BODY, BOOTH_SHADE, BOOTH_TOP, BOOTH_TRIM = "#232328", "#1a1a1f", "#383840", "#4a4a54"
BOOTH_DOOR, BOOTH_DOOR_DARK, BOOTH_GLASS = "#4a4a55", "#18181c", "#66727f"
DOOR_FRAME = "#62626e"  # a lighter frame so the entrance reads in 2D and 3D
GOLD, GOLD_DARK = p3.GOLD, p3.GOLD_DARK
LAMP_ON = "#f7d76a"
# The booth's inside: dark carpet, black acoustic foam on charcoal walls.
CARPET_A, CARPET_B = "#3b3a40", "#37363c"
FOAM_WALL, FOAM_DARK, FOAM_LIGHT, FOAM_TOP = "#2a2a30", "#1b1b20", "#3a3a42", "#26262d"
FOAM_BASE = "#3a3a42"
# Desks: the wood of desk_basic (desk.png) so the new desks belong to the same room.
DESK_TOP, DESK_EDGE, DESK_LEG = p3.DESK_TOP, p3.DESK_EDGE, p3.DESK_LEG
MONITOR, SCREEN, SCREEN_TEXT = p3.MONITOR, p3.SCREEN, p3.SCREEN_TEXT
KEYS, KEYS_DARK = p3.KEYS, p3.KEYS_DARK
# Monitor speakers: black cabinets, grey cone, light dust caps.
SPEAKER, SPEAKER_SIDE, CONE, CONE_RING, CAP = "#1f1d21", "#29272c", "#4a4850", "#5c5a63", "#9a98a3"
PIANO_WHITE, PIANO_BLACK, PIANO_BODY = "#eceae4", "#1d1b1f", "#2a282d"
INTERFACE, INTERFACE_KNOB, LED_GREEN = "#2f2d33", "#b9b6bd", "#7fd18a"
# The pen tablet: a black body, a bright canvas with the easel's colours on it.
TABLET, TABLET_EDGE, TABLET_STAND = "#1a191d", "#2c2b31", "#3a3940"
CANVAS = "#f4ede0"
PAINT_BLUE, PAINT_YELLOW, PAINT_PINK = p3.PAINT_BLUE, p3.PAINT_YELLOW, p3.PAINT_PINK
PEN, PEN_TIP = "#2b2a30", "#d9d6dd"

INTERIOR_WALL_HEIGHT = p3.INTERIOR_WALL_HEIGHT
INTERIOR_WALL_BASE = p3.INTERIOR_WALL_BASE
BOOTH_HEIGHT = 1.08     # a little lower than the room's walls (1.2): it reads as a small room inside the room
BOOTH_DOOR_PLANE = 0.17  # the door at the back of a walkable alcove in the root (door) cell, like house_main


def anchor(name, parent, location):
    return p3.anchor(name, parent, location)


# ---------------------------------------------------------------- the booth seen in the house

def build_soundbooth_exterior():
    """soundbooth_exterior: a black square booth, 2 x 2 cells. The root (bottom-left) cell is the
    walkable alcove with the door (walking in leads to halka_soundbooth); the other three cells are
    blocked. Front (with the door) faces South."""
    g = Geo()
    x0, x1, y0, y1, h = -0.235, 0.735, -0.215, 0.735, BOOTH_HEIGHT
    ax0, ax1 = -0.17, 0.17  # the alcove (door) inside the root cell
    # Walls around the alcove: left post, the right block (both front cells' right part), back.
    g.box((x0, y0, 0.0), (ax0, y1, h), BOOTH_BODY, top=BOOTH_TOP, front=BOOTH_BODY)
    g.box((ax1, y0, 0.0), (x1, y1, h), BOOTH_BODY, top=BOOTH_TOP, front=BOOTH_BODY)
    g.box((ax0, BOOTH_DOOR_PLANE, 0.0), (ax1, y1, h), BOOTH_BODY, top=BOOTH_TOP)
    g.box((ax0, y0, 0.84), (ax1, BOOTH_DOOR_PLANE, h), BOOTH_BODY, top=BOOTH_TOP, bottom=BOOTH_SHADE)
    # A thin lighter trim along the top edge and the plinth, so the square reads in 2D.
    g.box((x0 - 0.012, y0 - 0.012, h), (x1 + 0.012, y1 + 0.012, h + 0.035), BOOTH_TRIM, top=BOOTH_TOP)
    g.box((x0 - 0.008, y0 - 0.008, 0.0), (ax0, y0 + 0.02, 0.05), BOOTH_TRIM)
    g.box((ax1, y0 - 0.008, 0.0), (x1 + 0.008, y0 + 0.02, 0.05), BOOTH_TRIM)
    # Door frame and the door at the back of the alcove.
    g.box((ax0, BOOTH_DOOR_PLANE - 0.01, 0.0), (ax0 + 0.025, BOOTH_DOOR_PLANE, 0.84), DOOR_FRAME)
    g.box((ax1 - 0.025, BOOTH_DOOR_PLANE - 0.01, 0.0), (ax1, BOOTH_DOOR_PLANE, 0.84), DOOR_FRAME)
    g.box((ax0, BOOTH_DOOR_PLANE - 0.01, 0.815), (ax1, BOOTH_DOOR_PLANE, 0.84), DOOR_FRAME)
    g.box((ax0 + 0.025, BOOTH_DOOR_PLANE - 0.006, 0.0), (ax1 - 0.025, BOOTH_DOOR_PLANE, 0.815), BOOTH_DOOR)
    g.box((ax0 + 0.06, BOOTH_DOOR_PLANE - 0.011, 0.5), (ax1 - 0.06, BOOTH_DOOR_PLANE - 0.006, 0.72), BOOTH_GLASS)
    g.box((ax1 - 0.06, BOOTH_DOOR_PLANE - 0.016, 0.38), (ax1 - 0.035, BOOTH_DOOR_PLANE - 0.006, 0.42), GOLD)
    # The recording lamp over the door (the house's yellow accent): the one decoration.
    g.box((-0.05, y0 - 0.02, 0.92), (0.05, y0 + 0.005, 0.98), BOOTH_TRIM)
    g.box((-0.035, y0 - 0.026, 0.93), (0.035, y0 - 0.02, 0.97), LAMP_ON)
    # Two quiet panel seams on the right front, matching the room's dark wall shades.
    for x in (0.38, 0.58):
        g.box((x - 0.006, y0 - 0.004, 0.08), (x + 0.006, y0, h - 0.06), BOOTH_SHADE)
    booth = g.obj("soundbooth_exterior")
    hl.add_cell_guides("soundbooth_exterior", [(x, y) for x in range(2) for y in range(2)])
    return [booth], [anchor("DoorAnchor", booth, (0.0, 0.0, 0.0))], {
        "soundbooth_exterior": ["new design for ver3.1 (no sprite to follow)", "back and sides plain"]}


# ---------------------------------------------------------------- the booth's own map (pieces)

def _foam_panels(g, z0, z1):
    """Acoustic foam on the four faces of a wall cell: a 2 x 3 grid of flat panels in two shades
    (a checker of foam squares), each a single quad just off the wall (2 triangles), so a long
    booth wall stays light for WebGL."""
    rows, cols, gap, lift = 3, 2, 0.018, 0.006
    height = (z1 - z0 - gap * (rows + 1)) / rows
    width = (2 * HALF - gap * (cols + 1)) / cols
    sides = (((0, -1, 0), lambda a, z: (a, -HALF - lift, z)), ((0, 1, 0), lambda a, z: (-a, HALF + lift, z)),
             ((-1, 0, 0), lambda a, z: (-HALF - lift, -a, z)), ((1, 0, 0), lambda a, z: (HALF + lift, a, z)))
    for normal, at in sides:
        for r in range(rows):
            for c in range(cols):
                a = -HALF + gap + c * (width + gap)
                b = a + width
                zlo = z0 + gap + r * (height + gap)
                zhi = zlo + height
                color = FOAM_LIGHT if (r + c) % 2 == 0 else FOAM_DARK
                g.face([at(a, zlo), at(b, zlo), at(b, zhi), at(a, zhi)], color, normal)


def build_soundbooth_interior():
    """Floor tiles and wall pieces for halka_soundbooth, the same shapes as the house's interior
    pieces (base always shown, upper part cut away for the camera) in the booth's colours."""
    models = []
    for suffix, color in (("a", CARPET_A), ("b", CARPET_B)):
        g = Geo()
        g.face([(-HALF, -HALF, 0.0), (HALF, -HALF, 0.0), (HALF, HALF, 0.0), (-HALF, HALF, 0.0)], color, (0, 0, 1))
        models.append(g.obj("booth_floor_" + suffix))
    g = Geo()
    g.box((-HALF, -HALF, 0.0), (HALF, HALF, INTERIOR_WALL_BASE), FOAM_BASE, top=FOAM_TOP)
    models.append(g.obj("booth_wall_base"))
    g = Geo()
    g.box((-HALF, -HALF, INTERIOR_WALL_BASE), (HALF, HALF, INTERIOR_WALL_HEIGHT), FOAM_WALL, top=FOAM_TOP, bottom=FOAM_WALL)
    _foam_panels(g, INTERIOR_WALL_BASE + 0.06, INTERIOR_WALL_HEIGHT - 0.06)
    models.append(g.obj("booth_wall_upper"))
    # Doorway (interior_exit): threshold, posts and the closed dark door on the South edge.
    g = Geo()
    g.face([(-HALF, -HALF, 0.0), (HALF, -HALF, 0.0), (HALF, HALF, 0.0), (-HALF, HALF, 0.0)], CARPET_A, (0, 0, 1))
    g.box((-0.22, -HALF, 0.0), (0.22, -0.16, 0.02), BOOTH_TRIM, top=BOOTH_TOP)
    for xa, xb in ((-HALF, -0.2), (0.2, HALF)):
        g.box((xa, -HALF, 0.0), (xb, HALF, INTERIOR_WALL_BASE), FOAM_BASE, top=FOAM_TOP)
    models.append(g.obj("booth_doorway_base"))
    g = Geo()
    for xa, xb in ((-HALF, -0.2), (0.2, HALF)):
        g.box((xa, -HALF, INTERIOR_WALL_BASE), (xb, HALF, INTERIOR_WALL_HEIGHT), FOAM_WALL, top=FOAM_TOP)
        g.box((xa + (0.0 if xa < 0 else -0.02), -HALF + 0.02, INTERIOR_WALL_BASE),
              (xb + (0.02 if xa < 0 else 0.0), HALF - 0.02, 0.8), BOOTH_TRIM)
    g.box((-0.2, -HALF, 0.8), (0.2, HALF, INTERIOR_WALL_HEIGHT), FOAM_WALL, top=FOAM_TOP, bottom=BOOTH_TRIM)
    g.box((-0.2, -HALF, 0.0), (0.2, -HALF + 0.03, 0.8), BOOTH_DOOR)
    g.box((-0.12, -HALF + 0.03, 0.5), (0.12, -HALF + 0.036, 0.7), BOOTH_GLASS)
    g.box((0.12, -HALF + 0.03, 0.36), (0.16, -HALF + 0.05, 0.4), GOLD)
    models.append(g.obj("booth_doorway_upper"))
    return models, [], {"soundbooth_interior": ["new design for ver3.1", "foam panels on every face of a wall cell"]}


# ---------------------------------------------------------------- the large DTM desk

def _speaker(name, x, y, toe_in):
    """A nearfield monitor speaker standing on the desk top, its front turned toe_in degrees toward
    the listening position (the desk's centre)."""
    g = Geo()
    w, d, h = 0.12, 0.12, 0.2
    g.box((-w / 2, -d / 2, 0.0), (w / 2, d / 2, h), SPEAKER_SIDE, top=SPEAKER, front=SPEAKER)
    g.rod((0.0, -d / 2, 0.07), (0.0, -d / 2 - 0.006, 0.07), 0.038, CONE_RING, 10)
    g.rod((0.0, -d / 2 - 0.006, 0.07), (0.0, -d / 2 - 0.01, 0.07), 0.03, CONE, 10)
    g.rod((0.0, -d / 2 - 0.01, 0.07), (0.0, -d / 2 - 0.013, 0.07), 0.011, CAP, 8)
    g.rod((0.0, -d / 2, 0.155), (0.0, -d / 2 - 0.008, 0.155), 0.016, CONE_RING, 8)
    g.rod((0.0, -d / 2 - 0.008, 0.155), (0.0, -d / 2 - 0.011, 0.155), 0.009, CAP, 8)
    g.box((-0.035, -d / 2 - 0.004, 0.012), (0.035, -d / 2, 0.018), CONE)  # bass port slot
    obj = g.obj(name)
    obj.location = (x, y, 0.41)  # on the desk top
    obj.rotation_euler = (0.0, 0.0, math.radians(toe_in))
    return obj


def build_dtm_desk_large():
    """dtm_desk_large, 4 x 1 cells from the root (left) cell: twice desk_basic's width (not its height),
    a music-production desk with a wide monitor, a keyboard, an audio interface and two monitor
    speakers (separate objects, left and right, mirrored). All four cells are blocked; the player
    stands South of the two middle cells to use it."""
    g = Geo()
    left, right, front, back, top_lo, top_hi = -0.22, 1.72, -0.2, 0.2, 0.36, 0.41
    g.box((left, front, top_lo), (right, back, top_hi), DESK_EDGE, top=DESK_TOP)
    for x in (left + 0.02, 0.73, right - 0.07):  # three leg pairs (a long top needs a middle support)
        for y in (front + 0.02, back - 0.07):
            g.box((x, y, 0.0), (x + 0.05, y + 0.05, top_lo), DESK_LEG)
    g.box((left + 0.02, back - 0.05, 0.18), (right - 0.02, back - 0.02, top_lo), DESK_EDGE)  # back panel
    # A low shelf under the right half for the rack / audio gear.
    g.box((0.95, front + 0.04, 0.1), (right - 0.07, back - 0.05, 0.13), DESK_EDGE, top=DESK_TOP)
    g.box((1.05, front + 0.07, 0.13), (1.45, back - 0.08, 0.21), INTERFACE, front=INTERFACE)
    for i in range(4):
        g.box((1.1 + i * 0.08, front + 0.066, 0.165), (1.13 + i * 0.08, front + 0.07, 0.185), INTERFACE_KNOB)
    # The wide monitor in the middle (on a stand), its screen showing a DAW timeline.
    cx = 0.75
    g.box((cx - 0.08, 0.04, top_hi), (cx + 0.08, 0.14, top_hi + 0.012), MONITOR)
    g.box((cx - 0.02, 0.08, top_hi + 0.012), (cx + 0.02, 0.1, top_hi + 0.08), MONITOR)
    g.box((cx - 0.3, 0.05, top_hi + 0.07), (cx + 0.3, 0.09, top_hi + 0.33), MONITOR)
    g.box((cx - 0.285, 0.042, top_hi + 0.085), (cx + 0.285, 0.05, top_hi + 0.315), SCREEN, front=SCREEN)
    for row, (a, b) in enumerate(((0.0, 0.55), (0.1, 0.38), (0.22, 0.48), (0.05, 0.3))):
        z = top_hi + 0.27 - row * 0.05
        g.box((cx - 0.26 + a * 0.95, 0.036, z), (cx - 0.26 + b * 0.95, 0.042, z + 0.022), SCREEN_TEXT)
    # A small keyboard controller in front of the monitor (white and black keys) and a mouse.
    kx0, kx1, ky0, ky1 = cx - 0.27, cx + 0.27, front + 0.03, front + 0.15
    g.box((kx0, ky0, top_hi), (kx1, ky1, top_hi + 0.03), PIANO_BODY)
    keys = 14
    kw = (kx1 - kx0 - 0.02) / keys
    for i in range(keys):
        a = kx0 + 0.01 + i * kw
        g.box((a + 0.002, ky0 + 0.005, top_hi + 0.03), (a + kw - 0.002, ky1 - 0.04, top_hi + 0.036), PIANO_WHITE)
        if i % 7 not in (2, 6) and i < keys - 1:
            g.box((a + kw * 0.65, ky0 + 0.035, top_hi + 0.036), (a + kw * 1.35, ky1 - 0.045, top_hi + 0.044), PIANO_BLACK)
    g.box((cx + 0.36, front + 0.06, top_hi), (cx + 0.42, front + 0.13, top_hi + 0.03), KEYS_DARK, top=KEYS_DARK)
    desk = g.obj("dtm_desk_large")
    # Left and right speakers: mirrored about the desk's centre, both turned 12 degrees inward.
    spk_l = _speaker("monitor_speaker_L", cx - 0.62, 0.04, -12.0)
    spk_r = _speaker("monitor_speaker_R", cx + 0.62, 0.04, 12.0)
    for spk in (spk_l, spk_r):
        spk.parent = desk
    hl.add_cell_guides("dtm_desk_large", [(x, 0) for x in range(4)])
    anchors = [anchor("WorkAnchor", desk, (cx, -0.45, 0.0))]
    return [desk, spk_l, spk_r], anchors, {"dtm_desk_large": ["new design for ver3.1", "speaker backs plain",
                                                               "interface knobs simplified"]}


# ---------------------------------------------------------------- the illustration desk with a pen tablet

def build_illust_desk_tablet():
    """illust_desk_tablet, 2 x 1 cells from the root (left) cell: a desk like desk_basic (same wood)
    with a pen display tilted on its stand, a pen, nothing else. Both cells blocked; the player stands
    South of the left cell."""
    g = Geo()
    g.box((-0.22, -0.17, 0.36), (0.72, 0.17, 0.41), DESK_EDGE, top=DESK_TOP)
    for x in (-0.2, 0.65):
        for y in (-0.15, 0.1):
            g.box((x, y, 0.0), (x + 0.05, y + 0.05, 0.36), DESK_LEG)
    g.box((-0.2, 0.12, 0.2), (0.7, 0.15, 0.36), DESK_EDGE)
    # The pen display: a black slab tilted 25 degrees up from the desk on a stand, its screen with a drawing.
    top = 0.41
    tilt = math.radians(25.0)
    cx = 0.22
    w, hgt, t = 0.46, 0.3, 0.022
    up = Vector((0.0, math.cos(tilt), math.sin(tilt)))   # up the slope, toward North
    right = Vector((1.0, 0.0, 0.0))
    normal = right.cross(up)                              # the screen faces South and up
    origin = Vector((cx, -0.12, top + 0.025))

    def on_slab(u, v, lift=0.0):
        return tuple(origin + right * u + up * v + normal * lift)

    corners = [on_slab(-w / 2, 0.0), on_slab(w / 2, 0.0), on_slab(w / 2, hgt), on_slab(-w / 2, hgt)]
    g.prism(corners, tuple(-normal * t), TABLET, TABLET_EDGE, TABLET_EDGE)
    g.face([on_slab(-w / 2 + 0.025, 0.025, 0.002), on_slab(w / 2 - 0.025, 0.025, 0.002),
            on_slab(w / 2 - 0.025, hgt - 0.025, 0.002), on_slab(-w / 2 + 0.025, hgt - 0.025, 0.002)], CANVAS, normal)
    # The drawing on the screen: the easel's colours (blue, yellow, pink patches).
    for u0, u1, v0, v1, color in ((-0.17, -0.02, 0.06, 0.2, PAINT_BLUE), (0.0, 0.12, 0.14, 0.24, PAINT_YELLOW),
                                  (-0.06, 0.15, 0.04, 0.11, PAINT_PINK)):
        g.face([on_slab(u0, v0, 0.004), on_slab(u1, v0, 0.004), on_slab(u1, v1, 0.004), on_slab(u0, v1, 0.004)],
               color, normal)
    # The stand: two legs from the desk top to the back of the slab.
    back = -normal * t
    for u in (-w / 2 + 0.06, w / 2 - 0.06):
        knee = Vector(on_slab(u, hgt * 0.7)) + back
        foot = (cx + u, knee.y + 0.04, top)
        g.rod(foot, tuple(knee), 0.012, TABLET_STAND, 4)
        g.box((cx + u - 0.03, knee.y + 0.015, top), (cx + u + 0.03, knee.y + 0.065, top + 0.012), TABLET_STAND)
    # The front lip the slab rests on, and the pen lying on the desk to its right.
    g.box((cx - w / 2, -0.14, top), (cx + w / 2, -0.11, top + 0.025), TABLET_STAND)
    g.rod((0.52, -0.1, top + 0.012), (0.66, -0.04, top + 0.012), 0.008, PEN, 6)
    g.rod((0.66, -0.04, top + 0.012), (0.68, -0.032, top + 0.012), 0.005, PEN_TIP, 6)
    desk = g.obj("illust_desk_tablet")
    hl.add_cell_guides("illust_desk_tablet", [(0, 0), (1, 0)])
    return [desk], [anchor("WorkAnchor", desk, (0.0, -0.45, 0.0))], {
        "illust_desk_tablet": ["new design for ver3.1", "stand simplified to two legs"]}


FAMILIES = (
    ("soundbooth_exterior", build_soundbooth_exterior),
    ("soundbooth_interior", build_soundbooth_interior),
    ("dtm_desk_large", build_dtm_desk_large),
    ("illust_desk_tablet", build_illust_desk_tablet),
)


def main():
    for folder in (SOURCES, EXPORTS, UNITY_MODELS):
        os.makedirs(folder, exist_ok=True)
    manifest = {
        "generator": "blender/scripts/build_v31_studio.py",
        "blenderVersion": bpy.app.version_string,
        "units": "1 Blender unit = 1 Unity unit, 1 cell = 0.5, origin = ground centre of the root cell",
        "export": {"format": "FBX 7.4 binary", "axisForward": "-Z", "axisUp": "Y", "bakeSpaceTransform": True,
                   "colors": "SRGB face-corner attribute 'Col'", "material": "HALKA_VertexColor (1 slot)"},
        "approval": "pending: new ver3.1 designs, not approved by the user yet",
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
            spacing += 2.2
        bpy.ops.wm.save_as_mainfile(filepath=os.path.join(SOURCES, family + ".blend"), compress=False)
        for obj in roots:
            obj.location.x = 0.0
        bpy.context.view_layer.update()
        for obj in roots:
            parts = [o for o in objects if o.parent == obj]
            own = [a for a in anchors if a.parent == obj]
            fbx = os.path.join(UNITY_MODELS, obj.name + ".fbx")
            hl.export_fbx(obj, fbx, parts + own)
            lo, hi = p3.world_bounds([obj] + parts)
            entry = {
                "family": family, "name": obj.name,
                "blend": f"blender/sources/v31/{family}.blend",
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
            print(f"HARUKA_V31_ASSET {obj.name}: {entry['triangles']} tris, parts {list(entry['parts'])}")
    with open(os.path.join(EXPORTS, "v31_assets_manifest.json"), "w", encoding="utf-8") as f:
        json.dump(manifest, f, indent=2, ensure_ascii=False)
        f.write("\n")


if __name__ == "__main__":
    main()
