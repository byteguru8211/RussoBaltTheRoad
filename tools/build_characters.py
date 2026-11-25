from pathlib import Path
import sys
import importlib
import json
import math
import os
import traceback
sys.path.insert(0, str(Path(__file__).resolve().parent))
import blender_assetlib
importlib.reload(blender_assetlib)
from blender_assetlib import *

ASSET_DIR = Path(os.environ.get("RB_CHARACTER_OUT", str(ROOT / "game_assets" / "characters")))
PREVIEW_DIR = Path(os.environ.get("RB_CHARACTER_PREVIEW_OUT", str(ROOT / "previews")))
CHARACTERS = []


def torso_mesh(name, rings, mat, root, coll, segments=16):
    verts, uvs, faces = [], [], []
    for z, width, depth in rings:
        for i in range(segments):
            angle = i / segments * math.tau
            verts.append((width * math.cos(angle), depth * math.sin(angle), z))
            uvs.append((i / segments, z))
    for j in range(len(rings) - 1):
        for i in range(segments):
            ni = (i + 1) % segments
            faces.append((j * segments + i, (j + 1) * segments + i, (j + 1) * segments + ni, j * segments + ni))
    faces.append(tuple(reversed(range(segments))))
    faces.append(tuple((len(rings) - 1) * segments + i for i in range(segments)))
    return mesh(name, verts, faces, mat, root, coll, True, uvs)


def combine_parts(parts, name, root, coll):
    bpy.context.view_layer.update()
    verts, faces, loop_uvs, smooth, face_materials = [], [], [], [], []
    materials = []
    for obj in parts:
        bpy.ops.object.select_all(action="DESELECT")
        obj.select_set(True)
        bpy.context.view_layer.objects.active = obj
        for modifier in list(obj.modifiers):
            bpy.ops.object.modifier_apply(modifier=modifier.name)
        source = obj.data
        transform = root.matrix_world.inverted() @ obj.matrix_world
        offset = len(verts)
        verts.extend(tuple(transform @ v.co) for v in source.vertices)
        faces.extend(tuple(offset + i for i in poly.vertices) for poly in source.polygons)
        smooth.extend(poly.use_smooth for poly in source.polygons)
        uv_layer = source.uv_layers.active
        if uv_layer:
            loop_uvs.extend(tuple(uv_layer.data[i].uv) for i in range(len(source.loops)))
        else:
            loop_uvs.extend([(0.0, 0.0)] * len(source.loops))
        for mat in source.materials:
            if mat not in materials:
                materials.append(mat)
        for poly in source.polygons:
            index = poly.material_index
            mat = source.materials[index] if source.materials else None
            face_materials.append(materials.index(mat) if mat in materials else 0)
        bpy.data.objects.remove(obj, do_unlink=True)
    data = bpy.data.meshes.new(f"{name}_Mesh")
    data.from_pydata(verts, [], faces)
    data.update()
    for mat in materials:
        data.materials.append(mat)
    uv_layer = data.uv_layers.new(name="UVMap")
    for i, uv in enumerate(loop_uvs):
        uv_layer.data[i].uv = uv
    for poly, use_smooth, mat_index in zip(data.polygons, smooth, face_materials):
        poly.use_smooth = use_smooth
        poly.material_index = mat_index
    combined = bpy.data.objects.new(f"{name}_Body", data)
    link_object(combined, coll)
    combined.parent = root
    return combined


