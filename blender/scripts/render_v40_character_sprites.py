"""ver4.0: renders a character for its 2D sprites (front, back, left, right x idle and two walk frames),
from the character's own .blend (Rin and Len: a COPY of the user's file; the fans and antis: the
game model), with flat light so the pixel-art step (tools/poc2d3d/v40_sprites.py) gets clean colours.
  blender -b <file.blend> --python render_v40_character_sprites.py -- <out_dir> <frames e.g. 0,10,20> [size]
The camera looks a little down (25 degrees) from the South, like the 2D game's front-facing art; the
character is turned for each direction (its front is Blender -Y = South)."""
import bpy, math, os, sys
from mathutils import Vector, Matrix
args = sys.argv[sys.argv.index("--") + 1:]
out = args[0]; frames = [int(f) for f in args[1].split(",")]; size = int(args[2]) if len(args) > 2 else 256
os.makedirs(out, exist_ok=True)
sc = bpy.context.scene
for o in sc.objects:
    if o.type == "MESH":
        o.hide_render = not o.visible_get()
# Vertex-coloured game models (the fans and antis): their export material is blank in Blender (the
# game reads the colours), so the colour attribute is wired to the base colour for these renders.
for o in sc.objects:
    if o.type != "MESH" or not o.data.color_attributes:
        continue
    for slot in o.material_slots:
        m = slot.material
        if m is None:
            continue
        m.use_nodes = True
        bsdf = next((n for n in m.node_tree.nodes if n.type == "BSDF_PRINCIPLED"), None)
        if bsdf is None or bsdf.inputs["Base Color"].is_linked:
            continue
        attr = m.node_tree.nodes.new("ShaderNodeVertexColor")
        attr.layer_name = o.data.color_attributes[o.data.color_attributes.render_color_index].name
        m.node_tree.links.new(attr.outputs["Color"], bsdf.inputs["Base Color"])
        bsdf.inputs["Roughness"].default_value = 1.0
roots = [o for o in sc.objects if o.parent is None and (o.type in ("ARMATURE", "EMPTY") or o.type == "MESH" and not o.hide_render)]
try: sc.render.engine = "BLENDER_EEVEE"
except TypeError: sc.render.engine = "BLENDER_EEVEE_NEXT"
sc.render.resolution_x = size; sc.render.resolution_y = size
sc.render.film_transparent = True
sc.view_settings.view_transform = "Standard"
sc.render.filter_size = 0.5
w = sc.world or bpy.data.worlds.new("w"); sc.world = w; w.use_nodes = True
bg = next(n for n in w.node_tree.nodes if n.type == "BACKGROUND"); bg.inputs[0].default_value = (1, 1, 1, 1); bg.inputs[1].default_value = 1.0
ld = bpy.data.lights.new("ssun", "SUN"); ld.energy = 1.6; l = bpy.data.objects.new("ssun", ld); sc.collection.objects.link(l)
l.rotation_euler = (math.radians(35), 0, math.radians(-25))
sc.frame_set(frames[0])
ms = [o for o in sc.objects if o.type == "MESH" and not o.hide_render]
pts = [o.matrix_world @ Vector(c) for o in ms for c in o.bound_box]
lo = Vector((min(p.x for p in pts), min(p.y for p in pts), min(p.z for p in pts)))
hi = Vector((max(p.x for p in pts), max(p.y for p in pts), max(p.z for p in pts)))
span = max(hi.x - lo.x, hi.y - lo.y, hi.z - lo.z) * 1.15
pivot = Vector(((lo.x + hi.x) / 2, (lo.y + hi.y) / 2, lo.z))
cam_data = bpy.data.cameras.new("scam"); cam_data.type = "ORTHO"; cam_data.ortho_scale = span
cam = bpy.data.objects.new("scam", cam_data); sc.collection.objects.link(cam); sc.camera = cam
elev = math.radians(25)
target = pivot + Vector((0, 0, (hi.z - lo.z) * 0.5))
cam.location = target + Vector((0, -math.cos(elev), math.sin(elev))) * 20
cam.rotation_euler = (target - cam.location).to_track_quat('-Z', 'Y').to_euler()
# The camera and the light go round the character (the files' own animations set the objects'
# transforms on every frame, so the character is not turned): the same view as turning it by yaw.
for name, yaw in [("front", 0), ("back", 180), ("left", -90), ("right", 90)]:
    turn = Matrix.Rotation(-math.radians(yaw), 4, 'Z')
    cam.location = target + turn @ (Vector((0, -math.cos(elev), math.sin(elev))) * 20)
    cam.rotation_euler = (target - cam.location).to_track_quat('-Z', 'Y').to_euler()
    l.rotation_euler = (math.radians(35), 0, math.radians(-25) - math.radians(yaw))
    for f in frames:
        sc.frame_set(f)
        sc.render.filepath = os.path.join(out, "%s_%02d.png" % (name, f))
        bpy.ops.render.render(write_still=True)
print("HALKA_V40_SPRITES", out, span)
