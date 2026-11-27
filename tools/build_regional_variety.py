import bpy
import math
import random
import shutil
from pathlib import Path
from mathutils import Vector

ROOT = Path("D:/programming/RussoBaltTheRoad")
SOURCE = ROOT / "game_assets/environment/variety"
UNITY = ROOT / "unity-project/Assets/Resources/Environment/RegionalKit"
SOURCE.mkdir(parents=True, exist_ok=True)
UNITY.mkdir(parents=True, exist_ok=True)
random.seed(1912)

scene = bpy.context.scene
for obj in list(scene.objects):
    bpy.data.objects.remove(obj, do_unlink=True)
collection = bpy.data.collections.new("Regional_Variety_Assets")
scene.collection.children.link(collection)


def move_to_collection(obj):
    for old in list(obj.users_collection):
        old.objects.unlink(obj)
    collection.objects.link(obj)


def pbr(name, color, roughness=0.75, metallic=0.0, bump=0.12, scale=5.0):
    material = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    material.use_nodes = True
    shader = material.node_tree.nodes.get("Principled BSDF")
    shader.inputs["Base Color"].default_value = (*color, 1)
    shader.inputs["Roughness"].default_value = roughness
    shader.inputs["Metallic"].default_value = metallic
    noise = material.node_tree.nodes.new("ShaderNodeTexNoise")
    noise.inputs["Scale"].default_value = scale
    noise.inputs["Detail"].default_value = 3.0
    bump_node = material.node_tree.nodes.new("ShaderNodeBump")
    bump_node.inputs["Strength"].default_value = bump
    bump_node.inputs["Distance"].default_value = 0.08
    material.node_tree.links.new(noise.outputs["Fac"], bump_node.inputs["Height"])
    material.node_tree.links.new(bump_node.outputs["Normal"], shader.inputs["Normal"])
    return material


stone = pbr("ENV_Regional_Limestone", (0.52, 0.49, 0.42), 0.92, bump=0.28, scale=7)
rock = pbr("ENV_Regional_Alpine_Rock", (0.31, 0.34, 0.36), 0.96, bump=0.42, scale=4)
snow = pbr("ENV_Regional_Snow", (0.91, 0.95, 0.98), 0.82, bump=0.08, scale=12)
wood = pbr("ENV_Regional_Dark_Timber", (0.20, 0.105, 0.052), 0.88, bump=0.24, scale=8)
plaster = pbr("ENV_Regional_Warm_Plaster", (0.76, 0.68, 0.53), 0.94, bump=0.16, scale=9)
terracotta = pbr("ENV_Regional_Terracotta", (0.46, 0.16, 0.085), 0.9, bump=0.2, scale=10)
slate = pbr("ENV_Regional_Slate", (0.12, 0.16, 0.19), 0.86, bump=0.22, scale=9)
leaf = pbr("ENV_Regional_Cypress", (0.075, 0.19, 0.095), 0.94, bump=0.18, scale=6)
reed = pbr("ENV_Regional_Reed", (0.31, 0.37, 0.12), 0.9, bump=0.12, scale=7)
metal = pbr("ENV_Regional_Iron", (0.055, 0.065, 0.07), 0.55, 0.72, 0.08, 12)
glass = pbr("ENV_Regional_Glass", (0.20, 0.34, 0.38), 0.18, 0.15, 0.03, 15)
canvas = pbr("ENV_Regional_Canvas", (0.72, 0.65, 0.48), 0.96, bump=0.12, scale=14)
cobble_a = pbr("ENV_Regional_Cobble_A", (0.38, 0.36, 0.31), 0.93, bump=0.32, scale=6)
cobble_b = pbr("ENV_Regional_Cobble_B", (0.47, 0.44, 0.37), 0.95, bump=0.3, scale=7)
dirt = pbr("ENV_Regional_Dirt", (0.24, 0.18, 0.11), 1.0, bump=0.35, scale=5)


def finish(obj, material, bevel=0.02, smooth=False):
    move_to_collection(obj)
    obj.data.materials.append(material)
    if bevel > 0:
        modifier = obj.modifiers.new("EdgeWear", "BEVEL")
        modifier.width = bevel
        modifier.segments = 2
        modifier.harden_normals = True
    if smooth:
        for polygon in obj.data.polygons:
            polygon.use_smooth = True
    return obj


def empty(name, location):
    obj = bpy.data.objects.new(name, None)
    obj.location = location
    collection.objects.link(obj)
    return obj


