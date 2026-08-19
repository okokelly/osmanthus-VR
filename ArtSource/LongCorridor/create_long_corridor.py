import bpy
import math
import os
from mathutils import Vector


ROOT_DIR = os.path.dirname(os.path.abspath(__file__))
PROJECT_DIR = os.path.abspath(os.path.join(ROOT_DIR, "..", ".."))
RENDER_DIR = os.path.join(ROOT_DIR, "renders")
BLEND_PATH = os.path.join(ROOT_DIR, "LongCorridor_Pavilion_Stylized.blend")
FBX_PATH = os.path.join(PROJECT_DIR, "Assets", "Art", "Models", "LongCorridor", "LongCorridor_Pavilion_Stylized.fbx")

os.makedirs(RENDER_DIR, exist_ok=True)
os.makedirs(os.path.dirname(FBX_PATH), exist_ok=True)


def clear_scene():
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    for datablocks in (bpy.data.meshes, bpy.data.curves, bpy.data.materials, bpy.data.cameras, bpy.data.lights):
        pass


clear_scene()

scene = bpy.context.scene
scene.unit_settings.system = "METRIC"
scene.unit_settings.length_unit = "METERS"
scene.unit_settings.scale_length = 1.0
scene.render.engine = "BLENDER_EEVEE"
scene.render.resolution_x = 1600
scene.render.resolution_y = 900
scene.render.resolution_percentage = 100
scene.render.image_settings.file_format = "PNG"
scene.render.film_transparent = False
scene.render.image_settings.color_mode = "RGBA"
scene.render.image_settings.color_depth = "8"
scene.world.color = (0.035, 0.045, 0.05)

asset_collection = bpy.data.collections.new("LongCorridor_Asset")
render_collection = bpy.data.collections.new("_RenderHelpers")
scene.collection.children.link(asset_collection)
scene.collection.children.link(render_collection)


def move_to_collection(obj, collection):
    for col in list(obj.users_collection):
        col.objects.unlink(obj)
    collection.objects.link(obj)


def material(name, rgba, roughness=0.72, metallic=0.0, emission=None, emission_strength=0.0):
    mat = bpy.data.materials.new(name)
    mat.diffuse_color = rgba
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes.get("Principled BSDF")
    bsdf.inputs["Base Color"].default_value = rgba
    bsdf.inputs["Roughness"].default_value = roughness
    bsdf.inputs["Metallic"].default_value = metallic
    if emission is not None:
        bsdf.inputs["Emission Color"].default_value = emission
        bsdf.inputs["Emission Strength"].default_value = emission_strength
    return mat


M_STONE = material("M_Stone_WarmGrey", (0.46, 0.47, 0.44, 1.0), 0.92)
M_STONE_EDGE = material("M_Stone_Edge", (0.63, 0.61, 0.55, 1.0), 0.92)
M_ROOF = material("M_Roof_DesaturatedGreen", (0.20, 0.31, 0.29, 1.0), 0.84)
M_ROOF_EDGE = material("M_Roof_Edge", (0.31, 0.45, 0.39, 1.0), 0.8)
M_GREEN = material("M_Wood_LowSatGreen", (0.17, 0.34, 0.27, 1.0), 0.78)
M_RED = material("M_Wood_DarkVermilion", (0.47, 0.13, 0.09, 1.0), 0.76)
M_BLUE = material("M_Paint_FogBlue", (0.20, 0.38, 0.48, 1.0), 0.7)
M_TEAL = material("M_Paint_Teal", (0.10, 0.45, 0.42, 1.0), 0.68)
M_GOLD = material("M_OsmanthusGold", (0.92, 0.58, 0.12, 1.0), 0.48, metallic=0.08)
M_INTERACTIVE = material(
    "M_Pillar_InteractiveMask",
    (0.34, 0.48, 0.30, 1.0),
    0.62,
    emission=(0.23, 0.08, 0.015, 1.0),
    emission_strength=0.22,
)
M_DARK = material("M_ShadowWood", (0.075, 0.09, 0.085, 1.0), 0.82)

