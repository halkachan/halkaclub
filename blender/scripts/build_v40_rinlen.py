"""ver4.0: Rin and Len (鏡音 1頭身, the user's own models) made into game models.

Run on a COPY of the user's file, never the original:
  blender -b "<copy of rin ver2.0.blend>" --python build_v40_rinlen.py -- rin <out_dir>
  blender -b "<copy of len ver2.0.blend>" --python build_v40_rinlen.py -- len <out_dir>

What it does (the look is the original's; nothing is redrawn):
  1. frame 0 of the file's own animation (the rest pose), modifiers applied (armature, mirror, solidify);
     every mesh visible in the file's viewport (what the user sees when opening it);
  2. every mesh that renders is copied into world space; hands (te) and feet (asi) stay separate
     parts so the game can swing them while walking (like ﾊﾙｶﾁｬﾝ's arms and legs); the rest is one body;
  3. the original materials (the packed rin.png texture, its mix with the vertex colours, the plain
     vertex-colour parts) become ONE material: the original UVs and texture (512 x 512 instead of
     2048 x 2048) and a colour attribute that says, per face corner, what to multiply and whether the
     texture is used, so the game
     draws each character with one material and one 512 x 512 image instead of a 2048 x 2048 one;
  4. FBX for Unity (forward -Z, up Y, scale applied; front = Blender -Y = South, like ﾊﾙｶﾁｬﾝ),
     the baked texture, a .blend of the game model, and a manifest (parts, triangles, sizes).
The source file is opened read-only in effect: this script never saves over it.
"""
import bpy
import bmesh
import json
import math
import os
import sys
from mathutils import Vector

args = sys.argv[sys.argv.index("--") + 1:]
WHO = args[0]
OUT = args[1]
TEX = 512
os.makedirs(OUT, exist_ok=True)
scene = bpy.context.scene
scene.frame_set(0)
depsgraph = bpy.context.evaluated_depsgraph_get()

source_file = bpy.data.filepath
parts = {"body": [], "HandR": [], "HandL": [], "FootR": [], "FootL": []}


def part_of(name):
    n = name.lower()
    if "te1" in n:
        return "HandR"   # +X (the character's left when it faces -Y; named by screen side in the game)
    if "te2" in n:
        return "HandL"
    if "asi1" in n:
        return "FootR"
    if "asi2" in n:
        return "FootL"
    return "body"


# What the file shows in its viewport (Rin's head mesh "atama" is visible there but marked not to
# render; without it the face and body are missing, so the viewport is taken as the original look).
sources = [o for o in scene.objects if o.type == "MESH" and o.visible_get()]
report = {"source": os.path.basename(source_file), "who": WHO, "frame": 0, "objects": []}
copies = []
for obj in sources:
    evaluated = obj.evaluated_get(depsgraph)
    mesh = bpy.data.meshes.new_from_object(evaluated, preserve_all_data_layers=True, depsgraph=depsgraph)
    mesh.transform(obj.matrix_world)
    copy = bpy.data.objects.new(obj.name + "_game", mesh)
    scene.collection.objects.link(copy)
    for slot_index, slot in enumerate(obj.material_slots):
        if slot_index < len(mesh.materials):
            mesh.materials[slot_index] = slot.material
    part = part_of(obj.name)
    parts[part].append(copy)
    copies.append(copy)
    report["objects"].append({"name": obj.name, "part": part, "triangles": sum(len(p.vertices) - 2 for p in mesh.polygons)})

# Hide everything else from the bake and the renders.
for obj in list(scene.objects):
    if obj not in copies:
        obj.hide_render = True
        obj.hide_set(True)

# One mesh per part.
joined = {}
for part, objs in parts.items():
    if not objs:
        continue
    bpy.ops.object.select_all(action="DESELECT")
    for o in objs:
        o.hide_set(False)
        o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    if len(objs) > 1:
        bpy.ops.object.join()
    target = bpy.context.view_layer.objects.active
    target.name = part
    target.data.name = WHO + "_" + part
    joined[part] = target

# The look of every original material, kept with the original UVs and texture:
#   - Rin's materials: the texture alone;
#   - Len's: the texture multiplied by the vertex colour (Mix MULTIPLY, factor 1), or the vertex
#     colour alone (the hair spikes), read from each material's nodes;
#   - a material with neither: its plain base colour.
# Each face corner gets one colour attribute "game": RGB the colour to multiply by (white for a
# texture alone), alpha 1 where the texture is used and 0 where it is not. The game's shader draws
# rgb * lerp(1, texture, alpha). The texture is the file's own packed rin.png at 512 x 512.
import numpy as np


