from pathlib import Path
import sys
import importlib
import json
import math
import random
import traceback
sys.path.insert(0, str(Path(__file__).resolve().parent))
import blender_assetlib
importlib.reload(blender_assetlib)
from blender_assetlib import *

ASSET_DIR = ROOT / "game_assets" / "environment" / "Russia_1912"
MODULES = ASSET_DIR / "modules"
PREVIEW_DIR = ROOT / "previews"
FONT_PATH = Path("C:/Windows/Fonts/arial.ttf")
RNG = random.Random(1912)
EXPORTS = []


def export_module(root, name, major=False, collision_root=None):
    ensure_uvs(root)
    detailed_stats = asset_stats(root)
    if major:
        export_fbx(root, MODULES / f"{name}_Detailed.fbx")
    optimized = optimize_hierarchy(root, "_OPT")
    stats = asset_stats(optimized)
    export_fbx_clean_nodes(optimized, MODULES / f"{name}_LOD0.fbx", ("_OPT",))
    if major:
        lod1 = duplicate_lod(optimized, "_LOD1", 0.55)
        export_fbx_clean_nodes(lod1, MODULES / f"{name}_LOD1.fbx", ("_OPT", "_LOD1"))
        for obj in descendants(lod1):
            obj.hide_render = True
            obj.hide_viewport = True
    for obj in descendants(optimized):
        obj.hide_render = True
        obj.hide_viewport = True
    if collision_root:
        export_fbx(collision_root, MODULES / f"{name}_Collision.fbx")
        for obj in descendants(collision_root):
            obj.hide_render = True
            obj.hide_viewport = True
    record = {"id": name, "stats": stats, "stats_detailed": detailed_stats, "exports": [f"{name}_LOD0.fbx"]}
    if major:
        record["exports"] = [f"{name}_LOD0.fbx", f"{name}_LOD1.fbx", f"{name}_Detailed.fbx"]
    if collision_root:
        record["exports"].append(f"{name}_Collision.fbx")
    EXPORTS.append(record)
    return optimized


def road(name, length, width, mat, snowy=False):
    coll = collection(name)
    root = empty(name, coll=coll)
    nx, ny = 24, max(40, int(length))
    verts, uvs, faces = [], [], []
    for j in range(ny + 1):
        y = -length / 2 + length * j / ny
        for i in range(nx + 1):
            x = -width / 2 + width * i / nx
            rut = -0.018 * math.exp(-((abs(x) - width * 0.11) / 0.22) ** 2)
            undulation = 0.006 * math.sin(y * 0.37 + x * 0.8) + 0.004 * math.sin(y * 1.13 - x)
            verts.append((x, y, rut + undulation))
            uvs.append((i / nx * length / 6, j / ny * length / 6))
    for j in range(ny):
        for i in range(nx):
            a = j * (nx + 1) + i
            faces.append((a, a + 1, a + nx + 2, a + nx + 1))
    mesh(f"{name}_Surface", verts, faces, mat, root, coll, True, uvs)
    for side in (-1, 1):
        box(f"{name}_Shoulder_{side}", (side * (width / 2 + 0.55), 0, -0.035), (1.1, length, 0.06), mat, root, coll, 0)
    if snowy:
        for side in (-1, 1):
            box(f"{name}_Rut_{side}", (side * width * 0.11, 0, -0.002), (0.20, length, 0.012), material("ENV_Road_Dirt", (0.32, 0.23, 0.13), roughness=0.95, texture="road_dirt"), root, coll, 0)
    return root


def cobblestone_road():
    name = "Road_Cobblestone_25m"
    coll = collection(name)
    root = empty(name, coll=coll)
    p = palette()
    box(f"{name}_Surface", (0, 0, -0.04), (6.4, 25, 0.08), p["cobble"], root, coll, 0)
    for side in (-1, 1):
        for i in range(26):
            box(f"{name}_Curb_{side}_{i:02d}", (side * 3.28, -12 + i, 0.06), (0.28, 0.94, 0.20), p["plaster"], root, coll, 0.015)
    return root