root = bpy.data.objects.new("LongCorridor_Pavilion_Stylized_ROOT", None)
asset_collection.objects.link(root)
root["asset_type"] = "Modular corridor with octagonal pavilion"
root["unit"] = "meter"
root["target"] = "Unity URP / Meta Quest"
root["style"] = "fragmented cultural memory / stylized"


def assign_asset(obj, name=None):
    move_to_collection(obj, asset_collection)
    obj.parent = root
    if name:
        obj.name = name
    return obj


def cube(name, loc, dims, mat, bevel=0.0, collection=asset_collection, parent=root):
    bpy.ops.mesh.primitive_cube_add(location=loc)
    obj = bpy.context.object
    obj.name = name
    obj.dimensions = dims
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    if mat:
        obj.data.materials.append(mat)
    move_to_collection(obj, collection)
    obj.parent = parent
    if bevel > 0:
        modifier = obj.modifiers.new("Soft_Edges", "BEVEL")
        modifier.width = bevel
        modifier.segments = 2
    return obj


def cylinder(name, loc, radius, depth, mat, vertices=12, rotation=(0, 0, 0), collection=asset_collection, parent=root):
    bpy.ops.mesh.primitive_cylinder_add(vertices=vertices, radius=radius, depth=depth, location=loc, rotation=rotation)
    obj = bpy.context.object
    obj.name = name
    if mat:
        obj.data.materials.append(mat)
    move_to_collection(obj, collection)
    obj.parent = parent
    bevel = obj.modifiers.new("Soft_Edges", "BEVEL")
    bevel.width = min(radius * 0.12, 0.025)
    bevel.segments = 2
    return obj


def box_between(name, p1, p2, thickness, mat, depth=None, parent=root):
    p1 = Vector(p1)
    p2 = Vector(p2)
    delta = p2 - p1
    length = delta.length
    obj = cube(name, (p1 + p2) / 2, (length, thickness, depth if depth is not None else thickness), mat, bevel=thickness * 0.12, parent=parent)
    obj.rotation_mode = "QUATERNION"
    obj.rotation_quaternion = delta.to_track_quat("X", "Z")
    return obj


def octagonal_prism(name, radius, z_bottom, depth, mat, rotation=math.radians(22.5)):
    verts = []
    faces = []
    for z in (z_bottom, z_bottom + depth):
        for i in range(8):
            a = rotation + i * math.tau / 8
            verts.append((radius * math.cos(a), radius * math.sin(a), z))
    faces.append(tuple(range(7, -1, -1)))
    faces.append(tuple(range(8, 16)))
    for i in range(8):
        j = (i + 1) % 8
        faces.append((i, j, 8 + j, 8 + i))
    mesh = bpy.data.meshes.new(name + "_Mesh")
    mesh.from_pydata(verts, [], faces)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    asset_collection.objects.link(obj)
    obj.parent = root
    obj.data.materials.append(mat)
    bevel = obj.modifiers.new("Soft_Edges", "BEVEL")
    bevel.width = 0.035
    bevel.segments = 2
    return obj


def octagonal_roof_shell(name, r_outer, z_outer, r_inner, z_inner, thickness, mat, rotation=math.radians(22.5)):
    verts = []
    for radius, z in (
        (r_outer, z_outer),
        (r_inner, z_inner),
        (r_outer - thickness * 0.7, z_outer - thickness),
        (max(r_inner - thickness * 0.35, 0.06), z_inner - thickness),
    ):
        for i in range(8):
            a = rotation + i * math.tau / 8
            verts.append((radius * math.cos(a), radius * math.sin(a), z))
    faces = []
    for i in range(8):
        j = (i + 1) % 8
        faces.append((i, j, 8 + j, 8 + i))
        faces.append((16 + i, 24 + i, 24 + j, 16 + j))
        faces.append((i, 16 + i, 16 + j, j))
        faces.append((8 + i, 8 + j, 24 + j, 24 + i))
    mesh = bpy.data.meshes.new(name + "_Mesh")
    mesh.from_pydata(verts, [], faces)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    asset_collection.objects.link(obj)
    obj.parent = root
    obj.data.materials.append(mat)
    bevel = obj.modifiers.new("Soft_Edges", "BEVEL")
    bevel.width = 0.025
    bevel.segments = 2
    return obj


