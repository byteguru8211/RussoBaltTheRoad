import bpy
import math
from mathutils import Vector, Matrix


def material(name, color, roughness=0.7, metallic=0.0):
    mat = bpy.data.materials.get(name)
    if mat is None:
        mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes.get("Principled BSDF")
    bsdf.inputs["Base Color"].default_value = (*color, 1.0)
    bsdf.inputs["Roughness"].default_value = roughness
    bsdf.inputs["Metallic"].default_value = metallic
    if "Skin" in name and "Subsurface Weight" in bsdf.inputs:
        bsdf.inputs["Subsurface Weight"].default_value = 0.08
        bsdf.inputs["Subsurface Radius"].default_value = (1.0, 0.45, 0.25)
    if not mat.node_tree.nodes.get("CharacterNoise"):
        noise = mat.node_tree.nodes.new("ShaderNodeTexNoise")
        noise.name = "CharacterNoise"
        noise.inputs["Scale"].default_value = 85.0
        noise.inputs["Detail"].default_value = 3.0
        ramp = mat.node_tree.nodes.new("ShaderNodeValToRGB")
        ramp.name = "CharacterColorVariation"
        ramp.color_ramp.elements[0].position = 0.18
        ramp.color_ramp.elements[1].position = 0.82
        ramp.color_ramp.elements[0].color = (*[max(0.0, channel * 0.84) for channel in color], 1.0)
        ramp.color_ramp.elements[1].color = (*[min(1.0, channel * 1.14) for channel in color], 1.0)
        mat.node_tree.links.new(noise.outputs["Fac"], ramp.inputs["Fac"])
        mat.node_tree.links.new(ramp.outputs["Color"], bsdf.inputs["Base Color"])
        bump = mat.node_tree.nodes.new("ShaderNodeBump")
        bump.name = "CharacterBump"
        bump.inputs["Strength"].default_value = 0.16
        bump.inputs["Distance"].default_value = 0.012
        mat.node_tree.links.new(noise.outputs["Fac"], bump.inputs["Height"])
        mat.node_tree.links.new(bump.outputs["Normal"], bsdf.inputs["Normal"])
    return mat


def finish(obj, mat, smooth=True):
    if mat is not None:
        obj.data.materials.append(mat)
    if smooth:
        for poly in obj.data.polygons:
            poly.use_smooth = True
    return obj


def sphere(name, location, scale, mat, segments=32, rings=20):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=segments, ring_count=rings, location=location)
    obj = bpy.context.object
    obj.name = name
    obj.scale = scale
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    return finish(obj, mat)


def loft(name, rings, mat, sides=32):
    verts = []
    faces = []
    for z, width, depth, y in rings:
        for i in range(sides):
            a = 2.0 * math.pi * i / sides
            verts.append((math.cos(a) * width, y + math.sin(a) * depth, z))
    for r in range(len(rings) - 1):
        for i in range(sides):
            n = (i + 1) % sides
            faces.append((r * sides + i, r * sides + n, (r + 1) * sides + n, (r + 1) * sides + i))
    faces.append(tuple(range(sides - 1, -1, -1)))
    faces.append(tuple((len(rings) - 1) * sides + i for i in range(sides)))
    mesh = bpy.data.meshes.new(name + "_Mesh")
    mesh.from_pydata(verts, [], faces)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    return finish(obj, mat)


def tube(name, start, end, radii, mat, sides=20):
    start = Vector(start)
    end = Vector(end)
    direction = (end - start).normalized()
    helper = Vector((0, 1, 0)) if abs(direction.y) < 0.92 else Vector((1, 0, 0))
    u = direction.cross(helper).normalized()
    v = direction.cross(u).normalized()
    verts = []
    faces = []
    count = len(radii)
    for r in range(count):
        t = r / max(1, count - 1)
        center = start.lerp(end, t)
        rx, ry = radii[r]
        for i in range(sides):
            a = 2.0 * math.pi * i / sides
            verts.append(tuple(center + u * math.cos(a) * rx + v * math.sin(a) * ry))
    for r in range(count - 1):
        for i in range(sides):
            n = (i + 1) % sides
            faces.append((r * sides + i, r * sides + n, (r + 1) * sides + n, (r + 1) * sides + i))
    faces.append(tuple(range(sides - 1, -1, -1)))
    faces.append(tuple((count - 1) * sides + i for i in range(sides)))
    mesh = bpy.data.meshes.new(name + "_Mesh")
    mesh.from_pydata(verts, [], faces)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    return finish(obj, mat)


