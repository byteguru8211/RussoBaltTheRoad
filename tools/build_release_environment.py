from pathlib import Path
import json
import math
import random
import shutil
import sys
import traceback
import bpy
from mathutils import Vector

sys.path.insert(0, str(Path(__file__).resolve().parent))
import blender_assetlib
import importlib
importlib.reload(blender_assetlib)
from blender_assetlib import (
    ROOT, box, collection, cone, cylinder, cylinder_between, descendants, empty,
    ensure_uvs, export_fbx, export_fbx_clean_nodes, material, mesh, optimize_hierarchy,
    palette, save_blend, sphere, text_mesh, torus, asset_stats,
)

ASSET_DIR = ROOT / "game_assets" / "environment" / "release"
UNITY_DIR = ROOT / "unity-project" / "Assets" / "Resources" / "Environment" / "ReleaseKit"
PREVIEW_DIR = ROOT / "previews" / "release_assets"
FONT_PATH = Path("C:/Windows/Fonts/arial.ttf")
RNG = random.Random(1912)
EXPORTS = []


def pbr(name, color, roughness=0.8, metallic=0.0, bump=0.12, emission=None):
    mat = material(name, color, metallic=metallic, roughness=roughness, emission=emission)
    nodes = mat.node_tree.nodes
    links = mat.node_tree.links
    bsdf = nodes.get("Principled BSDF")
    noise = nodes.new("ShaderNodeTexNoise")
    noise.name = "SurfaceMicrodetail"
    noise.inputs["Scale"].default_value = 18.0
    noise.inputs["Detail"].default_value = 3.0
    noise.inputs["Roughness"].default_value = 0.72
    bump_node = nodes.new("ShaderNodeBump")
    bump_node.name = "MicroRelief"
    bump_node.inputs["Strength"].default_value = bump
    bump_node.inputs["Distance"].default_value = 0.035
    links.new(noise.outputs["Fac"], bump_node.inputs["Height"])
    links.new(bump_node.outputs["Normal"], bsdf.inputs["Normal"])
    return mat


def new_asset(name):
    coll = collection(name)
    root = empty(name, coll=coll)
    root["units"] = "meters"
    root["ground_blender"] = "Z=0"
    root["front_blender"] = "-Y"
    root["forward_unity"] = "+Z"
    return root, coll


def rock_mesh(name, location, scale, mat, root, coll, seed):
    rng = random.Random(seed)
    bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=2, radius=1, location=location)
    obj = bpy.context.object
    obj.name = name
    for vertex in obj.data.vertices:
        factor = rng.uniform(0.82, 1.18)
        vertex.co.x *= scale[0] * factor
        vertex.co.y *= scale[1] * rng.uniform(0.78, 1.12)
        vertex.co.z *= scale[2] * factor
    obj.data.materials.append(mat)
    obj.parent = root
    for old in list(obj.users_collection):
        old.objects.unlink(obj)
    coll.objects.link(obj)
    for poly in obj.data.polygons:
        poly.use_smooth = True
    bevel = obj.modifiers.new("WeatheredEdges", "BEVEL")
    bevel.width = 0.025
    bevel.segments = 2
    return obj


def cloud(name, variant):
    root, coll = new_asset(name)
    white = pbr("ENV_Cloud_SoftWhite", (0.93, 0.95, 0.98), roughness=1.0, bump=0.05)
    shade = pbr("ENV_Cloud_Shadow", (0.70, 0.76, 0.82), roughness=1.0, bump=0.04)
    rng = random.Random(4200 + variant)
    lobes = [
        (-2.8, 0.0, 0.3, 1.7, 0.85, 1.25), (-1.4, 0.1, 0.9, 2.0, 1.35, 1.55),
        (0.2, 0.0, 1.35, 2.25, 1.65, 1.7), (1.9, 0.15, 0.85, 1.9, 1.25, 1.45),
        (3.0, -0.1, 0.25, 1.45, 0.75, 1.05), (-0.4, -0.65, 0.15, 2.8, 0.7, 1.5),
    ]
    for index, (x, y, z, sx, sy, sz) in enumerate(lobes):
        sphere(f"{name}_Lobe_{index:02d}", (x, y, z + rng.uniform(-0.12, 0.12)), (sx, sy, sz), white if index % 3 else shade, root, coll, 24, 16)
    for index in range(5):
        x = -2.2 + index * 1.1
        sphere(f"{name}_Detail_{index:02d}", (x, -0.9 + rng.uniform(-0.15, 0.15), 0.1), (0.75, 0.5, 0.55), white, root, coll, 16, 10)
    return root