def gable_roof(name, x0, x1, y_outer=1.61, z_eave=3.04, z_ridge=3.65, thickness=0.12):
    cross_top = [(-y_outer, z_eave), (0.0, z_ridge), (y_outer, z_eave)]
    cross_bottom = [(-y_outer + 0.035, z_eave - thickness), (0.0, z_ridge - thickness), (y_outer - 0.035, z_eave - thickness)]
    verts = []
    for x in (x0, x1):
        for y, z in cross_top + cross_bottom:
            verts.append((x, y, z))
    faces = [
        (0, 6, 7, 1), (1, 7, 8, 2),
        (3, 4, 10, 9), (4, 5, 11, 10),
        (0, 3, 9, 6), (2, 8, 11, 5),
        (0, 1, 4, 3), (1, 2, 5, 4),
        (6, 9, 10, 7), (7, 10, 11, 8),
    ]
    mesh = bpy.data.meshes.new(name + "_Mesh")
    mesh.from_pydata(verts, [], faces)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    asset_collection.objects.link(obj)
    obj.parent = root
    obj.data.materials.append(M_ROOF)
    bevel = obj.modifiers.new("Soft_Edges", "BEVEL")
    bevel.width = 0.018
    bevel.segments = 2

    # Stylized raised tile ribs. Geometry is deliberately sparse for Quest.
    x = x0 + 0.18
    tile_index = 0
    while x < x1 - 0.12:
        box_between(f"{name}_TileL_{tile_index:02d}", (x, -1.57, 3.075), (x, -0.02, 3.67), 0.034, M_ROOF_EDGE, depth=0.038)
        box_between(f"{name}_TileR_{tile_index:02d}", (x, 0.02, 3.67), (x, 1.57, 3.075), 0.034, M_ROOF_EDGE, depth=0.038)
        x += 0.34
        tile_index += 1
    cylinder(f"{name}_Ridge", ((x0 + x1) / 2, 0, 3.69), 0.055, x1 - x0 + 0.18, M_ROOF_EDGE, vertices=10, rotation=(0, math.pi / 2, 0))
    return obj