def box(name, location, size, material, parent, bevel=0.025, rotation=None):
    bpy.ops.mesh.primitive_cube_add(size=1, location=location)
    obj = bpy.context.object
    obj.name = name
    obj.scale = size
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    if rotation:
        obj.rotation_euler = rotation
    finish(obj, material, bevel, False)
    obj.parent = parent
    return obj


def cylinder(name, location, radius1, radius2, depth, material, parent, vertices=16, rotation=None):
    bpy.ops.mesh.primitive_cone_add(vertices=vertices, radius1=radius1, radius2=radius2, depth=depth, location=location)
    obj = bpy.context.object
    obj.name = name
    if rotation:
        obj.rotation_euler = rotation
    finish(obj, material, 0.012, True)
    obj.parent = parent
    return obj


def ico(name, location, size, material, parent, subdivisions=2):
    bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=subdivisions, radius=1, location=location)
    obj = bpy.context.object
    obj.name = name
    obj.scale = size
    for vertex in obj.data.vertices:
        vertex.co.x *= 1.0 + random.uniform(-0.08, 0.08)
        vertex.co.y *= 1.0 + random.uniform(-0.08, 0.08)
        vertex.co.z *= 1.0 + random.uniform(-0.06, 0.06)
    finish(obj, material, 0, True)
    obj.parent = parent
    return obj


def torus(name, location, major, minor, material, parent, rotation=None):
    bpy.ops.mesh.primitive_torus_add(major_segments=32, minor_segments=8, major_radius=major, minor_radius=minor, location=location)
    obj = bpy.context.object
    obj.name = name
    if rotation:
        obj.rotation_euler = rotation
    finish(obj, material, 0, True)
    obj.parent = parent
    return obj


def roof_pair(root, name, center_x, center_y, z, width, depth, rise, material):
    angle = math.atan2(rise, width * 0.5)
    box(name + "_L", (center_x - width * 0.25, center_y, z + rise * 0.5), (width * 0.56, depth, 0.16), material, root, 0.025, (0, -angle, 0))
    box(name + "_R", (center_x + width * 0.25, center_y, z + rise * 0.5), (width * 0.56, depth, 0.16), material, root, 0.025, (0, angle, 0))


def window(root, name, x, y, z, width=0.65, height=0.85, shutters=False):
    box(name + "_Frame", (x, y, z), (width + 0.12, height + 0.12, 0.09), wood, root, 0.018)
    box(name + "_Glass", (x, y - 0.055, z), (width, height, 0.055), glass, root, 0.008)
    box(name + "_Mullion", (x, y - 0.09, z), (0.045, height, 0.04), wood, root, 0.006)
    if shutters:
        box(name + "_ShutterL", (x - width * 0.72, y - 0.03, z), (width * 0.34, height, 0.07), terracotta, root, 0.012)
        box(name + "_ShutterR", (x + width * 0.72, y - 0.03, z), (width * 0.34, height, 0.07), terracotta, root, 0.012)


roots = []

root = empty("Prop_Mediterranean_Cypress_01", (0, 0, 0))
roots.append(root)
cylinder("Cypress_Trunk", (0, 0, 1.7), 0.19, 0.11, 3.4, wood, root, 14)
for index, (z, size) in enumerate([(1.6, 0.75), (2.35, 0.95), (3.15, 1.02), (3.95, 0.88), (4.7, 0.64)]):
    ico(f"Cypress_Crown_{index:02d}", (random.uniform(-0.1, 0.1), random.uniform(-0.08, 0.08), z), (size * 0.62, size * 0.52, size), leaf, root)
cylinder("Cypress_Tip", (0, 0, 5.55), 0.12, 0.0, 1.1, leaf, root, 12)
for index in range(7):
    ico(f"Cypress_Scree_{index:02d}", (random.uniform(-0.7, 0.7), random.uniform(-0.6, 0.6), 0.12), (0.18, 0.15, 0.12), stone, root, 1)

root = empty("Prop_Alpine_Cliff_01", (22, 0, 0))
roots.append(root)
for index in range(7):
    x, y, z = random.uniform(-3.2, 3.2), random.uniform(-1.8, 1.8), random.uniform(1.0, 2.2)
    ico(f"Cliff_Mass_{index:02d}", (x, y, z), (random.uniform(1.5, 2.8), random.uniform(1.2, 2.1), random.uniform(1.6, 3.0)), rock, root)
    if index % 2 == 0:
        ico(f"Cliff_SnowCap_{index:02d}", (x, y - 0.05, z + 1.35), (1.25, 1.0, 0.48), snow, root)
for index in range(16):
    ico(f"Cliff_Scree_{index:02d}", (random.uniform(-4, 4), random.uniform(-3, 3), 0.16), (random.uniform(0.15, 0.5), random.uniform(0.12, 0.4), random.uniform(0.12, 0.32)), stone, root, 1)

