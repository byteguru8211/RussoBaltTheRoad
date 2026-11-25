import bpy
import math
import os
from mathutils import Vector

SCENE_NAME = "PreRace_Paddock"
BLEND_PATH = "D:/programming/RussoBaltTheRoad/game_assets/pre_race/PreRace_Paddock.blend"
FBX_PATH = "D:/programming/RussoBaltTheRoad/unity-project/Assets/Resources/PreRace/PreRace_Paddock.fbx"
STAGE_COLLECTION = None
MATERIALS = {}


def activate(obj):
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj


def move_to_stage(obj, parent=None):
    for collection in list(obj.users_collection):
        collection.objects.unlink(obj)
    STAGE_COLLECTION.objects.link(obj)
    if parent is not None:
        obj.parent = parent
    return obj


def material(name, color, metallic=0.0, roughness=0.5, emission=None, emission_strength=0.0, bump=0.12, alpha=1.0):
    existing = bpy.data.materials.get(name)
    if existing is not None:
        return existing
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    mat.diffuse_color = (*color, alpha)
    bsdf = mat.node_tree.nodes.get("Principled BSDF")
    bsdf.inputs["Base Color"].default_value = (*color, alpha)
    bsdf.inputs["Metallic"].default_value = metallic
    bsdf.inputs["Roughness"].default_value = roughness
    bsdf.inputs["Alpha"].default_value = alpha
    if emission is not None:
        bsdf.inputs["Emission Color"].default_value = (*emission, 1.0)
        bsdf.inputs["Emission Strength"].default_value = emission_strength
    if bump > 0.0:
        noise = mat.node_tree.nodes.new("ShaderNodeTexNoise")
        noise.inputs["Scale"].default_value = 7.0
        noise.inputs["Detail"].default_value = 3.0
        noise.inputs["Roughness"].default_value = 0.72
        bump_node = mat.node_tree.nodes.new("ShaderNodeBump")
        bump_node.inputs["Strength"].default_value = bump
        bump_node.inputs["Distance"].default_value = 0.06
        mat.node_tree.links.new(noise.outputs["Fac"], bump_node.inputs["Height"])
        mat.node_tree.links.new(bump_node.outputs["Normal"], bsdf.inputs["Normal"])
    if alpha < 1.0 and hasattr(mat, "surface_render_method"):
        mat.surface_render_method = "DITHERED"
    MATERIALS[name] = mat
    return mat


def finish(obj, mat, parent=None, smooth=True, bevel=0.0, uv=True):
    move_to_stage(obj, parent)
    if mat is not None:
        obj.data.materials.append(mat)
    if bevel > 0.0:
        activate(obj)
        modifier = obj.modifiers.new("Soft manufactured edges", "BEVEL")
        modifier.width = bevel
        modifier.segments = 2
        modifier.limit_method = "ANGLE"
        bpy.ops.object.modifier_apply(modifier=modifier.name)
    if smooth and obj.type == "MESH":
        for polygon in obj.data.polygons:
            polygon.use_smooth = True
        activate(obj)
        normal = obj.modifiers.new("Weighted corner normals", "WEIGHTED_NORMAL")
        normal.keep_sharp = True
        normal.weight = 40
        bpy.ops.object.modifier_apply(modifier=normal.name)
    if uv and obj.type == "MESH":
        activate(obj)
        bpy.ops.object.mode_set(mode="EDIT")
        bpy.ops.mesh.select_all(action="SELECT")
        area = next(area for area in bpy.context.screen.areas if area.type == "VIEW_3D")
        region = next(region for region in area.regions if region.type == "WINDOW")
        with bpy.context.temp_override(area=area, region=region):
            bpy.ops.uv.smart_project(angle_limit=math.radians(66), island_margin=0.03)
        bpy.ops.object.mode_set(mode="OBJECT")
    return obj


def module(name):
    root = bpy.data.objects.get(name)
    if root is None:
        root = bpy.data.objects.new(name, None)
        STAGE_COLLECTION.objects.link(root)
    return root


def box(name, location, dimensions, mat, parent=None, bevel=0.035, rotation=None):
    bpy.ops.mesh.primitive_cube_add(size=1, location=location)
    obj = bpy.context.object
    obj.name = name
    obj.dimensions = dimensions
    if rotation is not None:
        obj.rotation_euler = rotation
    activate(obj)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    return finish(obj, mat, parent, bevel=min(bevel, min(dimensions) * 0.22))


