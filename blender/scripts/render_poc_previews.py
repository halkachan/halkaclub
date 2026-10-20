"""Renders review images of the Phase 1 placeholder .blend sources (Workbench, vertex colors).

  blender.exe --background --factory-startup --python blender/scripts/render_poc_previews.py

Writes blender/exports/previews/poc_<name>_<view>.png. The .blend files are opened
read-only; nothing is saved back to them.
"""

import math
import os

import bpy
from mathutils import Vector

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
SOURCES = os.path.join(ROOT, "blender", "sources")
OUT = os.path.join(ROOT, "blender", "exports", "previews")

# Camera position around the model: South (front), East, North (back), West.
VIEWS = {"south_front": 0.0, "east": 90.0, "north_back": 180.0, "west": 270.0}


def setup_render(scene, size):
    scene.render.engine = "BLENDER_WORKBENCH"
    scene.display.shading.light = "STUDIO"
    scene.display.shading.color_type = "VERTEX"
    scene.display.shading.show_object_outline = True
    scene.render.resolution_x = size[0]
    scene.render.resolution_y = size[1]
    scene.render.film_transparent = False
    scene.world = scene.world or bpy.data.worlds.new("World")
    scene.world.color = (0.78, 0.86, 0.7)


def render(name, distance, height, target_z, ortho_scale):
    bpy.ops.wm.open_mainfile(filepath=os.path.join(SOURCES, f"poc_{name}.blend"))
    scene = bpy.context.scene
    for guide in [c for c in bpy.data.collections if c.name.endswith("_GridGuides")]:
        guide.hide_render = True
    setup_render(scene, (480, 480))
    camera_data = bpy.data.cameras.new("PreviewCamera")
    camera_data.type = "ORTHO"
    camera_data.ortho_scale = ortho_scale
    camera = bpy.data.objects.new("PreviewCamera", camera_data)
    scene.collection.objects.link(camera)
    scene.camera = camera
    for view, yaw in VIEWS.items():
        angle = math.radians(yaw)
        # Front view looks from South (-Y) toward North.
        camera.location = Vector((distance * math.sin(angle), -distance * math.cos(angle), height))
        direction = Vector((0, 0, target_z)) - camera.location
        camera.rotation_euler = direction.to_track_quat("-Z", "Y").to_euler()
        scene.render.filepath = os.path.join(OUT, f"poc_{name}_{view}.png")
        bpy.ops.render.render(write_still=True)


def main():
    os.makedirs(OUT, exist_ok=True)
    render("player", 3.0, 1.4, 0.45, 1.3)
    render("tree", 4.0, 2.2, 0.8, 2.1)
    render("house", 5.0, 2.8, 0.8, 3.2)


if __name__ == "__main__":
    main()