def window(name, x, y, z, side, p, root, coll, width=0.9, height=1.0):
    if side == "FRONT":
        box(f"{name}_Glass", (x, y, z), (width, 0.025, height), material("ENV_Window_Glass", (0.18, 0.28, 0.31), roughness=0.16, transmission=0.55), root, coll, 0.005)
        for xx in (x - width / 2, x + width / 2):
            box(f"{name}_Frame_V_{xx}", (xx, y - 0.03, z), (0.07, 0.08, height + 0.14), p["white_paint"], root, coll, 0.008)
        for zz in (z - height / 2, z + height / 2):
            box(f"{name}_Frame_H_{zz}", (x, y - 0.03, zz), (width + 0.14, 0.08, 0.07), p["white_paint"], root, coll, 0.008)
        box(f"{name}_Mullion_V", (x, y - 0.05, z), (0.035, 0.05, height), p["white_paint"], root, coll, 0.004)
        box(f"{name}_Mullion_H", (x, y - 0.05, z), (width, 0.05, 0.035), p["white_paint"], root, coll, 0.004)
    else:
        box(f"{name}_Glass", (x, y, z), (0.025, width, height), material("ENV_Window_Glass", (0.18, 0.28, 0.31), roughness=0.16, transmission=0.55), root, coll, 0.005)
        for yy in (y - width / 2, y + width / 2):
            box(f"{name}_Frame_V_{yy}", (x, yy, z), (0.08, 0.07, height + 0.14), p["white_paint"], root, coll, 0.008)
        for zz in (z - height / 2, z + height / 2):
            box(f"{name}_Frame_H_{zz}", (x, y, zz), (0.08, width + 0.14, 0.07), p["white_paint"], root, coll, 0.008)
        box(f"{name}_Mullion_V", (x, y, z), (0.05, 0.035, height), p["white_paint"], root, coll, 0.004)
        box(f"{name}_Mullion_H", (x, y, z), (0.05, width, 0.035), p["white_paint"], root, coll, 0.004)