def cylinder(name, location, radius, depth, mat, parent=None, vertices=16, rotation=None):
    bpy.ops.mesh.primitive_cylinder_add(vertices=vertices, radius=radius, depth=depth, location=location)
    obj = bpy.context.object
    obj.name = name
    if rotation is not None:
        obj.rotation_euler = rotation
    activate(obj)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    return finish(obj, mat, parent, bevel=min(0.025, radius * 0.15))


def torus(name, location, major_radius, minor_radius, mat, parent=None):
    bpy.ops.mesh.primitive_torus_add(major_segments=20, minor_segments=8, location=location, major_radius=major_radius, minor_radius=minor_radius)
    obj = bpy.context.object
    obj.name = name
    return finish(obj, mat, parent, uv=True)


def text(name, body, location, size, mat, parent=None, rotation=(math.pi / 2, 0, 0), extrude=0.018):
    bpy.ops.object.text_add(location=location, rotation=rotation)
    obj = bpy.context.object
    obj.name = name
    obj.data.body = body
    obj.data.align_x = "CENTER"
    obj.data.align_y = "CENTER"
    obj.data.size = size
    obj.data.extrude = extrude
    obj.data.bevel_depth = 0.006
    obj.data.bevel_resolution = 2
    activate(obj)
    bpy.ops.object.convert(target="MESH")
    return finish(obj, mat, parent, uv=True)


def create_materials():
    material("RB_Asphalt", (0.055, 0.068, 0.075), metallic=0.08, roughness=0.3, bump=0.28)
    material("RB_Concrete", (0.16, 0.19, 0.2), roughness=0.68, bump=0.24)
    material("RB_Steel", (0.09, 0.13, 0.15), metallic=0.78, roughness=0.28, bump=0.06)
    material("RB_BlackSteel", (0.025, 0.04, 0.045), metallic=0.72, roughness=0.24, bump=0.05)
    material("RB_TealPaint", (0.035, 0.19, 0.19), metallic=0.42, roughness=0.32, bump=0.08)
    material("RB_CreamPaint", (0.74, 0.68, 0.53), metallic=0.16, roughness=0.4, bump=0.06)
    material("RB_Wood", (0.19, 0.095, 0.038), roughness=0.58, bump=0.22)
    material("RB_Rubber", (0.012, 0.016, 0.018), roughness=0.82, bump=0.12)
    material("RB_Glass", (0.055, 0.16, 0.2), metallic=0.48, roughness=0.12, bump=0.02)
    material("RB_RedPaint", (0.34, 0.035, 0.028), metallic=0.34, roughness=0.34, bump=0.08)
    material("RB_WhiteLight", (0.8, 0.9, 1.0), roughness=0.25, emission=(0.72, 0.9, 1.0), emission_strength=4.0, bump=0.0)
    material("RB_AmberLight", (1.0, 0.52, 0.16), roughness=0.25, emission=(1.0, 0.38, 0.08), emission_strength=5.0, bump=0.0)
    material("RB_CyanLight", (0.05, 0.55, 0.72), roughness=0.25, emission=(0.03, 0.65, 1.0), emission_strength=4.5, bump=0.0)
    material("RB_SignWhite", (0.82, 0.82, 0.7), roughness=0.4, emission=(0.82, 0.76, 0.56), emission_strength=0.45, bump=0.0)