def build_body(kind, root, coll, p):
    parts = []
    add = lambda obj: parts.append(obj)
    if kind == "driver":
        coat = material("CHR_Driver_LeatherCoat", (0.16, 0.075, 0.035), roughness=0.72, texture="leather_saddle")
        trousers = material("CHR_Driver_Trousers", (0.055, 0.06, 0.052), roughness=0.9, texture="fabric_charcoal")
        shirt = p["cream"]
        accent = p["brass"]
        shoe = p["leather"]
    elif kind == "mechanic":
        coat = material("CHR_Mechanic_Jacket", (0.035, 0.09, 0.14), roughness=0.88, texture="fabric_charcoal")
        trousers = material("CHR_Mechanic_Trousers", (0.045, 0.05, 0.045), roughness=0.92)
        shirt = p["cream"]
        accent = p["leather"]
        shoe = p["rubber"]
    else:
        coat = material("CHR_Lady_Coat", (0.12, 0.16, 0.12), roughness=0.86, texture="fabric_charcoal")
        trousers = coat
        shirt = p["cream"]
        accent = p["wood"]
        shoe = p["leather"]

    eye_white = material("CHR_Eye_White", (0.78, 0.75, 0.66), roughness=0.35)
    iris = material("CHR_Iris", (0.055, 0.075, 0.065), roughness=0.28)
    skin_bsdf = p["skin"].node_tree.nodes.get("Principled BSDF")
    if skin_bsdf and "Subsurface Weight" in skin_bsdf.inputs:
        skin_bsdf.inputs["Subsurface Weight"].default_value = 0.08

    for side in (-1, 1):
        x = side * 0.14
        if kind == "lady":
            add(cone(f"{kind}_Skirt_{side}", (x, 0, 0.68), 0.23, 0.15, 1.05, coat, root, coll, 16))
        else:
            add(cylinder_between(f"{kind}_Thigh_{side}", (side * 0.13, 0, 0.96), (side * 0.14, 0, 0.55), 0.092, trousers, root, coll, 14))
            add(cylinder_between(f"{kind}_Shin_{side}", (side * 0.14, 0, 0.55), (side * 0.14, 0, 0.18), 0.075, trousers, root, coll, 14))
        add(box(f"{kind}_Boot_{side}", (x, -0.055, 0.105), (0.17, 0.30, 0.18), shoe, root, coll, 0.055))
        add(box(f"{kind}_Boot_Sole_{side}", (x, -0.055, 0.025), (0.18, 0.31, 0.04), p["rubber"], root, coll, 0.01))
    add(sphere(f"{kind}_Pelvis", (0, 0, 0.98), (0.24, 0.15, 0.16), trousers, root, coll, 20, 10))
    rings = [(0.98, 0.20, 0.125), (1.14, 0.19, 0.12), (1.38, 0.235, 0.14), (1.51, 0.215, 0.12)]
    add(torso_mesh(f"{kind}_Torso", rings, coat, root, coll))
    add(box(f"{kind}_Shirt_Front", (0, -0.137, 1.33), (0.16, 0.022, 0.34), shirt, root, coll, 0.01))
    for side in (-1, 1):
        add(tube(f"{kind}_Coat_Fold_{side}", [(side * 0.11, -0.128, 1.08), (side * 0.15, -0.142, 1.22), (side * 0.13, -0.145, 1.38)], 0.008, coat, root, coll, 6))
    add(tube(f"{kind}_Collar_Left", [(-0.11, -0.13, 1.49), (-0.03, -0.16, 1.43), (0, -0.15, 1.39)], 0.018, shirt, root, coll, 8))
    add(tube(f"{kind}_Collar_Right", [(0.11, -0.13, 1.49), (0.03, -0.16, 1.43), (0, -0.15, 1.39)], 0.018, shirt, root, coll, 8))
    if kind == "driver":
        for z in (1.17, 1.28, 1.39):
            add(rivet(f"{kind}_Coat_Button_{z}", (0.02, -0.166, z), accent, root, coll, 0.012))
        add(tube(f"{kind}_Belt", [(-0.21, -0.10, 1.08), (0, -0.15, 1.06), (0.21, -0.10, 1.08)], 0.025, accent, root, coll, 8))
    elif kind == "mechanic":
        add(panel_x(f"{kind}_Apron", [(-0.19, 0.92), (-0.19, 1.45), (0.19, 1.45), (0.19, 0.92)], 0, 0.025, accent, root, coll))
    else:
        add(box(f"{kind}_Waist_Belt", (0, -0.14, 1.10), (0.40, 0.03, 0.055), accent, root, coll, 0.01))
    for side in (-1, 1):
        shoulder = (side * 0.225, 0, 1.45)
        elbow = (side * 0.31, -0.015, 1.16)
        hand = (side * 0.30, -0.065, 0.91)
        add(sphere(f"{kind}_Shoulder_{side}", shoulder, (0.082, 0.082, 0.092), coat, root, coll, 16, 8))
        add(cylinder_between(f"{kind}_UpperArm_{side}", shoulder, elbow, 0.064, coat, root, coll, 14))
        add(sphere(f"{kind}_Elbow_{side}", elbow, (0.061, 0.061, 0.061), coat, root, coll, 14, 8))
        add(cylinder_between(f"{kind}_Forearm_{side}", elbow, hand, 0.050, coat, root, coll, 14))
        add(sphere(f"{kind}_Hand_{side}", hand, (0.044, 0.034, 0.058), p["skin"], root, coll, 18, 10))
        add(box(f"{kind}_Cuff_{side}", (side * 0.305, -0.052, 0.985), (0.105, 0.095, 0.042), shirt, root, coll, 0.009))
        add(tube(f"{kind}_Sleeve_Seam_{side}", [(side * 0.255, -0.052, 1.40), (side * 0.305, -0.061, 1.20), (side * 0.305, -0.079, 1.00)], 0.006, coat, root, coll, 6))
        for finger in range(4):
            fx = hand[0] + (finger - 1.5) * 0.017
            start = (fx, hand[1] - 0.006, hand[2] - 0.035)
            end = (fx, hand[1] - 0.012, hand[2] - 0.098 + abs(finger - 1.5) * 0.006)
            add(cylinder_between(f"{kind}_Finger_{side}_{finger}", start, end, 0.0065, p["skin"], root, coll, 8))
            add(sphere(f"{kind}_Fingertip_{side}_{finger}", end, (0.007, 0.007, 0.009), p["skin"], root, coll, 10, 6))
        thumb_start = (hand[0] - side * 0.034, hand[1] - 0.012, hand[2] - 0.012)
        thumb_end = (hand[0] - side * 0.058, hand[1] - 0.042, hand[2] - 0.055)
        add(cylinder_between(f"{kind}_Thumb_{side}", thumb_start, thumb_end, 0.008, p["skin"], root, coll, 8))
    add(cylinder(f"{kind}_Neck", (0, 0, 1.56), 0.055, 0.14, p["skin"], root, coll, "Z", 16))
    add(sphere(f"{kind}_Head", (0, -0.005, 1.72), (0.108, 0.095, 0.135), p["skin"], root, coll, 28, 16))
    add(sphere(f"{kind}_Jaw", (0, -0.022, 1.65), (0.085, 0.080, 0.065), p["skin"], root, coll, 22, 12))
    add(sphere(f"{kind}_Nose", (0, -0.103, 1.72), (0.020, 0.032, 0.030), p["skin"], root, coll, 16, 10))
    add(sphere(f"{kind}_Nose_Tip", (0, -0.126, 1.705), (0.024, 0.020, 0.018), p["skin"], root, coll, 14, 8))
    add(tube(f"{kind}_Mouth", [(-0.028, -0.101, 1.655), (0, -0.108, 1.651), (0.028, -0.101, 1.655)], 0.004, p["hair"], root, coll, 6))
    for side in (-1, 1):
        add(sphere(f"{kind}_Ear_{side}", (side * 0.108, 0, 1.72), (0.022, 0.016, 0.034), p["skin"], root, coll, 14, 8))
        add(sphere(f"{kind}_Eye_White_{side}", (side * 0.039, -0.093, 1.755), (0.016, 0.008, 0.011), eye_white, root, coll, 14, 8))
        add(sphere(f"{kind}_Iris_{side}", (side * 0.039, -0.101, 1.755), (0.006, 0.003, 0.006), iris, root, coll, 12, 8))
        add(cylinder_between(f"{kind}_Brow_{side}", (side * 0.022, -0.098, 1.783), (side * 0.060, -0.095, 1.785), 0.005, p["hair"], root, coll, 6))
    add(sphere(f"{kind}_Hair", (0, 0.008, 1.805), (0.110, 0.095, 0.068), p["hair"], root, coll, 22, 12))
    if kind == "driver":
        add(sphere(f"{kind}_Cap", (0, 0, 1.865), (0.14, 0.13, 0.065), p["leather"], root, coll, 20, 10))
        add(box(f"{kind}_Cap_Brim", (0, -0.105, 1.85), (0.24, 0.12, 0.025), p["leather"], root, coll, 0.02))
        for side in (-1, 1):
            add(torus(f"{kind}_Goggle_{side}", (side * 0.047, -0.119, 1.755), 0.029, 0.007, p["brass"], root, coll, "Y", 20, 8))
            add(cylinder(f"{kind}_Goggle_Lens_{side}", (side * 0.047, -0.12, 1.755), 0.024, 0.006, p["glass"], root, coll, "Y", 20, 0.001))
        add(tube(f"{kind}_Scarf", [(-0.09, -0.10, 1.57), (0, -0.15, 1.54), (0.09, -0.10, 1.57)], 0.032, p["cream"], root, coll, 8))
    elif kind == "mechanic":
        add(sphere(f"{kind}_Cap", (0, 0, 1.865), (0.14, 0.13, 0.06), coat, root, coll, 20, 10))
        add(box(f"{kind}_Cap_Brim", (0, -0.11, 1.85), (0.22, 0.12, 0.025), coat, root, coll, 0.02))
        add(cylinder_between(f"{kind}_Moustache", (-0.045, -0.119, 1.665), (0.045, -0.119, 1.665), 0.009, p["hair"], root, coll, 8))
    else:
        add(cylinder(f"{kind}_Hat_Brim", (0, 0, 1.88), 0.22, 0.025, p["wood"], root, coll, "Z", 28))
        add(cylinder(f"{kind}_Hat_Crown", (0, 0, 1.95), 0.12, 0.15, p["wood"], root, coll, "Z", 24))
        add(torus(f"{kind}_Hat_Band", (0, 0, 1.91), 0.123, 0.018, p["brass"], root, coll, "Z", 24, 8))
    return combine_parts(parts, f"CHR_{kind.title()}_1912", root, coll)