def conifer():
    name = "Tree_Conifer_Detailed_01"
    root, coll = new_asset(name)
    p = palette()
    bark = pbr("ENV_Conifer_Bark", (0.19, 0.105, 0.055), roughness=0.96, bump=0.28)
    needle = pbr("ENV_Conifer_Needles", (0.035, 0.115, 0.065), roughness=0.93, bump=0.18)
    needle_light = pbr("ENV_Conifer_NewGrowth", (0.075, 0.18, 0.085), roughness=0.9, bump=0.16)
    snow = pbr("ENV_Conifer_Snow", (0.86, 0.91, 0.94), roughness=0.98, bump=0.1)
    cylinder(f"{name}_Trunk", (0, 0, 2.4), 0.22, 4.8, bark, root, coll, vertices=14, bevel=0.025)
    for level in range(7):
        z = 1.55 + level * 0.62
        radius = 2.15 - level * 0.245
        depth = 2.45 - level * 0.14
        cone(f"{name}_Bough_{level}", (0, 0, z), radius, 0.035, depth, needle if level % 2 else needle_light, root, coll, vertices=18, rotation=(0, 0, level * 0.31))
        cone(f"{name}_Snow_{level}", (0, 0, z + 0.24), radius * 0.78, radius * 0.12, depth * 0.54, snow, root, coll, vertices=18, rotation=(0, 0, level * 0.31 + 0.08))
    cone(f"{name}_Crown", (0, 0, 6.15), 0.58, 0, 1.55, needle_light, root, coll, vertices=16)
    for index in range(12):
        angle = index * math.tau / 12
        length = 0.55 + (index % 3) * 0.12
        cylinder_between(f"{name}_Twig_{index:02d}", (0, 0, 1.1 + index * 0.28), (math.cos(angle) * length, math.sin(angle) * length, 1.45 + index * 0.28), 0.025, bark, root, coll, vertices=6)
    return root


def birch():
    name = "Tree_Birch_Detailed_01"
    root, coll = new_asset(name)
    p = palette()
    bark = pbr("ENV_Birch_WhiteBark", (0.76, 0.75, 0.66), roughness=0.94, bump=0.22)
    scar = pbr("ENV_Birch_BarkScar", (0.075, 0.07, 0.055), roughness=0.96, bump=0.2)
    leaf = pbr("ENV_Birch_Leaves", (0.19, 0.31, 0.095), roughness=0.9, bump=0.14)
    leaf_gold = pbr("ENV_Birch_GoldLeaves", (0.43, 0.34, 0.085), roughness=0.9, bump=0.14)
    cylinder(f"{name}_Trunk", (0, 0, 2.8), 0.19, 5.6, bark, root, coll, vertices=14, bevel=0.018)
    rng = random.Random(811)
    for index in range(15):
        angle = rng.uniform(0, math.tau)
        z = 0.45 + index * 0.31
        box(f"{name}_BarkMark_{index:02d}", (math.cos(angle) * 0.185, math.sin(angle) * 0.185, z), (rng.uniform(0.035, 0.07), 0.018, rng.uniform(0.12, 0.3)), scar, root, coll, 0.003, (0, 0, angle))
    for index in range(9):
        angle = index * 2.399
        start_z = 2.7 + (index % 4) * 0.48
        end = (math.cos(angle) * rng.uniform(1.0, 1.8), math.sin(angle) * rng.uniform(1.0, 1.8), start_z + rng.uniform(0.8, 1.5))
        cylinder_between(f"{name}_Branch_{index:02d}", (0, 0, start_z), end, 0.055, bark, root, coll, vertices=8)
        for lobe in range(3):
            offset = Vector((rng.uniform(-0.45, 0.45), rng.uniform(-0.45, 0.45), rng.uniform(-0.25, 0.35)))
            sphere(f"{name}_Crown_{index:02d}_{lobe}", Vector(end) + offset, (0.85, 0.75, 0.95), leaf if (index + lobe) % 3 else leaf_gold, root, coll, 16, 10)
    return root


def oak():
    name = "Tree_Oak_Detailed_01"
    root, coll = new_asset(name)
    bark = pbr("ENV_Oak_Bark", (0.16, 0.105, 0.065), roughness=0.97, bump=0.3)
    leaf = pbr("ENV_Oak_Leaves", (0.09, 0.20, 0.06), roughness=0.92, bump=0.16)
    cylinder(f"{name}_Trunk", (0, 0, 2.2), 0.34, 4.4, bark, root, coll, vertices=16, bevel=0.04)
    for index in range(11):
        angle = index * 2.399
        start = (0, 0, 2.7 + (index % 3) * 0.45)
        end = (math.cos(angle) * (1.6 + index % 2 * 0.5), math.sin(angle) * (1.6 + index % 2 * 0.5), 4.3 + (index % 4) * 0.38)
        cylinder_between(f"{name}_Limb_{index:02d}", start, end, 0.09, bark, root, coll, vertices=10)
        for lobe in range(4):
            loc = Vector(end) + Vector((math.cos(lobe * 1.7) * 0.6, math.sin(lobe * 1.7) * 0.6, (lobe % 2) * 0.45))
            sphere(f"{name}_Canopy_{index:02d}_{lobe}", loc, (1.05, 0.95, 0.9), leaf, root, coll, 16, 10)
    return root


