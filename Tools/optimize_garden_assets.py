"""Build Quest-ready FBX + 512 px albedo assets from the seven Meshy GLBs.

Run with Blender, not Python:
  /Applications/Blender.app/Contents/MacOS/Blender --background \
    --python Tools/optimize_garden_assets.py

The source GLBs are never modified. Each output is one 3,000-triangle mesh with a
grounded pivot, real-world metre dimensions, one material, and one 512 px albedo.
"""

from pathlib import Path
import json
import math

import bpy
import bmesh
from mathutils import Matrix, Vector


PROJECT = Path("/Users/kellyjia/Osmanthus")
OUTPUT_ROOT = PROJECT / "Assets/Osmanthus/Art/GardenPlanting"
MODEL_DIR = OUTPUT_ROOT / "FBX"
TEXTURE_DIR = OUTPUT_ROOT / "Textures"
REPORT_PATH = OUTPUT_ROOT / "optimization_report.json"
TARGET_TRIANGLES = 3000

# Dimensions are width (X), depth (Y in Blender), height (Z), in metres.
ASSETS = [
    {
        "name": "01_Osmanthus_Tree",
        "source": "meshy_output/20260821_102330_01-osmanthus-tree_01a02334/01_Osmanthus_Tree.glb",
        "dimensions": (3.4, 3.4, 4.2),
    },
    {
        "name": "02_Taihu_Standing_Stone",
        "source": "meshy_output/20260821_102922_02-taihu-standing-stone_01a02339/02_Taihu_Standing_Stone.glb",
        "dimensions": (1.1, 0.9, 2.8),
    },
    {
        "name": "03_Weeping_Willow",
        "source": "meshy_output/20260821_103219_03-weeping-willow_01a0233c/03_Weeping_Willow.glb",
        "dimensions": (5.5, 5.5, 6.5),
    },
    {
        "name": "04_Rockery_Cluster",
        "source": "meshy_output/20260821_103826_04-rockery-cluster_01a02341/04_Rockery_Cluster.glb",
        "dimensions": (3.2, 2.0, 1.3),
    },
    {
        "name": "05_Flowering_Shrub_Clump",
        "source": "meshy_output/20260821_104149_05-flowering-shrub-clump_01a02344/05_Flowering_Shrub_Clump.glb",
        "dimensions": (1.6, 1.4, 0.9),
    },
    {
        "name": "06_Bamboo_Clump",
        "source": "meshy_output/20260821_104524_06-bamboo-clump_01a02348/06_Bamboo_Clump.glb",
        "dimensions": (2.4, 2.0, 3.5),
    },
    {
        "name": "07_Water_Edge_Clump",
        "source": "meshy_output/20260821_104915_07-water-edge-clump_01a0234b/07_Water_Edge_Clump.glb",
        "dimensions": (2.2, 1.6, 0.8),
    },
]


def reset_scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)


def mesh_triangles(obj):
    return sum(max(0, len(poly.vertices) - 2) for poly in obj.data.polygons)


def world_bounds(obj):
    corners = [obj.matrix_world @ Vector(corner) for corner in obj.bound_box]
    minimum = Vector((min(v.x for v in corners), min(v.y for v in corners), min(v.z for v in corners)))
    maximum = Vector((max(v.x for v in corners), max(v.y for v in corners), max(v.z for v in corners)))
    return minimum, maximum


def find_base_color_image(materials):
    for material in materials:
        if not material or not material.use_nodes:
            continue
        principled = next((n for n in material.node_tree.nodes if n.type == "BSDF_PRINCIPLED"), None)
        if principled:
            base_input = principled.inputs.get("Base Color")
            if base_input and base_input.is_linked:
                node = base_input.links[0].from_node
                if node.type == "TEX_IMAGE" and node.image:
                    return node.image
        for node in material.node_tree.nodes:
            if node.type == "TEX_IMAGE" and node.image and "base" in node.image.name.lower():
                return node.image
    raise RuntimeError("No base-color image found")