def build_stage():
    if bpy.data.objects.get("Stage_Base") is not None:
        return
    root = module("Stage_Base")
    asphalt = MATERIALS["RB_Asphalt"]
    concrete = MATERIALS["RB_Concrete"]
    steel = MATERIALS["RB_Steel"]
    black = MATERIALS["RB_BlackSteel"]
    cream = MATERIALS["RB_CreamPaint"]
    white = MATERIALS["RB_SignWhite"]
    cyan = MATERIALS["RB_CyanLight"]
    amber = MATERIALS["RB_AmberLight"]

    box("WorkshopFloor", (0, 0, -0.09), (38, 38, 0.18), concrete, root, bevel=0.02)
    for x in range(-16, 17, 4):
        box(f"FloorJoint_X_{x}", (x, 0, 0.006), (0.018, 36, 0.008), black, root, bevel=0.0)
    for y in range(-16, 17, 4):
        box(f"FloorJoint_Y_{y}", (0, y, 0.007), (36, 0.018, 0.008), black, root, bevel=0.0)

    box("DisplayApron", (0, 0, 0.11), (14, 20, 0.22), concrete, root, bevel=0.06)
    box("DisplayRoad", (0, -1, 0.237), (7, 19, 0.035), asphalt, root, bevel=0.008)
    for side in (-1, 1):
        box(f"ApronNeon_{side}", (side * 6.88, 0, 0.16), (0.045, 19.4, 0.035), cyan, root, bevel=0.008)
        box(f"RoadEdge_{side}", (side * 3.22, -1, 0.263), (0.075, 18.4, 0.012), cream, root, bevel=0.0)
        box(f"SafetyRail_{side}", (side * 5.15, -0.5, 0.74), (0.12, 0.12, 17.5), steel, root, bevel=0.025)
        box(f"SafetyRailLower_{side}", (side * 5.15, -0.5, 0.38), (0.09, 0.09, 17.5), black, root, bevel=0.02)
        for index, y in enumerate(range(-9, 9, 3)):
            box(f"BarrierPost_{side}_{index}", (side * 5.15, y, 0.55), (0.14, 0.14, 1.1), steel, root, bevel=0.025)
    for index, y in enumerate(range(-8, 9, 2)):
        box(f"CenterDash_{index}", (0, y, 0.264), (0.09, 0.9, 0.012), cream, root, bevel=0.0)
    for index in range(16):
        box(f"StartLine_{index:02}", (-3.0 + index * 0.4, -7.45, 0.267), (0.4, 0.62, 0.014), cream if index % 2 == 0 else black, root, bevel=0.0)

    gantry = module("Stage_StartGantry")
    for side in (-1, 1):
        box(f"GantryFoot_{side}", (side * 4.25, -9, 0.18), (0.72, 0.72, 0.36), black, gantry, bevel=0.06)
        box(f"GantryPillar_{side}", (side * 4.25, -9, 2.55), (0.32, 0.32, 5.0), steel, gantry, bevel=0.045)
        box(f"GantryPillarInset_{side}", (side * 4.25, -9.18, 2.6), (0.12, 0.035, 4.4), cyan, gantry, bevel=0.008)
    box("GantryBeam", (0, -9, 4.95), (9.25, 0.48, 0.95), black, gantry, bevel=0.06)
    box("GantrySign", (0, -9.255, 4.98), (6.3, 0.055, 0.69), MATERIALS["RB_TealPaint"], gantry, bevel=0.025)
    text("GantryTitle", "RUSSO-BALT  /  START", (0, -9.295, 4.99), 0.34, white, gantry)
    for side in (-1, 1):
        for row in range(2):
            for column in range(3):
                box(f"GantryCheck_{side}_{row}_{column}", (side * (3.35 + column * 0.29), -9.26, 4.77 + row * 0.31), (0.29, 0.04, 0.31), cream if (row + column) % 2 == 0 else black, gantry, bevel=0.0)
    for x in (-3.6, 3.6):
        box(f"StartLampHousing_{x}", (x, -9.05, 4.18), (0.34, 0.3, 0.18), black, gantry, bevel=0.035)
        box(f"StartLamp_{x}", (x, -9.05, 4.08), (0.25, 0.24, 0.045), amber, gantry, bevel=0.012)

    for side in (-1, 1):
        for index, y in enumerate((-5.5, 5.5)):
            x = side * 7.3
            cylinder(f"LampPost_{side}_{index}", (x, y, 2.65), 0.085, 5.3, steel, root)
            cylinder(f"LampBase_{side}_{index}", (x, y, 0.18), 0.22, 0.36, black, root)
            box(f"LampArm_{side}_{index}", (x - side * 0.45, y, 5.2), (0.95, 0.09, 0.09), steel, root, bevel=0.02)
            box(f"LampHead_{side}_{index}", (x - side * 0.88, y, 5.12), (0.52, 0.28, 0.16), black, root, bevel=0.04)
            box(f"LampGlow_{side}_{index}", (x - side * 0.88, y, 5.025), (0.4, 0.2, 0.035), amber, root, bevel=0.01)

    box("BackWall", (0, 11.4, 3.0), (27, 0.45, 6.0), concrete, root, bevel=0.04)
    for side in (-1, 1):
        box(f"SideWall_{side}", (side * 13.2, 0, 3.0), (0.4, 22, 6.0), black, root, bevel=0.04)
    for y in (-3, 3):
        box(f"OverheadTruss_{y}", (0, y, 7.0), (25, 0.18, 0.22), steel, root, bevel=0.025)
        for x in (-10, -5, 0, 5, 10):
            box(f"TrussBrace_{y}_{x}", (x, y, 6.45), (0.1, 0.12, 1.1), steel, root, bevel=0.018, rotation=(0, 0.55 if x % 10 else -0.55))