def rocks():
    name = "Prop_Rock_Cluster_01"
    root, coll = new_asset(name)
    mat = pbr("ENV_Weathered_Granite", (0.31, 0.33, 0.30), roughness=0.96, bump=0.32)
    moss = pbr("ENV_Rock_Moss", (0.12, 0.18, 0.07), roughness=1.0, bump=0.2)
    specs = [(-0.8, 0.2, 0.45, 1.0, 0.7, 0.85), (0.45, 0.3, 0.62, 1.25, 1.0, 1.0), (1.25, -0.35, 0.32, 0.72, 0.5, 0.65), (-0.15, -0.8, 0.25, 0.65, 0.42, 0.58)]
    for index, (x, y, z, sx, sy, sz) in enumerate(specs):
        rock_mesh(f"{name}_Stone_{index}", (x, y, z), (sx, sy, sz), mat, root, coll, 900 + index)
        sphere(f"{name}_Moss_{index}", (x - 0.1, y, z + sy * 0.72), (sx * 0.55, sy * 0.48, 0.08), moss, root, coll, 12, 8)
    return root


def fence():
    name = "Prop_Fence_10m_01"
    root, coll = new_asset(name)
    p = palette()
    wood = pbr("ENV_Fence_WeatheredWood", (0.25, 0.16, 0.085), roughness=0.93, bump=0.24)
    for index in range(9):
        x = -5 + index * 1.25
        box(f"{name}_Post_{index:02d}", (x, 0, 0.85), (0.17, 0.17, 1.7), wood, root, coll, 0.018)
        cone(f"{name}_PostCap_{index:02d}", (x, 0, 1.77), 0.14, 0, 0.18, wood, root, coll, vertices=4, rotation=(0, 0, math.pi / 4))
    for z in (0.48, 1.18):
        box(f"{name}_Rail_{z}", (0, 0, z), (10.0, 0.11, 0.15), wood, root, coll, 0.012)
    for index in range(31):
        box(f"{name}_Picket_{index:02d}", (-4.82 + index * 0.32, 0, 0.73), (0.10, 0.075, 1.18), wood, root, coll, 0.01)
    return root


def telegraph_pole():
    name = "Prop_Telegraph_Pole_01"
    root, coll = new_asset(name)
    p = palette()
    wood = pbr("ENV_Telegraph_CreosoteWood", (0.12, 0.085, 0.05), roughness=0.9, bump=0.24)
    ceramic = pbr("ENV_Telegraph_Ceramic", (0.56, 0.65, 0.63), roughness=0.24, bump=0.06)
    metal = pbr("ENV_Telegraph_Iron", (0.08, 0.09, 0.085), metallic=0.78, roughness=0.48, bump=0.12)
    cone(f"{name}_Pole", (0, 0, 4.0), 0.18, 0.10, 8.0, wood, root, coll, vertices=14)
    for z in (6.85, 7.5):
        box(f"{name}_Crossarm_{z}", (0, 0, z), (2.2, 0.14, 0.14), wood, root, coll, 0.012)
        for x in (-0.9, -0.3, 0.3, 0.9):
            cylinder(f"{name}_Insulator_{z}_{x}", (x, 0, z + 0.14), 0.07, 0.18, ceramic, root, coll, vertices=12)
            sphere(f"{name}_InsulatorCap_{z}_{x}", (x, 0, z + 0.25), (0.08, 0.08, 0.045), ceramic, root, coll, 12, 8)
    for index, z in enumerate((1.35, 2.05, 2.75)):
        box(f"{name}_Climber_{index}", (0.2, 0, z), (0.3, 0.07, 0.075), metal, root, coll, 0.004)
    for wire_index, x in enumerate((-0.9, 0.9)):
        points = []
        for step in range(9):
            t = step / 8.0
            points.append((x + (t - 0.5) * 16.0, 0, 7.72 - math.sin(t * math.pi) * 0.65))
        for step in range(8):
            cylinder_between(f"{name}_Wire_{wire_index}_{step}", points[step], points[step + 1], 0.012, metal, root, coll, vertices=5)
    return root


def milestone():
    name = "Prop_Milestone_01"
    root, coll = new_asset(name)
    p = palette()
    stone = pbr("ENV_Milestone_Stone", (0.61, 0.60, 0.53), roughness=0.94, bump=0.25)
    black = pbr("ENV_Milestone_Black", (0.035, 0.045, 0.04), roughness=0.8, bump=0.12)
    brass = pbr("ENV_Milestone_Brass", (0.55, 0.34, 0.12), metallic=0.82, roughness=0.38, bump=0.08)
    box(f"{name}_Base", (0, 0, 0.12), (0.62, 0.52, 0.24), stone, root, coll, 0.035)
    box(f"{name}_Post", (0, 0, 0.82), (0.42, 0.34, 1.2), stone, root, coll, 0.055)
    box(f"{name}_Cap", (0, 0, 1.47), (0.48, 0.40, 0.16), black, root, coll, 0.04)
    box(f"{name}_Plate", (0, -0.18, 0.95), (0.30, 0.025, 0.42), brass, root, coll, 0.015)
    if FONT_PATH.exists():
        text_mesh(f"{name}_Distance", "12", (0, -0.20, 0.97), 0.19, black, root, coll, font_path=FONT_PATH, extrude=0.003)
    for x in (-0.12, 0.12):
        sphere(f"{name}_Bolt_{x}", (x, -0.20, 1.10), (0.018, 0.01, 0.018), brass, root, coll, 10, 6)
    return root


