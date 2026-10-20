"""Small helpers for HALKA WORLD low-poly assets built from Blender Python.

Axis convention (shared with Unity, see HALKA_WORLD_2D3D_ARCHITECTURE.md):
  Blender +X = East, +Y = North, +Z = Up. 1 Blender unit = 1 Unity unit.
  One map cell is 0.5 units. An asset's origin is the ground center of its root cell.
  Assets face South (Blender -Y, the Front view), like the existing 2D sprites.

Colors are stored per face corner in the "Col" color attribute, so Unity only needs
one shared vertex-color material and the shape and colors stay owned by Blender.
"""

import math

import bmesh
import bpy
from mathutils import Matrix, Vector

CELL = 0.5
COLOR_LAYER = "Col"


def hex_color(value):
    value = value.lstrip("#")
    srgb = [int(value[i:i + 2], 16) / 255.0 for i in (0, 2, 4)]
    # Float color attributes are linear; the FBX exporter writes them back as sRGB.
    linear = [c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4 for c in srgb]
    return (linear[0], linear[1], linear[2], 1.0)


class MeshBuilder:
    """Collects primitives into one bmesh and colors only the faces each call adds."""

    def __init__(self):
        self.bm = bmesh.new()
        self.layer = self.bm.loops.layers.float_color.new(COLOR_LAYER)

    def _paint_new(self, before, color):
        rgba = hex_color(color) if isinstance(color, str) else color
        for face in self.bm.faces:
            if face not in before:
                for loop in face.loops:
                    loop[self.layer] = rgba

    def _snapshot(self):
        return set(self.bm.faces)

    def box(self, minimum, maximum, color):
        before = self._snapshot()
        lo, hi = Vector(minimum), Vector(maximum)
        size = hi - lo
        center = (lo + hi) * 0.5
        matrix = Matrix.Translation(center) @ Matrix.Diagonal((size.x, size.y, size.z, 1.0))
        bmesh.ops.create_cube(self.bm, size=1.0, matrix=matrix)
        self._paint_new(before, color)

    def cylinder(self, center, radius_bottom, radius_top, height, segments, color, rotation=0.0):
        before = self._snapshot()
        matrix = (Matrix.Translation(Vector(center) + Vector((0, 0, height * 0.5))) @
                  Matrix.Rotation(rotation, 4, "Z"))
        bmesh.ops.create_cone(self.bm, cap_ends=True, cap_tris=False, segments=segments,
                              radius1=radius_bottom, radius2=radius_top, depth=height,
                              matrix=matrix)
        self._paint_new(before, color)

    def blob(self, center, radius, color_top, color_side, color_bottom, subdivisions=1,
             squash=1.0):
        """Icosphere colored by face direction so the shape reads even without lighting."""
        before = self._snapshot()
        matrix = Matrix.Translation(center) @ Matrix.Diagonal((1.0, 1.0, squash, 1.0))
        bmesh.ops.create_icosphere(self.bm, subdivisions=subdivisions, radius=radius,
                                   matrix=matrix)
        for face in self.bm.faces:
            if face in before:
                continue
            nz = face.normal.z
            color = color_top if nz > 0.45 else color_bottom if nz < -0.35 else color_side
            rgba = hex_color(color)
            for loop in face.loops:
                loop[self.layer] = rgba

    def prism(self, outline, y_min, y_max, color):
        """Extrudes an XZ outline (list of (x, z)) along Y; used for gables and roofs."""
        before = self._snapshot()
        front = [self.bm.verts.new((x, y_min, z)) for x, z in outline]
        back = [self.bm.verts.new((x, y_max, z)) for x, z in outline]
        count = len(outline)
        self.bm.faces.new(list(reversed(front)))
        self.bm.faces.new(back)
        for i in range(count):
            j = (i + 1) % count
            self.bm.faces.new((front[i], front[j], back[j], back[i]))
        self._paint_new(before, color)

    def flat_polygon(self, outline, z, color):
        """Upward-facing polygon (counter-clockwise outline in XY), triangulated so concave
        outlines such as wavy road edges stay correct after export."""
        before = self._snapshot()
        verts = [self.bm.verts.new((x, y, z)) for x, y in outline]
        face = self.bm.faces.new(verts)
        bmesh.ops.triangulate(self.bm, faces=[face])
        self._paint_new(before, color)

    def slab(self, outline, z0, z1, top_color, side_color):
        """Convex or concave XY outline extruded upward: a petal, leaf or flat stone."""
        before = self._snapshot()
        bottom = [self.bm.verts.new((x, y, z0)) for x, y in outline]
        top = [self.bm.verts.new((x, y, z1)) for x, y in outline]
        top_face = self.bm.faces.new(top)
        bottom_face = self.bm.faces.new(list(reversed(bottom)))
        count = len(outline)
        sides = [self.bm.faces.new((bottom[i], bottom[(i + 1) % count], top[(i + 1) % count], top[i]))
                 for i in range(count)]
        new_faces = bmesh.ops.triangulate(self.bm, faces=[top_face, bottom_face])["faces"]
        self._paint_new(before, side_color)
        rgba = hex_color(top_color)
        for face in new_faces:
            if face.calc_center_median().z > (z0 + z1) * 0.5:
                for loop in face.loops:
                    loop[self.layer] = rgba
        return sides

    def blade(self, base, width, height, lean, yaw, dark, light):
        """One grass blade: a thin three-sided spike, darker at the root side faces."""
        before = self._snapshot()
        bx, by = base
        c, s = math.cos(yaw), math.sin(yaw)
        half = width * 0.5
        thick = width * 0.35
        corners = [(-half, 0.0), (half, 0.0), (0.0, thick)]
        verts = [self.bm.verts.new((bx + x * c - y * s, by + x * s + y * c, 0.0)) for x, y in corners]
        tip = self.bm.verts.new((bx - lean * s, by + lean * c, height))
        faces = [self.bm.faces.new((verts[0], verts[1], tip)), self.bm.faces.new((verts[1], verts[2], tip)),
                 self.bm.faces.new((verts[2], verts[0], tip))]
        centroid = (verts[0].co + verts[1].co + verts[2].co + tip.co) * 0.25
        for face in faces:
            face.normal_update()
            if face.normal.dot(face.calc_center_median() - centroid) < 0.0:
                face.normal_flip()
        self._paint_new(before, dark)
        rgba = hex_color(light)
        for loop in faces[0].loops:
            loop[self.layer] = rgba

    def jitter_blob(self, center, radii, subdivisions, seed, amount, color_for_normal):
        """Icosphere scaled to radii and nudged per vertex with a fixed seed (repeatable)."""
        import random
        before = self._snapshot()
        rng = random.Random(seed)
        matrix = Matrix.Translation(center) @ Matrix.Diagonal((radii[0], radii[1], radii[2], 1.0))
        result = bmesh.ops.create_icosphere(self.bm, subdivisions=subdivisions, radius=1.0, matrix=matrix)
        for vert in result["verts"]:
            vert.co += Vector((rng.uniform(-amount, amount), rng.uniform(-amount, amount),
                               rng.uniform(-amount, amount) * 0.6))
        self.bm.normal_update()
        for face in self.bm.faces:
            if face in before:
                continue
            rgba = hex_color(color_for_normal(face.normal, face.calc_center_median()))
            for loop in face.loops:
                loop[self.layer] = rgba

    def cylinder_between(self, p0, p1, r0, r1, segments, color):
        """A tapered cylinder from p0 to p1 (limbs, horns, branches)."""
        before = self._snapshot()
        p0, p1 = Vector(p0), Vector(p1)
        axis = p1 - p0
        rotation = Vector((0, 0, 1)).rotation_difference(axis.normalized()).to_matrix().to_4x4()
        matrix = Matrix.Translation((p0 + p1) * 0.5) @ rotation
        bmesh.ops.create_cone(self.bm, cap_ends=True, cap_tris=False, segments=segments,
                              radius1=r0, radius2=r1, depth=axis.length, matrix=matrix)
        self._paint_new(before, color)

    def to_object(self, name, collection=None, recalc_normals=True):
        if recalc_normals:
            bmesh.ops.recalc_face_normals(self.bm, faces=self.bm.faces)
        mesh = bpy.data.meshes.new(name + "_Mesh")
        self.bm.to_mesh(mesh)
        self.bm.free()
        for polygon in mesh.polygons:
            polygon.use_smooth = False
        attribute = mesh.color_attributes.get(COLOR_LAYER)
        mesh.color_attributes.active_color = attribute
        mesh.color_attributes.render_color_index = mesh.color_attributes.find(COLOR_LAYER)
        obj = bpy.data.objects.new(name, mesh)
        (collection or bpy.context.scene.collection).objects.link(obj)
        material = bpy.data.materials.get("HALKA_VertexColor")
        if material is None:
            material = bpy.data.materials.new("HALKA_VertexColor")
        mesh.materials.append(material)
        return obj