def box(name, location, scale, mat, bevel=0.01, rotation=(0, 0, 0)):
    bpy.ops.mesh.primitive_cube_add(size=1, location=location)
    obj = bpy.context.object
    obj.name = name
    obj.scale = scale
    obj.rotation_euler = rotation
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    mod = obj.modifiers.new("SoftEdges", "BEVEL")
    mod.width = bevel
    mod.segments = 3
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.modifier_apply(modifier=mod.name)
    return finish(obj, mat)


def torus(name, location, major, minor, mat, rotation=(math.pi / 2, 0, 0), scale=(1, 1, 1)):
    bpy.ops.mesh.primitive_torus_add(major_radius=major, minor_radius=minor, major_segments=40, minor_segments=10, location=location, rotation=rotation)
    obj = bpy.context.object
    obj.name = name
    obj.scale = scale
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    return finish(obj, mat)


def join_parts(name, parts):
    bpy.ops.object.select_all(action="DESELECT")
    for obj in parts:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = parts[0]
    bpy.ops.object.join()
    body = bpy.context.object
    body.name = name
    return body


def head_parts(prefix, mats, female=False):
    parts = []
    z = 1.69 if not female else 1.67
    head = sphere(prefix + "_Head", (0, -0.002, z), (0.088, 0.082, 0.116), mats["skin"], 48, 32)
    for vert in head.data.vertices:
        nz = vert.co.z / 0.116
        if nz < -0.15:
            vert.co.x *= 1.0 - min(0.32, (-nz - 0.15) * 0.42)
        if vert.co.y < -0.045:
            vert.co.y *= 0.93
    parts.append(head)
    parts.append(sphere(prefix + "_Chin", (0, -0.058, z - 0.092), (0.043, 0.032, 0.027), mats["skin"]))
    for side, sx in (("L", 1), ("R", -1)):
        parts.append(sphere(prefix + "_Cheek_" + side, (sx * 0.046, -0.061, z - 0.008), (0.027, 0.018, 0.026), mats["skin"]))
    parts.append(sphere(prefix + "_NoseBridge", (0, -0.080, z + 0.004), (0.012, 0.020, 0.036), mats["skin"]))
    parts.append(sphere(prefix + "_NoseTip", (0, -0.098, z - 0.020), (0.017, 0.018, 0.012), mats["skin"]))
    for side, sx in (("L", 1), ("R", -1)):
        parts.append(sphere(prefix + "_Nostril_" + side, (sx * 0.009, -0.112, z - 0.026), (0.0038, 0.0022, 0.0024), mats["lips"], 16, 8))
        parts.append(sphere(prefix + "_Ear_" + side, (sx * 0.087, 0.0, z - 0.004), (0.015, 0.010, 0.028), mats["skin"]))
        parts.append(sphere(prefix + "_EyeWhite_" + side, (sx * 0.032, -0.077, z + 0.025), (0.016, 0.008, 0.010), mats["eye"]))
        parts.append(sphere(prefix + "_Iris_" + side, (sx * 0.032, -0.085, z + 0.025), (0.0062, 0.0028, 0.0062), mats["iris"], 24, 12))
        parts.append(sphere(prefix + "_Pupil_" + side, (sx * 0.032, -0.0875, z + 0.025), (0.0028, 0.0015, 0.0030), mats["hair"], 20, 10))
        parts.append(tube(prefix + "_Brow_" + side, (sx * 0.014, -0.082, z + 0.043), (sx * 0.052, -0.076, z + 0.041), [(0.0035, 0.0035), (0.0045, 0.0035)], mats["hair"], 10))
        parts.append(tube(prefix + "_UpperEyelid_" + side, (sx * 0.017, -0.084, z + 0.030), (sx * 0.047, -0.082, z + 0.030), [(0.0022, 0.0022), (0.0028, 0.0022)], mats["skin"], 10))
        parts.append(tube(prefix + "_LowerEyelid_" + side, (sx * 0.018, -0.083, z + 0.020), (sx * 0.046, -0.081, z + 0.020), [(0.0018, 0.0018), (0.0022, 0.0018)], mats["skin"], 10))
        parts.append(tube(prefix + "_Nasolabial_" + side, (sx * 0.019, -0.088, z - 0.020), (sx * 0.029, -0.077, z - 0.054), [(0.0015, 0.0015), (0.0012, 0.0012)], mats["lips"], 8))
    parts.append(tube(prefix + "_UpperLip", (-0.025, -0.082, z - 0.052), (0.025, -0.082, z - 0.052), [(0.0028, 0.0025), (0.0035, 0.0028), (0.0028, 0.0025)], mats["lips"], 12))
    parts.append(tube(prefix + "_LowerLip", (-0.022, -0.081, z - 0.057), (0.022, -0.081, z - 0.057), [(0.0025, 0.0022), (0.0032, 0.0025), (0.0025, 0.0022)], mats["lips"], 12))
    parts.append(tube(prefix + "_Neck", (0, 0, 1.47), (0, 0, 1.59), [(0.048, 0.043), (0.043, 0.040)], mats["skin"], 24))
    return parts