def window(name, x, y, z, side, root, coll, width=0.9, height=1.1):
    p = palette()
    glass = pbr("ENV_Window_HistoricGlass", (0.16, 0.27, 0.29), roughness=0.18, metallic=0.08, bump=0.03)
    frame = pbr("ENV_Window_PaintedFrame", (0.73, 0.71, 0.61), roughness=0.76, bump=0.1)
    if side == "FRONT":
        box(f"{name}_Glass", (x, y, z), (width, 0.035, height), glass, root, coll, 0.004)
        for xx in (x - width / 2, x + width / 2):
            box(f"{name}_FrameV_{xx}", (xx, y - 0.03, z), (0.075, 0.09, height + 0.14), frame, root, coll, 0.008)
        for zz in (z - height / 2, z + height / 2):
            box(f"{name}_FrameH_{zz}", (x, y - 0.03, zz), (width + 0.14, 0.09, 0.075), frame, root, coll, 0.008)
        box(f"{name}_MullionV", (x, y - 0.045, z), (0.035, 0.05, height), frame, root, coll, 0.004)
        box(f"{name}_MullionH", (x, y - 0.045, z), (width, 0.05, 0.035), frame, root, coll, 0.004)
    else:
        box(f"{name}_Glass", (x, y, z), (0.035, width, height), glass, root, coll, 0.004)
        for yy in (y - width / 2, y + width / 2):
            box(f"{name}_FrameV_{yy}", (x, yy, z), (0.09, 0.075, height + 0.14), frame, root, coll, 0.008)
        for zz in (z - height / 2, z + height / 2):
            box(f"{name}_FrameH_{zz}", (x, y, zz), (0.09, width + 0.14, 0.075), frame, root, coll, 0.008)
        box(f"{name}_MullionV", (x, y, z), (0.05, 0.035, height), frame, root, coll, 0.004)
        box(f"{name}_MullionH", (x, y, z), (0.05, width, 0.035), frame, root, coll, 0.004)


def log_cabin():
    name = "Building_Log_Cabin_Detailed_01"
    root, coll = new_asset(name)
    p = palette()
    stone = pbr("ENV_Cabin_FoundationStone", (0.38, 0.39, 0.35), roughness=0.96, bump=0.3)
    wood = pbr("ENV_Cabin_Logs", (0.29, 0.16, 0.075), roughness=0.92, bump=0.28)
    roof = pbr("ENV_Cabin_ShingleRoof", (0.12, 0.105, 0.085), roughness=0.9, bump=0.3)
    trim = pbr("ENV_Cabin_PaintedTrim", (0.70, 0.67, 0.55), roughness=0.8, bump=0.12)
    box(f"{name}_Foundation", (0, 0, 0.25), (8.2, 6.2, 0.5), stone, root, coll, 0.06)
    for level in range(11):
        z = 0.65 + level * 0.265
        for y in (-2.92, 2.92):
            cylinder_between(f"{name}_LogFB_{level}_{y}", (-4.0, y, z), (4.0, y, z), 0.15, wood, root, coll, vertices=12)
        for x in (-3.92, 3.92):
            cylinder_between(f"{name}_LogSide_{level}_{x}", (x, -2.92, z), (x, 2.92, z), 0.15, wood, root, coll, vertices=12)
    for side in (-1, 1):
        box(f"{name}_Roof_{side}", (side * 2.05, 0, 4.2), (4.75, 6.8, 0.16), roof, root, coll, 0.025, (0, side * math.radians(31), 0))
        for row in range(5):
            for col in range(7):
                box(f"{name}_Shingle_{side}_{row}_{col}", (side * (0.35 + row * 0.78), -2.95 + col * 0.98, 4.48 - row * 0.47), (0.72, 0.90, 0.045), roof, root, coll, 0.008, (0, side * math.radians(31), 0))
    cylinder_between(f"{name}_Ridge", (0, -3.45, 5.0), (0, 3.45, 5.0), 0.09, roof, root, coll, vertices=12)
    for y in (-2.93, 2.93):
        mesh(f"{name}_Gable_{y}", [(-3.9, y, 3.35), (3.9, y, 3.35), (0, y, 4.95)], [(0, 1, 2)], wood, root, coll, False)
    box(f"{name}_Door", (0, -3.10, 1.47), (1.15, 0.12, 2.05), wood, root, coll, 0.025)
    for x in (-0.67, 0.67):
        box(f"{name}_DoorFrame_{x}", (x, -3.18, 1.48), (0.12, 0.16, 2.25), trim, root, coll, 0.01)
    box(f"{name}_Lintel", (0, -3.18, 2.57), (1.46, 0.16, 0.13), trim, root, coll, 0.01)
    sphere(f"{name}_DoorKnob", (0.42, -3.20, 1.43), (0.045, 0.025, 0.045), p["brass"], root, coll, 12, 6)
    for index, x in enumerate((-2.45, 2.45)):
        window(f"{name}_FrontWindow_{index}", x, -3.08, 2.12, "FRONT", root, coll)
        window(f"{name}_SideWindow_{index}", -4.08, -1.25 if index == 0 else 1.25, 2.12, "SIDE", root, coll)
    for x in (-1.25, 1.25):
        cylinder(f"{name}_PorchPost_{x}", (x, -3.75, 1.55), 0.09, 2.45, wood, root, coll, vertices=10)
    box(f"{name}_PorchRoof", (0, -3.55, 2.86), (3.2, 1.55, 0.14), roof, root, coll, 0.02, (math.radians(12), 0, 0))
    brick = pbr("ENV_Cabin_ChimneyBrick", (0.36, 0.14, 0.08), roughness=0.92, bump=0.3)
    box(f"{name}_Chimney", (2.65, 1.55, 4.45), (0.65, 0.65, 1.65), brick, root, coll, 0.02)
    box(f"{name}_ChimneyCap", (2.65, 1.55, 5.30), (0.82, 0.82, 0.14), stone, root, coll, 0.015)
    if FONT_PATH.exists():
        text_mesh(f"{name}_Date", "1912", (0, -2.96, 3.92), 0.38, trim, root, coll, font_path=FONT_PATH)
    return root