root = empty("Prop_Stone_Bridge_01", (44, 0, 0))
roots.append(root)
box("Bridge_Foundation", (0, 0, 0.25), (12, 4.6, 0.5), stone, root, 0.06)
box("Bridge_Deck", (0, 0, 1.45), (12, 4.2, 0.42), stone, root, 0.05)
for x in (-3.6, 3.6):
    box(f"Bridge_Pier_{x}", (x, 0, 0.85), (1.1, 4.0, 1.7), stone, root, 0.06)
for y in (-1.85, 1.85):
    torus(f"Bridge_Arch_{y}", (0, y, 1.0), 2.35, 0.42, stone, root, (math.pi / 2, 0, 0))
    box(f"Bridge_Parapet_{y}", (0, y, 2.0), (12, 0.38, 0.65), stone, root, 0.045)
    for x in (-5, -2.5, 0, 2.5, 5):
        box(f"Bridge_Cap_{y}_{x}", (x, y, 2.36), (0.7, 0.5, 0.16), stone, root, 0.035)

root = empty("Building_Alpine_Chalet_01", (66, 0, 0))
roots.append(root)
box("Chalet_StoneBase", (0, 0, 0.35), (8.2, 6.2, 0.7), stone, root, 0.06)
box("Chalet_LowerWalls", (0, 0, 2.0), (8, 6, 2.6), plaster, root, 0.04)
box("Chalet_UpperWalls", (0, 0, 4.15), (6.4, 4.8, 1.9), wood, root, 0.04)
roof_pair(root, "Chalet_Roof", 0, 0, 5.15, 7.6, 5.8, 2.0, slate)
for x in (-3.8, -1.3, 1.3, 3.8):
    box(f"Chalet_Timber_{x}", (x, -3.04, 2.0), (0.16, 0.14, 2.6), wood, root, 0.015)
for z in (0.85, 3.25):
    box(f"Chalet_Beam_{z}", (0, -3.06, z), (8.1, 0.16, 0.18), wood, root, 0.015)
for x in (-2.5, 2.5):
    window(root, f"Chalet_WindowUpper_{x}", x, -2.45, 4.2, 0.75, 0.85)
for x in (-2.6, 0, 2.6):
    window(root, f"Chalet_WindowLower_{x}", x, -3.08, 2.15, 0.7, 0.9)
box("Chalet_Door", (0, -3.1, 1.35), (1.0, 0.16, 1.9), wood, root, 0.035)
box("Chalet_Balcony", (0, -3.65, 3.45), (6.8, 1.2, 0.18), wood, root, 0.035)
for x in (-3.1, -1.6, 0, 1.6, 3.1):
    box(f"Chalet_Baluster_{x}", (x, -4.15, 3.95), (0.09, 0.09, 0.9), wood, root, 0.01)
box("Chalet_BalconyRail", (0, -4.15, 4.38), (6.5, 0.12, 0.12), wood, root, 0.015)
box("Chalet_Chimney", (2.1, 0.8, 6.0), (0.8, 0.8, 2.0), stone, root, 0.05)
roof_angle = math.atan2(2.0, 3.8)
box("Chalet_SnowRoofL", (-1.9, 0, 6.05), (3.8, 5.9, 0.12), snow, root, 0.02, (0, -roof_angle, 0))
box("Chalet_SnowRoofR", (1.9, 0, 6.05), (3.8, 5.9, 0.12), snow, root, 0.02, (0, roof_angle, 0))

root = empty("Building_Coastal_Cottage_01", (88, 0, 0))
roots.append(root)
box("Cottage_Base", (0, 0, 0.28), (7.4, 5.4, 0.56), stone, root, 0.05)
box("Cottage_Walls", (0, 0, 1.85), (7.2, 5.2, 2.6), plaster, root, 0.04)
roof_pair(root, "Cottage_Roof", 0, 0, 3.25, 8.0, 5.9, 1.55, terracotta)
for x in (-2.4, 0, 2.4):
    window(root, f"Cottage_Window_{x}", x, -2.64, 1.95, 0.7, 0.9, True)
box("Cottage_Door", (0, -2.67, 1.25), (0.95, 0.14, 1.9), wood, root, 0.03)
box("Cottage_Porch", (0, -3.35, 0.55), (4.2, 1.5, 0.18), stone, root, 0.04)
for x in (-1.8, 1.8):
    cylinder(f"Cottage_PorchPost_{x}", (x, -3.9, 1.75), 0.07, 0.07, 2.4, wood, root, 12)
