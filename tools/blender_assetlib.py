from pathlib import Path
import math
import bpy
from mathutils import Vector
from bpy_extras.object_utils import world_to_camera_view

ROOT = Path(__file__).resolve().parents[1]
TEXTURES = ROOT / "game_assets" / "textures" / "shared"
MATERIALS = {}
IMAGES = {}


def reset_scene():
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    for datablocks in (bpy.data.meshes, bpy.data.curves, bpy.data.materials, bpy.data.cameras, bpy.data.lights):
        for block in list(datablocks):
            if block.users == 0:
                datablocks.remove(block)
    MATERIALS.clear()
    IMAGES.clear()
    scene = bpy.context.scene
    scene.unit_settings.system = "METRIC"
    scene.unit_settings.scale_length = 1.0
    scene.render.engine = "BLENDER_EEVEE"
    scene.view_settings.view_transform = "AgX"
    try:
        scene.view_settings.look = "AgX - Medium High Contrast"
    except Exception:
        pass
    scene.render.image_settings.file_format = "PNG"
    scene.render.film_transparent = False
    return scene


def collection(name, parent=None):
    coll = bpy.data.collections.get(name)
    if coll is None:
        coll = bpy.data.collections.new(name)
        (parent or bpy.context.scene.collection).children.link(coll)
    return coll


def link_object(obj, coll=None):
    if coll is None:
        bpy.context.scene.collection.objects.link(obj)
    else:
        coll.objects.link(obj)
    return obj


def empty(name, location=(0, 0, 0), parent=None, coll=None, display="PLAIN_AXES"):
    obj = bpy.data.objects.new(name, None)
    link_object(obj, coll)
    obj.empty_display_type = display
    obj.empty_display_size = 0.12
    obj.location = location
    if parent:
        obj.parent = parent
    return obj


def _load_image(path):
    path = str(path)
    if path not in IMAGES:
        IMAGES[path] = bpy.data.images.load(path, check_existing=True)
    return IMAGES[path]


def material(name, color, metallic=0.0, roughness=0.5, texture=None, emission=None, transmission=0.0, coat=0.0):
    if name in MATERIALS:
        return MATERIALS[name]
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    nodes = mat.node_tree.nodes
    links = mat.node_tree.links
    bsdf = nodes.get("Principled BSDF")
    bsdf.inputs["Base Color"].default_value = (*color, 1.0)
    bsdf.inputs["Metallic"].default_value = metallic
    bsdf.inputs["Roughness"].default_value = roughness
    if "Coat Weight" in bsdf.inputs:
        bsdf.inputs["Coat Weight"].default_value = coat
        bsdf.inputs["Coat Roughness"].default_value = max(0.08, roughness * 0.45)
    if transmission and "Transmission Weight" in bsdf.inputs:
        bsdf.inputs["Transmission Weight"].default_value = transmission
        bsdf.inputs["IOR"].default_value = 1.45
    if emission:
        bsdf.inputs["Emission Color"].default_value = (*emission[0], 1.0)
        bsdf.inputs["Emission Strength"].default_value = emission[1]
    if texture:
        base_path = TEXTURES / f"{texture}_basecolor.png"
        rough_path = TEXTURES / f"{texture}_roughness.png"
        normal_path = TEXTURES / f"{texture}_normal.png"
        uv = nodes.new("ShaderNodeTexCoord")
        uv.location = (-900, 0)
        if base_path.exists():
            tex = nodes.new("ShaderNodeTexImage")
            tex.image = _load_image(base_path)
            tex.location = (-650, 220)
            links.new(uv.outputs["UV"], tex.inputs["Vector"])
            links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
        if rough_path.exists():
            tex = nodes.new("ShaderNodeTexImage")
            tex.image = _load_image(rough_path)
            tex.image.colorspace_settings.name = "Non-Color"
            tex.location = (-650, -20)
            links.new(uv.outputs["UV"], tex.inputs["Vector"])
            links.new(tex.outputs["Color"], bsdf.inputs["Roughness"])
        if normal_path.exists():
            tex = nodes.new("ShaderNodeTexImage")
            tex.image = _load_image(normal_path)
            tex.image.colorspace_settings.name = "Non-Color"
            tex.location = (-650, -250)
            normal = nodes.new("ShaderNodeNormalMap")
            normal.inputs["Strength"].default_value = 0.65
            normal.location = (-340, -250)
            links.new(uv.outputs["UV"], tex.inputs["Vector"])
            links.new(tex.outputs["Color"], normal.inputs["Color"])
            links.new(normal.outputs["Normal"], bsdf.inputs["Normal"])
    mat.diffuse_color = (*color, 1.0)
    MATERIALS[name] = mat
    return mat