def townhouse():
    name = "Building_Townhouse_Detailed_01"
    root, coll = new_asset(name)
    p = palette()
    plaster = pbr("ENV_Townhouse_OchrePlaster", (0.61, 0.49, 0.31), roughness=0.92, bump=0.18)
    trim = pbr("ENV_Townhouse_Cornice", (0.77, 0.73, 0.61), roughness=0.82, bump=0.12)
    roof = pbr("ENV_Townhouse_Terracotta", (0.37, 0.13, 0.07), roughness=0.88, bump=0.24)
    door = pbr("ENV_Townhouse_Door", (0.055, 0.105, 0.09), roughness=0.66, bump=0.14)
    box(f"{name}_Body", (0, 0, 3.6), (7.2, 6.0, 7.2), plaster, root, coll, 0.045)
    box(f"{name}_Cornice", (0, 0, 7.12), (7.55, 6.35, 0.28), trim, root, coll, 0.025)
    for side in (-1, 1):
        box(f"{name}_Roof_{side}", (side * 1.85, 0, 8.15), (4.45, 6.7, 0.22), roof, root, coll, 0.03, (0, side * math.radians(32), 0))
    for floor in range(3):
        for column in (-1, 0, 1):
            x = column * 2.15
            window(f"{name}_FrontWindow_{floor}_{column}", x, -3.04, 1.85 + floor * 2.15, "FRONT", root, coll, 1.05, 1.35)
            if column != 0:
                window(f"{name}_SideWindow_{floor}_{column}", 3.64, column * 1.85, 1.85 + floor * 2.15, "SIDE", root, coll, 1.0, 1.35)
    box(f"{name}_Door", (0, -3.08, 1.25), (1.3, 0.14, 2.5), door, root, coll, 0.025)
    for x in (-0.76, 0.76):
        box(f"{name}_DoorFrame_{x}", (x, -3.18, 1.28), (0.14, 0.18, 2.7), trim, root, coll, 0.012)
    box(f"{name}_DoorLintel", (0, -3.18, 2.68), (1.65, 0.18, 0.16), trim, root, coll, 0.012)
    sphere(f"{name}_DoorKnob", (0.45, -3.20, 1.2), (0.05, 0.03, 0.05), p["brass"], root, coll, 12, 6)
    box(f"{name}_Chimney", (-2.1, 1.3, 8.7), (0.72, 0.72, 1.8), plaster, root, coll, 0.02)
    box(f"{name}_ChimneyCap", (-2.1, 1.3, 9.65), (0.9, 0.9, 0.16), trim, root, coll, 0.015)
    for step in range(3):
        box(f"{name}_Step_{step}", (0, -3.45 - step * 0.28, 0.18 - step * 0.05), (1.8, 0.45, 0.16), trim, root, coll, 0.015)
    return root


def haystack():
    name = "Prop_Haystack_01"
    root, coll = new_asset(name)
    hay = pbr("ENV_Hay_Dry", (0.55, 0.36, 0.10), roughness=1.0, bump=0.38)
    rope = pbr("ENV_Hay_Rope", (0.30, 0.20, 0.08), roughness=0.96, bump=0.2)
    cone(f"{name}_Stack", (0, 0, 1.45), 1.45, 0.08, 2.9, hay, root, coll, vertices=32)
    for z in (0.72, 1.38, 2.02):
        radius = 1.45 * (1 - z / 3.0) + 0.05
        torus(f"{name}_Rope_{z}", (0, 0, z), radius, 0.025, rope, root, coll, major_segments=32, minor_segments=6)
    cylinder(f"{name}_Pole", (0, 0, 3.05), 0.045, 1.2, rope, root, coll, vertices=8)
    return root