roof_pair(root, "Cottage_PorchRoof", 0, -3.35, 2.95, 4.5, 1.8, 0.55, terracotta)
box("Cottage_Chimney", (2.0, 0.7, 4.0), (0.65, 0.65, 1.6), stone, root, 0.04)

root = empty("Prop_Vineyard_Rows_01", (110, 0, 0))
roots.append(root)
box("Vineyard_Terrace", (0, 0, 0.08), (12, 8, 0.16), dirt, root, 0.02)
for row in range(5):
    y = -3.0 + row * 1.5
    for x in (-5, -2.5, 0, 2.5, 5):
        cylinder(f"Vineyard_Post_{row}_{x}", (x, y, 0.8), 0.045, 0.055, 1.5, wood, root, 8)
    for z in (0.75, 1.05):
        box(f"Vineyard_Wire_{row}_{z}", (0, y, z), (10, 0.018, 0.018), metal, root, 0)
    for x in (-4, -1.5, 1, 3.5):
        cylinder(f"Vineyard_Trunk_{row}_{x}", (x, y, 0.65), 0.05, 0.035, 1.1, wood, root, 8)
        for branch in range(3):
            ico(f"Vineyard_Leaf_{row}_{x}_{branch}", (x + random.uniform(-0.35, 0.35), y + random.uniform(-0.25, 0.25), 1.05 + branch * 0.18), (0.45, 0.32, 0.25), leaf, root, 1)

root = empty("Prop_Race_Grandstand_01", (132, 0, 0))
roots.append(root)
for row in range(5):
    z = 0.45 + row * 0.55
    y = -2.4 + row * 0.9
    box(f"Grandstand_Step_{row}", (0, y, z * 0.5), (18, 1.0, z), wood, root, 0.035)
    box(f"Grandstand_Seat_{row}", (0, y - 0.15, z + 0.08), (17.5, 0.55, 0.12), wood, root, 0.025)
for x in (-8.4, -4.2, 0, 4.2, 8.4):
    for y in (-2.8, 3.6):
        cylinder(f"Grandstand_Post_{x}_{y}", (x, y, 2.8), 0.09, 0.09, 5.6, wood, root, 10)
