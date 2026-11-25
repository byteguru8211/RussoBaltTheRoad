from pathlib import Path
import sys
import importlib
import math
import traceback
sys.path.insert(0, str(Path(__file__).resolve().parent))
import blender_assetlib
importlib.reload(blender_assetlib)
from blender_assetlib import *

ROOT_DIR = ROOT
SCENE_PATH = ROOT / "game_assets" / "environment" / "Russia_1912" / "RussoBalt_The_Road_Hero.blend"
PREVIEW_PATH = ROOT / "previews" / "RussoBalt_The_Road_Hero.png"


def import_fbx(path):
    before = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=str(path), use_anim=True, ignore_leaf_bones=True, automatic_bone_orientation=False)
    return [obj for obj in bpy.data.objects if obj not in before]


def root_named(objects, prefix):
    for obj in objects:
        if obj.type == "EMPTY" and obj.name.startswith(prefix):
            return obj
    return None


def build():
    reset_scene()
    modules = ROOT / "game_assets" / "environment" / "Russia_1912" / "modules"
    car_objects = import_fbx(ROOT / "game_assets" / "vehicles" / "RussoBalt_C24_55" / "RussoBalt_C24_55_LOD0.fbx")
    car = root_named(car_objects, "RB_C24_55_Root")
    if car is None:
        raise RuntimeError("Car root not found after FBX import")
    road_objects = import_fbx(modules / "Road_Dirt_100m_LOD0.fbx")
    road = root_named(road_objects, "Road_Dirt_100m")
    house_objects = import_fbx(modules / "Building_Wood_House_01_LOD0.fbx")
    house = root_named(house_objects, "Building_Wood_House_01")
    if house:
        house.location = (-7.2, -1.5, 0)
    sign_objects = import_fbx(modules / "Signpost_Russia_1912_LOD0.fbx")
    sign = root_named(sign_objects, "Signpost_Russia_1912")
    if sign:
        sign.location = (-4.6, 3.0, 0)
    pole_objects = import_fbx(modules / "Telegraph_Pole_01_LOD0.fbx")
    pole = root_named(pole_objects, "Telegraph_Pole_01")
    if pole:
        pole.location = (5.4, 5.0, 0)
    birch_objects = import_fbx(modules / "Tree_Birch_01_LOD0.fbx")
    birch = root_named(birch_objects, "Tree_Birch_01")
    if birch:
        birch.location = (-9.0, 4.0, 0)
    driver_objects = import_fbx(ROOT / "game_assets" / "characters" / "Driver_1912.fbx")
    driver = root_named(driver_objects, "CHR_Driver_1912_Root")
    if driver:
        driver.location = (-0.31, 0.47, -0.27)
    for arm in [obj for obj in driver_objects if obj.type == "ARMATURE"]:
        actions = [action for action in bpy.data.actions if "Drive" in action.name and action.name.startswith(f"{arm.name}|")]
        if actions:
            arm.animation_data_create()
            arm.animation_data.use_nla = False
            arm.animation_data.action = actions[0]
            if actions[0].slots:
                arm.animation_data.action_slot = actions[0].slots[0]
            bpy.context.scene.frame_set(int(actions[0].frame_range[0]))
            print("HERO_DRIVE_ACTION", arm.name, actions[0].name, actions[0].frame_range[:])
    if not any(arm.animation_data and arm.animation_data.action for arm in [obj for obj in driver_objects if obj.type == "ARMATURE"]):
        bpy.context.scene.frame_set(1)
    bpy.context.view_layer.update()
    print("HERO_IMPORT_STATS", "car", asset_stats(car), "driver", asset_stats(driver) if driver else None)
    studio = add_studio(target=(0, -0.4, 0.8), camera=(6.8, -8.8, 3.4), size=12)
    bpy.data.lights["STUDIO_Key"].energy = 5200
    bpy.data.lights["STUDIO_Fill"].energy = 2100
    bpy.data.lights["STUDIO_Rim"].energy = 3600
    world = bpy.context.scene.world
    world.use_nodes = True
    bg = world.node_tree.nodes.get("Background")
    bg.inputs["Color"].default_value = (0.30, 0.36, 0.42, 1.0)
    bg.inputs["Strength"].default_value = 0.38
    sun_data = bpy.data.lights.new("HERO_WarmSun", "SUN")
    sun_data.energy = 2.0
    sun_data.angle = math.radians(14)
    sun = bpy.data.objects.new("HERO_WarmSun", sun_data)
    link_object(sun, studio)
    sun.rotation_euler = (math.radians(30), math.radians(-22), math.radians(-28))
    render_preview(PREVIEW_PATH, (1600, 1000), 64)
    save_blend(SCENE_PATH)
    print("HERO_SCENE_OK", SCENE_PATH, PREVIEW_PATH)


try:
    build()
except Exception as exc:
    print("HERO_SCENE_ERROR", type(exc).__name__, str(exc))
    traceback.print_exc()