def wooden_house():
    name = "Building_Wood_House_01"
    coll = collection(name)
    root = empty(name, coll=coll)
    p = palette()
    box(f"{name}_Foundation", (0, 0, 0.28), (8.2, 6.2, 0.56), p["plaster"], root, coll, 0.04)
    for level in range(10):
        z = 0.68 + level * 0.27
        for y in (-2.92, 2.92):
            cylinder_between(f"{name}_Log_FrontBack_{level}_{y}", (-4.0, y, z), (4.0, y, z), 0.145, p["wood"], root, coll, 12)
        for x in (-3.92, 3.92):
            cylinder_between(f"{name}_Log_Side_{level}_{x}", (x, -2.92, z), (x, 2.92, z), 0.145, p["wood"], root, coll, 12)
    for side in (-1, 1):
        roof = box(f"{name}_Roof_{side}", (side * 2.05, 0, 4.18), (4.75, 6.8, 0.16), p["roof"], root, coll, 0.02, (0, side * math.radians(31), 0))
    ridge = cylinder_between(f"{name}_Roof_Ridge", (0, -3.45, 4.98), (0, 3.45, 4.98), 0.09, p["roof"], root, coll, 12)
    for y in (-2.93, 2.93):
        mesh(f"{name}_Gable_{y}", [(-3.9, y, 3.35), (3.9, y, 3.35), (0, y, 4.95)], [(0, 1, 2)], p["plaster"], root, coll, False)
    box(f"{name}_Door", (0, -3.09, 1.47), (1.15, 0.12, 2.05), p["wood"], root, coll, 0.025)
    for x in (-0.67, 0.67):
        box(f"{name}_Door_Frame_{x}", (x, -3.18, 1.48), (0.12, 0.16, 2.25), p["white_paint"], root, coll, 0.01)
    box(f"{name}_Door_Lintel", (0, -3.18, 2.57), (1.46, 0.16, 0.13), p["white_paint"], root, coll, 0.01)
    sphere(f"{name}_Door_Knob", (0.42, -3.19, 1.43), (0.045, 0.025, 0.045), p["brass"], root, coll, 12, 6)
    for i in range(3):
        box(f"{name}_Step_{i}", (0, -3.42 - i * 0.28, 0.18 - i * 0.05), (1.65, 0.42, 0.16), p["plaster"], root, coll, 0.015)
    for i, x in enumerate((-2.45, 2.45)):
        window(f"{name}_FrontWindow_{i}", x, -3.075, 2.12, "FRONT", p, root, coll)
        window(f"{name}_SideWindow_{i}", -4.075, -1.25 if i == 0 else 1.25, 2.12, "SIDE", p, root, coll)
    for x in (-1.25, 1.25):
        cylinder(f"{name}_Porch_Post_{x}", (x, -3.75, 1.55), 0.09, 2.45, p["wood"], root, coll, "Z", 12)
    box(f"{name}_Porch_Roof", (0, -3.55, 2.86), (3.2, 1.55, 0.14), p["roof"], root, coll, 0.02, (math.radians(12), 0, 0))
    box(f"{name}_Chimney", (2.65, 1.55, 4.45), (0.65, 0.65, 1.65), material("ENV_Brick_Red", (0.38, 0.13, 0.08), roughness=0.9, texture="brick_red"), root, coll, 0.02)
    box(f"{name}_Chimney_Cap", (2.65, 1.55, 5.30), (0.82, 0.82, 0.14), p["plaster"], root, coll, 0.015)
    if FONT_PATH.exists():
        text_mesh(f"{name}_Gable_Date", "1912", (0, -2.96, 3.92), 0.38, p["white_paint"], root, coll, font_path=FONT_PATH)
    collision_coll = collection(f"{name}_COLLISION")
    collision_root = empty(f"{name}_Collision_Root", coll=collision_coll)
    box(f"UCX_{name}_Body", (0, 0, 2.25), (8.0, 6.0, 4.5), p["steel"], collision_root, collision_coll, 0)
    return root, collision_root


def gatehouse():
    name = "Building_Brick_Gatehouse_01"
    coll = collection(name)
    root = empty(name, coll=coll)
    p = palette()
    brick = material("ENV_Brick_Red", (0.38, 0.13, 0.08), roughness=0.9, texture="brick_red")
    for side in (-1, 1):
        box(f"{name}_Tower_{side}", (side * 1.85, 0, 1.85), (1.35, 4.2, 3.7), brick, root, coll, 0.03)
        box(f"{name}_Tower_Cap_{side}", (side * 1.85, 0, 3.83), (1.65, 4.5, 0.26), p["plaster"], root, coll, 0.02)
    box(f"{name}_Top_Beam", (0, 0, 3.30), (4.2, 4.2, 0.80), brick, root, coll, 0.02)
    for i in range(11):
        angle = math.pi * i / 10
        x = 1.18 * math.cos(angle)
        z = 1.42 + 1.18 * math.sin(angle)
        box(f"{name}_Arch_Block_{i}", (x, -2.13, z), (0.42, 0.34, 0.55), p["plaster"], root, coll, 0.015, (0, math.pi / 2 - angle, 0))
    for side in (-1, 1):
        window(f"{name}_Window_{side}", side * 1.85, -2.13, 2.70, "FRONT", p, root, coll, 0.55, 0.75)
    box(f"{name}_Gate_Left", (-0.83, 0, 1.05), (0.28, 0.16, 2.10), p["wood"], root, coll, 0.015, (0, 0, -0.30))
    box(f"{name}_Gate_Right", (0.83, 0, 1.05), (0.28, 0.16, 2.10), p["wood"], root, coll, 0.015, (0, 0, 0.30))
    if FONT_PATH.exists():
        text_mesh(f"{name}_Plaque", "СЛАВА", (0, -2.16, 3.34), 0.34, p["cream"], root, coll, font_path=FONT_PATH)
    collision_coll = collection(f"{name}_COLLISION")
    collision_root = empty(f"{name}_Collision_Root", coll=collision_coll)
    for side in (-1, 1):
        box(f"UCX_{name}_Tower_{side}", (side * 1.85, 0, 1.85), (1.35, 4.2, 3.7), p["steel"], collision_root, collision_coll, 0)
    box(f"UCX_{name}_Top", (0, 0, 3.30), (4.2, 4.2, 0.80), p["steel"], collision_root, collision_coll, 0)
    return root, collision_root


