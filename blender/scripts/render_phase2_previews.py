"""Workbench review renders of every Phase 2 model (vertex colors, read-only .blend use).

  blender.exe --background --factory-startup --python blender/scripts/render_phase2_previews.py

Writes blender/exports/previews/phase2/<model>_<view>.png for the views
south (front), east, north (back), west, top and game (3/4 from the south).
"""

import math
import os

import bpy
from mathutils import Vector

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
SOURCES = os.path.join(ROOT, "blender", "sources", "phase2")
OUT = os.path.join(ROOT, "blender", "exports", "previews", "phase2")
FAMILIES = ("ground_grass", "ground_dirt", "grass_tuft", "tree_basic", "flower_basic", "stone_basic")
# (yaw around the model, elevation): yaw 0 looks from the South at the model's front.
VIEWS = {"south": (0, 12), "east": (90, 12), "north": (180, 12), "west": (270, 12), "top": (0, 89.5), "game": (0, 50)}


def render_family(family):
    bpy.ops.wm.open_mainfile(filepath=os.path.join(SOURCES, family + ".blend"))
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_WORKBENCH"
    scene.display.shading.light = "FLAT"
    scene.display.shading.color_type = "VERTEX"
    scene.display.shading.show_object_outline = False
    scene.render.resolution_x = scene.render.resolution_y = 360
    scene.render.film_transparent = True
    camera_data = bpy.data.cameras.new("PreviewCamera")
    camera_data.type = "ORTHO"
    camera = bpy.data.objects.new("PreviewCamera", camera_data)
    scene.collection.objects.link(camera)
    scene.camera = camera
    models = [o for o in scene.objects if o.type == "MESH"]
    for model in models:
        for other in models:
            other.hide_render = other is not model
        corners = [model.matrix_world @ Vector(c) for c in model.bound_box]
        center = sum(corners, Vector()) / 8.0
        size = max((max(c[i] for c in corners) - min(c[i] for c in corners)) for i in range(3))
        camera_data.ortho_scale = max(0.3, size * 1.35)
        for view, (yaw, elevation) in VIEWS.items():
            y, e = math.radians(yaw), math.radians(elevation)
            direction = Vector((math.sin(y) * math.cos(e), -math.cos(y) * math.cos(e), math.sin(e)))
            camera.location = center + direction * 6.0
            camera.rotation_euler = (center - camera.location).to_track_quat("-Z", "Y").to_euler()
            scene.render.filepath = os.path.join(OUT, f"{model.name}_{view}.png")
            bpy.ops.render.render(write_still=True)


def main():
    os.makedirs(OUT, exist_ok=True)
    for family in FAMILIES:
        render_family(family)


if __name__ == "__main__":
    main()