box("Grandstand_Roof", (0, 0.4, 5.55), (19, 8.5, 0.22), canvas, root, 0.04)
for index in range(22):
    row = index % 5
    x = -7.8 + (index // 5) * 3.2 + random.uniform(-0.25, 0.25)
    y = -2.45 + row * 0.9
    z = 0.62 + row * 0.55
    clothing = (wood, plaster, slate, canvas)[index % 4]
    cylinder(f"Spectator_Body_{index:02d}", (x, y, z + 0.45), 0.16, 0.21, 0.55, clothing, root, 8)
    ico(f"Spectator_Head_{index:02d}", (x, y, z + 0.9), (0.16, 0.15, 0.18), plaster, root, 1)
    cylinder(f"Spectator_Hat_{index:02d}", (x, y, z + 1.04), 0.2, 0.16, 0.12, slate, root, 10)
for x in (-9, 9):
    cylinder(f"Grandstand_Flagpole_{x}", (x, 3.8, 3.4), 0.04, 0.04, 6.8, wood, root, 8)
box("Grandstand_Flag_L", (-9.8, 3.8, 6.1), (1.5, 0.04, 0.65), canvas, root, 0.01)
box("Grandstand_Flag_R", (9.8, 3.8, 6.1), (1.5, 0.04, 0.65), canvas, root, 0.01)

root = empty("Prop_Roadside_Shrine_01", (154, 0, 0))
roots.append(root)
box("Shrine_Base", (0, 0, 0.18), (2.6, 1.6, 0.36), stone, root, 0.05)
box("Shrine_Back", (0, 0.55, 1.65), (2.2, 0.18, 2.5), wood, root, 0.035)
for x in (-0.95, 0.95):
    cylinder(f"Shrine_Post_{x}", (x, -0.55, 1.6), 0.09, 0.09, 2.6, wood, root, 10)
roof_pair(root, "Shrine_Roof", 0, -0.05, 2.95, 2.8, 1.9, 0.75, slate)
box("Shrine_IconFrame", (0, 0.4, 1.75), (1.05, 0.12, 1.35), wood, root, 0.035)
box("Shrine_Icon", (0, 0.31, 1.75), (0.82, 0.05, 1.1), plaster, root, 0.02)
box("Shrine_CrossV", (0, 0.26, 1.9), (0.09, 0.04, 0.55), wood, root, 0.01)
box("Shrine_CrossH", (0, 0.26, 2.02), (0.36, 0.04, 0.09), wood, root, 0.01)
cylinder("Shrine_Lamp", (1.25, -0.5, 1.2), 0.11, 0.08, 0.3, metal, root, 10)

root = empty("Prop_Reed_Cluster_01", (176, 0, 0))
roots.append(root)
for index in range(42):
    x, y = random.uniform(-1.5, 1.5), random.uniform(-1.2, 1.2)
    height = random.uniform(1.0, 2.3)
    tilt = random.uniform(-0.12, 0.12)
    cylinder(f"Reed_Stem_{index:02d}", (x, y, height * 0.5), 0.014, 0.009, height, reed, root, 6, (tilt, 0, random.uniform(-0.08, 0.08)))
    if index % 3 == 0:
        cylinder(f"Reed_Cattail_{index:02d}", (x + tilt * height, y, height + 0.1), 0.045, 0.035, 0.28, wood, root, 8)
    ico(f"Reed_Leaf_{index:02d}", (x + 0.12, y, height * 0.55), (0.08, 0.035, 0.55), reed, root, 1)

root = empty("Prop_Cobbled_Road_10m_01", (198, 0, 0))
roots.append(root)
box("Cobble_RoadBase", (0, 0, 0.02), (10, 4, 0.12), dirt, root, 0.02)
for ix in range(20):
    for iy in range(8):
        x = -4.75 + ix * 0.5 + (iy % 2) * 0.06
        y = -1.75 + iy * 0.5
        material = cobble_a if (ix + iy) % 3 else cobble_b
        box(f"Cobble_{ix:02d}_{iy:02d}", (x + random.uniform(-0.025, 0.025), y + random.uniform(-0.025, 0.025), 0.13 + random.uniform(-0.015, 0.015)), (0.46, 0.46, 0.16), material, root, 0.035, (0, 0, random.uniform(-0.035, 0.035)))

bpy.ops.mesh.primitive_plane_add(size=2, location=(99, 0, -0.08))
ground = bpy.context.object
ground.name = "Inspection_Ground"
ground.scale = (120, 22, 1)
finish(ground, stone, 0, False)
ground.hide_select = True
bpy.ops.object.light_add(type="AREA", location=(95, -18, 35))
light = bpy.context.object
light.name = "Inspection_Key"
light.data.energy = 4500
light.data.shape = "DISK"
light.data.size = 25
move_to_collection(light)
bpy.ops.object.camera_add(location=(99, -62, 48))
camera = bpy.context.object
camera.name = "Inspection_Camera"
camera.rotation_euler = (math.radians(55), 0, 0)
camera.data.lens = 42
move_to_collection(camera)
scene.camera = camera
scene.render.engine = "BLENDER_EEVEE"


def export(root, path):
    bpy.ops.object.select_all(action="DESELECT")
    root.select_set(True)
    for obj in root.children_recursive:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = root
    bpy.ops.export_scene.fbx(
        filepath=str(path), use_selection=True, apply_unit_scale=True, global_scale=1.0,
        axis_forward="-Z", axis_up="Y", object_types={"EMPTY", "MESH"},
        mesh_smooth_type="FACE", use_mesh_modifiers=True, path_mode="COPY",
        embed_textures=True, bake_anim=False, check_existing=False,
    )


for root in roots:
    bpy.ops.object.select_all(action="DESELECT")
    original = root.location.copy()
    root.location = (0, 0, 0)
    bpy.context.view_layer.update()
    detailed = SOURCE / f"{root.name}_Detailed.fbx"
    lod1 = SOURCE / f"{root.name}_LOD1.fbx"
    export(root, detailed)
    for obj in root.children_recursive:
        if obj.type == "MESH":
            modifier = obj.modifiers.new("LOD1_Decimate", "DECIMATE")
            modifier.ratio = 0.42
            modifier.use_collapse_triangulate = True
    export(root, lod1)
    for obj in root.children_recursive:
        for modifier in list(obj.modifiers):
            if modifier.name == "LOD1_Decimate":
                obj.modifiers.remove(modifier)
    root.location = original
    shutil.copy2(detailed, UNITY / f"{root.name}.fbx")
    shutil.copy2(lod1, UNITY / f"{root.name}_LOD1.fbx")
    triangles = sum(len(obj.data.polygons) for obj in root.children_recursive if obj.type == "MESH")
    print("EXPORTED", root.name, "objects", len(root.children_recursive), "polygons", triangles)

bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE / "Regional_Variety.blend"), check_existing=False)
print("REGIONAL_VARIETY_DONE", len(roots))