def driver(prefix, rig_name, root_name):
    mats = {
        "skin": material("CHR_Skin_HD", (0.58, 0.37, 0.24), 0.64),
        "coat": material("CHR_Driver_LeatherCoat_HD", (0.24, 0.095, 0.038), 0.64),
        "trousers": material("CHR_Driver_Trousers_HD", (0.105, 0.09, 0.07), 0.88),
        "shirt": material("CHR_Shirt_HD", (0.72, 0.67, 0.52), 0.82),
        "waistcoat": material("CHR_Waistcoat_HD", (0.07, 0.12, 0.105), 0.76),
        "leather": material("CHR_BootLeather_HD", (0.045, 0.022, 0.012), 0.48),
        "sole": material("CHR_Sole_HD", (0.018, 0.014, 0.011), 0.9),
        "glove": material("CHR_Gloves_HD", (0.12, 0.065, 0.032), 0.68),
        "brass": material("CHR_Brass_HD", (0.55, 0.32, 0.09), 0.28, 0.88),
        "hair": material("CHR_Hair_HD", (0.035, 0.022, 0.014), 0.86),
        "eye": material("CHR_EyeWhite_HD", (0.78, 0.75, 0.67), 0.32),
        "iris": material("CHR_Iris_HD", (0.05, 0.08, 0.06), 0.24),
        "lips": material("CHR_Lips_HD", (0.32, 0.12, 0.09), 0.55),
        "glass": material("CHR_GoggleGlass_HD", (0.45, 0.62, 0.64), 0.12, 0.12),
        "cap": material("CHR_Cap_HD", (0.09, 0.07, 0.045), 0.86),
    }
    mats["glass"].node_tree.nodes["Principled BSDF"].inputs["Alpha"].default_value = 0.42
    mats["glass"].blend_method = "BLEND"
    parts = []
    parts.append(loft(prefix + "_Jacket", [(0.98, 0.15, 0.105, 0.015), (1.08, 0.165, 0.11, 0.01), (1.24, 0.195, 0.12, 0.005), (1.39, 0.215, 0.108, 0.0), (1.46, 0.18, 0.09, 0.0), (1.49, 0.075, 0.068, 0.0)], mats["coat"]))
    parts.append(sphere(prefix + "_Hips", (0, 0.01, 0.99), (0.17, 0.115, 0.145), mats["trousers"]))
    parts.append(sphere(prefix + "_Waistcoat", (0, -0.105, 1.255), (0.105, 0.018, 0.17), mats["waistcoat"]))
    parts.append(box(prefix + "_ShirtBib", (0, -0.124, 1.34), (0.13, 0.018, 0.13), mats["shirt"], 0.014))
    for side, sx in (("L", 1), ("R", -1)):
        parts.append(box(prefix + "_Lapel_" + side, (sx * 0.074, -0.126, 1.385), (0.052, 0.018, 0.13), mats["coat"], 0.012, (0.08, sx * 0.10, sx * 0.26)))
        parts.append(box(prefix + "_Pocket_" + side, (sx * 0.115, -0.096, 1.10), (0.074, 0.018, 0.052), mats["coat"], 0.009, (0, 0, sx * 0.06)))
        parts.append(box(prefix + "_CoatTail_" + side, (sx * 0.085, 0.085, 0.985), (0.09, 0.075, 0.19), mats["coat"], 0.018, (0.10, 0, sx * 0.05)))
        parts.append(tube(prefix + "_UpperSleeve_" + side, (sx * 0.205, 0.0, 1.405), (sx * 0.275, -0.015, 1.17), [(0.078, 0.075), (0.068, 0.067), (0.052, 0.052)], mats["coat"], 24))
        parts.append(sphere(prefix + "_Elbow_" + side, (sx * 0.275, -0.015, 1.17), (0.055, 0.055, 0.060), mats["coat"]))
        parts.append(tube(prefix + "_ForearmSleeve_" + side, (sx * 0.275, -0.015, 1.17), (sx * 0.255, -0.075, 0.95), [(0.055, 0.055), (0.048, 0.047), (0.038, 0.038)], mats["coat"], 24))
        parts.append(tube(prefix + "_Cuff_" + side, (sx * 0.257, -0.071, 0.975), (sx * 0.253, -0.080, 0.94), [(0.041, 0.041), (0.039, 0.039)], mats["shirt"], 20))
        parts.append(sphere(prefix + "_Palm_" + side, (sx * 0.252, -0.085, 0.905), (0.038, 0.025, 0.055), mats["glove"]))
        for finger in range(4):
            fx = sx * (0.231 + finger * 0.013)
            parts.append(tube(prefix + "_Finger_" + side + "_" + str(finger), (fx, -0.087, 0.89), (fx, -0.092, 0.845 - abs(1.5 - finger) * 0.006), [(0.007, 0.007), (0.006, 0.006)], mats["glove"], 10))
        parts.append(tube(prefix + "_Thumb_" + side, (sx * 0.225, -0.084, 0.92), (sx * 0.207, -0.105, 0.89), [(0.010, 0.010), (0.008, 0.008)], mats["glove"], 12))
        parts.append(tube(prefix + "_Thigh_" + side, (sx * 0.095, 0.01, 0.99), (sx * 0.11, -0.015, 0.60), [(0.102, 0.11), (0.105, 0.11), (0.073, 0.078)], mats["trousers"], 28))
        parts.append(sphere(prefix + "_Knee_" + side, (sx * 0.11, -0.015, 0.59), (0.072, 0.075, 0.075), mats["trousers"]))
        parts.append(tube(prefix + "_Puttee_" + side, (sx * 0.11, -0.015, 0.57), (sx * 0.11, -0.025, 0.19), [(0.060, 0.062), (0.050, 0.052), (0.043, 0.045)], mats["shirt"], 24))
        for wrap in range(7):
            z = 0.23 + wrap * 0.044
            parts.append(torus(prefix + "_PutteeWrap_" + side + "_" + str(wrap), (sx * 0.11, -0.02, z), 0.050 - wrap * 0.001, 0.0035, mats["trousers"], (0, 0, 0)))
        parts.append(tube(prefix + "_BootAnkle_" + side, (sx * 0.11, -0.025, 0.09), (sx * 0.11, -0.025, 0.29), [(0.070, 0.075), (0.064, 0.068)], mats["leather"], 28))
        parts.append(sphere(prefix + "_BootFoot_" + side, (sx * 0.11, -0.095, 0.105), (0.077, 0.145, 0.095), mats["leather"], 36, 20))
        parts.append(sphere(prefix + "_BootSole_" + side, (sx * 0.11, -0.10, 0.038), (0.082, 0.15, 0.025), mats["sole"], 36, 16))
        for lace in range(4):
            parts.append(tube(prefix + "_BootLace_" + side + "_" + str(lace), (sx * 0.075, -0.065, 0.16 + lace * 0.022), (sx * 0.145, -0.065, 0.16 + lace * 0.022), [(0.0025, 0.0025), (0.0025, 0.0025)], mats["sole"], 8))
    parts.extend(head_parts(prefix, mats))
    parts.append(box(prefix + "_CollarLeft", (-0.045, -0.075, 1.48), (0.055, 0.03, 0.075), mats["shirt"], 0.01, (0.15, 0.1, -0.32)))
    parts.append(box(prefix + "_CollarRight", (0.045, -0.075, 1.48), (0.055, 0.03, 0.075), mats["shirt"], 0.01, (0.15, -0.1, 0.32)))
    parts.append(tube(prefix + "_Scarf", (-0.045, -0.09, 1.475), (0.045, -0.09, 1.475), [(0.018, 0.012), (0.020, 0.012), (0.018, 0.012)], mats["waistcoat"], 16))
    parts.append(tube(prefix + "_ScarfTail", (0.025, -0.105, 1.47), (0.04, -0.12, 1.34), [(0.018, 0.008), (0.014, 0.007), (0.01, 0.005)], mats["waistcoat"], 14))
    for i in range(5):
        parts.append(sphere(prefix + "_Button_" + str(i), (0, -0.137, 1.36 - i * 0.052), (0.008, 0.004, 0.008), mats["brass"], 16, 10))
    parts.append(tube(prefix + "_WatchChain", (-0.075, -0.13, 1.23), (0.065, -0.135, 1.14), [(0.003, 0.003), (0.003, 0.003), (0.003, 0.003)], mats["brass"], 8))
    parts.append(sphere(prefix + "_CapCrown", (0, 0.005, 1.785), (0.096, 0.089, 0.042), mats["cap"], 40, 20))
    parts.append(sphere(prefix + "_CapVisor", (0, -0.080, 1.776), (0.095, 0.065, 0.012), mats["cap"], 36, 16))
    parts.append(tube(prefix + "_CapBand", (-0.086, -0.005, 1.765), (0.086, -0.005, 1.765), [(0.009, 0.009), (0.009, 0.009)], mats["leather"], 14))
    for side, sx in (("L", 1), ("R", -1)):
        parts.append(torus(prefix + "_GoggleRim_" + side, (sx * 0.037, -0.086, 1.758), 0.027, 0.0045, mats["brass"]))
        parts.append(sphere(prefix + "_GoggleGlass_" + side, (sx * 0.037, -0.086, 1.758), (0.023, 0.003, 0.023), mats["glass"], 32, 16))
    parts.append(torus(prefix + "_GoggleStrap", (0, 0.0, 1.758), 0.088, 0.006, mats["leather"], (math.pi / 2, 0, 0), (1.0, 0.92, 1.0)))
    body = join_parts(prefix + "_Body_HD", parts)
    return attach_rig(body, rig_name, prefix + "_Body")


