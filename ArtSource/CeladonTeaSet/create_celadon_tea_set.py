import bpy
import math
import os
from mathutils import Vector


ROOT_DIR = os.path.dirname(os.path.abspath(__file__))
PROJECT_DIR = os.path.abspath(os.path.join(ROOT_DIR, "..", ".."))
RENDER_DIR = os.path.join(ROOT_DIR, "renders")
BLEND_PATH = os.path.join(ROOT_DIR, "CeladonTeaSet_Stylized.blend")
FBX_PATH = os.path.join(PROJECT_DIR, "Assets", "Art", "Models", "CeladonTeaSet", "CeladonTeaSet_Stylized.fbx")
os.makedirs(RENDER_DIR, exist_ok=True)
os.makedirs(os.path.dirname(FBX_PATH), exist_ok=True)

bpy.ops.object.select_all(action="SELECT")
bpy.ops.object.delete(use_global=False)

scene = bpy.context.scene
scene.unit_settings.system = "METRIC"
scene.unit_settings.length_unit = "METERS"
scene.render.engine = "BLENDER_EEVEE"
scene.render.resolution_x = 1600
scene.render.resolution_y = 1000
scene.render.resolution_percentage = 100
scene.render.image_settings.file_format = "PNG"
scene.world.color = (0.025, 0.035, 0.035)
scene.view_settings.exposure = -1.0

asset_col = bpy.data.collections.new("CeladonTeaSet_Asset")
render_col = bpy.data.collections.new("_RenderHelpers")
scene.collection.children.link(asset_col)
scene.collection.children.link(render_col)


def move_to(obj, collection):
    for col in list(obj.users_collection):
        col.objects.unlink(obj)
    collection.objects.link(obj)


def make_material(name, color, roughness=0.22, metallic=0.0):
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    mat.diffuse_color = color
    bsdf = mat.node_tree.nodes.get("Principled BSDF")
    bsdf.inputs["Base Color"].default_value = color
    bsdf.inputs["Roughness"].default_value = roughness
    bsdf.inputs["Metallic"].default_value = metallic
    bsdf.inputs["IOR"].default_value = 1.46
    bsdf.inputs["Coat Weight"].default_value = 0.20
    bsdf.inputs["Coat Roughness"].default_value = 0.12
    return mat


M_CELADON = make_material("M_Celadon_JadeGreen", (0.16, 0.40, 0.30, 1.0), 0.20)
M_CELADON_LIGHT = make_material("M_Celadon_LightGlaze", (0.29, 0.54, 0.42, 1.0), 0.18)
M_CELADON_DARK = make_material("M_Celadon_DeepGlaze", (0.07, 0.23, 0.17, 1.0), 0.24)
M_TEA = make_material("M_Tea_Amber", (0.35, 0.12, 0.025, 1.0), 0.12)
M_CAVITY = make_material("M_Ceramic_Cavity", (0.008, 0.018, 0.014, 1.0), 0.90)
M_FLOOR = make_material("M_RenderFloor", (0.10, 0.115, 0.11, 1.0), 0.86)

root = bpy.data.objects.new("CeladonTeaSet_Stylized_ROOT", None)
asset_col.objects.link(root)
root["target"] = "Unity URP / Meta Quest"
root["unit"] = "meter"
root["style"] = "stylized Longquan-inspired celadon"


def new_interaction_root(name, location):
    obj = bpy.data.objects.new(name, None)
    asset_col.objects.link(obj)
    obj.location = location
    obj.parent = root
    obj["interaction_ready"] = True
    return obj


def link_asset(obj, parent, name=None):
    move_to(obj, asset_col)
    obj.parent = parent
    if name:
        obj.name = name
    return obj


def lathe(name, profile, segments, material, parent, location=(0, 0, 0)):
    verts = []
    rings = len(profile)
    for i in range(segments):
        a = i * math.tau / segments
        ca, sa = math.cos(a), math.sin(a)
        for radius, z in profile:
            verts.append((radius * ca + location[0], radius * sa + location[1], z + location[2]))
    faces = []
    for i in range(segments):
        ni = (i + 1) % segments
        for j in range(rings - 1):
            faces.append((i * rings + j, ni * rings + j, ni * rings + j + 1, i * rings + j + 1))
    mesh = bpy.data.meshes.new(name + "_Mesh")
    mesh.from_pydata(verts, [], faces)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    asset_col.objects.link(obj)
    obj.parent = parent
    obj.data.materials.append(material)
    for poly in obj.data.polygons:
        poly.use_smooth = True
    bevel = obj.modifiers.new("Glazed_Rim_Soften", "BEVEL")
    bevel.width = 0.0018
    bevel.segments = 2
    return obj


