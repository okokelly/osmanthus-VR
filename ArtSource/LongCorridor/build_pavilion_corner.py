import bpy
import math
import os
from mathutils import Matrix, Vector


SOURCE_DIR = os.path.dirname(os.path.abspath(__file__))
PROJECT_DIR = os.path.abspath(os.path.join(SOURCE_DIR, "..", ".."))
BLEND_PATH = os.path.join(SOURCE_DIR, "LongCorridor_PavilionCorner_Stylized.blend")
FBX_PATH = os.path.join(
    PROJECT_DIR,
    "Assets",
    "Art",
    "Models",
    "LongCorridor",
    "LongCorridor_PavilionCorner_Stylized.fbx",
)
PREVIEW_PATH = os.path.join(
    SOURCE_DIR,
    "renders",
    "LongCorridor_PavilionCorner_Preview.png",
)


def rotate_world_about_origin(obj, radians):
    obj.matrix_world = Matrix.Rotation(radians, 4, "Z") @ obj.matrix_world


def remove_pavilion_north_seat():
    # Bay 01 is the north-facing octagonal bay. Removing its seat opens a
    # properly framed passage from the pavilion into the perpendicular arm.
    for obj in list(bpy.data.objects):
        if obj.name.startswith("Pavilion_Seat_01") or obj.name.startswith("Pavilion_LowRail_01"):
            bpy.data.objects.remove(obj, do_unlink=True)


def make_corner_geometry():
    root = bpy.data.objects.get("LongCorridor_Pavilion_Stylized_ROOT")
    if root is None:
        raise RuntimeError("Long corridor root was not found in the source blend file")

    # The source asset has corridor arms along -X and +X. Rotate the +X arm
    # counter-clockwise around the pavilion to produce an L-shaped -X -> +Y route.
    turn_objects = [obj for obj in bpy.data.objects if obj.name.startswith("Right_")]
    if not turn_objects:
        raise RuntimeError("The source corridor's Right_ arm objects were not found")

    for obj in turn_objects:
        rotate_world_about_origin(obj, math.radians(90.0))
        obj.name = obj.name.replace("Right_", "LakeTurn_", 1)

    remove_pavilion_north_seat()
    root.name = "LongCorridor_PavilionCorner_Stylized_ROOT"
    root["asset_type"] = "L-shaped corridor turn through octagonal pavilion"
    root["turn_angle_degrees"] = 90
    root["route"] = "incoming -X, outgoing +Y"
    root["target"] = "Unity URP / Meta Quest"
    return root


def configure_preview(root):
    scene = bpy.context.scene
    scene.render.resolution_x = 1600
    scene.render.resolution_y = 900
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.film_transparent = False
    scene.world.color = (0.035, 0.045, 0.05)

    helper_collection = bpy.data.collections.get("_RenderHelpers")
    if helper_collection is None:
        helper_collection = bpy.data.collections.new("_RenderHelpers")
        scene.collection.children.link(helper_collection)

    ground = bpy.data.objects.get("_RenderGround")
    if ground is not None:
        ground.location = (-1.0, 1.0, -0.08)
        ground.dimensions = (25.0, 25.0, 0.12)

    camera_data = bpy.data.cameras.new("Camera_CornerPreview")
    camera = bpy.data.objects.new("Camera_CornerPreview", camera_data)
    helper_collection.objects.link(camera)
    camera.location = (-15.5, -16.5, 10.5)
    target = Vector((-0.8, 1.2, 2.2))
    camera.rotation_euler = (target - camera.location).to_track_quat("-Z", "Y").to_euler()
    camera_data.type = "PERSP"
    camera_data.lens = 43
    scene.camera = camera

    scene.render.filepath = PREVIEW_PATH
    os.makedirs(os.path.dirname(PREVIEW_PATH), exist_ok=True)
    bpy.ops.render.render(write_still=True)


def export_asset(root):
    bpy.ops.object.select_all(action="DESELECT")
    root.select_set(True)
    for obj in root.children_recursive:
        if obj.type in {"MESH", "EMPTY"}:
            obj.select_set(True)
    bpy.context.view_layer.objects.active = root

    os.makedirs(os.path.dirname(FBX_PATH), exist_ok=True)
    bpy.ops.export_scene.fbx(
        filepath=FBX_PATH,
        use_selection=True,
        object_types={"MESH", "EMPTY"},
        apply_unit_scale=True,
        apply_scale_options="FBX_SCALE_UNITS",
        axis_forward="-Z",
        axis_up="Y",
        bake_space_transform=False,
        add_leaf_bones=False,
        use_mesh_modifiers=True,
        path_mode="AUTO",
    )


root = make_corner_geometry()
configure_preview(root)
bpy.ops.wm.save_as_mainfile(filepath=BLEND_PATH)
export_asset(root)
print("CORNER_BLEND=" + BLEND_PATH)
print("CORNER_FBX=" + FBX_PATH)
print("CORNER_PREVIEW=" + PREVIEW_PATH)