def palette():
    return {
        "green": material("RB_Paint_RacingGreen", (0.035, 0.12, 0.085), metallic=0.35, roughness=0.3, texture="paint_racing_green", coat=0.65),
        "green_dark": material("RB_Paint_DeepGreen", (0.018, 0.052, 0.039), metallic=0.3, roughness=0.38, coat=0.35),
        "brass": material("RB_Brass_Aged", (0.56, 0.34, 0.105), metallic=0.88, roughness=0.31),
        "brass_bright": material("RB_Brass_Polished", (0.72, 0.49, 0.18), metallic=0.92, roughness=0.2),
        "steel": material("RB_Steel_Blackened", (0.075, 0.085, 0.082), metallic=0.82, roughness=0.43),
        "iron": material("RB_Iron_Rough", (0.12, 0.115, 0.10), metallic=0.72, roughness=0.65),
        "rubber": material("RB_Tire_Rubber", (0.028, 0.024, 0.019), roughness=0.9),
        "leather": material("RB_Leather_Saddle", (0.22, 0.095, 0.045), roughness=0.62, texture="leather_saddle"),
        "wood": material("RB_Wood_Walnut", (0.24, 0.105, 0.04), roughness=0.55, texture="wood_walnut"),
        "fabric": material("RB_Fabric_Charcoal", (0.055, 0.05, 0.041), roughness=0.92, texture="fabric_charcoal"),
        "cream": material("RB_Ivory_Number", (0.83, 0.79, 0.65), roughness=0.62),
        "glass": material("RB_Acetylene_Glass", (0.63, 0.76, 0.78), metallic=0.05, roughness=0.12, transmission=0.72),
        "lamp": material("RB_Acetylene_Flame", (1.0, 0.61, 0.22), roughness=0.25, emission=((1.0, 0.48, 0.12), 3.0)),
        "red": material("RB_Mudguard_Red", (0.25, 0.035, 0.025), metallic=0.35, roughness=0.45),
        "dirt": material("ENV_Road_Dirt", (0.32, 0.23, 0.13), roughness=0.95, texture="road_dirt"),
        "snow": material("ENV_Road_Snow", (0.8, 0.85, 0.86), roughness=0.88, texture="road_snow"),
        "cobble": material("ENV_Road_Cobblestone", (0.25, 0.24, 0.21), roughness=0.9, texture="road_cobblestone"),
        "plaster": material("ENV_Plaster_Ochre", (0.56, 0.49, 0.36), roughness=0.9, texture="plaster_ochre"),
        "roof": material("ENV_Roof_Iron", (0.15, 0.14, 0.12), metallic=0.55, roughness=0.7, texture="roof_iron"),
        "bark": material("ENV_Bark_Birch", (0.55, 0.53, 0.43), roughness=0.92, texture="bark_birch"),
        "white_paint": material("ENV_Paint_White", (0.76, 0.74, 0.65), roughness=0.72),
        "blue_paint": material("ENV_Paint_Blue", (0.08, 0.18, 0.31), roughness=0.58),
        "skin": material("CHR_Skin", (0.56, 0.35, 0.23), roughness=0.72),
        "hair": material("CHR_Hair", (0.045, 0.03, 0.02), roughness=0.86),
        "eye": material("CHR_Eye", (0.035, 0.045, 0.04), roughness=0.25),
    }


def _smooth(obj):
    if obj.type == "MESH":
        for poly in obj.data.polygons:
            poly.use_smooth = True
    return obj


def _bevel(obj, amount=0.015, segments=2):
    if amount <= 0:
        return obj
    mod = obj.modifiers.new("SoftEdges", "BEVEL")
    mod.width = amount
    mod.segments = segments
    mod.limit_method = "ANGLE"
    try:
        mod.harden_normals = True
    except Exception:
        pass
    return obj