def join_meshes():
    meshes = [obj for obj in bpy.context.scene.objects if obj.type == "MESH"]
    if not meshes:
        raise RuntimeError("Imported GLB contains no mesh")
    bpy.ops.object.select_all(action="DESELECT")
    for obj in meshes:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = meshes[0]
    if len(meshes) > 1:
        bpy.ops.object.join()
    obj = bpy.context.view_layer.objects.active
    obj.data.transform(obj.matrix_world)
    obj.matrix_world = Matrix.Identity(4)
    return obj


def scale_and_ground(obj, dimensions):
    minimum, maximum = world_bounds(obj)
    current = maximum - minimum
    if min(current) <= 0:
        raise RuntimeError(f"Degenerate bounds: {tuple(current)}")
    scale = Vector((dimensions[0] / current.x, dimensions[1] / current.y, dimensions[2] / current.z))
    for vertex in obj.data.vertices:
        vertex.co.x *= scale.x
        vertex.co.y *= scale.y
        vertex.co.z *= scale.z

    minimum, maximum = world_bounds(obj)
    offset = Vector((-(minimum.x + maximum.x) * 0.5, -(minimum.y + maximum.y) * 0.5, -minimum.z))
    for vertex in obj.data.vertices:
        vertex.co += offset
    obj.data.update()


def decimate(obj):
    before = mesh_triangles(obj)
    pass_number = 1
    # Blender clamps one collapse modifier to a minimum ratio of 0.01. Meshy's
    # 1.9 M triangle output therefore needs two passes to reach 3 K.
    while mesh_triangles(obj) > TARGET_TRIANGLES + 50:
        current = mesh_triangles(obj)
        ratio = max(0.01, TARGET_TRIANGLES / float(current))
        modifier = obj.modifiers.new(name=f"Quest_Decimate_{pass_number}", type="DECIMATE")
        modifier.decimate_type = "COLLAPSE"
        modifier.ratio = ratio
        modifier.use_collapse_triangulate = True
        bpy.context.view_layer.objects.active = obj
        bpy.ops.object.modifier_apply(modifier=modifier.name)
        after_pass = mesh_triangles(obj)
        if after_pass >= current:
            # Meshy foliage is built from thousands of disconnected leaf cards.
            # A collapse modifier cannot reduce a one-triangle island, so retain a
            # deterministic, spatially distributed subset of those cards instead.
            prune_disconnected_islands(obj, TARGET_TRIANGLES)
            break
        pass_number += 1
    return before, mesh_triangles(obj)


def prune_disconnected_islands(obj, triangle_budget):
    mesh = obj.data
    vertex_faces = [[] for _ in mesh.vertices]
    for poly in mesh.polygons:
        for vertex_index in poly.vertices:
            vertex_faces[vertex_index].append(poly.index)

    unvisited = set(range(len(mesh.polygons)))
    components = []
    while unvisited:
        seed = unvisited.pop()
        stack = [seed]
        face_indices = [seed]
        while stack:
            face_index = stack.pop()
            for vertex_index in mesh.polygons[face_index].vertices:
                for neighbour in vertex_faces[vertex_index]:
                    if neighbour in unvisited:
                        unvisited.remove(neighbour)
                        stack.append(neighbour)
                        face_indices.append(neighbour)
        tris = sum(max(0, len(mesh.polygons[i].vertices) - 2) for i in face_indices)
        center = sum((mesh.polygons[i].center for i in face_indices), Vector((0.0, 0.0, 0.0))) / len(face_indices)
        # Stable spatial hash prevents retaining only one end of the source mesh.
        order = math.sin(center.x * 12.9898 + center.y * 78.233 + center.z * 37.719) * 43758.5453
        components.append((face_indices, tris, order - math.floor(order)))

    # Always prioritize substantial connected forms (trunks, stones, stems), then
    # distribute the remaining budget across the tiny leaf/grass card islands.
    substantial = sorted((c for c in components if c[1] > 8), key=lambda c: c[1], reverse=True)
    cards = sorted((c for c in components if c[1] <= 8), key=lambda c: c[2])
    keep = []
    used = 0
    for component in substantial + cards:
        if used + component[1] <= triangle_budget:
            keep.extend(component[0])
            used += component[1]

    keep_set = set(keep)
    bm = bmesh.new()
    bm.from_mesh(mesh)
    bm.faces.ensure_lookup_table()
    remove = [face for face in bm.faces if face.index not in keep_set]
    bmesh.ops.delete(bm, geom=remove, context="FACES")
    bm.to_mesh(mesh)
    bm.free()
    mesh.update()
    print(
        f"[GardenOptimize] Pruned disconnected cards: {len(components):,} islands, "
        f"{used:,} triangles retained"
    )