def cart():
    name = "Prop_Cart_Detailed_01"
    root, coll = new_asset(name)
    p = palette()
    wood = pbr("ENV_Cart_Wood", (0.31, 0.18, 0.075), roughness=0.9, bump=0.26)
    iron = pbr("ENV_Cart_Iron", (0.075, 0.08, 0.07), metallic=0.78, roughness=0.52, bump=0.15)
    box(f"{name}_Bed", (0, 0, 1.0), (2.0, 3.2, 0.22), wood, root, coll, 0.025)
    for side in (-1, 1):
        for index in range(6):
            box(f"{name}_Side_{side}_{index}", (side * 1.0, -1.35 + index * 0.54, 1.42), (0.10, 0.46, 0.75), wood, root, coll, 0.015)
        box(f"{name}_Rail_{side}", (side * 1.04, 0, 1.83), (0.12, 3.35, 0.12), wood, root, coll, 0.015)
        cylinder_between(f"{name}_Shaft_{side}", (side * 0.72, -1.5, 0.92), (side * 0.55, -4.0, 0.62), 0.055, wood, root, coll, vertices=8)
    for index in range(5):
        box(f"{name}_Floor_{index}", (0, -1.25 + index * 0.62, 1.14), (1.9, 0.54, 0.08), wood, root, coll, 0.01)
    for x in (-1.12, 1.12):
        torus(f"{name}_WheelTire_{x}", (x, 0.15, 0.72), 0.68, 0.065, iron, root, coll, axis="X", major_segments=32, minor_segments=8)
        cylinder(f"{name}_WheelHub_{x}", (x, 0.15, 0.72), 0.12, 0.22, iron, root, coll, axis="X", vertices=12)
        for spoke in range(10):
            angle = spoke * math.tau / 10
            cylinder_between(f"{name}_Spoke_{x}_{spoke}", (x, 0.15, 0.72), (x, 0.15 + math.cos(angle) * 0.61, 0.72 + math.sin(angle) * 0.61), 0.022, wood, root, coll, vertices=6)
    cylinder_between(f"{name}_Axle", (-1.25, 0.15, 0.72), (1.25, 0.15, 0.72), 0.07, iron, root, coll, vertices=10)
    return root


def start_arch():
    name = "Prop_Start_Arch_1912"
    root, coll = new_asset(name)
    p = palette()
    stone = pbr("ENV_Start_Arch_Stone", (0.47, 0.46, 0.40), roughness=0.94, bump=0.28)
    wood = pbr("ENV_Start_Arch_Wood", (0.16, 0.085, 0.04), roughness=0.84, bump=0.22)
    cloth = pbr("ENV_Start_Arch_Cloth", (0.80, 0.75, 0.60), roughness=0.95, bump=0.1)
    black = pbr("ENV_Start_Arch_Black", (0.025, 0.032, 0.028), roughness=0.82, bump=0.1)
    for side in (-1, 1):
        box(f"{name}_PillarBase_{side}", (side * 4.2, 0, 0.3), (1.0, 1.0, 0.6), stone, root, coll, 0.06)
        box(f"{name}_Pillar_{side}", (side * 4.2, 0, 2.5), (0.62, 0.62, 4.4), stone, root, coll, 0.05)
        box(f"{name}_PillarCap_{side}", (side * 4.2, 0, 4.75), (0.9, 0.9, 0.25), stone, root, coll, 0.04)
    box(f"{name}_Crossbeam", (0, 0, 4.65), (9.2, 0.55, 0.65), wood, root, coll, 0.04)
    box(f"{name}_Banner", (0, -0.31, 4.62), (8.5, 0.08, 1.05), cloth, root, coll, 0.02)
    for index in range(20):
        box(f"{name}_Checker_{index:02d}", (-4.0 + index * 0.42, -0.36, 4.62), (0.42, 0.02, 0.18), black if index % 2 else cloth, root, coll, 0)
    for side in (-1, 1):
        for index in range(3):
            mesh(f"{name}_Pennant_{side}_{index}", [(side * (2.0 + index * 0.65), -0.34, 4.15), (side * (2.45 + index * 0.65), -0.34, 4.15), (side * (2.22 + index * 0.65), -0.34, 3.65)], [(0, 1, 2)], p["red"], root, coll, False)
    if FONT_PATH.exists():
        text_mesh(f"{name}_Label", "RUSSO-BALT · 1912", (0, -0.38, 4.88), 0.42, cloth, root, coll, font_path=FONT_PATH, extrude=0.004)
    return root