def mesh(name, verts, faces, mat=None, parent=None, coll=None, smooth=True, uvs=None):
    data = bpy.data.meshes.new(name)
    data.from_pydata(verts, [], faces)
    data.update()
    obj = bpy.data.objects.new(name, data)
    link_object(obj, coll)
    if mat:
        data.materials.append(mat)
    if parent:
        obj.parent = parent
    if smooth:
        _smooth(obj)
    if uvs:
        uv_layer = data.uv_layers.new(name="UVMap")
        for poly in data.polygons:
            for loop_index in poly.loop_indices:
                uv_layer.data[loop_index].uv = uvs[data.loops[loop_index].vertex_index]
    return obj


def box(name, location, dimensions, mat, parent=None, coll=None, bevel=0.015, rotation=(0, 0, 0)):
    bpy.ops.mesh.primitive_cube_add(size=1, location=location, rotation=rotation)
    obj = bpy.context.object
    obj.name = name
    obj.dimensions = dimensions
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    if mat:
        obj.data.materials.append(mat)
    if parent:
        obj.parent = parent
    if coll:
        for old in list(obj.users_collection):
            old.objects.unlink(obj)
        coll.objects.link(obj)
    _bevel(obj, bevel)
    return obj


def cylinder(name, location, radius, depth, mat, parent=None, coll=None, axis="Z", vertices=24, bevel=0.006, rotation=None):
    rot = rotation
    if rot is None:
        rot = (0, 0, 0)
        if axis == "X":
            rot = (0, math.pi / 2, 0)
        elif axis == "Y":
            rot = (math.pi / 2, 0, 0)
    bpy.ops.mesh.primitive_cylinder_add(vertices=vertices, radius=radius, depth=depth, location=location, rotation=rot)
    obj = bpy.context.object
    obj.name = name
    if mat:
        obj.data.materials.append(mat)
    if parent:
        obj.parent = parent
    if coll:
        for old in list(obj.users_collection):
            old.objects.unlink(obj)
        coll.objects.link(obj)
    _smooth(obj)
    _bevel(obj, bevel, 2)
    return obj


def cylinder_between(name, start, end, radius, mat, parent=None, coll=None, vertices=16):
    start_v = Vector(start)
    end_v = Vector(end)
    direction = end_v - start_v
    midpoint = (start_v + end_v) * 0.5
    rotation = direction.to_track_quat("Z", "Y").to_euler()
    return cylinder(name, midpoint, radius, direction.length, mat, parent, coll, vertices=vertices, rotation=rotation, bevel=0.003)


def cone(name, location, radius1, radius2, depth, mat, parent=None, coll=None, vertices=24, rotation=(0, 0, 0)):
    bpy.ops.mesh.primitive_cone_add(vertices=vertices, radius1=radius1, radius2=radius2, depth=depth, location=location, rotation=rotation)
    obj = bpy.context.object
    obj.name = name
    if mat:
        obj.data.materials.append(mat)
    if parent:
        obj.parent = parent
    if coll:
        for old in list(obj.users_collection):
            old.objects.unlink(obj)
        coll.objects.link(obj)
    _smooth(obj)
    return obj


def sphere(name, location, scale, mat, parent=None, coll=None, segments=24, rings=12):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=segments, ring_count=rings, radius=1, location=location)
    obj = bpy.context.object
    obj.name = name
    obj.scale = scale
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    if mat:
        obj.data.materials.append(mat)
    if parent:
        obj.parent = parent
    if coll:
        for old in list(obj.users_collection):
            old.objects.unlink(obj)
        coll.objects.link(obj)
    return _smooth(obj)


def torus(name, location, major_radius, minor_radius, mat, parent=None, coll=None, axis="Z", major_segments=48, minor_segments=10):
    rot = (0, 0, 0)
    if axis == "X":
        rot = (0, math.pi / 2, 0)
    elif axis == "Y":
        rot = (math.pi / 2, 0, 0)
    bpy.ops.mesh.primitive_torus_add(major_segments=major_segments, minor_segments=minor_segments, location=location, rotation=rot, major_radius=major_radius, minor_radius=minor_radius)
    obj = bpy.context.object
    obj.name = name
    if mat:
        obj.data.materials.append(mat)
    if parent:
        obj.parent = parent
    if coll:
        for old in list(obj.users_collection):
            old.objects.unlink(obj)
        coll.objects.link(obj)
    return _smooth(obj)