def save_albedo_and_strip_materials(obj, name):
    source_materials = [slot.material for slot in obj.material_slots if slot.material]
    image = find_base_color_image(source_materials)
    image.scale(512, 512)
    albedo_path = TEXTURE_DIR / f"{name}_Albedo.png"
    image.filepath_raw = str(albedo_path)
    image.file_format = "PNG"
    image.save()

    material = bpy.data.materials.new(name=f"M_{name}")
    material.use_nodes = True
    nodes = material.node_tree.nodes
    links = material.node_tree.links
    nodes.clear()
    output = nodes.new("ShaderNodeOutputMaterial")
    shader = nodes.new("ShaderNodeBsdfPrincipled")
    texture = nodes.new("ShaderNodeTexImage")
    texture.image = image
    shader.inputs["Roughness"].default_value = 1.0
    shader.inputs["Metallic IOR" if "Metallic IOR" in shader.inputs else "Metallic"].default_value = 0.0
    links.new(texture.outputs["Color"], shader.inputs["Base Color"])
    links.new(shader.outputs["BSDF"], output.inputs["Surface"])

    obj.data.materials.clear()
    obj.data.materials.append(material)
    for polygon in obj.data.polygons:
        polygon.material_index = 0
    return albedo_path


def export_fbx(obj, name):
    model_path = MODEL_DIR / f"{name}.fbx"
    obj.name = name
    obj.data.name = f"{name}_Mesh"
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.export_scene.fbx(
        filepath=str(model_path),
        use_selection=True,
        object_types={"MESH"},
        apply_unit_scale=True,
        apply_scale_options="FBX_SCALE_ALL",
        axis_forward="-Z",
        axis_up="Y",
        bake_space_transform=False,
        add_leaf_bones=False,
        use_mesh_modifiers=True,
        mesh_smooth_type="FACE",
        path_mode="AUTO",
        embed_textures=False,
    )
    return model_path


def optimize(spec):
    reset_scene()
    source = PROJECT / spec["source"]
    print(f"[GardenOptimize] Importing {spec['name']}: {source}")
    bpy.ops.import_scene.gltf(filepath=str(source))
    obj = join_meshes()
    scale_and_ground(obj, spec["dimensions"])
    original_triangles, final_triangles = decimate(obj)
    scale_and_ground(obj, spec["dimensions"])
    albedo_path = save_albedo_and_strip_materials(obj, spec["name"])
    model_path = export_fbx(obj, spec["name"])
    minimum, maximum = world_bounds(obj)
    report = {
        "name": spec["name"],
        "source": str(source),
        "model": str(model_path),
        "albedo": str(albedo_path),
        "original_triangles": original_triangles,
        "final_triangles": final_triangles,
        "bounds_min_metres": [round(v, 5) for v in minimum],
        "bounds_max_metres": [round(v, 5) for v in maximum],
        "dimensions_metres": list(spec["dimensions"]),
    }
    print(f"[GardenOptimize] {spec['name']}: {original_triangles:,} -> {final_triangles:,} tris")
    return report


def main():
    MODEL_DIR.mkdir(parents=True, exist_ok=True)
    TEXTURE_DIR.mkdir(parents=True, exist_ok=True)
    reports = [optimize(spec) for spec in ASSETS]
    REPORT_PATH.write_text(json.dumps(reports, indent=2), encoding="utf-8")
    print(f"[GardenOptimize] Complete. Report: {REPORT_PATH}")


if __name__ == "__main__":
    main()