def create_rig(name, root, coll):
    data = bpy.data.armatures.new(f"{name}_Armature")
    arm = bpy.data.objects.new(f"{name}_Rig", data)
    link_object(arm, coll)
    arm.parent = root
    arm.show_in_front = True
    bpy.context.view_layer.objects.active = arm
    bpy.ops.object.mode_set(mode="EDIT")
    bones = {
        "Root": ((0, 0, 0), (0, 0, 0.20), None),
        "Pelvis": ((0, 0, 0.90), (0, 0, 1.05), "Root"),
        "Spine": ((0, 0, 1.05), (0, 0, 1.30), "Pelvis"),
        "Chest": ((0, 0, 1.30), (0, 0, 1.50), "Spine"),
        "Neck": ((0, 0, 1.50), (0, 0, 1.61), "Chest"),
        "Head": ((0, 0, 1.61), (0, 0, 1.88), "Neck"),
    }
    for side, sign in (("L", 1), ("R", -1)):
        bones[f"Clavicle.{side}"] = ((0, 0, 1.45), (sign * 0.22, 0, 1.45), "Chest")
        bones[f"UpperArm.{side}"] = ((sign * 0.22, 0, 1.45), (sign * 0.31, -0.015, 1.16), f"Clavicle.{side}")
        bones[f"Forearm.{side}"] = ((sign * 0.31, -0.015, 1.16), (sign * 0.30, -0.065, 0.91), f"UpperArm.{side}")
        bones[f"Hand.{side}"] = ((sign * 0.30, -0.065, 0.91), (sign * 0.30, -0.065, 0.80), f"Forearm.{side}")
        bones[f"Thigh.{side}"] = ((sign * 0.13, 0, 0.96), (sign * 0.14, 0, 0.55), "Pelvis")
        bones[f"Shin.{side}"] = ((sign * 0.14, 0, 0.55), (sign * 0.14, 0, 0.18), f"Thigh.{side}")
        bones[f"Foot.{side}"] = ((sign * 0.14, 0, 0.18), (sign * 0.14, -0.20, 0.10), f"Shin.{side}")
    for bone_name, (head, tail, parent) in bones.items():
        bone = data.edit_bones.new(bone_name)
        bone.head = head
        bone.tail = tail
        if parent:
            bone.parent = data.edit_bones[parent]
    bpy.ops.object.mode_set(mode="OBJECT")
    return arm