def add_corridor_side(prefix, x_positions):
    x_min = min(x_positions)
    x_max = max(x_positions)
    cube(f"{prefix}_Floor", ((x_min + x_max) / 2, 0, 0.09), (x_max - x_min + 0.5, 2.42, 0.18), M_STONE, bevel=0.035)
    cube(f"{prefix}_FloorInset", ((x_min + x_max) / 2, 0, 0.195), (x_max - x_min + 0.28, 1.82, 0.035), M_STONE_EDGE, bevel=0.012)

    interactive_x = {-7.23: "01", -4.74: "02", -2.25: "03"}
    for xi, x in enumerate(x_positions):
        for yi, y in enumerate((-1.14, 1.14)):
            key = round(x, 2)
            is_interactive = prefix == "Left" and yi == 0 and key in interactive_x
            name = f"Pillar_Interactive_{interactive_x[key]}" if is_interactive else f"{prefix}_Pillar_{xi:02d}_{yi}"
            mat = M_INTERACTIVE if is_interactive else M_GREEN
            cylinder(name, (x, y, 1.43), 0.115, 2.52, mat, vertices=12)
            cylinder(f"{name}_Base", (x, y, 0.23), 0.19, 0.12, M_STONE_EDGE, vertices=12)
            if is_interactive:
                cylinder(f"{name}_GoldRing", (x, y, 0.39), 0.128, 0.055, M_GOLD, vertices=16)

        cube(f"{prefix}_CrossBeam_{xi:02d}", (x, 0, 2.68), (0.20, 2.64, 0.25), M_RED, bevel=0.025)
        cube(f"{prefix}_PaintedTie_{xi:02d}", (x, 0, 2.48), (0.16, 2.28, 0.14), M_BLUE if xi % 2 == 0 else M_TEAL, bevel=0.018)

    for bay in range(len(x_positions) - 1):
        xa, xb = x_positions[bay], x_positions[bay + 1]
        xm = (xa + xb) / 2
        length = abs(xb - xa)
        for y in (-1.14, 1.14):
            cube(f"{prefix}_LongBeam_{bay:02d}_{'S' if y < 0 else 'N'}", (xm, y, 2.66), (length, 0.20, 0.27), M_RED, bevel=0.025)
            cube(f"{prefix}_PaintBand_{bay:02d}_{'S' if y < 0 else 'N'}", (xm, y, 2.45), (length - 0.10, 0.11, 0.18), M_BLUE if bay % 2 == 0 else M_TEAL, bevel=0.015)

            # Inverted geometric lattice, simplified as crossed rods.
            z_top, z_bottom = 2.30, 2.03
            box_between(f"{prefix}_LatticeTop_{bay:02d}_{y}", (xa + 0.13, y, z_top), (xb - 0.13, y, z_top), 0.045, M_GREEN)
            box_between(f"{prefix}_LatticeA_{bay:02d}_{y}", (xa + 0.16, y, z_top), (xm, y, z_bottom), 0.045, M_GREEN)
            box_between(f"{prefix}_LatticeB_{bay:02d}_{y}", (xm, y, z_bottom), (xb - 0.16, y, z_top), 0.045, M_GREEN)

            # Continuous seated railing, split per bay for modularity.
            cube(f"{prefix}_Seat_{bay:02d}_{y}", (xm, y * 0.91, 0.55), (length - 0.16, 0.26, 0.12), M_RED, bevel=0.025)
            for support_x in (xa + 0.32, xm, xb - 0.32):
                cube(f"{prefix}_SeatSupport_{bay:02d}_{y}_{support_x:.2f}", (support_x, y * 0.91, 0.36), (0.07, 0.18, 0.36), M_GREEN, bevel=0.012)

        cube(f"{prefix}_CeilingPanel_{bay:02d}", (xm, 0, 2.79), (length - 0.16, 1.92, 0.075), M_BLUE if bay % 2 == 0 else M_TEAL, bevel=0.02)
        cylinder(f"{prefix}_CeilingMedallion_{bay:02d}", (xm, 0, 2.735), 0.30, 0.045, M_GOLD, vertices=16)

    roof_x0 = x_min - 0.28
    roof_x1 = x_max + 0.28
    gable_roof(f"{prefix}_GableRoof", roof_x0, roof_x1)


left_positions = [-9.72, -7.23, -4.74, -2.25]
right_positions = [2.25, 4.74, 7.23, 9.72]
add_corridor_side("Left", left_positions)
add_corridor_side("Right", right_positions)


# Central octagonal double-eaved pavilion.
octagonal_prism("Pavilion_StonePlinth", 2.48, 0.0, 0.20, M_STONE)
octagonal_prism("Pavilion_FloorInset", 2.18, 0.20, 0.04, M_STONE_EDGE)

pavilion_columns = []
column_radius = 1.78
for i in range(8):
    angle = math.radians(22.5 + i * 45)
    x = column_radius * math.cos(angle)
    y = column_radius * math.sin(angle)
    pavilion_columns.append(Vector((x, y, 0)))
    cylinder(f"Pavilion_Pillar_{i:02d}", (x, y, 1.78), 0.14, 3.08, M_GREEN, vertices=12)
    cylinder(f"Pavilion_PillarBase_{i:02d}", (x, y, 0.26), 0.22, 0.13, M_STONE_EDGE, vertices=12)