def fence():
    name = "Fence_Wood_10m"
    coll = collection(name)
    root = empty(name, coll=coll)
    p = palette()
    for i in range(9):
        x = -5 + i * 1.25
        box(f"{name}_Post_{i}", (x, 0, 0.85), (0.16, 0.16, 1.70), p["wood"], root, coll, 0.015)
        cone(f"{name}_Post_Cap_{i}", (x, 0, 1.76), 0.13, 0, 0.18, p["wood"], root, coll, 4, (0, 0, math.pi / 4))
    for z in (0.48, 1.18):
        box(f"{name}_Rail_{z}", (0, 0, z), (10.0, 0.10, 0.14), p["wood"], root, coll, 0.01)
    for i in range(31):
        x = -4.82 + i * 0.32
        box(f"{name}_Picket_{i}", (x, 0, 0.73), (0.11, 0.08, 1.18), p["wood"], root, coll, 0.01)
    return root


def telegraph_pole(index=1):
    name = f"Telegraph_Pole_{index:02d}"
    coll = collection(name)
    root = empty(name, coll=coll)
    p = palette()
    cone(f"{name}_Pole", (0, 0, 4.0), 0.17, 0.10, 8.0, p["wood"], root, coll, 12)
    for z in (6.9, 7.55):
        box(f"{name}_Crossarm_{z}", (0, 0, z), (2.15, 0.13, 0.13), p["wood"], root, coll, 0.01)
        for x in (-0.88, -0.30, 0.30, 0.88):
            cylinder(f"{name}_Insulator_{z}_{x}", (x, 0, z + 0.13), 0.065, 0.16, p["glass"], root, coll, "Z", 12)
            sphere(f"{name}_Insulator_Cap_{z}_{x}", (x, 0, z + 0.23), (0.075, 0.075, 0.045), p["glass"], root, coll, 12, 6)
    box(f"{name}_Step_1", (0.20, 0, 1.4), (0.28, 0.08, 0.08), p["steel"], root, coll, 0.005)
    box(f"{name}_Step_2", (0.20, 0, 2.1), (0.28, 0.08, 0.08), p["steel"], root, coll, 0.005)
    return root


def signpost():
    name = "Signpost_Russia_1912"
    coll = collection(name)
    root = empty(name, coll=coll)
    p = palette()
    cylinder(f"{name}_Post", (0, 0, 1.65), 0.09, 3.3, p["wood"], root, coll, "Z", 12)
    for i, (label, z, direction) in enumerate((("МОСКВА", 2.55, 1), ("С.ПЕТЕРБУРГ", 1.95, -1))):
        board = box(f"{name}_Board_{i}", (direction * 0.62, 0, z), (1.55, 0.10, 0.42), p["white_paint"], root, coll, 0.015)
        tip = mesh(f"{name}_Arrow_{i}", [(direction * 1.40, -0.06, z - 0.21), (direction * 1.72, -0.06, z), (direction * 1.40, -0.06, z + 0.21), (direction * 1.40, 0.06, z + 0.21), (direction * 1.72, 0.06, z), (direction * 1.40, 0.06, z - 0.21)], [(0, 1, 2, 3, 4, 5)], p["white_paint"], root, coll, False)
        if FONT_PATH.exists():
            text_mesh(f"{name}_Text_{i}", label, (direction * 0.57, -0.061, z), 0.20 if i == 0 else 0.145, p["green_dark"], root, coll, font_path=FONT_PATH)
    return root