def tube_along_path(name, path, radii, sides, material, parent, cap_start=True, cap_end=False):
    verts = []
    n = len(path)
    for i, point in enumerate(path):
        point = Vector(point)
        if i == 0:
            tangent = (Vector(path[1]) - point).normalized()
        elif i == n - 1:
            tangent = (point - Vector(path[i - 1])).normalized()
        else:
            tangent = (Vector(path[i + 1]) - Vector(path[i - 1])).normalized()
        reference = Vector((0, 1, 0))
        if abs(tangent.dot(reference)) > 0.92:
            reference = Vector((0, 0, 1))
        normal = tangent.cross(reference).normalized()
        binormal = tangent.cross(normal).normalized()
        for s in range(sides):
            a = s * math.tau / sides
            offset = normal * math.cos(a) * radii[i] + binormal * math.sin(a) * radii[i]
            verts.append(tuple(point + offset))
    faces = []
    for i in range(n - 1):
        for s in range(sides):
            ns = (s + 1) % sides
            faces.append((i * sides + s, (i + 1) * sides + s, (i + 1) * sides + ns, i * sides + ns))
    if cap_start:
        faces.append(tuple(range(sides - 1, -1, -1)))
    if cap_end:
        start = (n - 1) * sides
        faces.append(tuple(start + s for s in range(sides)))
    mesh = bpy.data.meshes.new(name + "_Mesh")
    mesh.from_pydata(verts, [], faces)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    asset_col.objects.link(obj)
    obj.parent = parent
    obj.data.materials.append(material)
    for poly in obj.data.polygons:
        poly.use_smooth = True
    bevel = obj.modifiers.new("Glazed_Edge_Soften", "BEVEL")
    bevel.width = 0.0015
    bevel.segments = 2
    return obj


def cube(name, location, dimensions, material, parent, bevel=0.008, collection=asset_col):
    bpy.ops.mesh.primitive_cube_add(location=location)
    obj = bpy.context.object
    obj.name = name
    obj.dimensions = dimensions
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    obj.data.materials.append(material)
    move_to(obj, collection)
    obj.parent = parent
    if bevel:
        mod = obj.modifiers.new("Rounded_Glaze_Edges", "BEVEL")
        mod.width = bevel
        mod.segments = 4
    return obj


tray_root = new_interaction_root("Tray_InteractionRoot", (0, 0, 0))
teapot_root = new_interaction_root("Teapot_InteractionRoot", (-0.075, 0.045, 0.043))
lid_root = new_interaction_root("TeapotLid_InteractionRoot", (-0.075, 0.045, 0.043))
cup_a_root = new_interaction_root("Cup_A_InteractionRoot", (0.115, -0.068, 0.043))
cup_b_root = new_interaction_root("Cup_B_InteractionRoot", (0.125, 0.078, 0.043))

# Shallow ceramic tray with a raised rounded rim.
cube("Tray_Base", (0, 0, 0.021), (0.50, 0.34, 0.032), M_CELADON_DARK, tray_root, bevel=0.035)
cube("Tray_Inset", (0, 0, 0.041), (0.448, 0.288, 0.018), M_CELADON_LIGHT, tray_root, bevel=0.027)
cube("Tray_Rim_Front", (0, -0.154, 0.058), (0.445, 0.027, 0.038), M_CELADON, tray_root, bevel=0.013)
cube("Tray_Rim_Back", (0, 0.154, 0.058), (0.445, 0.027, 0.038), M_CELADON, tray_root, bevel=0.013)
cube("Tray_Rim_Left", (-0.234, 0, 0.058), (0.027, 0.285, 0.038), M_CELADON, tray_root, bevel=0.013)
cube("Tray_Rim_Right", (0.234, 0, 0.058), (0.027, 0.285, 0.038), M_CELADON, tray_root, bevel=0.013)

# Teapot body: rotational body with real open mouth and wall thickness.
teapot_profile = [
    (0.000, 0.000), (0.048, 0.000), (0.066, 0.008), (0.084, 0.032),
    (0.099, 0.075), (0.102, 0.112), (0.092, 0.148), (0.073, 0.178),
    (0.056, 0.193), (0.052, 0.204), (0.043, 0.204), (0.044, 0.192),
    (0.060, 0.174), (0.080, 0.145), (0.090, 0.111), (0.087, 0.078),
    (0.074, 0.040), (0.059, 0.016), (0.043, 0.008), (0.000, 0.008),
]
lathe("Teapot_Body", teapot_profile, 48, M_CELADON, teapot_root)

# Foot ring.
foot_profile = [(0.0, -0.001), (0.050, -0.001), (0.055, 0.004), (0.052, 0.010), (0.0, 0.010)]
lathe("Teapot_FootRing", foot_profile, 40, M_CELADON_DARK, teapot_root)

# Tapered upturned spout and open rim.
spout_path = [
    (0.060, 0.0, 0.070), (0.096, 0.0, 0.082), (0.129, 0.0, 0.112),
    (0.153, 0.0, 0.151), (0.171, 0.0, 0.181),
]
tube_along_path("Teapot_Spout", spout_path, [0.025, 0.024, 0.021, 0.017, 0.014], 16, M_CELADON, teapot_root, cap_start=True, cap_end=False)
tube_along_path("Teapot_SpoutInner", [(0.151, 0, 0.161), (0.165, 0, 0.176)], [0.0105, 0.009], 16, M_CAVITY, teapot_root, cap_start=False, cap_end=True)