def gear_emblem(name, cat=False):
    root, coll = new_asset(name)
    p = palette()
    brass = pbr("ENV_Emblem_AgedBrass", (0.62, 0.38, 0.11), metallic=0.88, roughness=0.3, bump=0.08)
    dark = pbr("ENV_Emblem_BlackenedSteel", (0.035, 0.045, 0.04), metallic=0.82, roughness=0.38, bump=0.1)
    copper = pbr("ENV_Emblem_Copper", (0.52, 0.19, 0.07), metallic=0.8, roughness=0.36, bump=0.1)
    cylinder(f"{name}_Hub", (0, 0, 0), 1.05, 0.24, brass, root, coll, axis="Y", vertices=48, bevel=0.025)
    torus(f"{name}_Ring", (0, -0.15, 0), 0.82, 0.10, brass, root, coll, axis="Y", major_segments=48, minor_segments=10)
    for index in range(16):
        angle = index * math.tau / 16
        box(f"{name}_Tooth_{index:02d}", (math.cos(angle) * 1.25, 0, math.sin(angle) * 1.25), (0.34, 0.28, 0.30), brass, root, coll, 0.025, (0, -angle, 0))
    cylinder(f"{name}_Center", (0, -0.18, 0), 0.43, 0.18, dark, root, coll, axis="Y", vertices=32, bevel=0.02)
    if cat:
        sphere(f"{name}_Head", (0, -0.30, 0.05), (0.62, 0.18, 0.55), copper, root, coll, 24, 14)
        for side in (-1, 1):
            cone(f"{name}_Ear_{side}", (side * 0.38, -0.30, 0.55), 0.20, 0, 0.48, copper, root, coll, vertices=3, rotation=(math.pi / 2, 0, side * 0.18))
            sphere(f"{name}_Eye_{side}", (side * 0.22, -0.50, 0.14), (0.09, 0.035, 0.07), p["lamp"], root, coll, 16, 8)
        sphere(f"{name}_Muzzle", (0, -0.51, -0.18), (0.30, 0.05, 0.18), brass, root, coll, 16, 10)
        torus(f"{name}_Collar", (0, -0.26, -0.28), 0.55, 0.045, dark, root, coll, axis="Y", major_segments=32, minor_segments=8)
    else:
        box(f"{name}_B", (-0.16, -0.31, -0.02), (0.12, 0.05, 0.44), brass, root, coll, 0.01)
        box(f"{name}_BTop", (0.06, -0.31, 0.15), (0.30, 0.05, 0.12), brass, root, coll, 0.01)
        box(f"{name}_BBottom", (0.06, -0.31, -0.18), (0.30, 0.05, 0.12), brass, root, coll, 0.01)
        box(f"{name}_BMiddle", (0.12, -0.31, -0.015), (0.18, 0.05, 0.12), brass, root, coll, 0.01)
    return root


def workshop_stage():
    name = "Welcome_Workshop_Stage"
    root, coll = new_asset(name)
    p = palette()
    brick = pbr("ENV_Welcome_Brick", (0.25, 0.085, 0.045), roughness=0.9, bump=0.3)
    stone = pbr("ENV_Welcome_Stone", (0.39, 0.40, 0.36), roughness=0.94, bump=0.28)
    brass = pbr("ENV_Welcome_Brass", (0.60, 0.36, 0.10), metallic=0.86, roughness=0.32, bump=0.08)
    dark = pbr("ENV_Welcome_Iron", (0.045, 0.055, 0.05), metallic=0.8, roughness=0.44, bump=0.14)
    wood = pbr("ENV_Welcome_Walnut", (0.20, 0.095, 0.035), roughness=0.58, bump=0.2)
    box(f"{name}_Plinth", (0, 0, 0.18), (18.0, 11.0, 0.36), stone, root, coll, 0.08)
    box(f"{name}_BackWall", (0, 4.4, 3.25), (16.2, 0.55, 6.5), brick, root, coll, 0.06)
    for side in (-1, 1):
        box(f"{name}_Column_{side}", (side * 7.25, 3.95, 3.1), (0.85, 0.95, 6.2), stone, root, coll, 0.07)
        box(f"{name}_ColumnBase_{side}", (side * 7.25, 3.95, 0.55), (1.25, 1.3, 0.55), stone, root, coll, 0.05)
        box(f"{name}_ColumnCapital_{side}", (side * 7.25, 3.95, 6.0), (1.25, 1.3, 0.35), brass, root, coll, 0.04)
    box(f"{name}_Entablature", (0, 4.0, 6.35), (15.8, 1.1, 0.55), stone, root, coll, 0.05)
    box(f"{name}_BrassLine", (0, 3.42, 6.02), (15.2, 0.06, 0.08), brass, root, coll, 0.01)
    for index in range(13):
        angle = math.pi * index / 12
        x = 3.0 * math.cos(angle)
        z = 3.0 + 3.0 * math.sin(angle)
        box(f"{name}_ArchBlock_{index:02d}", (x, 3.95, z), (0.72, 0.85, 0.48), stone, root, coll, 0.035, (0, math.pi / 2 - angle, 0))
    for side in (-1, 1):
        for row in range(4):
            for col in range(3):
                box(f"{name}_BrickRelief_{side}_{row}_{col}", (side * (4.3 + col * 0.78), 4.09, 1.2 + row * 0.52), (0.68, 0.06, 0.40), brick, root, coll, 0.018)
    for side in (-1, 1):
        cylinder(f"{name}_GaugeRim_{side}", (side * 2.25, 3.99, 5.25), 0.55, 0.12, brass, root, coll, axis="Y", vertices=40, bevel=0.015)
        cylinder(f"{name}_GaugeFace_{side}", (side * 2.25, 3.90, 5.25), 0.46, 0.04, p["cream"], root, coll, axis="Y", vertices=40, bevel=0.005)
        for tick in range(11):
            angle = -math.pi * 0.75 + tick * math.pi * 1.5 / 10
            x = side * 2.25 + math.cos(angle) * 0.35
            z = 5.25 + math.sin(angle) * 0.35
            box(f"{name}_GaugeTick_{side}_{tick}", (x, 3.86, z), (0.035, 0.025, 0.10), dark, root, coll, 0.002, (0, -angle + math.pi / 2, 0))
        cylinder_between(f"{name}_GaugeNeedle_{side}", (side * 2.25, 3.82, 5.25), (side * 2.25 + 0.25, 3.82, 5.43), 0.018, p["red"], root, coll, vertices=6)
        cylinder(f"{name}_GaugePin_{side}", (side * 2.25, 3.79, 5.25), 0.06, 0.06, brass, root, coll, axis="Y", vertices=16)
    box(f"{name}_Plaque", (0, 3.96, 5.65), (3.2, 0.18, 0.85), wood, root, coll, 0.04)
    box(f"{name}_PlaqueInset", (0, 3.84, 5.65), (2.9, 0.04, 0.58), brass, root, coll, 0.02)
    if FONT_PATH.exists():
        text_mesh(f"{name}_PlaqueText", "RB · MOTOR WORKS", (0, 3.80, 5.66), 0.28, dark, root, coll, font_path=FONT_PATH, extrude=0.003)
    for side in (-1, 1):
        box(f"{name}_Banner_{side}", (side * 5.8, 3.83, 4.9), (1.4, 0.08, 2.2), p["green"], root, coll, 0.02)
        if FONT_PATH.exists():
            text_mesh(f"{name}_BannerText_{side}", "1912", (side * 5.8, 3.76, 4.9), 0.42, p["cream"], root, coll, font_path=FONT_PATH, extrude=0.003)
    return root


