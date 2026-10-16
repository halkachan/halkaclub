"""はるかわーるど ver3.0 Phase 4: the crow (カラス) as a cute low-poly 3D character.

After the shipped 2D sprites Assets/Content/World/crow_*.png (read only): a round black body
with a small head on top, a grey beak, white eyes, a red scarf knotted at the side, wings,
a tail and two legs. Not a realistic bird.

  "C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" --background --factory-startup \
      --python blender/scripts/build_phase4_crow.py

Outputs:
  blender/sources/phase4/crow_basic.blend
  unity-project/Assets/POC2D3D/Models/Phase4/crow_basic.fbx
  blender/exports/phase4_crow_manifest.json

Hierarchy (each part pivots at its joint, so Unity can animate it with plain transforms):
  crow_basic        body (origin = ground centre between the feet)
    Head            neck pivot (beak, eyes)
    Scarf           around the neck, with the knot and the two loose ends
    WingL / WingR   shoulder pivots; L = the crow's left = +X while it faces South
    Tail            rump pivot
    LegL / LegR     hip pivots (legs and toes)
Front faces South (Blender -Y), like every other はるかわーるど asset.
"""

import json
import math
import os
import sys

import bmesh
import bpy
from mathutils import Vector

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import halka_lowpoly as hl  # noqa: E402

NAME = "crow_basic"
ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
SOURCE = os.path.join(ROOT, "blender", "sources", "phase4", NAME + ".blend")
FBX = os.path.join(ROOT, "unity-project", "Assets", "POC2D3D", "Models", "Phase4", NAME + ".fbx")
MANIFEST = os.path.join(ROOT, "blender", "exports", "phase4_crow_manifest.json")

# Palette sampled from crow_idle_down.png / crow_idle_right.png.
BLACK = "#24232b"
BLACK_SHADE = "#17161c"
BLACK_LIGHT = "#3a3946"
BEAK = "#8d8c96"
BEAK_DARK = "#64636c"
EYE = "#f4f4f4"
PUPIL = "#141418"
SCARF = "#d9262c"
SCARF_DARK = "#a3171d"
LEG = "#4a4952"

# Sizes (metres; one cell = 0.5). The crow stands about 0.36 tall, a third of ﾊﾙｶﾁｬﾝ.
BODY_RADIUS = (0.13, 0.12, 0.12)
BODY_CENTRE = Vector((0.0, 0.01, 0.17))
HEAD_RADIUS = 0.085
HEAD_PIVOT = Vector((0.0, -0.02, 0.26))      # neck
LEG_HIP_Z = 0.075
SHOULDER = (0.105, 0.0, 0.205)
TAIL_PIVOT = Vector((0.0, 0.115, 0.14))


def body_color(normal, centre):
    if normal.z > 0.55:
        return BLACK_LIGHT
    if normal.z < -0.4:
        return BLACK_SHADE
    return BLACK


def build_body():
    m = hl.MeshBuilder()
    m.jitter_blob(BODY_CENTRE, BODY_RADIUS, 2, 7, 0.006, body_color)
    return m.to_object(NAME)


def build_head():
    m = hl.MeshBuilder()
    # The head centre sits above and slightly in front of the neck pivot (local coordinates).
    centre = Vector((0.0, -0.025, 0.075))
    m.jitter_blob(centre, (HEAD_RADIUS, HEAD_RADIUS * 0.95, HEAD_RADIUS * 0.92), 2, 11, 0.004, body_color)
    # Beak: a short four-sided spike pointing South and a little down.
    base_y = centre.y - HEAD_RADIUS * 0.82
    tip = Vector((0.0, base_y - 0.075, centre.z - 0.02))
    half_w, half_h = 0.03, 0.022
    corners = [Vector((-half_w, base_y, centre.z - half_h)), Vector((half_w, base_y, centre.z - half_h)),
               Vector((half_w, base_y, centre.z + half_h)), Vector((-half_w, base_y, centre.z + half_h))]
    beak = bmesh.new()
    layer = beak.loops.layers.float_color.new(hl.COLOR_LAYER)
    vs = [beak.verts.new(c) for c in corners]
    vt = beak.verts.new(tip)
    faces = [beak.faces.new((vs[i], vs[(i + 1) % 4], vt)) for i in range(4)]
    faces.append(beak.faces.new(list(reversed(vs))))
    for i, face in enumerate(faces):
        color = hl.hex_color(BEAK_DARK if i == 0 else BEAK)
        for loop in face.loops:
            loop[layer] = color
    merge(m, beak)
    # Eyes: white discs with a dark pupil, on the front of the head, a little apart.
    for side in (-1, 1):
        eye_centre = Vector((side * 0.043, centre.y - HEAD_RADIUS * 0.74, centre.z + 0.018))
        m.cylinder_between(eye_centre + Vector((0, 0.012, 0)), eye_centre - Vector((0, 0.004, 0)),
                           0.022, 0.022, 8, EYE)
        m.cylinder_between(eye_centre - Vector((0, 0.003, 0)), eye_centre - Vector((0, 0.008, 0)),
                           0.010, 0.010, 6, PUPIL)
    head = m.to_object("Head")
    head.location = HEAD_PIVOT
    return head