def build_workshop():
    if bpy.data.objects.get("Stage_Workshop") is not None:
        return
    root = module("Stage_Workshop")
    steel = MATERIALS["RB_Steel"]
    black = MATERIALS["RB_BlackSteel"]
    teal = MATERIALS["RB_TealPaint"]
    wood = MATERIALS["RB_Wood"]
    cream = MATERIALS["RB_CreamPaint"]
    glass = MATERIALS["RB_Glass"]
    white = MATERIALS["RB_SignWhite"]
    cyan = MATERIALS["RB_CyanLight"]
    amber = MATERIALS["RB_AmberLight"]

    box("WorkshopBody", (0, 8.2, 2.55), (17, 5.2, 5.1), teal, root, bevel=0.06)
    box("WorkshopRoof", (0, 8.25, 5.35), (18, 5.8, 0.28), black, root, bevel=0.05, rotation=(0.035, 0, 0))
    for x in range(-8, 9, 2):
        box(f"RoofRib_{x}", (x, 8.25, 5.53), (0.07, 5.7, 0.06), steel, root, bevel=0.012, rotation=(0.035, 0, 0))
    for x in (-8.25, -5.65, 5.65, 8.25):
        box(f"WorkshopCorner_{x}", (x, 5.54, 2.55), (0.22, 0.22, 5.1), steel, root, bevel=0.025)
    box("WorkshopHeader", (0, 5.5, 4.55), (11.6, 0.32, 0.75), black, root, bevel=0.04)
    text("WorkshopSign", "RUSSO-BALT", (0, 5.31, 4.58), 0.54, white, root)
    text("WorkshopSubSign", "RIGA  /  MOTOR WORKS  /  1912", (0, 5.3, 4.18), 0.15, cream, root)
    box("NeonSignBack", (5.3, 5.28, 3.32), (3.4, 0.12, 0.74), black, root, bevel=0.045)
    text("NeonSign", "THE ROAD", (5.3, 5.19, 3.34), 0.31, cyan, root)

    box("WorkshopDoorFrame", (0, 5.48, 2.05), (5.6, 0.28, 4.1), steel, root, bevel=0.04)
    box("WorkshopOpening", (0, 5.3, 2.0), (5.05, 0.12, 3.65), black, root, bevel=0.015)
    for index in range(8):
        box(f"DoorSlat_{index}", (0, 5.2, 0.42 + index * 0.43), (4.9, 0.08, 0.38), MATERIALS["RB_Wood"], root, bevel=0.018)
    box("DoorHeader", (0, 5.15, 3.73), (5.05, 0.12, 0.22), steel, root, bevel=0.025)
    for x in (-2.35, 2.35):
        box(f"DoorTrack_{x}", (x, 5.12, 2.0), (0.08, 0.08, 3.65), steel, root, bevel=0.015)
    box("WorkshopStep", (0, 4.75, 0.13), (5.8, 1.0, 0.26), MATERIALS["RB_Concrete"], root, bevel=0.04)

    for side in (-1, 1):
        x = side * 6.65
        box(f"WindowFrame_{side}", (x, 5.48, 2.65), (2.25, 0.2, 1.8), steel, root, bevel=0.035)
        box(f"WindowGlass_{side}", (x, 5.34, 2.65), (1.95, 0.06, 1.5), glass, root, bevel=0.015)
        box(f"WindowMullionV_{side}", (x, 5.29, 2.65), (0.055, 0.06, 1.5), steel, root, bevel=0.008)
        box(f"WindowMullionH_{side}", (x, 5.29, 2.65), (1.95, 0.06, 0.055), steel, root, bevel=0.008)
        box(f"WindowLamp_{side}", (x, 5.22, 3.72), (1.5, 0.08, 0.07), amber, root, bevel=0.015)

    for index, x in enumerate((-4.15, 4.15)):
        box(f"BannerPlate_{index}", (x, 5.26, 1.45), (0.85, 0.09, 1.65), MATERIALS["RB_RedPaint"], root, bevel=0.035)
        text(f"BannerText_{index}", "RB", (x, 5.19, 1.45), 0.36, cream, root)
    for x in (-7.6, 7.6):
        cylinder(f"Downpipe_{x}", (x, 10.65, 2.7), 0.075, 5.4, steel, root)