def fuel_can():
    name = "Prop_Fuel_Can_01"
    coll = collection(name)
    root = empty(name, coll=coll)
    p = palette()
    box(f"{name}_Body", (0, 0, 0.31), (0.42, 0.22, 0.56), p["green_dark"], root, coll, 0.045)
    tube(f"{name}_Handle", [(-0.14, 0, 0.60), (-0.14, 0, 0.76), (0.14, 0, 0.76), (0.14, 0, 0.60)], 0.022, p["steel"], root, coll, 10)
    cylinder(f"{name}_Cap", (0.12, 0, 0.63), 0.038, 0.06, p["brass"], root, coll, "Z", 16)
    for side in (-1, 1):
        box(f"{name}_Strap_{side}", (side * 0.13, -0.118, 0.31), (0.035, 0.018, 0.50), p["brass"], root, coll, 0.004)
    if FONT_PATH.exists():
        text_mesh(f"{name}_Label", "БЕНЗИН", (0, -0.122, 0.34), 0.085, p["cream"], root, coll, font_path=FONT_PATH)
    return root


def tool_chest():
    name = "Prop_Tool_Chest_01"
    coll = collection(name)
    root = empty(name, coll=coll)
    p = palette()
    box(f"{name}_Body", (0, 0, 0.28), (0.75, 0.42, 0.46), p["wood"], root, coll, 0.025)
    box(f"{name}_Lid", (0, 0, 0.54), (0.79, 0.46, 0.08), p["wood"], root, coll, 0.015)
    for x in (-0.32, 0.32):
        box(f"{name}_Band_{x}", (x, 0, 0.34), (0.055, 0.44, 0.50), p["steel"], root, coll, 0.005)
    cylinder_between(f"{name}_Handle", (-0.12, 0, 0.61), (0.12, 0, 0.61), 0.018, p["brass"], root, coll, 12)
    return root


def barrel():
    name = "Prop_Barrel_01"
    coll = collection(name)
    root = empty(name, coll=coll)
    p = palette()
    obj = lathe_x(f"{name}_Body", [(-0.43, 0.25), (-0.40, 0.31), (-0.28, 0.35), (0.28, 0.35), (0.40, 0.31), (0.43, 0.25)], p["wood"], root, coll, 32)
    obj.rotation_euler[1] = math.pi / 2
    obj.location.z = 0.48
    for z in (0.14, 0.80):
        torus(f"{name}_Hoop_{z}", (0, 0, z), 0.345, 0.022, p["steel"], root, coll, "Z", 32, 8)
    cylinder(f"{name}_Top", (0, 0, 0.87), 0.30, 0.035, p["wood"], root, coll, "Z", 24)
    return root


def crate():
    name = "Prop_Crate_01"
    coll = collection(name)
    root = empty(name, coll=coll)
    p = palette()
    box(f"{name}_Body", (0, 0, 0.38), (0.72, 0.62, 0.72), p["wood"], root, coll, 0.015)
    for z in (0.08, 0.68):
        for x in (-0.34, 0.34):
            box(f"{name}_Edge_X_{z}_{x}", (x, 0, z), (0.07, 0.66, 0.07), p["brass"], root, coll, 0.004)
        for y in (-0.29, 0.29):
            box(f"{name}_Edge_Y_{z}_{y}", (0, y, z), (0.76, 0.07, 0.07), p["brass"], root, coll, 0.004)
    for i in range(5):
        box(f"{name}_Slat_{i}", (-0.30 + i * 0.15, -0.32, 0.38), (0.12, 0.035, 0.62), p["wood"], root, coll, 0.005)
    return root


def hay_bale():
    name = "Prop_Hay_Bale_01"
    coll = collection(name)
    root = empty(name, coll=coll)
    p = palette()
    hay = material("ENV_Hay", (0.56, 0.39, 0.12), roughness=0.96)
    box(f"{name}_Body", (0, 0, 0.48), (1.25, 0.70, 0.90), hay, root, coll, 0.16)
    for i in range(26):
        y = RNG.uniform(-0.32, 0.32)
        z = RNG.uniform(0.12, 0.86)
        cylinder_between(f"{name}_Straw_{i}", (-0.60, y, z), (0.60, y + RNG.uniform(-0.08, 0.08), z + RNG.uniform(-0.04, 0.04)), 0.006, hay, root, coll, 5)
    for x in (-0.38, 0.38):
        box(f"{name}_Twine_{x}", (x, 0, 0.48), (0.025, 0.73, 0.93), p["cream"], root, coll, 0.003)
    return root


