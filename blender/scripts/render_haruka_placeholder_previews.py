"""Workbench turnaround of the TEMPORARY ﾊﾙｶﾁｬﾝ placeholder (the whole hierarchy).

  blender.exe --background --factory-startup --python blender/scripts/render_haruka_placeholder_previews.py

Writes blender/exports/previews/phase2_1/haruka_placeholder_<view>.png.
"""

import math
import os

import bpy
from mathutils import Vector

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
SOURCE = os.path.join(ROOT, "blender", "sources", "phase2_1", "haruka_placeholder.blend")
OUT = os.path.join(ROOT, "blender", "exports", "previews", "phase2_1")
VIEWS = {"south_front": (0, 8), "east": (90, 8), "north_back": (180, 8), "west": (270, 8),
         "top": (0, 89.5), "game_2d_angle": (0, 50), "three_quarter": (35, 20)}


def main():
    os.makedirs(OUT, exist_ok=True)
    bpy.ops.wm.open_mainfile(filepath=SOURCE)
    scene = bpy.context.scene
    for collection in bpy.data.collections:
        if collection.name.endswith("_GridGuides"):
            collection.hide_render = True
    scene.render.engine = "BLENDER_WORKBENCH"
    scene.display.shading.light = "FLAT"
    scene.display.shading.color_type = "VERTEX"
    scene.render.resolution_x, scene.render.resolution_y = 400, 480
    scene.render.film_transparent = True
    data = bpy.data.cameras.new("PreviewCamera")
    data.type = "ORTHO"
    data.ortho_scale = 1.15
    camera = bpy.data.objects.new("PreviewCamera", data)
    scene.collection.objects.link(camera)
    scene.camera = camera
    target = Vector((0.0, 0.0, 0.47))
    for view, (yaw, elevation) in VIEWS.items():
        y, e = math.radians(yaw), math.radians(elevation)
        camera.location = target + Vector((math.sin(y) * math.cos(e), -math.cos(y) * math.cos(e), math.sin(e))) * 5.0
        camera.rotation_euler = (target - camera.location).to_track_quat("-Z", "Y").to_euler()
        scene.render.filepath = os.path.join(OUT, f"haruka_placeholder_{view}.png")
        bpy.ops.render.render(write_still=True)


if __name__ == "__main__":
    main()