def merge(builder, other):
    """Appends another bmesh (with its colours) into a MeshBuilder."""
    mesh = bpy.data.meshes.new("tmp_merge")
    other.to_mesh(mesh)
    other.free()
    builder.bm.from_mesh(mesh)
    bpy.data.meshes.remove(mesh)


def build_scarf():
    m = hl.MeshBuilder()
    # A thick ring around the neck (local to the neck pivot), then the knot on the crow's
    # left side and two loose ends hanging behind it, as in the sprites.
    m.cylinder((0.0, 0.0, -0.035), 0.098, 0.088, 0.05, 12, SCARF)
    m.box((0.075, -0.035, -0.045), (0.125, 0.02, 0.005), SCARF_DARK)
    m.prism([(0.085, -0.04), (0.125, -0.04), (0.16, -0.13), (0.12, -0.14)], -0.005, 0.03, SCARF)
    m.prism([(0.095, -0.04), (0.125, -0.04), (0.145, -0.11), (0.11, -0.115)], 0.035, 0.06, SCARF_DARK)
    scarf = m.to_object("Scarf")
    scarf.location = HEAD_PIVOT
    return scarf


def solid_from_profile(builder, profile, axis_offset, color_for_normal):
    """A flat piece: a (y, z) profile at x = 0 extruded to x = axis_offset, coloured by normal."""
    inner = [builder.bm.verts.new((0.0, y, z)) for y, z in profile]
    outer = [builder.bm.verts.new((axis_offset, y, z)) for y, z in profile]
    faces = [builder.bm.faces.new(inner), builder.bm.faces.new(list(reversed(outer)))]
    count = len(profile)
    for i in range(count):
        j = (i + 1) % count
        faces.append(builder.bm.faces.new((inner[i], inner[j], outer[j], outer[i])))
    bmesh.ops.recalc_face_normals(builder.bm, faces=faces)
    for face in faces:
        rgba = hl.hex_color(color_for_normal(face.normal))
        for loop in face.loops:
            loop[builder.layer] = rgba


def wing_color(normal):
    if normal.z < -0.3:
        return BLACK_SHADE
    if abs(normal.x) > 0.5:
        return BLACK
    return BLACK_LIGHT


def build_wing(name, side):
    m = hl.MeshBuilder()
    # A folded wing: a flat rounded wedge from the shoulder pivot, its tip pointing back (North)
    # and down, standing a little out of the body's side.
    profile = [(-0.03, 0.0), (0.06, 0.005), (0.13, -0.03), (0.165, -0.085), (0.145, -0.11), (0.05, -0.07)]
    solid_from_profile(m, profile, side * 0.036, wing_color)
    wing = m.to_object(name)
    wing.location = Vector((side * SHOULDER[0], SHOULDER[1], SHOULDER[2]))
    return wing