def lathe_x(name, profile, mat, parent=None, coll=None, segments=48):
    verts = []
    uvs = []
    count = len(profile)
    for i in range(segments):
        theta = i / segments * math.tau
        for j, (x, radius) in enumerate(profile):
            verts.append((x, radius * math.cos(theta), radius * math.sin(theta)))
            uvs.append((i / segments, j / max(1, count - 1)))
    faces = []
    for i in range(segments):
        ni = (i + 1) % segments
        for j in range(count):
            nj = (j + 1) % count
            faces.append((i * count + j, ni * count + j, ni * count + nj, i * count + nj))
    return mesh(name, verts, faces, mat, parent, coll, True, uvs)


def loft_y(name, stations, mat, parent=None, coll=None):
    profile = [(-0.72, 0.0), (-1.0, 0.11), (-1.0, 0.78), (-0.78, 1.0), (0.78, 1.0), (1.0, 0.78), (1.0, 0.11), (0.72, 0.0)]
    verts = []
    uvs = []
    for y, width, bottom, top in stations:
        height = top - bottom
        for j, (px, pz) in enumerate(profile):
            verts.append((px * width, y, bottom + pz * height))
            uvs.append((j / len(profile), (y - stations[0][0]) / max(0.001, stations[-1][0] - stations[0][0])))
    faces = []
    ring = len(profile)
    for i in range(len(stations) - 1):
        for j in range(ring):
            nj = (j + 1) % ring
            faces.append((i * ring + j, (i + 1) * ring + j, (i + 1) * ring + nj, i * ring + nj))
    faces.append(tuple(reversed(range(ring))))
    faces.append(tuple((len(stations) - 1) * ring + j for j in range(ring)))
    return mesh(name, verts, faces, mat, parent, coll, True, uvs)


def panel_x(name, polygon_yz, x_center, thickness, mat, parent=None, coll=None):
    verts = []
    for x in (x_center - thickness / 2, x_center + thickness / 2):
        verts.extend((x, y, z) for y, z in polygon_yz)
    n = len(polygon_yz)
    faces = [tuple(reversed(range(n))), tuple(range(n, n * 2))]
    for i in range(n):
        j = (i + 1) % n
        faces.append((i, j, n + j, n + i))
    return mesh(name, verts, faces, mat, parent, coll, False)


def tube(name, points, radius, mat, parent=None, coll=None, sides=10):
    points = [Vector(p) for p in points]
    verts = []
    for i, point in enumerate(points):
        if i == 0:
            tangent = points[1] - point
        elif i == len(points) - 1:
            tangent = point - points[i - 1]
        else:
            tangent = points[i + 1] - points[i - 1]
        tangent.normalize()
        ref = Vector((0, 0, 1)) if abs(tangent.z) < 0.9 else Vector((1, 0, 0))
        normal = tangent.cross(ref).normalized()
        binormal = tangent.cross(normal).normalized()
        for j in range(sides):
            angle = j / sides * math.tau
            verts.append(tuple(point + radius * (math.cos(angle) * normal + math.sin(angle) * binormal)))
    faces = []
    for i in range(len(points) - 1):
        for j in range(sides):
            nj = (j + 1) % sides
            faces.append((i * sides + j, (i + 1) * sides + j, (i + 1) * sides + nj, i * sides + nj))
    faces.append(tuple(reversed(range(sides))))
    faces.append(tuple((len(points) - 1) * sides + j for j in range(sides)))
    return mesh(name, verts, faces, mat, parent, coll, True)


def mudguard(name, center, radius, width, thickness, mat, parent=None, coll=None, start_deg=8, end_deg=172):
    cx, cy, cz = center
    steps = 28
    verts = []
    for x in (cx - width / 2, cx + width / 2):
        for r in (radius, radius + thickness):
            for i in range(steps + 1):
                angle = math.radians(start_deg + (end_deg - start_deg) * i / steps)
                verts.append((x, cy + r * math.cos(angle), cz + r * math.sin(angle)))
    row = steps + 1
    faces = []
    for i in range(steps):
        faces.extend([
            (i, i + 1, row + i + 1, row + i),
            (2 * row + i, 3 * row + i, 3 * row + i + 1, 2 * row + i + 1),
            (i, 2 * row + i, 2 * row + i + 1, i + 1),
            (row + i, row + i + 1, 3 * row + i + 1, 3 * row + i),
        ])
    faces.extend([(0, row, 3 * row, 2 * row), (steps, 2 * row + steps, 3 * row + steps, row + steps)])
    return mesh(name, verts, faces, mat, parent, coll, True)