def mechanic(prefix, rig_name):
    mats = {
        "skin": material("CHR_Skin_HD", (0.56, 0.35, 0.22), 0.66),
        "shirt": material("CHR_Shirt_HD", (0.55, 0.50, 0.38), 0.86),
        "overalls": material("CHR_Overalls_HD", (0.055, 0.10, 0.12), 0.86),
        "leather": material("CHR_BootLeather_HD", (0.045, 0.022, 0.012), 0.5),
        "sole": material("CHR_Sole_HD", (0.018, 0.014, 0.011), 0.9),
        "brass": material("CHR_Brass_HD", (0.55, 0.32, 0.09), 0.3, 0.88),
        "hair": material("CHR_Hair_HD", (0.035, 0.022, 0.014), 0.86),
        "eye": material("CHR_EyeWhite_HD", (0.78, 0.75, 0.67), 0.32),
        "iris": material("CHR_Iris_HD", (0.05, 0.08, 0.06), 0.24),
        "lips": material("CHR_Lips_HD", (0.32, 0.12, 0.09), 0.55),
        "cap": material("CHR_Cap_HD", (0.09, 0.07, 0.045), 0.86),
    }
    parts = [loft(prefix + "_Torso", [(1.0, 0.15, 0.10, 0), (1.12, 0.16, 0.105, 0), (1.30, 0.19, 0.11, 0), (1.43, 0.205, 0.095, 0), (1.49, 0.07, 0.06, 0)], mats["shirt"])]
    parts.append(sphere(prefix + "_Hips", (0, 0, 1.0), (0.165, 0.11, 0.13), mats["overalls"]))
    parts.append(box(prefix + "_Bib", (0, -0.112, 1.24), (0.16, 0.02, 0.23), mats["overalls"], 0.015))
    for side, sx in (("L", 1), ("R", -1)):
        parts.append(tube(prefix + "_Strap_" + side, (sx * 0.08, -0.115, 1.37), (sx * 0.08, -0.11, 1.08), [(0.014, 0.01), (0.014, 0.01)], mats["overalls"], 12))
        parts.append(tube(prefix + "_UpperArm_" + side, (sx * 0.20, 0, 1.40), (sx * 0.27, -0.015, 1.16), [(0.075, 0.072), (0.052, 0.052)], mats["shirt"], 24))
        parts.append(tube(prefix + "_Forearm_" + side, (sx * 0.27, -0.015, 1.16), (sx * 0.25, -0.07, 0.95), [(0.052, 0.052), (0.038, 0.038)], mats["skin"], 24))
        parts.append(sphere(prefix + "_Hand_" + side, (sx * 0.25, -0.075, 0.90), (0.038, 0.026, 0.055), mats["skin"]))
        parts.append(tube(prefix + "_Thigh_" + side, (sx * 0.095, 0, 1.0), (sx * 0.11, -0.01, 0.59), [(0.10, 0.105), (0.072, 0.075)], mats["overalls"], 28))
        parts.append(tube(prefix + "_Shin_" + side, (sx * 0.11, -0.01, 0.57), (sx * 0.11, -0.02, 0.18), [(0.058, 0.06), (0.043, 0.045)], mats["overalls"], 24))
        parts.append(tube(prefix + "_Boot_" + side, (sx * 0.11, -0.02, 0.08), (sx * 0.11, -0.02, 0.28), [(0.067, 0.072), (0.062, 0.067)], mats["leather"], 28))
        parts.append(sphere(prefix + "_Foot_" + side, (sx * 0.11, -0.09, 0.105), (0.075, 0.14, 0.09), mats["leather"]))
        parts.append(sphere(prefix + "_Sole_" + side, (sx * 0.11, -0.095, 0.038), (0.08, 0.145, 0.024), mats["sole"]))
    parts.extend(head_parts(prefix, mats))
    parts.append(sphere(prefix + "_Cap", (0, 0.01, 1.79), (0.094, 0.09, 0.036), mats["cap"]))
    parts.append(sphere(prefix + "_Visor", (0, -0.08, 1.78), (0.09, 0.06, 0.012), mats["cap"]))
    parts.append(box(prefix + "_Wrench", (0.24, -0.07, 0.91), (0.018, 0.025, 0.22), mats["brass"], 0.004, (0.15, 0, -0.12)))
    body = join_parts(prefix + "_Body_HD", parts)
    return attach_rig(body, rig_name, prefix + "_Body")