def lantern():
    name = "Prop_Roadside_Lantern_01"
    coll = collection(name)
    root = empty(name, coll=coll)
    p = palette()
    cylinder(f"{name}_Base", (0, 0, 0.12), 0.20, 0.24, p["iron"], root, coll, "Z", 16)
    cylinder(f"{name}_Post", (0, 0, 1.20), 0.055, 2.15, p["iron"], root, coll, "Z", 16)
    box(f"{name}_Glass", (0, 0, 2.38), (0.34, 0.34, 0.48), p["glass"], root, coll, 0.01)
    for x in (-0.18, 0.18):
        for y in (-0.18, 0.18):
            cylinder(f"{name}_Frame_{x}_{y}", (x, y, 2.38), 0.018, 0.55, p["iron"], root, coll, "Z", 8)
    cone(f"{name}_Roof", (0, 0, 2.76), 0.30, 0.05, 0.22, p["iron"], root, coll, 4, (0, 0, math.pi / 4))
    sphere(f"{name}_Flame", (0, 0, 2.38), (0.06, 0.06, 0.11), p["lamp"], root, coll, 16, 8)
    return root


def milestone():
    name = "Prop_Milestone_01"
    coll = collection(name)
    root = empty(name, coll=coll)
    p = palette()
    box(f"{name}_Post", (0, 0, 0.62), (0.42, 0.30, 1.24), p["plaster"], root, coll, 0.08)
    cone(f"{name}_Cap", (0, 0, 1.31), 0.24, 0, 0.20, p["plaster"], root, coll, 4, (0, 0, math.pi / 4))
    if FONT_PATH.exists():
        text_mesh(f"{name}_Text", "МОСКВА\n620", (0, -0.161, 0.73), 0.105, p["green_dark"], root, coll, font_path=FONT_PATH)
    return root


def birch(index=1):
    name = f"Tree_Birch_{index:02d}"
    coll = collection(name)
    root = empty(name, coll=coll)
    p = palette()
    bark = material("ENV_Birch_Bark", (0.72, 0.70, 0.61), roughness=0.92)
    leaf1 = material("ENV_Leaf_Sage", (0.18, 0.29, 0.11), roughness=0.9)
    leaf2 = material("ENV_Leaf_Light", (0.28, 0.38, 0.14), roughness=0.9)
    cone(f"{name}_Trunk", (0, 0, 3.4), 0.24, 0.09, 6.8, bark, root, coll, 14)
    for i in range(12):
        z = 0.6 + i * 0.45
        box(f"{name}_Bark_Mark_{i}", (0.12, -0.17, z), (0.16, 0.02, 0.035), p["steel"], root, coll, 0.004, (0, 0, RNG.uniform(-0.2, 0.2)))
    for i in range(7):
        angle = i / 7 * math.tau
        start = (0, 0, 3.3 + i * 0.25)
        end = (1.25 * math.cos(angle), 1.25 * math.sin(angle), 4.8 + RNG.uniform(-0.2, 0.6))
        cylinder_between(f"{name}_Branch_{i}", start, end, 0.045, bark, root, coll, 8)
        sphere(f"{name}_Crown_{i}", end, (1.15, 1.05, 1.25), leaf1 if i % 2 else leaf2, root, coll, 14, 8)
    return root


def pine(index=1):
    name = f"Tree_Pine_{index:02d}"
    coll = collection(name)
    root = empty(name, coll=coll)
    p = palette()
    leaf = material("ENV_Pine_Needles", (0.055, 0.16, 0.075), roughness=0.95)
    cone(f"{name}_Trunk", (0, 0, 2.7), 0.28, 0.10, 5.4, p["bark"], root, coll, 12)
    for i in range(5):
        z = 2.0 + i * 0.85
        radius = 2.0 - i * 0.30
        cone(f"{name}_Crown_{i}", (0, 0, z), radius, 0, 2.5, leaf, root, coll, 14)
    return root