def leaf_spring(name, center_y, center_z, x, length, arch, mat, parent=None, coll=None, layers=4):
    verts = []
    steps = 16
    width = 0.075
    thickness = 0.012
    for layer in range(layers):
        layer_len = length - layer * 0.16
        zoff = layer * thickness
        for i in range(steps + 1):
            t = i / steps
            y = center_y - layer_len / 2 + t * layer_len
            z = center_z + zoff + math.sin(t * math.pi) * arch * (1 - layer * 0.1)
            verts.extend([(x - width / 2, y, z - thickness / 2), (x + width / 2, y, z - thickness / 2), (x + width / 2, y, z + thickness / 2), (x - width / 2, y, z + thickness / 2)])
    faces = []
    for layer in range(layers):
        base = layer * (steps + 1) * 4
        for i in range(steps):
            a = base + i * 4
            b = a + 4
            faces.extend([(a, b, b + 1, a + 1), (a + 2, a + 3, b + 3, b + 2), (a, a + 2, b + 2, b), (a + 1, b + 1, b + 3, a + 3)])
    return mesh(name, verts, faces, mat, parent, coll, False)


def text_mesh(name, body, location, size, mat, parent=None, coll=None, rotation=(math.pi / 2, 0, 0), extrude=0.002, font_path=None):
    bpy.ops.object.text_add(location=location, rotation=rotation)
    obj = bpy.context.object
    obj.name = name
    obj.data.body = body
    obj.data.align_x = "CENTER"
    obj.data.align_y = "CENTER"
    obj.data.size = size
    obj.data.extrude = extrude
    obj.data.bevel_depth = 0.0005
    if font_path:
        obj.data.font = bpy.data.fonts.load(str(font_path), check_existing=True)
    if mat:
        obj.data.materials.append(mat)
    bpy.ops.object.convert(target="MESH", keep_original=False)
    obj = bpy.context.object
    if parent:
        obj.parent = parent
    if coll:
        for old in list(obj.users_collection):
            old.objects.unlink(obj)
        coll.objects.link(obj)
    return obj


def rivet(name, location, mat, parent=None, coll=None, radius=0.012):
    return sphere(name, location, (radius, radius, radius * 0.65), mat, parent, coll, 12, 6)


def set_metadata(root, asset_id, historical_status, notes=""):
    root["asset_id"] = asset_id
    root["units"] = "meters"
    root["front_blender"] = "-Y"
    root["forward_unity"] = "+Z"
    root["historical_status"] = historical_status
    root["notes"] = notes
    return root


def add_studio(coll_name="PREVIEW_ONLY", target=(0, 0, 0.6), camera=(6.8, -8.6, 4.1), size=9.0):
    coll = collection(coll_name)
    floor_mat = material("STUDIO_Ground", (0.075, 0.08, 0.073), roughness=0.86)
    box("STUDIO_Ground", (0, 0, -0.06), (size * 2.2, size * 2.2, 0.1), floor_mat, coll=coll, bevel=0)
    light_data = bpy.data.lights.new("STUDIO_Key", "AREA")
    light_data.energy = 1700
    light_data.shape = "DISK"
    light_data.size = 5
    light = bpy.data.objects.new("STUDIO_Key", light_data)
    link_object(light, coll)
    light.location = (3.5, -4.5, 7)
    light.rotation_euler = (Vector(target) - light.location).to_track_quat("-Z", "Y").to_euler()
    fill_data = bpy.data.lights.new("STUDIO_Fill", "AREA")
    fill_data.energy = 700
    fill_data.size = 4
    fill = bpy.data.objects.new("STUDIO_Fill", fill_data)
    link_object(fill, coll)
    fill.location = (-5, -1, 4)
    fill.rotation_euler = (Vector(target) - fill.location).to_track_quat("-Z", "Y").to_euler()
    rim_data = bpy.data.lights.new("STUDIO_Rim", "AREA")
    rim_data.energy = 1100
    rim_data.size = 3
    rim = bpy.data.objects.new("STUDIO_Rim", rim_data)
    link_object(rim, coll)
    rim.location = (1, 5, 5)
    rim.rotation_euler = (Vector(target) - rim.location).to_track_quat("-Z", "Y").to_euler()
    cam_data = bpy.data.cameras.new("STUDIO_Camera")
    cam = bpy.data.objects.new("STUDIO_Camera", cam_data)
    link_object(cam, coll)
    cam.location = camera
    cam.rotation_euler = (Vector(target) - cam.location).to_track_quat("-Z", "Y").to_euler()
    cam_data.lens = 52
    bpy.context.scene.camera = cam
    return coll