for i, p1 in enumerate(pavilion_columns):
    p2 = pavilion_columns[(i + 1) % 8]
    box_between(f"Pavilion_RingBeam_{i:02d}", (p1.x, p1.y, 3.24), (p2.x, p2.y, 3.24), 0.23, M_RED, depth=0.26)
    box_between(f"Pavilion_PaintBand_{i:02d}", (p1.x * 0.98, p1.y * 0.98, 3.03), (p2.x * 0.98, p2.y * 0.98, 3.03), 0.14, M_BLUE if i % 2 == 0 else M_TEAL, depth=0.16)

    midpoint = (p1 + p2) * 0.5
    # Leave east/west bays open for the corridor axis.
    if abs(midpoint.y) > 0.82:
        box_between(f"Pavilion_Seat_{i:02d}", (p1.x * 0.92, p1.y * 0.92, 0.57), (p2.x * 0.92, p2.y * 0.92, 0.57), 0.24, M_RED, depth=0.12)
        box_between(f"Pavilion_LowRail_{i:02d}", (p1.x * 0.92, p1.y * 0.92, 0.34), (p2.x * 0.92, p2.y * 0.92, 0.34), 0.07, M_GREEN, depth=0.08)

    # Simplified bracket cluster at every column, readable at player distance.
    radial = Vector((p1.x, p1.y, 0)).normalized()
    tangent = Vector((-radial.y, radial.x, 0))
    for level, (z, length, mat) in enumerate(((3.30, 0.62, M_RED), (3.39, 0.46, M_GREEN), (3.47, 0.34, M_GOLD))):
        center = Vector((p1.x, p1.y, z)) + radial * (0.05 + level * 0.025)
        obj = cube(f"Pavilion_Bracket_{i:02d}_{level}", center, (length, 0.12, 0.10), mat, bevel=0.018)
        obj.rotation_euler[2] = math.atan2(tangent.y, tangent.x)


octagonal_roof_shell("Pavilion_LowerRoof", 2.72, 3.48, 1.10, 4.20, 0.14, M_ROOF)

# Upper drum and upper roof.
upper_radius = 0.90
for i in range(8):
    angle = math.radians(22.5 + i * 45)
    x = upper_radius * math.cos(angle)
    y = upper_radius * math.sin(angle)
    cylinder(f"Pavilion_UpperPillar_{i:02d}", (x, y, 4.27), 0.075, 0.62, M_RED, vertices=10)
    p1 = Vector((x, y, 4.52))
    angle2 = math.radians(22.5 + ((i + 1) % 8) * 45)
    p2 = Vector((upper_radius * math.cos(angle2), upper_radius * math.sin(angle2), 4.52))
    box_between(f"Pavilion_UpperBeam_{i:02d}", p1, p2, 0.105, M_TEAL, depth=0.12)

octagonal_roof_shell("Pavilion_UpperRoof", 1.64, 4.48, 0.16, 5.63, 0.12, M_ROOF)
cylinder("Pavilion_FinialBase", (0, 0, 5.66), 0.17, 0.20, M_GOLD, vertices=12)
bpy.ops.mesh.primitive_uv_sphere_add(segments=16, ring_count=8, radius=0.17, location=(0, 0, 5.87))
finial = bpy.context.object
assign_asset(finial, "Pavilion_FinialOrb")
finial.data.materials.append(M_GOLD)
cylinder("Pavilion_FinialTip", (0, 0, 6.12), 0.065, 0.38, M_GOLD, vertices=10)

