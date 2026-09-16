"""Prepare one Meshy Remesh FBX for the Osmanthus Unity project.

Usage (after Blender's own arguments):
  -- <name> <input.fbx> <input_albedo> <width> <depth> <height>
"""

from pathlib import Path
import sys

import bpy
from mathutils import Matrix, Vector


PROJECT = Path("/Users/kellyjia/Osmanthus")
args = sys.argv[sys.argv.index("--") + 1 :]
if len(args) != 6:
    raise RuntimeError("Expected: name input.fbx input_albedo width depth height")

name, input_fbx, input_albedo = args[:3]
dimensions = tuple(float(value) for value in args[3:])
model_dir = PROJECT / "Assets/Osmanthus/Art/GardenPlanting/FBX"
texture_dir = PROJECT / "Assets/Osmanthus/Art/GardenPlanting/Textures"
model_path = model_dir / f"{name}.fbx"
texture_path = texture_dir / f"{name}_Albedo.png"
model_dir.mkdir(parents=True, exist_ok=True)
texture_dir.mkdir(parents=True, exist_ok=True)

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=input_fbx)
meshes = [obj for obj in bpy.context.scene.objects if obj.type == "MESH"]
if not meshes:
    raise RuntimeError("FBX contains no mesh")

bpy.ops.object.select_all(action="DESELECT")
for obj in meshes:
    obj.select_set(True)
bpy.context.view_layer.objects.active = meshes[0]
if len(meshes) > 1:
    bpy.ops.object.join()
obj = bpy.context.view_layer.objects.active
obj.data.transform(obj.matrix_world)
obj.matrix_world = Matrix.Identity(4)

# Meshy documents target_polycount as approximate. Enforce the Quest budget when
# its result lands a few percent above the requested 3,000-triangle ceiling.
triangle_count = sum(max(0, len(poly.vertices) - 2) for poly in obj.data.polygons)
if triangle_count > 3000:
    modifier = obj.modifiers.new(name="Quest_3000_Triangle_Ceiling", type="DECIMATE")
    modifier.decimate_type = "COLLAPSE"
    modifier.ratio = 2990.0 / triangle_count
    modifier.use_collapse_triangulate = True
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.modifier_apply(modifier=modifier.name)

minimum = Vector(tuple(min(vertex.co[i] for vertex in obj.data.vertices) for i in range(3)))
maximum = Vector(tuple(max(vertex.co[i] for vertex in obj.data.vertices) for i in range(3)))
current = maximum - minimum
scale = Vector((dimensions[0] / current.x, dimensions[1] / current.y, dimensions[2] / current.z))
for vertex in obj.data.vertices:
    vertex.co.x *= scale.x
    vertex.co.y *= scale.y
    vertex.co.z *= scale.z

minimum = Vector(tuple(min(vertex.co[i] for vertex in obj.data.vertices) for i in range(3)))
maximum = Vector(tuple(max(vertex.co[i] for vertex in obj.data.vertices) for i in range(3)))
offset = Vector((-(minimum.x + maximum.x) * 0.5, -(minimum.y + maximum.y) * 0.5, -minimum.z))
for vertex in obj.data.vertices:
    vertex.co += offset
obj.data.update()

image = bpy.data.images.load(input_albedo, check_existing=True)
image.scale(512, 512)
image.filepath_raw = str(texture_path)
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
links.new(texture.outputs["Color"], shader.inputs["Base Color"])
links.new(shader.outputs["BSDF"], output.inputs["Surface"])
obj.data.materials.clear()
obj.data.materials.append(material)
for polygon in obj.data.polygons:
    polygon.material_index = 0

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
    add_leaf_bones=False,
    mesh_smooth_type="FACE",
    path_mode="AUTO",
    embed_textures=False,
)

triangles = sum(max(0, len(poly.vertices) - 2) for poly in obj.data.polygons)
print(f"[GardenPostprocess] {name}: {triangles:,} triangles")
print(f"[GardenPostprocess] Model: {model_path}")
print(f"[GardenPostprocess] Albedo: {texture_path}")