def lady(prefix, rig_name):
    mats = {
        "skin": material("CHR_Skin_HD", (0.64, 0.43, 0.30), 0.62),
        "dress": material("CHR_LadyDress_HD", (0.13, 0.16, 0.19), 0.82),
        "blouse": material("CHR_Shirt_HD", (0.72, 0.67, 0.52), 0.82),
        "leather": material("CHR_BootLeather_HD", (0.045, 0.022, 0.012), 0.5),
        "sole": material("CHR_Sole_HD", (0.018, 0.014, 0.011), 0.9),
        "hair": material("CHR_Hair_HD", (0.07, 0.035, 0.018), 0.72),
        "eye": material("CHR_EyeWhite_HD", (0.78, 0.75, 0.67), 0.32),
        "iris": material("CHR_Iris_HD", (0.05, 0.08, 0.06), 0.24),
        "lips": material("CHR_Lips_HD", (0.32, 0.12, 0.09), 0.55),
    }
    parts = [loft(prefix + "_Bodice", [(1.02, 0.14, 0.095, 0), (1.18, 0.16, 0.10, 0), (1.35, 0.19, 0.10, 0), (1.43, 0.17, 0.085, 0), (1.48, 0.065, 0.055, 0)], mats["dress"])]
    parts.append(loft(prefix + "_Skirt", [(0.20, 0.24, 0.17, 0.015), (0.45, 0.22, 0.15, 0.01), (0.75, 0.18, 0.125, 0), (1.02, 0.145, 0.10, 0)], mats["dress"], 40))
    parts.append(sphere(prefix + "_Blouse", (0, -0.09, 1.32), (0.10, 0.018, 0.12), mats["blouse"]))
    for side, sx in (("L", 1), ("R", -1)):
        parts.append(tube(prefix + "_Sleeve_" + side, (sx * 0.18, 0, 1.40), (sx * 0.27, -0.01, 1.18), [(0.065, 0.065), (0.048, 0.048)], mats["blouse"], 24))
        parts.append(tube(prefix + "_Forearm_" + side, (sx * 0.27, -0.01, 1.18), (sx * 0.25, -0.07, 0.96), [(0.043, 0.043), (0.032, 0.032)], mats["skin"], 24))
        parts.append(sphere(prefix + "_Hand_" + side, (sx * 0.25, -0.075, 0.91), (0.032, 0.023, 0.05), mats["skin"]))
        parts.append(sphere(prefix + "_Shoe_" + side, (sx * 0.10, -0.07, 0.10), (0.065, 0.13, 0.075), mats["leather"]))
        parts.append(sphere(prefix + "_Sole_" + side, (sx * 0.10, -0.075, 0.04), (0.07, 0.135, 0.022), mats["sole"]))
    parts.extend(head_parts(prefix, mats, female=True))
    parts.append(sphere(prefix + "_Hair", (0, 0.025, 1.77), (0.094, 0.085, 0.075), mats["hair"]))
    parts.append(sphere(prefix + "_HairBun", (0, 0.09, 1.75), (0.055, 0.05, 0.055), mats["hair"]))
    parts.append(tube(prefix + "_Ribbon", (-0.06, -0.08, 1.50), (0.06, -0.08, 1.50), [(0.012, 0.008), (0.012, 0.008)], mats["dress"], 12))
    body = join_parts(prefix + "_Body_HD", parts)
    return attach_rig(body, rig_name, prefix + "_Body")


