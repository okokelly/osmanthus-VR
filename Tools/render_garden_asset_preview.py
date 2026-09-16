"""Render a neutral studio preview of a Quest-ready garden FBX."""

from pathlib import Path
import sys

import bpy
from mathutils import Vector


PROJECT = Path("/Users/kellyjia/Osmanthus")
ASSET_NAME = sys.argv[sys.argv.index("--") + 1] if "--" in sys.argv else "01_Osmanthus_Tree"
MODEL = PROJECT / "Assets/Osmanthus/Art/GardenPlanting/FBX" / f"{ASSET_NAME}.fbx"
ALBEDO = PROJECT / "Assets/Osmanthus/Art/GardenPlanting/Textures" / f"{ASSET_NAME}_Albedo.png"
OUTPUT = PROJECT / "Assets/Osmanthus/Art/GardenPlanting/Previews" / f"{ASSET_NAME}_Quest_Preview.png"


def look_at(obj, point):
    obj.rotation_euler = (Vector(point) - obj.location).to_track_quat("-Z", "Y").to_euler()


bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=str(MODEL))
meshes = [obj for obj in bpy.context.scene.objects if obj.type == "MESH"]
if not meshes:
    raise RuntimeError(f"No mesh imported from {MODEL}")

image = bpy.data.images.load(str(ALBEDO), check_existing=True)
material = bpy.data.materials.new(name="Preview_UnlitAlbedo")
material.use_nodes = True
nodes = material.node_tree.nodes
links = material.node_tree.links
nodes.clear()
out = nodes.new("ShaderNodeOutputMaterial")
shader = nodes.new("ShaderNodeBsdfPrincipled")
texture = nodes.new("ShaderNodeTexImage")
texture.image = image
shader.inputs["Roughness"].default_value = 1.0
links.new(texture.outputs["Color"], shader.inputs["Base Color"])
links.new(shader.outputs["BSDF"], out.inputs["Surface"])
for obj in meshes:
    obj.data.materials.clear()
    obj.data.materials.append(material)

minimum = Vector((1e9, 1e9, 1e9))
maximum = Vector((-1e9, -1e9, -1e9))
for obj in meshes:
    for corner in obj.bound_box:
        world = obj.matrix_world @ Vector(corner)
        minimum.x, minimum.y, minimum.z = min(minimum.x, world.x), min(minimum.y, world.y), min(minimum.z, world.z)
        maximum.x, maximum.y, maximum.z = max(maximum.x, world.x), max(maximum.y, world.y), max(maximum.z, world.z)
center = (minimum + maximum) * 0.5
height = maximum.z - minimum.z
width = max(maximum.x - minimum.x, maximum.y - minimum.y)

# Matte ground catches the contact shadow and makes the grounded pivot visible.
bpy.ops.mesh.primitive_plane_add(size=max(12.0, width * 3.0), location=(center.x, center.y, minimum.z - 0.004))
ground = bpy.context.object
ground_mat = bpy.data.materials.new(name="PreviewGround")
ground_mat.diffuse_color = (0.32, 0.34, 0.29, 1.0)
ground.data.materials.append(ground_mat)

camera_data = bpy.data.cameras.new("PreviewCamera")
camera = bpy.data.objects.new("PreviewCamera", camera_data)
bpy.context.collection.objects.link(camera)
distance = max(height, width) * 1.65
camera.location = (center.x + distance * 0.72, center.y - distance, minimum.z + height * 0.60)
camera_data.lens = 58
look_at(camera, (center.x, center.y, minimum.z + height * 0.47))
bpy.context.scene.camera = camera

key_data = bpy.data.lights.new("Key", type="AREA")
key_data.energy = 950
key_data.shape = "DISK"
key_data.size = max(height, width) * 1.4
key = bpy.data.objects.new("Key", key_data)
bpy.context.collection.objects.link(key)
key.location = (center.x - width, center.y - width, minimum.z + height * 1.45)
look_at(key, center)

fill_data = bpy.data.lights.new("Fill", type="AREA")
fill_data.energy = 550
fill_data.size = max(height, width)
fill = bpy.data.objects.new("Fill", fill_data)
bpy.context.collection.objects.link(fill)
fill.location = (center.x + width * 1.4, center.y + width, minimum.z + height)
look_at(fill, center)

world = bpy.data.worlds.new("PreviewWorld")
world.use_nodes = True
world.node_tree.nodes["Background"].inputs["Color"].default_value = (0.045, 0.055, 0.045, 1.0)
world.node_tree.nodes["Background"].inputs["Strength"].default_value = 0.55
bpy.context.scene.world = world

scene = bpy.context.scene
scene.render.engine = "BLENDER_EEVEE"
scene.render.resolution_x = 1000
scene.render.resolution_y = 1000
scene.render.resolution_percentage = 100
scene.render.image_settings.file_format = "PNG"
scene.render.filepath = str(OUTPUT)
scene.render.film_transparent = False
scene.render.image_settings.color_mode = "RGBA"
scene.view_settings.look = "AgX - Medium High Contrast"
OUTPUT.parent.mkdir(parents=True, exist_ok=True)
bpy.ops.render.render(write_still=True)
print(f"[GardenPreview] Saved {OUTPUT}")