def recipe(mat):
    """('tex' | 'tex*vc' | 'vc' | 'const', rgba)"""
    if mat is None or not mat.use_nodes:
        return ("const", (0.8, 0.8, 0.8, 1.0))
    bsdf = next((n for n in mat.node_tree.nodes if n.type == "BSDF_PRINCIPLED"), None)
    if bsdf is None:
        return ("const", (0.8, 0.8, 0.8, 1.0))
    base = bsdf.inputs["Base Color"]
    if not base.is_linked:
        return ("const", tuple(base.default_value))
    src = base.links[0].from_node
    if src.type == "TEX_IMAGE":
        return ("tex", None)
    if src.type in ("VERTEX_COLOR", "ATTRIBUTE"):
        return ("vc", None)
    if src.type == "MIX":
        nodes = [i.links[0].from_node for i in (src.inputs["A"], src.inputs["B"]) if i.is_linked]
        has_tex = any(x.type == "TEX_IMAGE" for x in nodes)
        has_vc = any(x.type in ("VERTEX_COLOR", "ATTRIBUTE") for x in nodes)
        if has_tex and has_vc and src.blend_type == "MULTIPLY":
            return ("tex*vc", None)
        if has_tex:
            return ("tex", None)
    return ("const", tuple(base.default_value))


def linear_to_srgb(c):
    c = max(0.0, min(1.0, c))
    return c * 12.92 if c <= 0.0031308 else 1.055 * c ** (1.0 / 2.4) - 0.055


report["materials"] = {}
for obj in joined.values():
    me = obj.data
    render_layer = next((layer for layer in me.uv_layers if layer.active_render), me.uv_layers[0] if me.uv_layers else None)
    if render_layer is None:
        render_layer = me.uv_layers.new(name="orig")
    render_layer.name = "orig"
    for layer in [l for l in me.uv_layers if l.name != "orig"]:
        me.uv_layers.remove(layer)
    source_colors = me.color_attributes[me.color_attributes.render_color_index] if len(me.color_attributes) else None
    source_values = [tuple(source_colors.data[i].color) for i in range(len(me.loops))] if source_colors is not None and source_colors.domain == "CORNER" else None
    recipes = [recipe(slot.material) for slot in obj.material_slots] or [("const", (0.8, 0.8, 0.8, 1.0))]
    for slot, r in zip(obj.material_slots, recipes):
        if slot.material:
            report["materials"][slot.material.name] = r[0]
    for attribute in list(me.color_attributes):
        me.color_attributes.remove(attribute)
    game_colors = me.color_attributes.new(name="game", type="BYTE_COLOR", domain="CORNER")
    values = np.ones((len(me.loops), 4), dtype=np.float32)
    for poly in me.polygons:
        kind, const = recipes[min(poly.material_index, len(recipes) - 1)]
        for loop_index in poly.loop_indices:
            if kind == "tex":
                rgba = (1.0, 1.0, 1.0, 1.0)
            elif kind in ("tex*vc", "vc"):
                vc = source_values[loop_index] if source_values is not None else (1.0, 1.0, 1.0, 1.0)
                rgba = (vc[0], vc[1], vc[2], 1.0 if kind == "tex*vc" else 0.0)
            else:
                rgba = (const[0], const[1], const[2], 0.0)
            values[loop_index] = rgba
    # BYTE_COLOR .color is linear; FBX export writes sRGB (the game's Gamma colour space).
    game_colors.data.foreach_set("color", values.ravel())
    me.color_attributes.active_color = game_colors
    me.color_attributes.render_color_index = 0

source_image = next(i for i in bpy.data.images if i.packed_file is not None)
image = source_image.copy()
image.name = WHO + "_onehead_tex"
image.scale(TEX, TEX)
tex_path = os.path.join(OUT, WHO + "_onehead_tex.png")
image.filepath_raw = tex_path
image.file_format = "PNG"
image.save()
report["texture_source"] = source_image.name + " %dx%d (packed in the file)" % tuple(source_image.size)