def build_tail():
    m = hl.MeshBuilder()
    # A short fan pointing North and a little up from the rump pivot.
    outline = [(-0.05, 0.0), (0.05, 0.0), (0.08, 0.11), (0.0, 0.135), (-0.08, 0.11)]
    top = [m.bm.verts.new((x, y, y * 0.45 + 0.012)) for x, y in outline]
    bottom = [m.bm.verts.new((x, y, y * 0.45 - 0.012)) for x, y in outline]
    faces = [m.bm.faces.new(top), m.bm.faces.new(list(reversed(bottom)))]
    for i in range(len(outline)):
        j = (i + 1) % len(outline)
        faces.append(m.bm.faces.new((bottom[i], bottom[j], top[j], top[i])))
    bmesh.ops.recalc_face_normals(m.bm, faces=faces)
    for face in faces:
        rgba = hl.hex_color(BLACK_LIGHT if face.normal.z > 0.5 else BLACK_SHADE if face.normal.z < -0.5 else BLACK)
        for loop in face.loops:
            loop[m.layer] = rgba
    tail = m.to_object("Tail")
    tail.location = TAIL_PIVOT
    return tail


def build_leg(name, side):
    m = hl.MeshBuilder()
    # A thin leg from the hip pivot down to the ground, and three short toes.
    m.cylinder_between((0, 0, 0), (0, -0.005, -LEG_HIP_Z + 0.008), 0.012, 0.01, 6, LEG)
    for angle in (-30.0, 0.0, 30.0):
        a = math.radians(angle)
        tip = Vector((math.sin(a) * 0.045, -math.cos(a) * 0.045, -LEG_HIP_Z + 0.006))
        m.cylinder_between((0, -0.005, -LEG_HIP_Z + 0.008), tip, 0.009, 0.006, 5, LEG)
    m.cylinder_between((0, -0.005, -LEG_HIP_Z + 0.008), (0, 0.025, -LEG_HIP_Z + 0.006), 0.008, 0.006, 5, LEG)
    leg = m.to_object(name)
    leg.location = Vector((side * 0.045, 0.0, LEG_HIP_Z))
    return leg


def main():
    for path in (SOURCE, FBX, MANIFEST):
        os.makedirs(os.path.dirname(path), exist_ok=True)
    hl.reset_scene()
    body = build_body()
    parts = [build_head(), build_scarf(), build_wing("WingL", 1), build_wing("WingR", -1), build_tail(),
             build_leg("LegL", 1), build_leg("LegR", -1)]
    for part in parts:
        part.parent = body
    anchors = [hl.add_anchor("PerchFeet", body, (0.0, 0.0, 0.0))]
    hl.add_cell_guides(NAME, [(0, 0)])
    bpy.ops.wm.save_as_mainfile(filepath=SOURCE, compress=False)
    hl.export_fbx(body, FBX, parts + anchors)
    corners = []
    for obj in [body] + parts:
        corners += [obj.matrix_world @ Vector(c) for c in obj.bound_box]
    xs, ys, zs = zip(*[tuple(c) for c in corners])
    manifest = {
        "generator": "blender/scripts/build_phase4_crow.py",
        "blenderVersion": bpy.app.version_string,
        "blend": f"blender/sources/phase4/{NAME}.blend",
        "fbx": f"unity-project/Assets/POC2D3D/Models/Phase4/{NAME}.fbx",
        "front": "South (-Y in Blender, -Z in Unity)",
        "origin": "ground centre between the feet",
        "spriteReference": "Assets/Content/World/crow_*.png (read only)",
        "parts": {obj.name: {"triangles": hl.triangle_count(obj), "vertices": len(obj.data.vertices),
                             "materials": hl.material_count(obj),
                             "pivot": [round(v, 4) for v in obj.matrix_world.translation]}
                  for obj in [body] + parts},
        "anchors": {a.name: [round(v, 4) for v in a.matrix_world.translation] for a in anchors},
        "triangles": sum(hl.triangle_count(o) for o in [body] + parts),
        "materials": 1,
        "boundsMin": [round(min(xs), 4), round(min(ys), 4), round(min(zs), 4)],
        "boundsMax": [round(max(xs), 4), round(max(ys), 4), round(max(zs), 4)],
        "supplemented": ["the back of the head and body (the sprites show front, back and sides only "
                         "at 32 px)", "the wing shape at rest (folded) and the tail fan", "three toes"],
    }
    with open(MANIFEST, "w", encoding="utf-8") as f:
        json.dump(manifest, f, indent=2, ensure_ascii=False)
        f.write("\n")
    print("CROW", manifest["triangles"], "tris", manifest["boundsMin"], manifest["boundsMax"])


if __name__ == "__main__":
    main()