def cart():
    name = "Prop_Horse_Cart_01"
    coll = collection(name)
    root = empty(name, coll=coll)
    p = palette()
    box(f"{name}_Bed", (0, 0.45, 0.95), (1.55, 2.15, 0.18), p["wood"], root, coll, 0.02)
    for side in (-1, 1):
        box(f"{name}_Side_{side}", (side * 0.75, 0.45, 1.28), (0.10, 2.15, 0.55), p["wood"], root, coll, 0.015)
        box(f"{name}_Shaft_{side}", (side * 0.48, -1.75, 0.72), (0.10, 2.8, 0.10), p["wood"], root, coll, 0.01, (0.08, 0, 0))
        cylinder(f"{name}_Wheel_Hub_{side}", (side * 0.92, 0.45, 0.58), 0.09, 0.16, p["wood"], root, coll, "X", 16)
        torus(f"{name}_Wheel_Rim_{side}", (side * 0.92, 0.45, 0.58), 0.50, 0.045, p["wood"], root, coll, "X", 32, 8)
        for i in range(12):
            angle = i / 12 * math.tau
            cylinder_between(f"{name}_Wheel_Spoke_{side}_{i}", (side * 0.92, 0.45, 0.58), (side * 0.92, 0.45 + 0.47 * math.cos(angle), 0.58 + 0.47 * math.sin(angle)), 0.018, p["wood"], root, coll, 8)
        torus(f"{name}_Wheel_Tire_{side}", (side * 0.92, 0.45, 0.58), 0.52, 0.028, p["steel"], root, coll, "X", 32, 8)
    box(f"{name}_Front_Board", (0, -0.63, 1.28), (1.55, 0.10, 0.55), p["wood"], root, coll, 0.015)
    return root


