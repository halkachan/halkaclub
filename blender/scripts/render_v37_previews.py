"""はるかわーるど ver3.7 (West area): Workbench previews of the studio models, rendered from the exported FBX
(so the preview also proves the FBX re-imports with its vertex colors), and the editor sprites
(Object Catalog previewSpritePath) rendered from the same models at the game's 2D angle.

  blender.exe --background --factory-startup --python blender/scripts/render_v37_previews.py

Writes
  blender/exports/previews/v37/<model>_<view>.png
  blender/exports/previews/v37/sprites_hires/<sprite>.png   (8x, before reduction; reduced by
      tools/poc2d3d/v37_reduce_sprites.py into unity-project/Assets/Content/World/)

The sprites are candidates generated from the 3D models (not hand-drawn): the game's 2D view draws
the 3D models themselves, the sprites are what WORLD EDITOR and the catalog validation show.
"""

import json
import math
import os

import bpy
from mathutils import Vector

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
MANIFEST = os.path.join(ROOT, "blender", "exports", "v37_assets_manifest.json")
OUT = os.path.join(ROOT, "blender", "exports", "previews", "v37")
SPRITES = os.path.join(OUT, "sprites_hires")
VIEWS = {"south_front": (0, 8), "east": (90, 8), "north_back": (180, 8), "west": (270, 8),
         "top": (0, 89.5), "game_2d_angle": (0, 50), "three_quarter": (35, 25)}
PITCH = 50.0          # PocPixelView2D.PitchDegrees (square ground: a cell is as deep as it is wide)
PIXELS_PER_UNIT = 64  # catalog sprites: 32 px per 0.5 cell
SUPER = 8
# sprite name -> (model, width px, height px, left x, front y of the footprint, pitch)
SPRITE_SPECS = {
    "stump_hollow": ("stump_hollow", 32, 32, -0.25, -0.25, PITCH),
    "forest_bush": ("forest_bush", 32, 32, -0.25, -0.25, PITCH),
    "forest_fern": ("forest_fern", 32, 32, -0.25, -0.25, PITCH),
    "fallen_leaves": ("fallen_leaves", 32, 32, -0.25, -0.25, PITCH),
    "sign_green": ("sign_green", 32, 32, -0.25, -0.25, PITCH),
}


def setup_scene(fbx):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    bpy.ops.import_scene.fbx(filepath=os.path.join(ROOT, fbx))
    scene.render.engine = "BLENDER_WORKBENCH"
    scene.display.shading.light = "STUDIO"
    scene.display.shading.color_type = "VERTEX"
    scene.display.shading.show_object_outline = False
    scene.render.film_transparent = True
    data = bpy.data.cameras.new("PreviewCamera")
    data.type = "ORTHO"
    camera = bpy.data.objects.new("PreviewCamera", data)
    scene.collection.objects.link(camera)
    scene.camera = camera
    return scene, camera


def render_previews(model):
    scene, camera = setup_scene(model["fbx"])
    lo, hi = Vector(model["boundsMin"]), Vector(model["boundsMax"])
    size = hi - lo
    large = max(size.x, size.y, size.z) > 1.2
    scene.render.resolution_x, scene.render.resolution_y = (640, 480) if large else (400, 400)
    camera.data.ortho_scale = max(size.x, size.y, size.z) * 1.35 + 0.1
    target = (lo + hi) * 0.5
    for view, (yaw, elevation) in VIEWS.items():
        y, e = math.radians(yaw), math.radians(elevation)
        camera.location = target + Vector((math.sin(y) * math.cos(e), -math.cos(y) * math.cos(e), math.sin(e))) * 8.0
        camera.rotation_euler = (target - camera.location).to_track_quat("-Z", "Y").to_euler()
        scene.render.filepath = os.path.join(OUT, f"{model['name']}_{view}.png")
        bpy.ops.render.render(write_still=True)


def render_sprite(name, model, width, height, left, front, pitch):
    """Frame: the footprint's front-left ground corner at the sprite's bottom-left pixel. Rendered
    with square pixels at SUPER x; the reduction script stretches it to the square-ground ratio."""
    scene, camera = setup_scene(model["fbx"])
    p = math.radians(pitch)
    forward = Vector((0.0, math.cos(p), -math.sin(p))) if pitch < 89.9 else Vector((0.0, 0.0, -1.0))
    up = Vector((0.0, math.sin(p), math.cos(p))) if pitch < 89.9 else Vector((0.0, 1.0, 0.0))
    world_w = width / PIXELS_PER_UNIT
    squash = math.sin(p) if pitch > 0.1 else 1.0  # square ground: camera-up units per sprite row
    world_h = height / PIXELS_PER_UNIT * squash
    origin = Vector((left, front, 0.0))
    s0 = origin.dot(up)
    centre = Vector((left + world_w * 0.5, 0.0, 0.0)) + up * (s0 + world_h * 0.5) - forward * 10.0
    # Remove the x component the up/forward basis does not carry (both have x = 0).
    camera.location = centre
    camera.rotation_euler = (-forward).to_track_quat("Z", "Y").to_euler()
    res_x = width * SUPER
    res_y = max(1, round(height * SUPER * squash))
    scene.render.resolution_x, scene.render.resolution_y = res_x, res_y
    camera.data.ortho_scale = max(world_w, world_h)
    camera.data.sensor_fit = "AUTO"
    scene.render.filter_size = 0.0
    os.makedirs(SPRITES, exist_ok=True)
    scene.render.filepath = os.path.join(SPRITES, name + ".png")
    bpy.ops.render.render(write_still=True)
    return {"sprite": name, "model": model["name"], "width": width, "height": height, "pitch": pitch,
            "renderSize": [res_x, res_y]}


def main():
    os.makedirs(OUT, exist_ok=True)
    with open(MANIFEST, encoding="utf-8") as f:
        manifest = json.load(f)
    models = {m["name"]: m for m in manifest["models"]}
    for model in manifest["models"]:
        render_previews(model)
        print("HARUKA_V36_PREVIEW", model["name"])
    specs = []
    for name, (model, width, height, left, front, pitch) in SPRITE_SPECS.items():
        specs.append(render_sprite(name, models[model], width, height, left, front, pitch))
        print("HARUKA_V36_SPRITE", name)
    with open(os.path.join(SPRITES, "sprites.json"), "w", encoding="utf-8") as f:
        json.dump(specs, f, indent=2)


if __name__ == "__main__":
    main()