# A gold ring beneath each roof makes the stylized silhouette legible in fog.
for i in range(8):
    a1 = math.radians(22.5 + i * 45)
    a2 = math.radians(22.5 + ((i + 1) % 8) * 45)
    box_between(
        f"Pavilion_LowerGoldEdge_{i:02d}",
        (2.66 * math.cos(a1), 2.66 * math.sin(a1), 3.51),
        (2.66 * math.cos(a2), 2.66 * math.sin(a2), 3.51),
        0.055,
        M_GOLD,
        depth=0.055,
    )
    box_between(
        f"Pavilion_UpperGoldEdge_{i:02d}",
        (1.58 * math.cos(a1), 1.58 * math.sin(a1), 4.51),
        (1.58 * math.cos(a2), 1.58 * math.sin(a2), 4.51),
        0.045,
        M_GOLD,
        depth=0.045,
    )


# Render helpers (excluded from Unity export).
ground = cube("_RenderGround", (0, 0, -0.08), (24.0, 10.0, 0.12), M_DARK, bevel=0.0, collection=render_collection, parent=None)


def add_area_light(name, loc, energy, size, color):
    data = bpy.data.lights.new(name, "AREA")
    data.energy = energy
    data.shape = "DISK"
    data.size = size
    data.color = color
    obj = bpy.data.objects.new(name, data)
    render_collection.objects.link(obj)
    obj.location = loc
    return obj


key = add_area_light("Key_Area", (-6, -9, 11), 1700, 7.0, (1.0, 0.78, 0.57))
fill = add_area_light("Fill_Area", (7, 6, 8), 1100, 8.0, (0.43, 0.62, 1.0))
rim = add_area_light("Rim_Area", (0, 4, 12), 900, 5.0, (1.0, 0.52, 0.22))


def track_to(obj, target, forward="-Z", up="Y"):
    direction = Vector(target) - obj.location
    obj.rotation_euler = direction.to_track_quat(forward, up).to_euler()


track_to(key, (0, 0, 2.0))
track_to(fill, (0, 0, 2.5))
track_to(rim, (0, 0, 3.0))


def create_camera(name, loc, target, ortho_scale, lens=50):
    data = bpy.data.cameras.new(name)
    obj = bpy.data.objects.new(name, data)
    render_collection.objects.link(obj)
    obj.location = loc
    data.type = "ORTHO"
    data.ortho_scale = ortho_scale
    data.lens = lens
    track_to(obj, target)
    return obj


camera_long = create_camera("Camera_Longitudinal", (0, -30, 3.25), (0, 0, 2.65), 21.4)
camera_cross = create_camera("Camera_Transverse", (-28, 0, 3.15), (0, 0, 2.8), 8.6)
camera_top = create_camera("Camera_Top", (0, 0, 32), (0, 0, 0), 21.8)


def create_perspective_camera(name, loc, target, lens=26):
    data = bpy.data.cameras.new(name)
    obj = bpy.data.objects.new(name, data)
    render_collection.objects.link(obj)
    obj.location = loc
    data.type = "PERSP"
    data.lens = lens
    data.sensor_width = 36
    track_to(obj, target)
    return obj


camera_player = create_perspective_camera("Camera_PlayerEye", (-8.65, 0.0, 1.65), (0.0, 0.0, 2.15), 25)


def render(camera, filename, x=1600, y=900):
    scene.camera = camera
    scene.render.resolution_x = x
    scene.render.resolution_y = y
    scene.render.filepath = os.path.join(RENDER_DIR, filename)
    bpy.ops.render.render(write_still=True)


render(camera_long, "LongCorridor_Longitudinal.png")
render(camera_cross, "LongCorridor_Transverse.png")
render(camera_top, "LongCorridor_Top.png")
render(camera_player, "LongCorridor_PlayerView.png")

# Save an editable Blender source with cameras and named interaction pillars.
bpy.ops.wm.save_as_mainfile(filepath=BLEND_PATH)

# Export only the asset hierarchy for Unity; render lights/cameras/ground stay in .blend only.
bpy.ops.object.select_all(action="DESELECT")
root.select_set(True)
for obj in asset_collection.objects:
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
