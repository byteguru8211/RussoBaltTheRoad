from pathlib import Path
import sys
import importlib
import json
import math
import traceback
sys.path.insert(0, str(Path(__file__).resolve().parent))
import blender_assetlib
importlib.reload(blender_assetlib)
from blender_assetlib import *
from mathutils import Matrix

ASSET_DIR = ROOT / "game_assets" / "vehicles" / "Benz_1912_Prototype"
PREVIEW_DIR = ROOT / "previews"


def wheel(code, center, p, root, coll, front=False, spare=False):
    steer = empty(f"BENZ_Wheel_{code}_Steer", center, root, coll, "CIRCLE") if front else None
    spin = empty(f"BENZ_Wheel_{code}_Spin", (0, 0, 0), steer or root, coll, "CIRCLE")
    if not front:
        spin.location = center
    if spare:
        spin.rotation_euler[2] = math.pi / 2
    lathe_x(f"BENZ_Wheel_{code}_Tire", [(-0.064, 0.33), (-0.068, 0.40), (-0.052, 0.448), (-0.035, 0.46), (0.035, 0.46), (0.052, 0.448), (0.068, 0.40), (0.064, 0.33)], p["rubber"], spin, coll, 52)
    lathe_x(f"BENZ_Wheel_{code}_Rim", [(-0.048, 0.30), (-0.054, 0.327), (-0.04, 0.338), (0.04, 0.338), (0.054, 0.327), (0.048, 0.30)], p["wood"], spin, coll, 44)
    torus(f"BENZ_Wheel_{code}_Band", (0, 0, 0), 0.327, 0.012, p["steel"], spin, coll, "X", 44, 8)
    for i in range(16):
        angle = i / 16 * math.tau
        cylinder_between(f"BENZ_Wheel_{code}_Spoke_{i+1:02d}", (0, 0, 0), (0, 0.307 * math.cos(angle), 0.307 * math.sin(angle)), 0.010, p["wood"], spin, coll, 10)
    cylinder(f"BENZ_Wheel_{code}_Hub", (0, 0, 0), 0.078, 0.17, p["brass"], spin, coll, "X", 24)
    for i in range(28):
        angle = i / 28 * math.tau
        box(f"BENZ_Wheel_{code}_Tread_{i+1:02d}", (0, 0.451 * math.cos(angle), 0.451 * math.sin(angle)), (0.12, 0.03, 0.013), p["rubber"], spin, coll, 0.003, (angle - math.pi / 2, 0, 0))
    return steer, spin


