"""Workbench previews of the Phase 4 crow, rendered from the exported FBX (so the preview also
proves the FBX re-imports with its vertex colours and its part hierarchy).

  blender.exe --background --factory-startup --python blender/scripts/render_phase4_previews.py

Writes blender/exports/previews/phase4/crow_basic_<view>.png, plus a wings-up pose.
"""

import json
import math
import os

import bpy
from mathutils import Vector

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
MANIFEST = os.path.join(ROOT, "blender", "exports", "phase4_crow_manifest.json")
OUT = os.path.join(ROOT, "blender", "exports", "previews", "phase4")
VIEWS = {"south_front": (0, 8), "east": (90, 8), "north_back": (180, 8), "west": (270, 8),
         "top": (0, 89.5), "game_2d_angle": (0, 50), "three_quarter": (35, 25)}


def render_views(scene, camera, target, name, views):
    for view, (yaw, elevation) in views.items():
        y, e = math.radians(yaw), math.radians(elevation)
        camera.location = target + Vector((math.sin(y) * math.cos(e), -math.cos(y) * math.cos(e), math.sin(e))) * 8.0
        camera.rotation_euler = (target - camera.location).to_track_quat("-Z", "Y").to_euler()
        scene.render.filepath = os.path.join(OUT, f"{name}_{view}.png")
        bpy.ops.render.render(write_still=True)


def main():
    os.makedirs(OUT, exist_ok=True)
    with open(MANIFEST, encoding="utf-8") as f:
        model = json.load(f)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    bpy.ops.import_scene.fbx(filepath=os.path.join(ROOT, model["fbx"]))
    scene.render.engine = "BLENDER_WORKBENCH"
    scene.display.shading.light = "STUDIO"
    scene.display.shading.color_type = "VERTEX"
    scene.display.shading.show_object_outline = False
    scene.render.film_transparent = True
    scene.render.resolution_x, scene.render.resolution_y = 400, 400
    lo, hi = Vector(model["boundsMin"]), Vector(model["boundsMax"])
    size = hi - lo
    data = bpy.data.cameras.new("PreviewCamera")
    data.type = "ORTHO"
    data.ortho_scale = max(size.x, size.y, size.z) * 1.6 + 0.1
    camera = bpy.data.objects.new("PreviewCamera", data)
    scene.collection.objects.link(camera)
    scene.camera = camera
    target = (lo + hi) * 0.5
    render_views(scene, camera, target, "crow_basic", VIEWS)
    # The same model with the wings raised (the flight pose Unity animates from these pivots).
    for obj in bpy.data.objects:
        if obj.name.startswith("WingL"):
            obj.rotation_euler.rotate_axis("Y", math.radians(-70))
        if obj.name.startswith("WingR"):
            obj.rotation_euler.rotate_axis("Y", math.radians(70))
    render_views(scene, camera, target, "crow_basic_wings_up", {"south_front": (0, 8), "three_quarter": (35, 25)})
    print("HARUKA_PHASE4_PREVIEW crow_basic", [o.name for o in bpy.data.objects])


if __name__ == "__main__":
    main()