def descendants(root):
    return [root] + list(root.children_recursive)


def select_hierarchy(root):
    bpy.ops.object.select_all(action="DESELECT")
    for obj in descendants(root):
        obj.select_set(True)
    bpy.context.view_layer.objects.active = root


def export_fbx(root, filepath, animations=False):
    path = Path(filepath)
    path.parent.mkdir(parents=True, exist_ok=True)
    select_hierarchy(root)
    options = {
        "filepath": str(path),
        "use_selection": True,
        "apply_unit_scale": True,
        "global_scale": 1.0,
        "axis_forward": "-Z",
        "axis_up": "Y",
        "object_types": {"EMPTY", "MESH", "ARMATURE"},
        "mesh_smooth_type": "FACE",
        "use_mesh_modifiers": True,
        "add_leaf_bones": False,
        "path_mode": "COPY",
        "embed_textures": True,
        "bake_anim": animations,
    }
    if animations:
        options.update({
            "bake_anim_use_all_bones": True,
            "bake_anim_use_nla_strips": True,
            "bake_anim_use_all_actions": False,
            "bake_anim_force_startend_keying": True,
            "bake_anim_step": 1,
            "bake_anim_simplify_factor": 0.0,
        })
    bpy.ops.export_scene.fbx(**options)
    bpy.ops.object.select_all(action="DESELECT")
    return path


def export_fbx_clean_nodes(root, filepath, strip_tokens):
    objects = descendants(root)
    targets = {}
    for obj in objects:
        if obj.type not in {"EMPTY", "ARMATURE"}:
            continue
        name = obj.name
        for token in strip_tokens:
            name = name.replace(token, "")
        targets[obj] = name
    temporary = {}
    target_names = set(targets.values())
    for obj in bpy.data.objects:
        if obj not in targets and obj.name in target_names:
            temporary[obj] = obj.name
            obj.name = "__EXPORT_" + obj.name
    original = {obj: obj.name for obj in targets}
    for obj, name in targets.items():
        obj.name = name
    try:
        return export_fbx(root, filepath)
    finally:
        for obj, name in original.items():
            obj.name = name
        for obj, name in temporary.items():
            obj.name = name


def duplicate_lod(root, suffix, ratio):
    objects = descendants(root)
    dup = {}
    lod_coll = collection(f"{root.name}_{suffix}_COLL")
    for old in objects:
        new = old.copy()
        if old.data and old.type == "MESH":
            new.data = old.data.copy()
        new.name = old.name + suffix
        link_object(new, lod_coll)
        dup[old] = new
    for old, new in dup.items():
        new.parent = dup.get(old.parent)
    for old in objects:
        if old.type == "MESH" and len(old.data.polygons) > 120:
            obj = dup[old]
            for modifier in list(obj.modifiers):
                if modifier.type == "DECIMATE":
                    obj.modifiers.remove(modifier)
            mod = obj.modifiers.new("LOD_Decimate", "DECIMATE")
            mod.ratio = ratio
            mod.use_collapse_triangulate = True
    return dup[root]


def ensure_uvs(root):
    bpy.ops.object.select_all(action="DESELECT")
    for obj in descendants(root):
        if obj.type != "MESH" or obj.data.uv_layers:
            continue
        bpy.context.view_layer.objects.active = obj
        obj.select_set(True)
        bpy.ops.object.mode_set(mode="EDIT")
        bpy.ops.mesh.select_all(action="SELECT")
        bpy.ops.uv.smart_project(angle_limit=math.radians(66), island_margin=0.02)
        bpy.ops.object.mode_set(mode="OBJECT")
        obj.select_set(False)
    bpy.ops.object.select_all(action="DESELECT")