def build():
    reset_scene()
    p = palette()
    ivory = material("BENZ_Paint_Ivory", (0.72, 0.70, 0.61), metallic=0.28, roughness=0.31, coat=0.55)
    black = material("BENZ_Paint_Black", (0.018, 0.022, 0.021), metallic=0.38, roughness=0.34, coat=0.4)
    asset_coll = collection("BENZ_1912_ASSET")
    chassis_coll = collection("BENZ_Chassis", asset_coll)
    body_coll = collection("BENZ_Body", asset_coll)
    wheel_coll = collection("BENZ_Wheels", asset_coll)
    cockpit_coll = collection("BENZ_Cockpit", asset_coll)
    detail_coll = collection("BENZ_Details", asset_coll)
    root = empty("BENZ_1912_Racer_Concept_Root", coll=asset_coll)
    set_metadata(root, "benz_1912_racer_concept", "concept_not_exact_replica", "Original concept inspired by 1912 German racers; not a dimensionally exact Blitzen Benz replica.")

    chassis = empty("BENZ_Chassis_Root", parent=root, coll=chassis_coll)
    for side in (-1, 1):
        box(f"BENZ_Chassis_Rail_{side}", (side * 0.36, 0, 0.48), (0.10, 4.15, 0.14), p["steel"], chassis, chassis_coll, 0.012)
        for y in (-1.75, -0.8, 0.25, 1.35, 1.9):
            rivet(f"BENZ_Chassis_Rivet_{side}_{y}", (side * 0.415, y, 0.51), p["brass"], chassis, chassis_coll, 0.011)
    for y in (-1.82, -0.75, 0.35, 1.45, 1.92):
        box(f"BENZ_Chassis_Cross_{y}", (0, y, 0.48), (0.80, 0.09, 0.11), p["steel"], chassis, chassis_coll, 0.008)
    cylinder("BENZ_Front_Axle", (0, -1.65, 0.46), 0.05, 1.46, p["steel"], chassis, chassis_coll, "X", 20)
    cylinder("BENZ_Rear_Axle", (0, 1.70, 0.46), 0.06, 1.46, p["steel"], chassis, chassis_coll, "X", 20)
    sphere("BENZ_Differential", (0, 1.70, 0.46), (0.18, 0.16, 0.16), p["iron"], chassis, chassis_coll)
    for y in (-1.65, 1.70):
        for side in (-1, 1):
            leaf_spring(f"BENZ_LeafSpring_{y}_{side}", y, 0.37, side * 0.49, 1.18, 0.08, p["steel"], chassis, chassis_coll, 4)
    cylinder_between("BENZ_Steering_TieRod", (-0.63, -1.61, 0.42), (0.63, -1.61, 0.42), 0.017, p["steel"], chassis, chassis_coll, 12)
    cylinder_between("BENZ_Steering_DragLink", (0.55, -1.76, 0.42), (0.29, -0.28, 0.55), 0.018, p["steel"], chassis, chassis_coll, 12)

    body = empty("BENZ_Body_Root", parent=root, coll=body_coll)
    loft_y("BENZ_Long_Hood", [(-2.23, 0.34, 0.57, 0.80), (-2.02, 0.40, 0.55, 0.98), (-1.45, 0.43, 0.55, 1.02), (-0.70, 0.45, 0.56, 1.02), (-0.28, 0.46, 0.57, 0.98)], black, body, body_coll)
    box("BENZ_Hood_Seam", (0, -1.25, 1.064), (0.012, 1.72, 0.009), p["brass"], body, body_coll, 0.002)
    for side in (-1, 1):
        panel_x(f"BENZ_Body_Side_{side}", [(-0.28, 0.57), (-0.28, 0.82), (0.08, 0.86), (0.18, 0.76), (1.02, 0.76), (1.20, 0.86), (2.08, 0.78), (2.20, 0.64), (2.12, 0.56)], side * 0.49, 0.05, black, body, body_coll)
        box(f"BENZ_RunningBoard_{side}", (side * 0.70, 0.10, 0.45), (0.26, 2.65, 0.055), p["steel"], body, body_coll, 0.01)
        for i in range(8):
            box(f"BENZ_RunningBoard_Slat_{side}_{i}", (side * 0.70, -1.05 + i * 0.31, 0.485), (0.27, 0.24, 0.03), p["wood"], body, body_coll, 0.006)
        for y in (-1.65, 1.70):
            mudguard(f"BENZ_Mudguard_{y}_{side}", (side * 0.71, y, 0.46), 0.495, 0.24, 0.03, black if y < 0 else ivory, body, body_coll, 12, 168)
    loft_y("BENZ_Rear_Deck", [(1.05, 0.47, 0.57, 0.87), (1.35, 0.49, 0.57, 0.94), (1.92, 0.45, 0.57, 0.88), (2.22, 0.34, 0.57, 0.72)], ivory, body, body_coll)
    box("BENZ_Cockpit_Floor", (0, 0.57, 0.61), (0.90, 1.02, 0.08), p["wood"], body, body_coll, 0.02)

    radiator = empty("BENZ_Radiator_Root", parent=body, coll=body_coll)
    box("BENZ_Radiator_Core", (0, -2.29, 0.85), (0.74, 0.09, 0.72), p["iron"], radiator, body_coll, 0.04)
    for side in (-1, 1):
        box(f"BENZ_Radiator_Side_{side}", (side * 0.42, -2.33, 0.85), (0.055, 0.14, 0.84), p["brass"], radiator, body_coll, 0.02)
    for z in (0.45, 1.25):
        box(f"BENZ_Radiator_Cross_{z}", (0, -2.33, z), (0.85, 0.14, 0.055), p["brass"], radiator, body_coll, 0.02)
    for i in range(14):
        x = -0.36 + i * 0.0554
        cylinder_between(f"BENZ_Radiator_Grid_V_{i}", (x, -2.345, 0.51), (x, -2.345, 1.19), 0.004, p["brass"], radiator, body_coll, 6)
    for i in range(9):
        z = 0.53 + i * 0.08
        cylinder_between(f"BENZ_Radiator_Grid_H_{i}", (-0.37, -2.346, z), (0.37, -2.346, z), 0.004, p["brass"], radiator, body_coll, 6)
    cylinder("BENZ_Radiator_Cap", (0, -2.28, 1.30), 0.065, 0.08, p["brass_bright"], radiator, body_coll, "Z", 20)
    sphere("BENZ_Radiator_Emblem", (0, -2.31, 1.40), (0.06, 0.025, 0.055), p["brass_bright"], radiator, body_coll, 16, 8)
    text_mesh("BENZ_Radiator_Number_1", "1", (0, -2.355, 0.85), 0.33, p["cream"], radiator, body_coll)
    tube("BENZ_Front_Bumper", [(-0.70, -2.42, 0.39), (-0.42, -2.47, 0.37), (0.42, -2.47, 0.37), (0.70, -2.42, 0.39)], 0.027, p["steel"], body, body_coll, 12)

    for side in (-1, 1):
        for i in range(4):
            y = -1.78 + i * 0.34
            box(f"BENZ_Engine_Cylinder_{side}_{i}", (side * 0.43, y, 0.73), (0.20, 0.25, 0.31), p["iron"], body, detail_coll, 0.025)
            cylinder(f"BENZ_Engine_Head_{side}_{i}", (side * 0.43, y, 0.91), 0.105, 0.055, p["steel"], body, detail_coll, "Z", 20)
            tube(f"BENZ_Exhaust_Branch_{side}_{i}", [(side * 0.50, y, 0.80), (side * 0.62, y, 0.69), (side * 0.64, y + 0.10, 0.54)], 0.024, p["steel"], body, detail_coll, 10)
        tube(f"BENZ_Exhaust_Header_{side}", [(side * 0.64, -1.75, 0.54), (side * 0.70, -0.85, 0.56), (side * 0.72, 0.35, 0.58), (side * 0.78, 1.65, 0.62)], 0.038, p["steel"], body, detail_coll, 12)
        for stack_y in (-1.30, -0.72):
            cylinder(f"BENZ_Exhaust_Stack_{side}_{stack_y}", (side * 0.72, stack_y, 1.03), 0.034, 0.62, p["steel"], body, detail_coll, "Z", 14)
            cone(f"BENZ_Exhaust_Stack_Cap_{side}_{stack_y}", (side * 0.72, stack_y, 1.36), 0.045, 0.025, 0.06, p["steel"], body, detail_coll, 12)
        cylinder(f"BENZ_Headlamp_{side}", (side * 0.66, -2.12, 1.00), 0.17, 0.22, p["brass"], body, detail_coll, "Y", 28)
        cylinder(f"BENZ_Headlamp_Glass_{side}", (side * 0.66, -2.24, 1.00), 0.147, 0.012, p["glass"], body, detail_coll, "Y", 28)
        torus(f"BENZ_Headlamp_Rim_{side}", (side * 0.66, -2.25, 1.00), 0.153, 0.012, p["brass_bright"], body, detail_coll, "Y", 28, 8)
        cylinder(f"BENZ_Headlamp_Chimney_{side}", (side * 0.66, -2.11, 1.20), 0.03, 0.10, p["brass"], body, detail_coll, "Z", 16)
        cylinder(f"BENZ_Side_Number_{side}", (side * 0.525, 0.55, 0.74), 0.18, 0.012, p["cream"], body, body_coll, "X", 32, 0.002)
        text_mesh(f"BENZ_Side_Number_1_{side}", "1", (side * 0.535, 0.55, 0.74), 0.23, black, body, body_coll, (math.pi / 2, 0, side * math.pi / 2), 0.001)
        torus(f"BENZ_Rear_Sprocket_{side}", (side * 0.25, 1.70, 0.46), 0.17, 0.025, p["steel"], chassis, chassis_coll, "X", 28, 8)
        tube(f"BENZ_Drive_Chain_{side}", [(side * 0.25, -0.05, 0.60), (side * 0.25, 1.70, 0.64), (side * 0.25, 1.87, 0.46), (side * 0.25, 1.70, 0.28), (side * 0.25, -0.05, 0.32), (side * 0.25, -0.22, 0.46), (side * 0.25, -0.05, 0.60)], 0.016, p["steel"], chassis, chassis_coll, 8)

    cockpit = empty("BENZ_Cockpit_Root", parent=root, coll=cockpit_coll)
    box("BENZ_Dashboard", (0, -0.17, 0.96), (0.88, 0.07, 0.22), p["wood"], cockpit, cockpit_coll, 0.02)
    for i, x in enumerate((-0.20, 0.0, 0.20)):
        cylinder(f"BENZ_Gauge_Bezel_{i}", (x, -0.215, 0.99), 0.05, 0.018, p["brass"], cockpit, cockpit_coll, "Y", 24)
        cylinder(f"BENZ_Gauge_Face_{i}", (x, -0.227, 0.99), 0.041, 0.006, p["cream"], cockpit, cockpit_coll, "Y", 24)
    for side in (-1, 1):
        box(f"BENZ_Seat_Cushion_{side}", (side * 0.25, 0.58, 0.71), (0.41, 0.44, 0.13), p["leather"], cockpit, cockpit_coll, 0.05)
        box(f"BENZ_Seat_Back_{side}", (side * 0.25, 0.84, 0.90), (0.41, 0.11, 0.42), p["leather"], cockpit, cockpit_coll, 0.05, (-0.14, 0, 0))
    steering_pivot = empty("BENZ_SteeringWheel_Pivot", (0.29, 0.17, 1.06), cockpit, cockpit_coll, "CIRCLE")
    steering_rot = (math.radians(62), 0, 0)
    torus("BENZ_SteeringWheel_Rim", (0, 0, 0), 0.18, 0.014, p["wood"], steering_pivot, cockpit_coll, "Z", 36, 10).rotation_euler = steering_rot
    rot_matrix = Matrix.Rotation(steering_rot[0], 3, "X")
    for angle in (0, math.tau / 3, 2 * math.tau / 3):
        cylinder_between(f"BENZ_SteeringWheel_Spoke_{angle}", rot_matrix @ Vector((0, 0, 0)), rot_matrix @ Vector((0.17 * math.cos(angle), 0.17 * math.sin(angle), 0)), 0.009, p["brass"], steering_pivot, cockpit_coll, 10)
    cylinder_between("BENZ_Steering_Column", (0.29, -0.16, 0.91), (0.29, 0.17, 1.06), 0.018, p["steel"], cockpit, cockpit_coll, 14)
    for code, x in (("Clutch", 0.32), ("Brake", 0.15), ("Throttle", -0.02)):
        pedal = empty(f"BENZ_{code}_Pedal_Pivot", (x, -0.05, 0.62), cockpit, cockpit_coll)
        box(f"BENZ_{code}_Pedal", (0, -0.07, 0.04), (0.075, 0.17, 0.025), p["steel"], pedal, cockpit_coll, 0.006, (0.35, 0, 0))
    gear = empty("BENZ_GearLever_Pivot", (-0.10, 0.22, 0.63), cockpit, cockpit_coll)
    cylinder_between("BENZ_GearLever", (0, 0, 0), (0, 0.08, 0.30), 0.012, p["steel"], gear, cockpit_coll, 12)
    sphere("BENZ_GearKnob", (0, 0.08, 0.32), (0.034, 0.034, 0.043), p["wood"], gear, cockpit_coll, 16, 8)

    for code, x, y, front in (("FL", -0.70, -1.65, True), ("FR", 0.70, -1.65, True), ("RL", -0.70, 1.70, False), ("RR", 0.70, 1.70, False)):
        wheel(code, (x, y, 0.46), p, root, wheel_coll, front)
        if not front:
            cylinder(f"BENZ_Rear_Brake_{code}", (x * 0.80, y, 0.46), 0.16, 0.08, p["iron"], chassis, chassis_coll, "X", 24)
    cylinder("BENZ_Rear_Fuel_Tank", (0, 2.05, 0.84), 0.31, 0.62, ivory, body, detail_coll, "Y", 28)
    for y in (1.80, 2.30):
        torus(f"BENZ_Fuel_Tank_Band_{y}", (0, y, 0.84), 0.315, 0.018, p["brass"], body, detail_coll, "Y", 28, 8)
    cylinder("BENZ_Fuel_Filler", (0, 2.05, 1.18), 0.055, 0.07, p["brass"], body, detail_coll, "Z", 18)

    collision_coll = collection("BENZ_1912_COLLISION")
    collision_root = empty("BENZ_1912_Racer_Concept_Collision_Root", coll=collision_coll)
    box("UCX_Benz_1912_Racer_Concept_Hull", (0, 0, 0.67), (1.52, 4.70, 1.08), p["steel"], collision_root, collision_coll, 0)
    for code, x, y in (("FL", -0.70, -1.65), ("FR", 0.70, -1.65), ("RL", -0.70, 1.70), ("RR", 0.70, 1.70)):
        cylinder(f"UCX_Benz_1912_Racer_Concept_Wheel_{code}", (x, y, 0.46), 0.46, 0.14, p["steel"], collision_root, collision_coll, "X", 16, 0)

    bpy.context.view_layer.update()
    ensure_uvs(root)
    detailed_stats = asset_stats(root)
    add_studio(target=(0, 0, 0.74), camera=(6.6, -8.0, 3.8), size=8)
    export_fbx(root, ASSET_DIR / "Benz_1912_Racer_Concept_Detailed.fbx")
    export_fbx(collision_root, ASSET_DIR / "Benz_1912_Racer_Concept_Collision.fbx")
    optimized0 = optimize_hierarchy(root, "_OPT", 0.72)
    stats = asset_stats(optimized0)
    export_fbx_clean_nodes(optimized0, ASSET_DIR / "Benz_1912_Racer_Concept_LOD0.fbx", ("_OPT",))
    lod1 = duplicate_lod(optimized0, "_LOD1", 0.55)
    export_fbx_clean_nodes(lod1, ASSET_DIR / "Benz_1912_Racer_Concept_LOD1.fbx", ("_OPT", "_LOD1"))
    lod2 = duplicate_lod(optimized0, "_LOD2", 0.28)
    export_fbx_clean_nodes(lod2, ASSET_DIR / "Benz_1912_Racer_Concept_LOD2.fbx", ("_OPT", "_LOD2"))
    for hidden_root in (optimized0, lod1, lod2):
        for obj in descendants(hidden_root):
            obj.hide_render = True
            obj.hide_viewport = True
    for obj in descendants(collision_root):
        obj.hide_render = True
        obj.hide_viewport = True
    render_preview(PREVIEW_DIR / "Benz_1912_Racer_Concept_preview.png", (1400, 900), 48)
    save_blend(ASSET_DIR / "Benz_1912_Racer_Concept.blend")
    metadata = {
        "asset_id": "benz_1912_racer_concept",
        "display_name": "Blitzen Benz 1911 — original racing concept",
        "historical_status": "concept_not_exact_replica",
        "inspiration": "Blitzen Benz 1911–1912: black long bonnet, exposed four-cylinder engine, individual exhaust stacks, chain drive and rear fuel tank; no claim of exact reconstruction",
        "assumptions": {"length_m": 4.8, "width_m": 1.7, "wheelbase_m": 3.35, "drive": "RWD chain drive visualization", "brakes": "rear-only mechanical", "steering": "left-hand drive"},
        "coordinate_system": {"blender": "meters, Z up, front -Y", "unity_fbx": "meters, Y up, forward +Z"},
        "stats": stats,
        "stats_detailed": detailed_stats,
        "exports": ["Benz_1912_Racer_Concept_LOD0.fbx", "Benz_1912_Racer_Concept_LOD1.fbx", "Benz_1912_Racer_Concept_LOD2.fbx", "Benz_1912_Racer_Concept_Detailed.fbx", "Benz_1912_Racer_Concept_Collision.fbx"],
        "textures": "game_assets/textures/shared (CC0 procedural maps, packed into BLEND/FBX)"
    }
    (ASSET_DIR / "asset_metadata.json").write_text(json.dumps(metadata, ensure_ascii=False, indent=2), encoding="utf-8")
    print("BENZ_BUILD_OK", json.dumps({"detailed": detailed_stats, "optimized": stats}, ensure_ascii=False))


try:
    build()
except Exception as exc:
    print("BENZ_BUILD_ERROR", type(exc).__name__, str(exc))
    traceback.print_exc()