def build():
    reset_scene()
    p = palette()
    roots = []
    dirt = road("Road_Dirt_100m", 100, 7, p["dirt"])
    export_module(dirt, "Road_Dirt_100m")
    roots.append((dirt, (0, 0, 0)))
    snow = road("Road_Snow_100m", 100, 7, p["snow"], True)
    export_module(snow, "Road_Snow_100m")
    roots.append((snow, (60, 120, 0)))
    cobble = cobblestone_road()
    export_module(cobble, "Road_Cobblestone_25m")
    roots.append((cobble, (60, 190, 0)))

    house, house_collision = wooden_house()
    export_module(house, "Building_Wood_House_01", True, house_collision)
    roots.append((house, (-7.2, -7.5, 0)))
    gate, gate_collision = gatehouse()
    export_module(gate, "Building_Brick_Gatehouse_01", True, gate_collision)
    roots.append((gate, (7.0, -20, 0)))

    fence_root = fence()
    export_module(fence_root, "Fence_Wood_10m")
    roots.append((fence_root, (-6.5, 5.0, 0)))
    pole1 = telegraph_pole(1)
    export_module(pole1, "Telegraph_Pole_01", True)
    roots.append((pole1, (5.2, -7, 0)))
    pole2 = telegraph_pole(2)
    roots.append((pole2, (5.2, -22, 0)))
    pole3 = telegraph_pole(3)
    roots.append((pole3, (5.2, -37, 0)))
    tube("SCENE_Telegraph_Wire_A", [(5.2, -7, 7.78), (5.2, -22, 7.78), (5.2, -37, 7.78)], 0.009, p["steel"], coll=collection("SCENE_Details"), sides=6)
    tube("SCENE_Telegraph_Wire_B", [(4.52, -7, 7.12), (4.52, -22, 7.12), (4.52, -37, 7.12)], 0.008, p["steel"], coll=collection("SCENE_Details"), sides=6)

    sign = signpost()
    export_module(sign, "Signpost_Russia_1912")
    roots.append((sign, (4.4, -2.5, 0)))
    for factory, filename, location in (
        (fuel_can, "Prop_Fuel_Can_01", (-5.4, -4.4, 0)),
        (tool_chest, "Prop_Tool_Chest_01", (-5.0, -4.6, 0)),
        (barrel, "Prop_Barrel_01", (-8.8, -4.5, 0)),
        (crate, "Prop_Crate_01", (-8.5, -3.8, 0)),
        (hay_bale, "Prop_Hay_Bale_01", (8.5, -11, 0)),
        (lantern, "Prop_Roadside_Lantern_01", (4.2, -5.0, 0)),
        (milestone, "Prop_Milestone_01", (4.6, 4.5, 0)),
        (cart, "Prop_Horse_Cart_01", (7.5, -12.5, 0)),
    ):
        obj = factory()
        export_module(obj, filename)
        roots.append((obj, location))

    birch1 = birch(1)
    export_module(birch1, "Tree_Birch_01", True)
    roots.append((birch1, (-10.5, -13, 0)))
    pine1 = pine(1)
    export_module(pine1, "Tree_Pine_01", True)
    roots.append((pine1, (10.5, -28, 0)))
    for i, location in enumerate(((-11, 2, 0), (11, -4, 0), (-12, -24, 0), (12, -34, 0)), start=2):
        roots.append((birch(i), location))
    for i, location in enumerate(((13, -15, 0), (-13, -32, 0), (14, 4, 0)), start=2):
        roots.append((pine(i), location))

    for root, location in roots:
        root.location = location
    bpy.context.view_layer.update()
    studio = add_studio(target=(0, -14, 2.0), camera=(24, -42, 19), size=42)
    bpy.data.lights["STUDIO_Key"].energy = 15000
    bpy.data.lights["STUDIO_Fill"].energy = 7000
    bpy.data.lights["STUDIO_Rim"].energy = 11000
    ground_bsdf = bpy.data.materials["STUDIO_Ground"].node_tree.nodes.get("Principled BSDF")
    ground_bsdf.inputs["Base Color"].default_value = (0.16, 0.18, 0.15, 1.0)
    world = bpy.context.scene.world
    world.use_nodes = True
    background = world.node_tree.nodes.get("Background")
    background.inputs["Color"].default_value = (0.34, 0.40, 0.46, 1.0)
    background.inputs["Strength"].default_value = 0.42
    sun_data = bpy.data.lights.new("SCENE_WarmSun", "SUN")
    sun_data.energy = 2.2
    sun_data.angle = math.radians(12)
    sun = bpy.data.objects.new("SCENE_WarmSun", sun_data)
    link_object(sun, studio)
    sun.rotation_euler = (math.radians(28), math.radians(-24), math.radians(-32))
    render_preview(PREVIEW_DIR / "Russia_1912_Modular_Kit_preview.png", (1600, 1000), 48)
    save_blend(ASSET_DIR / "Russia_1912_Modular_Kit.blend")
    metadata = {
        "asset_id": "russia_1912_modular_kit",
        "historical_status": "era_inspired_modular_environment",
        "scope": "Russian Empire roadside architecture and props, circa 1912; stylized realistic game-art reconstruction, not a surveyed historical building",
        "coordinate_system": {"blender": "meters, Z up", "unity_fbx": "meters, Y up"},
        "module_count": len(EXPORTS),
        "modules": EXPORTS,
        "textures": "game_assets/textures/shared (CC0 procedural maps)",
        "notes": "FBX module roots are exported at local origin. The BLEND arranges modules into a test street for browsing."
    }
    (ASSET_DIR / "asset_metadata.json").write_text(json.dumps(metadata, ensure_ascii=False, indent=2), encoding="utf-8")
    print("ENVIRONMENT_BUILD_OK", len(EXPORTS), "modules")


try:
    build()
except Exception as exc:
    print("ENVIRONMENT_BUILD_ERROR", type(exc).__name__, str(exc))
    traceback.print_exc()