def attach_rig(body, rig_name, original_name):
    rig = bpy.data.objects[rig_name]
    prefix = rig_name.removesuffix("_Rig")
    root = bpy.data.objects.get(prefix + "_Root")
    legacy = bpy.data.objects.get(original_name)
    if legacy is not None and legacy != body:
        legacy.name = original_name + "_Legacy"
        legacy.hide_render = True
        legacy.hide_viewport = True
    rig.parent = root
    rig.matrix_parent_inverse = Matrix.Identity(4)
    body.parent = rig
    body.matrix_parent_inverse = Matrix.Identity(4)
    if legacy is not None:
        legacy.parent = rig
        legacy.matrix_parent_inverse = Matrix.Identity(4)
    body.vertex_groups.clear()
    bones = list(rig.data.bones)
    groups = {bone.name: body.vertex_groups.new(name=bone.name) for bone in bones}
    segments = []
    for bone in bones:
        start = bone.head_local
        delta = bone.tail_local - start
        segments.append((start, delta, max(1e-8, delta.length_squared)))
    for vertex in body.data.vertices:
        scored = []
        for bone, (start, delta, denominator) in zip(bones, segments):
            t = max(0.0, min(1.0, (vertex.co - start).dot(delta) / denominator))
            closest = start + delta * t
            scored.append(((vertex.co - closest).length, bone.name))
        scored.sort(key=lambda item: item[0])
        nearest = scored[:2]
        weights = [1.0 / max(0.008, distance) ** 4 for distance, _ in nearest]
        total = sum(weights)
        for (_, name), weight in zip(nearest, weights):
            if weight / total > 0.001:
                groups[name].add([vertex.index], weight / total, "REPLACE")
    if not any(mod.type == "ARMATURE" for mod in body.modifiers):
        modifier = body.modifiers.new("CharacterRig", "ARMATURE")
        modifier.object = rig
    bpy.context.view_layer.update()
    return body