def optimize_hierarchy(root, suffix="_OPT", decimate_ratio=None):
    objects = descendants(root)
    dup = {}
    opt_coll = collection(f"{root.name}{suffix}_COLL")
    for old in objects:
        new = old.copy()
        if old.data and old.type == "MESH":
            new.data = old.data.copy()
        new.name = old.name + suffix
        link_object(new, opt_coll)
        dup[old] = new
    for old, new in dup.items():
        new.parent = dup.get(old.parent)
    bpy.context.view_layer.update()
    groups = {}
    for old in objects:
        if old.type != "MESH":
            continue
        obj = dup[old]
        mat_key = obj.data.materials[0].name if obj.data.materials else "NO_MATERIAL"
        groups.setdefault((obj.parent, mat_key), []).append(obj)
    depsgraph = bpy.context.evaluated_depsgraph_get()
    for (parent, mat_name), group in groups.items():
        if len(group) < 2:
            continue
        verts = []
        faces = []
        loop_uvs = []
        smooth = []
        material = group[0].data.materials[0] if group[0].data.materials else None
        for obj in group:
            evaluated = obj.evaluated_get(depsgraph)
            evaluated_mesh = evaluated.to_mesh()
            transform = parent.matrix_world.inverted() @ evaluated.matrix_world if parent else evaluated.matrix_world.copy()
            offset = len(verts)
            verts.extend(tuple(transform @ v.co) for v in evaluated_mesh.vertices)
            faces.extend(tuple(offset + i for i in poly.vertices) for poly in evaluated_mesh.polygons)
            smooth.extend(poly.use_smooth for poly in evaluated_mesh.polygons)
            uv_layer = evaluated_mesh.uv_layers.active
            if uv_layer:
                loop_uvs.extend(tuple(uv_layer.data[i].uv) for i in range(len(evaluated_mesh.loops)))
            else:
                loop_uvs.extend([(0.0, 0.0)] * len(evaluated_mesh.loops))
            evaluated.to_mesh_clear()
        data = bpy.data.meshes.new(f"{parent.name if parent else root.name}_Merged_{mat_name}{suffix}_DATA")
        data.from_pydata(verts, [], faces)
        data.update()
        if material:
            data.materials.append(material)
        uv_layer = data.uv_layers.new(name="UVMap")
        for i, uv in enumerate(loop_uvs):
            uv_layer.data[i].uv = uv
        for poly, use_smooth in zip(data.polygons, smooth):
            poly.use_smooth = use_smooth
        joined = bpy.data.objects.new(data.name, data)
        link_object(joined, opt_coll)
        joined.parent = parent
        for obj in group:
            bpy.data.objects.remove(obj, do_unlink=True)
    if decimate_ratio:
        for obj in opt_coll.objects:
            if obj.type == "MESH" and mesh_triangle_count(obj) > 800:
                mod = obj.modifiers.new("LOD0_Decimate", "DECIMATE")
                mod.ratio = decimate_ratio
                mod.use_collapse_triangulate = True
    bpy.context.view_layer.update()
    return dup[root]


def mesh_triangle_count(obj):
    obj.data.calc_loop_triangles()
    return len(obj.data.loop_triangles)


def asset_stats(root):
    objs = [o for o in descendants(root) if o.type == "MESH"]
    points = [o.matrix_world @ Vector(corner) for o in objs for corner in o.bound_box]
    if not points:
        return {"meshes": 0, "vertices": 0, "polygons": 0, "dimensions_m": [0, 0, 0]}
    mins = [min(p[i] for p in points) for i in range(3)]
    maxs = [max(p[i] for p in points) for i in range(3)]
    return {
        "meshes": len(objs),
        "vertices": sum(len(o.data.vertices) for o in objs),
        "polygons": sum(len(o.data.polygons) for o in objs),
        "triangles": sum(mesh_triangle_count(o) for o in objs),
        "dimensions_m": [round(maxs[i] - mins[i], 4) for i in range(3)],
        "bounds_min_m": [round(v, 4) for v in mins],
        "bounds_max_m": [round(v, 4) for v in maxs],
    }


def save_blend(filepath):
    path = Path(filepath)
    path.parent.mkdir(parents=True, exist_ok=True)
    bpy.ops.file.pack_all()
    bpy.ops.wm.save_as_mainfile(filepath=str(path), check_existing=False)
    return path


def render_preview(filepath, resolution=(1400, 900), samples=48):
    scene = bpy.context.scene
    scene.render.resolution_x = resolution[0]
    scene.render.resolution_y = resolution[1]
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.filepath = str(filepath)
    if hasattr(scene, "eevee"):
        scene.eevee.taa_render_samples = samples
    bpy.ops.render.render(write_still=True)
    return Path(filepath)