# The game material (for the .blend and the previews; Unity uses HALKA/POC/ToonTextureTint):
# base colour = game.rgb * mix(1, texture, game.alpha).
game_mat = bpy.data.materials.new(WHO + "_onehead")
game_mat.use_nodes = True
nt = game_mat.node_tree
bsdf = next(n for n in nt.nodes if n.type == "BSDF_PRINCIPLED")
bsdf.inputs["Roughness"].default_value = 0.9
tex = nt.nodes.new("ShaderNodeTexImage")
tex.image = image
uvn = nt.nodes.new("ShaderNodeUVMap")
uvn.uv_map = "orig"
nt.links.new(uvn.outputs["UV"], tex.inputs["Vector"])
attr = nt.nodes.new("ShaderNodeVertexColor")
attr.layer_name = "game"
pick = nt.nodes.new("ShaderNodeMix")
pick.data_type = "RGBA"
pick.blend_type = "MIX"
nt.links.new(attr.outputs["Alpha"], pick.inputs["Factor"])
pick.inputs["A"].default_value = (1.0, 1.0, 1.0, 1.0)
nt.links.new(tex.outputs["Color"], pick.inputs["B"])
mul = nt.nodes.new("ShaderNodeMix")
mul.data_type = "RGBA"
mul.blend_type = "MULTIPLY"
mul.inputs["Factor"].default_value = 1.0
nt.links.new(pick.outputs["Result"], mul.inputs["A"])
nt.links.new(attr.outputs["Color"], mul.inputs["B"])
nt.links.new(mul.outputs["Result"], bsdf.inputs["Base Color"])
for obj in joined.values():
    obj.data.materials.clear()
    obj.data.materials.append(game_mat)

# Pivots: the body at the feet's floor point under its centre; hands and feet at their own centre
# (they swing a little around it). Then the four parts become children of the body.
body = joined["body"]
all_min = Vector((min(v.co.x for o in joined.values() for v in o.data.vertices),
                  min(v.co.y for o in joined.values() for v in o.data.vertices),
                  min(v.co.z for o in joined.values() for v in o.data.vertices)))
all_max = Vector((max(v.co.x for o in joined.values() for v in o.data.vertices),
                  max(v.co.y for o in joined.values() for v in o.data.vertices),
                  max(v.co.z for o in joined.values() for v in o.data.vertices)))
floor_point = Vector(((all_min.x + all_max.x) * 0.5, (all_min.y + all_max.y) * 0.5, all_min.z))


def set_origin(obj, point):
    obj.data.transform(__import__("mathutils").Matrix.Translation(-point))
    obj.location = point


set_origin(body, floor_point)
for name in ("HandR", "HandL", "FootR", "FootL"):
    obj = joined.get(name)
    if obj is None:
        continue
    vs = obj.data.vertices
    centre = Vector((sum(v.co.x for v in vs) / len(vs), sum(v.co.y for v in vs) / len(vs), sum(v.co.z for v in vs) / len(vs)))
    set_origin(obj, centre)
    obj.parent = body
    obj.matrix_parent_inverse = body.matrix_world.inverted()

body.location = Vector((0.0, 0.0, 0.0))

report["size"] = [round(all_max[i] - all_min[i], 4) for i in range(3)]
report["parts"] = {name: {"triangles": sum(len(p.vertices) - 2 for p in obj.data.polygons),
                          "vertices": len(obj.data.vertices)} for name, obj in joined.items()}
report["triangles"] = sum(v["triangles"] for v in report["parts"].values())
report["texture"] = os.path.basename(tex_path) + " %dx%d" % (TEX, TEX)

# FBX (only the game model).
bpy.ops.object.select_all(action="DESELECT")
for obj in joined.values():
    obj.select_set(True)
fbx_path = os.path.join(OUT, WHO + "_onehead.fbx")
bpy.ops.export_scene.fbx(filepath=fbx_path, use_selection=True, apply_unit_scale=True, apply_scale_options="FBX_SCALE_ALL",
                         axis_forward="-Z", axis_up="Y", object_types={"MESH"}, use_mesh_modifiers=True,
                         mesh_smooth_type="OFF", add_leaf_bones=False, bake_anim=False, path_mode="STRIP",
                         embed_textures=False, colors_type="SRGB", bake_space_transform=True)
report["fbx"] = os.path.basename(fbx_path)

# The game model's own .blend (the source file is not touched).
for obj in list(bpy.data.objects):
    if obj not in joined.values():
        bpy.data.objects.remove(obj, do_unlink=True)
# Only the game texture is kept (packed) in the game .blend; the 2048 x 2048 original stays in the
# user's own file.
for img in list(bpy.data.images):
    if img != image:
        bpy.data.images.remove(img)
image.pack()
blend_path = os.path.join(OUT, WHO + "_onehead_game.blend")
bpy.ops.wm.save_as_mainfile(filepath=blend_path, copy=True)
report["blend"] = os.path.basename(blend_path)
with open(os.path.join(OUT, WHO + "_onehead_manifest.json"), "w", encoding="utf-8") as f:
    json.dump(report, f, ensure_ascii=False, indent=2)
print("HALKA_V40_RINLEN", json.dumps(report, ensure_ascii=False))