# High loop handle on the opposite side.
handle_path = [
    (-0.073, 0.0, 0.066), (-0.112, 0.0, 0.071), (-0.151, 0.0, 0.101),
    (-0.166, 0.0, 0.146), (-0.151, 0.0, 0.184), (-0.111, 0.0, 0.198), (-0.071, 0.0, 0.176),
]
tube_along_path("Teapot_Handle", handle_path, [0.010] * len(handle_path), 12, M_CELADON_DARK, teapot_root, cap_start=True, cap_end=True)

# Independent lid and knob.
lid_profile = [
    (0.000, 0.205), (0.042, 0.205), (0.059, 0.210), (0.061, 0.218),
    (0.052, 0.231), (0.034, 0.241), (0.000, 0.244),
]
lathe("Teapot_Lid", lid_profile, 48, M_CELADON_LIGHT, lid_root)
knob_profile = [(0.0, 0.241), (0.013, 0.241), (0.018, 0.250), (0.015, 0.263), (0.0, 0.269)]
lathe("Teapot_LidKnob", knob_profile, 32, M_CELADON_DARK, lid_root)


def make_cup(prefix, parent):
    cup_profile = [
        (0.000, 0.000), (0.026, 0.000), (0.034, 0.007), (0.040, 0.032),
        (0.043, 0.060), (0.036, 0.060), (0.034, 0.036), (0.029, 0.012),
        (0.022, 0.006), (0.000, 0.006),
    ]
    lathe(prefix + "_Bowl", cup_profile, 40, M_CELADON_LIGHT, parent)
    foot = [(0.0, -0.001), (0.025, -0.001), (0.029, 0.003), (0.026, 0.008), (0.0, 0.008)]
    lathe(prefix + "_FootRing", foot, 36, M_CELADON_DARK, parent)
    tea = [(0.0, 0.047), (0.034, 0.047), (0.034, 0.049), (0.0, 0.049)]
    lathe(prefix + "_TeaSurface", tea, 40, M_TEA, parent)


make_cup("Cup_A", cup_a_root)
make_cup("Cup_B", cup_b_root)

# Render helpers.
cube("_RenderFloor", (0, 0, -0.018), (1.3, 1.0, 0.03), M_FLOOR, None, bevel=0.0, collection=render_col)


def area_light(name, location, energy, size, color):
    data = bpy.data.lights.new(name, "AREA")
    data.energy = energy
    data.shape = "DISK"
    data.size = size
    data.color = color
    obj = bpy.data.objects.new(name, data)
    render_col.objects.link(obj)
    obj.location = location
    return obj


def track(obj, target):
    obj.rotation_euler = (Vector(target) - obj.location).to_track_quat("-Z", "Y").to_euler()


key = area_light("Key_Warm", (-0.45, -0.55, 0.75), 110, 0.45, (1.0, 0.76, 0.54))
fill = area_light("Fill_Cool", (0.55, 0.35, 0.55), 70, 0.55, (0.52, 0.75, 1.0))
rim = area_light("Rim_Green", (-0.35, 0.52, 0.42), 55, 0.35, (0.48, 1.0, 0.72))
track(key, (0, 0, 0.10))
track(fill, (0, 0, 0.10))
track(rim, (-0.05, 0.04, 0.16))


def camera(name, location, target, lens=52, ortho=None):
    data = bpy.data.cameras.new(name)
    obj = bpy.data.objects.new(name, data)
    render_col.objects.link(obj)
    obj.location = location
    if ortho is not None:
        data.type = "ORTHO"
        data.ortho_scale = ortho
    else:
        data.type = "PERSP"
        data.lens = lens
    track(obj, target)
    return obj


cam_beauty = camera("Camera_Beauty", (0.72, -0.82, 0.55), (0.00, 0.02, 0.12), lens=58)
cam_front = camera("Camera_Front", (0.0, -1.2, 0.23), (0.0, 0.02, 0.13), ortho=0.64)
cam_top = camera("Camera_Top", (0.0, 0.0, 1.3), (0.0, 0.0, 0.0), ortho=0.62)


def render(cam, filename):
    scene.camera = cam
    scene.render.filepath = os.path.join(RENDER_DIR, filename)
    bpy.ops.render.render(write_still=True)


render(cam_beauty, "CeladonTeaSet_Beauty.png")
render(cam_front, "CeladonTeaSet_Front.png")
render(cam_top, "CeladonTeaSet_Top.png")

bpy.ops.wm.save_as_mainfile(filepath=BLEND_PATH)

bpy.ops.object.select_all(action="DESELECT")
root.select_set(True)
for obj in asset_col.objects:
    obj.select_set(True)
bpy.context.view_layer.objects.active = root
bpy.ops.export_scene.fbx(
    filepath=FBX_PATH,
    use_selection=True,
    object_types={"EMPTY", "MESH"},
    apply_unit_scale=True,
    apply_scale_options="FBX_SCALE_UNITS",
    axis_forward="-Z",
    axis_up="Y",
    use_mesh_modifiers=True,
    add_leaf_bones=False,
    bake_anim=False,
)

print(f"BLEND={BLEND_PATH}")
print(f"FBX={FBX_PATH}")
print(f"RENDERS={RENDER_DIR}")