def build_props():
    if bpy.data.objects.get("Stage_Props") is not None:
        return
    root = module("Stage_Props")
    steel = MATERIALS["RB_Steel"]
    black = MATERIALS["RB_BlackSteel"]
    teal = MATERIALS["RB_TealPaint"]
    wood = MATERIALS["RB_Wood"]
    cream = MATERIALS["RB_CreamPaint"]
    rubber = MATERIALS["RB_Rubber"]
    red = MATERIALS["RB_RedPaint"]
    amber = MATERIALS["RB_AmberLight"]

    for side in (-1, 1):
        for stack in range(2):
            for level in range(3):
                torus(f"Tire_{side}_{stack}_{level}", (side * (5.9 + stack * 0.95), 4.3, 0.18 + level * 0.31), 0.39, 0.14, rubber, root)
        box(f"ToolChest_{side}", (side * 6.4, 3.3, 0.52), (1.15, 0.62, 1.04), red, root, bevel=0.06)
        for drawer in range(4):
            box(f"ToolDrawer_{side}_{drawer}", (side * 6.4, 2.965, 0.18 + drawer * 0.23), (1.02, 0.035, 0.18), black, root, bevel=0.012)
            box(f"ToolHandle_{side}_{drawer}", (side * 6.4, 2.93, 0.18 + drawer * 0.23), (0.38, 0.035, 0.035), steel, root, bevel=0.008)
        cylinder(f"FuelCan_{side}", (side * 6.2, 1.9, 0.43), 0.26, 0.86, teal, root, vertices=12)
        box(f"FuelCanHandle_{side}", (side * 6.2, 1.9, 0.92), (0.28, 0.12, 0.12), black, root, bevel=0.025, rotation=(0, 0, math.pi / 4))
        cylinder(f"FuelCanCap_{side}", (side * 6.36, 1.9, 0.88), 0.07, 0.08, steel, root, vertices=12)

    box("WorkbenchTop", (-8.8, 6.2, 1.05), (3.0, 0.85, 0.14), wood, root, bevel=0.04)
    for x in (-10.0, -7.6):
        for y in (5.9, 6.5):
            box(f"WorkbenchLeg_{x}_{y}", (x, y, 0.52), (0.12, 0.12, 1.04), steel, root, bevel=0.02)
    box("ViceBase", (-8.8, 6.1, 1.18), (0.48, 0.42, 0.18), steel, root, bevel=0.025)
    box("ViceJaw", (-8.8, 5.95, 1.38), (0.5, 0.14, 0.25), black, root, bevel=0.025)
    cylinder("ViceScrew", (-8.8, 5.72, 1.28), 0.055, 0.5, steel, root, rotation=(math.pi / 2, 0, 0))
    box("BenchLampStem", (-9.7, 6.35, 1.65), (0.055, 0.055, 1.2), steel, root, bevel=0.012, rotation=(0, -0.35, 0))
    box("BenchLampShade", (-9.48, 6.35, 2.28), (0.5, 0.32, 0.22), teal, root, bevel=0.05, rotation=(0, -0.35, 0))
    box("BenchLampGlow", (-9.48, 6.16, 2.22), (0.36, 0.22, 0.035), amber, root, bevel=0.01)

    for index, x in enumerate((-3.8, 3.8)):
        box(f"WoodCrate_{index}", (x, 7.5, 0.48), (0.85, 0.85, 0.96), wood, root, bevel=0.045)
        for y in (7.06, 7.94):
            box(f"CrateBandY_{index}_{y}", (x, y, 0.48), (0.9, 0.055, 1.0), black, root, bevel=0.01)
        for z in (0.08, 0.88):
            box(f"CrateBandZ_{index}_{z}", (x, 7.5, z), (0.9, 0.9, 0.055), black, root, bevel=0.01)

    for x in (-11.5, 11.5):
        for index, z in enumerate((-7, -1, 5)):
            box(f"BackdropBlock_{x}_{index}", (x, 10.8 + index * 0.4, 3.2 + index * 0.6), (2.1, 2.0, 6.4 + index * 1.2), black, root, bevel=0.08)
            for row in range(3):
                box(f"BackdropWindow_{x}_{index}_{row}", (x, 9.76 + index * 0.4, 2.0 + row * 1.2), (0.045, 1.25, 0.28), amber if (row + index) % 2 == 0 else teal, root, bevel=0.01)