def bind_mesh(mesh_obj, arm):
    groups = {bone.name: mesh_obj.vertex_groups.new(name=bone.name) for bone in arm.data.bones}
    for vertex in mesh_obj.data.vertices:
        x, y, z = vertex.co
        side = "L" if x >= 0 else "R"
        if z > 1.60:
            bone = "Head"
        elif z > 1.49:
            bone = "Neck"
        elif abs(x) > 0.225 and z > 1.02:
            if z > 1.31:
                bone = f"UpperArm.{side}"
            elif z > 0.96:
                bone = f"Forearm.{side}"
            else:
                bone = f"Hand.{side}"
        elif z < 0.96 and abs(x) > 0.07:
            if z > 0.55:
                bone = f"Thigh.{side}"
            elif z > 0.18:
                bone = f"Shin.{side}"
            else:
                bone = f"Foot.{side}"
        elif z > 1.30:
            bone = "Chest"
        elif z > 1.05:
            bone = "Spine"
        else:
            bone = "Pelvis"
        groups[bone].add([vertex.index], 1.0, "REPLACE")
    modifier = mesh_obj.modifiers.new("CharacterRig", "ARMATURE")
    modifier.object = arm
    mesh_obj.parent = arm


def reset_pose(arm):
    for bone in arm.pose.bones:
        bone.rotation_mode = "XYZ"
        bone.rotation_euler = (0, 0, 0)
        bone.location = (0, 0, 0)


