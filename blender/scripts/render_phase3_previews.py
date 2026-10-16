"""Workbench previews of every Phase 3 model, rendered from the exported FBX (so the preview
also proves the FBX re-imports with its vertex colors).

  blender.exe --background --factory-startup --python blender/scripts/render_phase3_previews.py

Writes blender/exports/previews/phase3/<model>_<view>.png for the views below.
"""

import json
import math
import os

import bpy
from mathutils import Vector

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
MANIFEST = os.path.join(ROOT, "blender", "exports", "phase3_assets_manifest.json")
OUT = os.path.join(ROOT, "blender", "exports", "previews", "phase3")
VIEWS = {"south_front": (0, 8), "east": (90, 8), "north_back": (180, 8), "west": (270, 8),
         "top": (0, 89.5), "game_2d_angle": (0, 50), "three_quarter": (35, 25)}


def main():
    os.makedirs(OUT, exist_ok=True)
    with open(MANIFEST, encoding="utf-8") as f:
        manifest = json.load(f)
    for model in manifest["models"]:
        bpy.ops.wm.read_factory_settings(use_empty=True)
        scene = bpy.context.scene
        bpy.ops.import_scene.fbx(filepath=os.path.join(ROOT, model["fbx"]))
        scene.render.engine = "BLENDER_WORKBENCH"
        scene.display.shading.light = "STUDIO"
        scene.display.shading.color_type = "VERTEX"
        scene.display.shading.show_object_outline = False
        scene.render.film_transparent = True
        lo, hi = Vector(model["boundsMin"]), Vector(model["boundsMax"])
        size = hi - lo
        large = max(size.x, size.y, size.z) > 1.2
        scene.render.resolution_x, scene.render.resolution_y = (640, 480) if large else (400, 400)
        data = bpy.data.cameras.new("PreviewCamera")
        data.type = "ORTHO"
        data.ortho_scale = max(size.x, size.y, size.z) * 1.35 + 0.1
        camera = bpy.data.objects.new("PreviewCamera", data)
        scene.collection.objects.link(camera)
        scene.camera = camera
        target = (lo + hi) * 0.5
        for view, (yaw, elevation) in VIEWS.items():
            y, e = math.radians(yaw), math.radians(elevation)
            camera.location = target + Vector((math.sin(y) * math.cos(e), -math.cos(y) * math.cos(e), math.sin(e))) * 8.0
            camera.rotation_euler = (target - camera.location).to_track_quat("-Z", "Y").to_euler()
            scene.render.filepath = os.path.join(OUT, f"{model['name']}_{view}.png")
            bpy.ops.render.render(write_still=True)
        print("HARUKA_PHASE3_PREVIEW", model["name"])


if __name__ == "__main__":
    main()