def setup_preview():
    scene = bpy.context.scene
    scene.render.engine = "CYCLES"
    scene.cycles.samples = 32
    scene.cycles.use_denoising = True
    scene.render.resolution_x = 1400
    scene.render.resolution_y = 900
    scene.render.resolution_percentage = 100
    scene.world.color = (0.025, 0.035, 0.045)
    scene.view_settings.view_transform = "AgX"
    scene.view_settings.look = "AgX - Medium High Contrast"

    camera_data = bpy.data.cameras.new("PreRacePreviewCamera")
    camera = bpy.data.objects.new("PreRacePreviewCamera", camera_data)
    scene.collection.objects.link(camera)
    camera.location = (10.5, -12.5, 6.2)
    direction = Vector((0, 0.3, 1.1)) - camera.location
    camera.rotation_euler = direction.to_track_quat("-Z", "Y").to_euler()
    camera_data.lens = 43
    scene.camera = camera

    for name, location, energy, color, size in (
        ("PreviewKey", (4, -3, 8), 1800, (1.0, 0.72, 0.45), 6),
        ("PreviewRim", (-6, 3, 6), 1500, (0.25, 0.65, 1.0), 5),
        ("PreviewGate", (0, -8, 6), 900, (1.0, 0.85, 0.6), 4),
    ):
        data = bpy.data.lights.new(name, "AREA")
        data.energy = energy
        data.color = color
        data.shape = "DISK"
        data.size = size
        obj = bpy.data.objects.new(name, data)
        scene.collection.objects.link(obj)
        obj.location = location
        obj.rotation_euler = (Vector((0, 0, 0.8)) - obj.location).to_track_quat("-Z", "Y").to_euler()


def export_stage():
    bpy.context.window.scene = bpy.data.scenes[SCENE_NAME]
    bpy.ops.object.select_all(action="DESELECT")
    meshes = [obj for obj in STAGE_COLLECTION.objects if obj.type == "MESH"]
    for obj in meshes:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = meshes[0]
    bpy.ops.export_scene.fbx(
        filepath=FBX_PATH,
        check_existing=False,
        use_selection=True,
        object_types={"MESH"},
        global_scale=1.0,
        apply_unit_scale=True,
        apply_scale_options="FBX_SCALE_UNITS",
        axis_forward="-Z",
        axis_up="Y",
        mesh_smooth_type="FACE",
        use_mesh_modifiers=True,
        add_leaf_bones=False,
        bake_anim=False,
        path_mode="COPY",
        embed_textures=True,
    )
    bpy.ops.wm.save_as_mainfile(filepath=BLEND_PATH, check_existing=False)
    return {"objects": len(meshes), "vertices": sum(len(obj.data.vertices) for obj in meshes), "fbx": FBX_PATH, "blend": BLEND_PATH, "bytes": os.path.getsize(FBX_PATH)}


def initialize():
    global STAGE_COLLECTION
    scene = bpy.data.scenes.get(SCENE_NAME)
    if scene is None:
        scene = bpy.data.scenes.new(SCENE_NAME)
    bpy.context.window.scene = scene
    STAGE_COLLECTION = bpy.data.collections.get("PreRace_Stage")
    if STAGE_COLLECTION is None:
        STAGE_COLLECTION = bpy.data.collections.new("PreRace_Stage")
        scene.collection.children.link(STAGE_COLLECTION)
    create_materials()


if __name__ == "__main__":
    initialize()
    build_stage()
    build_workshop()
    build_props()
    setup_preview()
    print(export_stage())