def export_asset(root, name, major=False):
    ensure_uvs(root)
    detailed = asset_stats(root)
    export_fbx(root, ASSET_DIR / f"{name}_Detailed.fbx")
    optimized = optimize_hierarchy(root, "_OPT")
    ensure_uvs(optimized)
    export_fbx_clean_nodes(optimized, ASSET_DIR / f"{name}_LOD0.fbx", ("_OPT",))
    target = UNITY_DIR / f"{name}.fbx"
    target.parent.mkdir(parents=True, exist_ok=True)
    shutil.copy2(ASSET_DIR / f"{name}_LOD0.fbx", target)
    stats = asset_stats(optimized)
    EXPORTS.append({"id": name, "runtime": target.relative_to(ROOT).as_posix(), "source": (ASSET_DIR / f"{name}_Detailed.fbx").relative_to(ROOT).as_posix(), "stats": stats, "detailed": detailed})
    for obj in descendants(optimized):
        obj.hide_render = True
        obj.hide_viewport = True
    print("EXPORTED", name, stats["meshes"], stats["triangles"], stats["dimensions_m"])


def build():
    scene = bpy.data.scenes.new("ReleaseEnvironmentKit")
    bpy.context.window.scene = scene
    scene.unit_settings.system = "METRIC"
    scene.unit_settings.scale_length = 1.0
    scene.render.engine = "BLENDER_EEVEE"
    scene.view_settings.view_transform = "AgX"
    assets = [
        (conifer(), "Tree_Conifer_Detailed_01", True),
        (birch(), "Tree_Birch_Detailed_01", True),
        (oak(), "Tree_Oak_Detailed_01", True),
        (rocks(), "Prop_Rock_Cluster_01", True),
        (fence(), "Prop_Fence_10m_01", False),
        (telegraph_pole(), "Prop_Telegraph_Pole_01", True),
        (milestone(), "Prop_Milestone_01", False),
        (log_cabin(), "Building_Log_Cabin_Detailed_01", True),
        (townhouse(), "Building_Townhouse_Detailed_01", True),
        (haystack(), "Prop_Haystack_01", False),
        (cart(), "Prop_Cart_Detailed_01", True),
        (start_arch(), "Prop_Start_Arch_1912", True),
        (cloud("Cloud_Cumulus_01", 1), "Cloud_Cumulus_01", True),
        (cloud("Cloud_Cumulus_02", 2), "Cloud_Cumulus_02", True),
        (cloud("Cloud_Cumulus_03", 3), "Cloud_Cumulus_03", True),
        (gear_emblem("Welcome_Byteguru_Emblem", False), "Welcome_Byteguru_Emblem", True),
        (gear_emblem("Welcome_Heckercat_Emblem", True), "Welcome_Heckercat_Emblem", True),
        (workshop_stage(), "Welcome_Workshop_Stage", True),
    ]
    for root, name, major in assets:
        export_asset(root, name, major)
    ASSET_DIR.mkdir(parents=True, exist_ok=True)
    (ASSET_DIR / "metadata.json").write_text(json.dumps({"units": "meters", "seed": 1912, "assets": EXPORTS}, ensure_ascii=False, indent=2), encoding="utf-8")
    save_blend(ASSET_DIR / "Track_Scenery_Detailed.blend")
    print("RELEASE_ENVIRONMENT_COMPLETE", len(EXPORTS))


try:
    build()
except Exception:
    traceback.print_exc()
    raise