def reset_scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    scene.unit_settings.system = "METRIC"
    scene.unit_settings.scale_length = 1.0


def add_cell_guides(name, cells):
    """Non-exported empties that show the Grid cells an asset covers in the .blend file."""
    guides = bpy.data.collections.new(name + "_GridGuides")
    bpy.context.scene.collection.children.link(guides)
    for cx, cy in cells:
        empty = bpy.data.objects.new(f"cell_{cx}_{cy}", None)
        empty.empty_display_type = "PLAIN_AXES"
        empty.empty_display_size = CELL * 0.5
        empty.location = (cx * CELL, cy * CELL, 0.0)
        guides.objects.link(empty)
    return guides


def export_fbx(obj, path, extra=()):
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    for item in extra:
        item.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.export_scene.fbx(
        filepath=path,
        use_selection=True,
        object_types={"MESH", "ARMATURE", "EMPTY"},
        apply_unit_scale=True,
        apply_scale_options="FBX_SCALE_ALL",
        axis_forward="-Z",
        axis_up="Y",
        bake_space_transform=True,
        use_mesh_modifiers=True,
        mesh_smooth_type="FACE",
        colors_type="SRGB",
        prioritize_active_color=True,
        use_triangles=False,
        add_leaf_bones=False,
        bake_anim=False,
        use_custom_props=False,
        path_mode="STRIP",
        use_metadata=True,
    )


def add_anchor(name, parent, location):
    """An exported Empty under a model, e.g. a future Crow perch point on a tree."""
    empty = bpy.data.objects.new(name, None)
    empty.empty_display_type = "SPHERE"
    empty.empty_display_size = 0.05
    bpy.context.scene.collection.objects.link(empty)
    empty.parent = parent
    empty.location = location
    return empty


def material_count(obj):
    return len(obj.data.materials)


def triangle_count(obj):
    return sum(len(p.vertices) - 2 for p in obj.data.polygons)


def deg(value):
    return math.radians(value)