def create_action(arm, name, frames):
    reset_pose(arm)
    action = bpy.data.actions.new(f"{arm.name}_{name}")
    action.use_fake_user = True
    arm.animation_data_create()
    arm.animation_data.action = action
    for frame, pose in frames:
        bpy.context.scene.frame_set(frame)
        reset_pose(arm)
        for bone_name, rotation in pose.items():
            arm.pose.bones[bone_name].rotation_euler = rotation
            arm.pose.bones[bone_name].keyframe_insert(data_path="rotation_euler", frame=frame)
    return action


def animate(arm):
    actions = [create_action(arm, "Idle", [
        (1, {}),
        (31, {"Chest": (0.02, 0.0, 0.015), "Head": (0.0, 0.02, -0.01)}),
    ])]
    walk_frames = []
    for frame, phase in ((1, 0), (7, 1), (13, 0), (19, -1), (25, 0), (31, 1)):
        walk_frames.append((frame, {
            "Thigh.L": (phase * 0.42, 0, 0),
            "Thigh.R": (-phase * 0.42, 0, 0),
            "Shin.L": (max(0, -phase) * 0.55, 0, 0),
            "Shin.R": (max(0, phase) * 0.55, 0, 0),
            "UpperArm.L": (-phase * 0.35, 0, 0),
            "UpperArm.R": (phase * 0.35, 0, 0),
            "Forearm.L": (0.18, 0, 0),
            "Forearm.R": (0.18, 0, 0),
            "Spine": (0.03, 0, phase * 0.025),
        }))
    actions.append(create_action(arm, "Walk", walk_frames))
    drive_pose = {
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
    actions.append(create_action(arm, "Drive", [(1, drive_pose), (31, drive_pose)]))
    for index, action in enumerate(actions):
        track = arm.animation_data.nla_tracks.new()
        track.name = action.name
        start = 1 + index * 40
        strip = track.strips.new(action.name, start, action)
        strip.frame_end = start + 30
        strip.extrapolation = "NOTHING"
    arm.animation_data.action = None
    bpy.context.scene.frame_set(1)
    reset_pose(arm)


def build_character(kind, index, p):
    slug = f"{kind.title()}_1912"
    coll = collection(f"CHR_{slug}_ASSET")
    root = empty(f"CHR_{slug}_Root", coll=coll)
    set_metadata(root, f"chr_{kind}_1912", "stylized_historical_character", "Original game-art character inspired by 1900–1914 clothing; not a portrait of a real person.")
    body = build_body(kind, root, coll, p)
    arm = create_rig(f"CHR_{slug}", root, coll)
    bind_mesh(body, arm)
    animate(arm)
    bpy.context.scene.frame_start = 1
    bpy.context.scene.frame_end = 120
    bpy.context.scene.frame_set(1)
    bpy.context.view_layer.update()
    ensure_uvs(body)
    export_fbx(root, ASSET_DIR / f"{slug}.fbx", animations=os.environ.get("RB_SKIP_ANIM_BAKE") != "1")
    CHARACTERS.append({
        "id": f"chr_{kind}_1912",
        "file": f"{slug}.fbx",
        "actions": ["Idle", "Walk", "Drive"],
        "height_m": round(asset_stats(root)["dimensions_m"][2], 3),
        "stats": asset_stats(root),
        "notes": "Humanoid rig with 21 bones; manual weights; 30-frame clips at 30 fps."
    })
    return root


def build():
    reset_scene()
    p = palette()
    roots = []
    for i, kind in enumerate(("driver", "mechanic", "lady")):
        root = build_character(kind, i, p)
        roots.append((root, ((i - 1) * 1.2, 0, 0)))
    for root, location in roots:
        root.location = location
    bpy.context.scene.frame_set(1)
    bpy.context.view_layer.update()
    add_studio(target=(0, 0, 1.0), camera=(4.2, -7.0, 3.0), size=5)
    render_preview(PREVIEW_DIR / "Historical_Characters_preview.png", (1400, 900), 48)
    save_blend(ASSET_DIR / "Historical_Characters.blend")
    metadata = {
        "asset_id": "historical_characters_1912",
        "historical_status": "original_stylized_game_art",
        "coordinate_system": {"blender": "meters, Z up, front -Y", "unity_fbx": "meters, Y up, forward +Z"},
        "characters": CHARACTERS,
        "notes": "Clips are baked into each FBX. Import with Rig > Humanoid and remap bones; use Drive clip for open-car seating."
    }
    (ASSET_DIR / "asset_metadata.json").write_text(json.dumps(metadata, ensure_ascii=False, indent=2), encoding="utf-8")
    print("CHARACTERS_BUILD_OK", len(CHARACTERS))


try:
    build()
except Exception as exc:
    print("CHARACTERS_BUILD_ERROR", type(exc).__name__, str(exc))
    traceback.print_exc()