def animate_rig(rig):
    rig.animation_data_clear()
    for existing in list(bpy.data.actions):
        if existing.name.startswith(rig.name + "_"):
            bpy.data.actions.remove(existing)

    def reset():
        for bone in rig.pose.bones:
            bone.rotation_mode = "XYZ"
            bone.rotation_euler = (0, 0, 0)
            bone.location = (0, 0, 0)

    def action(name, frames):
        reset()
        data = bpy.data.actions.new(rig.name + "_" + name)
        data.use_fake_user = True
        rig.animation_data_create()
        rig.animation_data.action = data
        for frame, pose in frames:
            bpy.context.scene.frame_set(frame)
            reset()
            for bone_name, rotation in pose.items():
                rig.pose.bones[bone_name].rotation_euler = rotation
                rig.pose.bones[bone_name].keyframe_insert(data_path="rotation_euler", frame=frame)
        return data

    clips = [action("Idle", [(1, {}), (31, {"Chest": (0.018, 0.0, 0.012), "Head": (0.0, 0.018, -0.008)})])]
    walk = []
    for frame, phase in ((1, 0), (7, 1), (13, 0), (19, -1), (25, 0), (31, 1)):
        walk.append((frame, {
            "Thigh.L": (phase * 0.38, 0, 0),
            "Thigh.R": (-phase * 0.38, 0, 0),
            "Shin.L": (max(0, -phase) * 0.5, 0, 0),
            "Shin.R": (max(0, phase) * 0.5, 0, 0),
            "UpperArm.L": (-phase * 0.30, 0, 0),
            "UpperArm.R": (phase * 0.30, 0, 0),
            "Forearm.L": (0.16, 0, 0),
            "Forearm.R": (0.16, 0, 0),
            "Spine": (0.025, 0, phase * 0.02),
        }))
    clips.append(action("Walk", walk))
    drive = {
        "Spine": (0.10, 0, 0),
        "UpperArm.L": (-1.02, 0, -0.10),
        "UpperArm.R": (-1.02, 0, 0.10),
        "Forearm.L": (-0.42, 0, 0),
        "Forearm.R": (-0.42, 0, 0),
        "Hand.L": (-0.18, 0, 0),
        "Hand.R": (-0.18, 0, 0),
        "Thigh.L": (-1.32, 0, 0),
        "Thigh.R": (-1.32, 0, 0),
        "Shin.L": (1.22, 0, 0),
        "Shin.R": (1.22, 0, 0),
        "Foot.L": (-0.20, 0, 0),
        "Foot.R": (-0.20, 0, 0),
    }
    clips.append(action("Drive", [(1, drive), (31, drive)]))
    for index, clip in enumerate(clips):
        track = rig.animation_data.nla_tracks.new()
        track.name = clip.name
        start = 1 + index * 40
        strip = track.strips.new(clip.name, start, clip)
        strip.frame_end = start + 30
        strip.extrapolation = "NOTHING"
    rig.animation_data.action = None
    bpy.context.scene.frame_set(1)
    reset()


def main():
    driver("CHR_Driver_1912", "CHR_Driver_1912_Rig", "CHR_Driver_1912_Root")
    mechanic("CHR_Mechanic_1912", "CHR_Mechanic_1912_Rig")
    lady("CHR_Lady_1912", "CHR_Lady_1912_Rig")
    for prefix in ("CHR_Driver_1912", "CHR_Mechanic_1912", "CHR_Lady_1912"):
        animate_rig(bpy.data.objects[prefix + "_Rig"])
    for obj in bpy.data.objects:
        if obj.name.startswith("CHR_") and obj.type == "MESH":
            obj.hide_render = False
            obj.hide_viewport = False
    test = bpy.data.objects.get("CHR_Driver_TestRemesh")
    if test is not None:
        bpy.data.objects.remove(test, do_unlink=True)
    bpy.context.scene.render.engine = "BLENDER_EEVEE"
    print("Historical characters rebuilt")


main()
bpy.ops.wm.save_as_mainfile(filepath=r"D:/programming/RussoBaltTheRoad/game_assets/characters/Historical_Characters.blend")
