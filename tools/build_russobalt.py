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

ASSET_DIR = ROOT / "game_assets" / "vehicles" / "RussoBalt_C24_55"
PREVIEW_DIR = ROOT / "previews"


def build_wheel(code, center, p, root, coll, front=False, spare=False):
    sx, sy, sz = center
    steer = None
    if front:
        steer = empty(f"RB_Wheel_{code}_Steer", center, root, coll, "CIRCLE")
    spin = empty(f"RB_Wheel_{code}_Spin", (0, 0, 0), steer or root, coll, "CIRCLE")
    if not front:
        spin.location = center
    if spare:
        spin.rotation_euler[2] = math.pi / 2
    tire_profile = [(-0.060, 0.315), (-0.064, 0.382), (-0.052, 0.423), (-0.036, 0.44), (0.036, 0.44), (0.052, 0.423), (0.064, 0.382), (0.060, 0.315)]
    lathe_x(f"RB_Wheel_{code}_Tire", tire_profile, p["rubber"], spin, coll, 56)
    rim_profile = [(-0.046, 0.285), (-0.052, 0.309), (-0.040, 0.322), (0.040, 0.322), (0.052, 0.309), (0.046, 0.285)]
    lathe_x(f"RB_Wheel_{code}_WoodRim", rim_profile, p["wood"], spin, coll, 48)
    torus(f"RB_Wheel_{code}_RimBand", (0, 0, 0), 0.311, 0.012, p["brass"], spin, coll, "X", 48, 8)
    for i in range(16):
        angle = i / 16 * math.tau
        end = (0, 0.292 * math.cos(angle), 0.292 * math.sin(angle))
        cylinder_between(f"RB_Wheel_{code}_Spoke_{i+1:02d}", (0, 0, 0), end, 0.011, p["wood"], spin, coll, 10)
    cylinder(f"RB_Wheel_{code}_Hub", (0, 0, 0), 0.073, 0.16, p["brass"], spin, coll, "X", 24)
    cylinder(f"RB_Wheel_{code}_HubCap", (0.088 if not spare else 0, 0, 0), 0.049, 0.025, p["brass_bright"], spin, coll, "X", 24)
    for i in range(32):
        angle = i / 32 * math.tau
        loc = (0, 0.431 * math.cos(angle), 0.431 * math.sin(angle))
        block = box(f"RB_Wheel_{code}_Tread_{i+1:02d}", loc, (0.116, 0.032, 0.014), p["rubber"], spin, coll, 0.003, (angle - math.pi / 2, 0, 0))
    for i in range(6):
        angle = i / 6 * math.tau
        rivet(f"RB_Wheel_{code}_Nut_{i+1:02d}", (0.086, 0.056 * math.cos(angle), 0.056 * math.sin(angle)), p["steel"], spin, coll, 0.009)
    return steer, spin


def build_russobalt():
    reset_scene()
    p = palette()
    asset_coll = collection("RB_C24_55_ASSET")
    chassis_coll = collection("RB_Chassis", asset_coll)
    body_coll = collection("RB_Body", asset_coll)
    wheel_coll = collection("RB_Wheels", asset_coll)
    cockpit_coll = collection("RB_Cockpit", asset_coll)
    detail_coll = collection("RB_Details", asset_coll)
    root = empty("RB_C24_55_Root", (0, 0, 0), None, asset_coll, "CUBE")
    set_metadata(root, "russo_balt_c24_55_1912", "research_based_reconstruction", "Original car lost; green film-inspired livery; dimensions and wheelbase are approximate.")

    chassis = empty("RB_Chassis_Root", parent=root, coll=chassis_coll)
    for side in (-1, 1):
        box(f"RB_Chassis_Rail_{'L' if side > 0 else 'R'}", (side * 0.34, 0, 0.46), (0.09, 3.84, 0.13), p["steel"], chassis, chassis_coll, 0.012)
        for y in (-1.65, -0.85, 0.15, 1.2, 1.72):
            rivet(f"RB_Chassis_Rivet_{side}_{y}", (side * 0.395, y, 0.49), p["brass"], chassis, chassis_coll, 0.011)
    for y in (-1.72, -0.9, 0.1, 1.18, 1.75):
        box(f"RB_Chassis_Crossmember_{y}", (0, y, 0.46), (0.76, 0.08, 0.1), p["steel"], chassis, chassis_coll, 0.008)
    cylinder("RB_Front_Axle", (0, -1.60, 0.44), 0.046, 1.38, p["steel"], chassis, chassis_coll, "X", 20)
    cylinder("RB_Rear_Axle", (0, 1.60, 0.44), 0.058, 1.38, p["steel"], chassis, chassis_coll, "X", 20)
    sphere("RB_Rear_Differential", (0, 1.60, 0.44), (0.17, 0.15, 0.15), p["iron"], chassis, chassis_coll)
    cylinder_between("RB_Driveshaft", (0, -0.62, 0.42), (0, 1.47, 0.42), 0.033, p["steel"], chassis, chassis_coll, 16)
    for axle_y in (-1.60, 1.60):
        for side in (-1, 1):
            leaf_spring(f"RB_LeafSpring_{axle_y}_{side}", axle_y, 0.36, side * 0.47, 1.12 if axle_y < 0 else 1.24, 0.075, p["steel"], chassis, chassis_coll, 4)
            for offset in (-0.48, 0.48):
                cylinder(f"RB_Spring_Shackle_{axle_y}_{side}_{offset}", (side * 0.47, axle_y + offset, 0.39), 0.025, 0.11, p["brass"], chassis, chassis_coll, "X", 12)
    cylinder_between("RB_Steering_DragLink", (-0.56, -1.74, 0.40), (-0.30, -0.30, 0.51), 0.018, p["steel"], chassis, chassis_coll, 12)
    cylinder_between("RB_Steering_TieRod", (-0.60, -1.56, 0.40), (0.60, -1.56, 0.40), 0.016, p["steel"], chassis, chassis_coll, 12)
    box("RB_Steering_GearBox", (-0.30, -0.24, 0.54), (0.16, 0.18, 0.18), p["iron"], chassis, chassis_coll, 0.02)
    tube("RB_Exhaust_Pipe", [(0.40, -1.28, 0.40), (0.53, -0.92, 0.34), (0.58, 0.15, 0.32), (0.57, 1.42, 0.31), (0.70, 1.82, 0.35)], 0.026, p["steel"], chassis, chassis_coll, 12)
    cylinder("RB_Exhaust_Muffler", (0.57, 0.72, 0.32), 0.075, 0.52, p["iron"], chassis, chassis_coll, "Y", 20)
    box("RB_Fuel_Tank", (0, 1.73, 0.38), (0.64, 0.48, 0.22), p["iron"], chassis, chassis_coll, 0.04)
    cylinder("RB_Fuel_Filler", (0.22, 1.73, 0.52), 0.045, 0.05, p["brass"], chassis, chassis_coll, "Z", 16)

    body = empty("RB_Body_Root", parent=root, coll=body_coll)
    loft_y("RB_Hood", [(-1.98, 0.40, 0.54, 0.84), (-1.82, 0.46, 0.52, 1.01), (-1.25, 0.50, 0.52, 1.045), (-0.55, 0.515, 0.53, 1.05), (-0.12, 0.52, 0.55, 1.01)], p["green"], body, body_coll)
    box("RB_Hood_Center_Seam", (0, -1.05, 1.052), (0.012, 1.62, 0.009), p["brass"], body, body_coll, 0.002)
    for side in (-1, 1):
        for i in range(10):
            y = -1.48 + i * 0.105
            louver = box(f"RB_Hood_Louver_{side}_{i+1:02d}", (side * 0.506, y, 0.86), (0.009, 0.038, 0.19), p["green_dark"], body, body_coll, 0.006, (0, 0, side * 0.10))
            cylinder_between(f"RB_Hood_Louver_Hinge_{side}_{i+1:02d}", (side * 0.513, y - 0.02, 0.95), (side * 0.513, y + 0.02, 0.95), 0.006, p["brass"], body, body_coll, 8)
    for y in (-1.58, -0.70):
        box(f"RB_Hood_Strap_Top_{y}", (0, y, 1.062), (0.09, 0.045, 0.012), p["leather"], body, body_coll, 0.004)
        for side in (-1, 1):
            box(f"RB_Hood_Strap_Side_{y}_{side}", (side * 0.515, y, 0.80), (0.012, 0.045, 0.48), p["leather"], body, body_coll, 0.004)
            box(f"RB_Hood_Strap_Buckle_{y}_{side}", (side * 0.525, y, 0.82), (0.018, 0.062, 0.055), p["brass"], body, body_coll, 0.004)
    side_polygon = [(-0.12, 0.55), (-0.12, 0.91), (0.04, 0.96), (0.12, 0.84), (1.00, 0.84), (1.16, 0.96), (1.94, 0.89), (2.10, 0.73), (2.03, 0.55), (1.00, 0.55)]
    for side in (-1, 1):
        panel_x(f"RB_Body_Side_{'L' if side > 0 else 'R'}", side_polygon, side * 0.515, 0.045, p["green"], body, body_coll)
        for y in (-0.02, 0.18, 1.12, 1.35, 1.62, 1.88):
            rivet(f"RB_Body_Rivet_{side}_{y}", (side * 0.545, y, 0.62), p["brass"], body, body_coll, 0.009)
    loft_y("RB_Rear_Deck", [(1.00, 0.50, 0.55, 0.86), (1.25, 0.53, 0.55, 0.94), (1.72, 0.51, 0.55, 0.90), (2.08, 0.40, 0.55, 0.75)], p["green"], body, body_coll)
    box("RB_Cockpit_Floor", (0, 0.55, 0.59), (0.92, 1.02, 0.08), p["wood"], body, body_coll, 0.025)
    for side in (-1, 1):
        box(f"RB_RunningBoard_Frame_{side}", (side * 0.69, 0, 0.43), (0.24, 2.55, 0.055), p["steel"], body, body_coll, 0.012)
        for i in range(9):
            box(f"RB_RunningBoard_Slat_{side}_{i+1:02d}", (side * 0.69, -1.12 + i * 0.28, 0.465), (0.25, 0.22, 0.035), p["wood"], body, body_coll, 0.008)
        for axle_y in (-1.60, 1.60):
            mudguard(f"RB_Mudguard_{'F' if axle_y < 0 else 'R'}_{side}", (side * 0.69, axle_y, 0.44), 0.475, 0.25, 0.032, p["green"], body, body_coll)
            for yoff in (-0.37, 0.37):
                cylinder_between(f"RB_Mudguard_Stay_{axle_y}_{side}_{yoff}", (side * 0.61, axle_y + yoff, 0.46), (side * 0.69, axle_y + yoff * 1.15, 0.77), 0.012, p["steel"], body, body_coll, 10)

    radiator = empty("RB_Radiator_Root", parent=body, coll=body_coll)
    box("RB_Radiator_Core", (0, -2.035, 0.82), (0.72, 0.08, 0.67), p["iron"], radiator, body_coll, 0.035)
    for side in (-1, 1):
        box(f"RB_Radiator_Side_{side}", (side * 0.405, -2.075, 0.82), (0.052, 0.13, 0.80), p["brass"], radiator, body_coll, 0.018)
    for z in (0.435, 1.205):
        box(f"RB_Radiator_Cross_{z}", (0, -2.075, z), (0.82, 0.13, 0.052), p["brass"], radiator, body_coll, 0.018)
    for i in range(15):
        x = -0.345 + i * 0.0493
        cylinder_between(f"RB_Radiator_Grid_V_{i+1:02d}", (x, -2.086, 0.49), (x, -2.086, 1.15), 0.004, p["brass"], radiator, body_coll, 6)
    for i in range(10):
        z = 0.50 + i * 0.071
        cylinder_between(f"RB_Radiator_Grid_H_{i+1:02d}", (-0.35, -2.087, z), (0.35, -2.087, z), 0.004, p["brass"], radiator, body_coll, 6)
    cylinder("RB_Radiator_Cap", (0, -2.03, 1.25), 0.06, 0.07, p["brass_bright"], radiator, body_coll, "Z", 20)
    sphere("RB_Radiator_Eagle_Body", (0, -2.04, 1.34), (0.033, 0.026, 0.070), p["brass_bright"], radiator, body_coll, 16, 8)
    for side in (-1, 1):
        wing = mesh(f"RB_Radiator_Eagle_Wing_{side}", [(0, -2.04, 1.35), (side * 0.13, -2.04, 1.39), (side * 0.10, -2.04, 1.32), (side * 0.025, -2.04, 1.31)], [(0, 1, 2, 3)], p["brass_bright"], radiator, body_coll, False)
    text_mesh("RB_Radiator_Number_6", "6", (0, -2.098, 0.83), 0.31, p["cream"], radiator, body_coll)
    tube("RB_Front_Bumper", [(-0.68, -2.23, 0.37), (-0.45, -2.27, 0.35), (0.45, -2.27, 0.35), (0.68, -2.23, 0.37)], 0.026, p["steel"], body, body_coll, 12)
    for side in (-1, 1):
        cylinder_between(f"RB_Bumper_Bracket_{side}", (side * 0.42, -2.18, 0.37), (side * 0.34, -1.92, 0.47), 0.022, p["steel"], body, body_coll, 12)

    for side in (-1, 1):
        x = side * 0.63
        cylinder(f"RB_Headlamp_Housing_{side}", (x, -1.89, 0.96), 0.165, 0.22, p["brass"], body, detail_coll, "Y", 32, 0.012)
        cylinder(f"RB_Headlamp_Glass_{side}", (x, -2.012, 0.96), 0.143, 0.012, p["glass"], body, detail_coll, "Y", 32, 0.003)
        torus(f"RB_Headlamp_Rim_{side}", (x, -2.022, 0.96), 0.151, 0.012, p["brass_bright"], body, detail_coll, "Y", 32, 8)
        cylinder(f"RB_Headlamp_Chimney_{side}", (x, -1.88, 1.16), 0.032, 0.11, p["brass"], body, detail_coll, "Z", 16)
        sphere(f"RB_Headlamp_Flame_{side}", (x, -1.99, 0.97), (0.055, 0.018, 0.055), p["lamp"], body, detail_coll, 16, 8)
        cylinder_between(f"RB_Headlamp_Bracket_{side}", (side * 0.48, -1.88, 0.74), (x, -1.88, 0.87), 0.018, p["steel"], body, detail_coll, 12)
        cylinder(f"RB_Side_Lamp_{side}", (side * 0.70, -0.76, 0.83), 0.085, 0.13, p["brass"], body, detail_coll, "Y", 24)
        cylinder(f"RB_Side_Lamp_Glass_{side}", (side * 0.70, -0.835, 0.83), 0.071, 0.008, p["glass"], body, detail_coll, "Y", 24)
    cylinder("RB_Horn_Bulb", (-0.70, -0.28, 0.82), 0.095, 0.16, p["brass"], body, detail_coll, "Y", 24)
    sphere("RB_Horn_Bulb_End", (-0.70, -0.38, 0.82), (0.095, 0.06, 0.095), p["brass"], body, detail_coll, 20, 10)

    cockpit = empty("RB_Cockpit_Root", parent=root, coll=cockpit_coll)
    box("RB_Dashboard", (0, -0.015, 0.93), (0.92, 0.075, 0.23), p["wood"], cockpit, cockpit_coll, 0.025)
    for i, x in enumerate((-0.22, 0.0, 0.22)):
        cylinder(f"RB_Gauge_Bezel_{i}", (x, -0.06, 0.965), 0.052, 0.018, p["brass"], cockpit, cockpit_coll, "Y", 24)
        cylinder(f"RB_Gauge_Face_{i}", (x, -0.072, 0.965), 0.043, 0.006, p["cream"], cockpit, cockpit_coll, "Y", 24)
        cylinder_between(f"RB_Gauge_Needle_{i}", (x, -0.078, 0.965), (x + 0.018, -0.078, 0.982), 0.003, p["steel"], cockpit, cockpit_coll, 6)
    for side in (-1, 1):
        x = side * 0.265
        box(f"RB_Seat_Cushion_{side}", (x, 0.61, 0.69), (0.43, 0.45, 0.13), p["leather"], cockpit, cockpit_coll, 0.06)
        box(f"RB_Seat_Back_{side}", (x, 0.88, 0.88), (0.43, 0.11, 0.43), p["leather"], cockpit, cockpit_coll, 0.055, (-0.14, 0, 0))
        for yy in (0.50, 0.64, 0.77):
            for xx in (x - 0.11, x + 0.11):
                rivet(f"RB_Seat_Button_{side}_{xx}_{yy}", (xx, yy, 0.765), p["brass"], cockpit, cockpit_coll, 0.009)
    steering_pivot = empty("RB_SteeringWheel_Pivot", (-0.31, 0.30, 1.14), cockpit, cockpit_coll, "CIRCLE")
    steering_rotation = (math.radians(76), 0, 0)
    torus("RB_SteeringWheel_Rim", (0, 0, 0), 0.185, 0.014, p["wood"], steering_pivot, cockpit_coll, "Z", 40, 10).rotation_euler = steering_rotation
    for angle in (0, math.tau / 3, 2 * math.tau / 3):
        end = (0.172 * math.cos(angle), 0.172 * math.sin(angle), 0)
        start = (0, 0, 0)
        a = Vector(start)
        b = Vector(end)
        rot = steering_rotation
        mat = __import__("mathutils").Matrix.Rotation(rot[0], 3, "X")
        a = mat @ a
        b = mat @ b
        cylinder_between(f"RB_SteeringWheel_Spoke_{angle}", a, b, 0.009, p["brass"], steering_pivot, cockpit_coll, 10)
    cylinder("RB_SteeringWheel_Hub", (0, 0, 0), 0.035, 0.04, p["brass"], steering_pivot, cockpit_coll, "Z", 20).rotation_euler = steering_rotation
    cylinder_between("RB_Steering_Column", (-0.31, -0.02, 0.88), (-0.31, 0.30, 1.14), 0.018, p["steel"], cockpit, cockpit_coll, 14)
    for code, x in (("Clutch", -0.34), ("Brake", -0.16), ("Throttle", 0.02)):
        pedal_pivot = empty(f"RB_{code}_Pedal_Pivot", (x, 0.04, 0.59), cockpit, cockpit_coll)
        box(f"RB_{code}_Pedal", (0, -0.08, 0.04), (0.075, 0.18, 0.025), p["steel"], pedal_pivot, cockpit_coll, 0.008, (0.35, 0, 0))
        cylinder_between(f"RB_{code}_Pedal_Arm", (0, 0, 0), (0, 0.13, -0.03), 0.012, p["steel"], pedal_pivot, cockpit_coll, 10)
    gear_pivot = empty("RB_GearLever_Pivot", (0.10, 0.27, 0.61), cockpit, cockpit_coll)
    cylinder_between("RB_GearLever", (0, 0, 0), (0, 0.08, 0.31), 0.012, p["steel"], gear_pivot, cockpit_coll, 12)
    sphere("RB_GearKnob", (0, 0.08, 0.33), (0.035, 0.035, 0.045), p["wood"], gear_pivot, cockpit_coll, 16, 8)
    cylinder_between("RB_HandBrake_Lever", (0.36, 0.40, 0.61), (0.36, 0.30, 0.92), 0.013, p["steel"], cockpit, cockpit_coll, 12)
    sphere("RB_HandBrake_Knob", (0.36, 0.30, 0.94), (0.028, 0.028, 0.035), p["wood"], cockpit, cockpit_coll, 16, 8)
    windshield_pivot = empty("RB_Windshield_Pivot", (0, -0.08, 1.00), cockpit, cockpit_coll)
    for side in (-1, 1):
        cylinder_between(f"RB_Windshield_Frame_{side}", (side * 0.41, 0, 0), (side * 0.41, 0, 0.39), 0.012, p["brass"], windshield_pivot, cockpit_coll, 12)
    cylinder_between("RB_Windshield_Top", (-0.41, 0, 0.39), (0.41, 0, 0.39), 0.012, p["brass"], windshield_pivot, cockpit_coll, 12)
    cylinder_between("RB_Windshield_Center", (0, 0, 0), (0, 0, 0.39), 0.009, p["brass"], windshield_pivot, cockpit_coll, 10)
    for side in (-1, 1):
        box(f"RB_Windshield_Glass_{side}", (side * 0.205, 0, 0.20), (0.38, 0.006, 0.35), p["glass"], windshield_pivot, cockpit_coll, 0.008)
    windshield_pivot.rotation_euler[0] = math.radians(82)

    box("RB_Toolbox", (0.43, 1.25, 0.72), (0.34, 0.55, 0.26), p["wood"], root, detail_coll, 0.025)
    for x in (0.285, 0.575):
        for y in (1.01, 1.49):
            box(f"RB_Toolbox_Corner_{x}_{y}", (x, y, 0.72), (0.035, 0.035, 0.28), p["brass"], root, detail_coll, 0.004)
    box("RB_Toolbox_Lid", (0.43, 1.25, 0.865), (0.37, 0.58, 0.035), p["wood"], root, detail_coll, 0.012)
    cylinder("RB_Toolbox_Handle", (0.43, 1.25, 0.91), 0.018, 0.18, p["steel"], root, detail_coll, "X", 12)
    for side in (-1, 1):
        cylinder(f"RB_Number_Roundel_{side}", (side * 0.548, 0.48, 0.73), 0.17, 0.012, p["cream"], body, body_coll, "X", 32, 0.002)
        text_mesh(f"RB_Side_Number_6_{side}", "6", (side * 0.558, 0.48, 0.73), 0.22, p["green_dark"], body, body_coll, (math.pi / 2, 0, side * math.pi / 2), 0.001)
    tube("RB_SpareWheel_Strap", [(-0.18, 2.12, 0.98), (0, 2.27, 1.17), (0.18, 2.12, 0.98)], 0.018, p["leather"], body, detail_coll, 8)

    for code, x, y, front in (("FL", -0.6825, -1.60, True), ("FR", 0.6825, -1.60, True), ("RL", -0.6825, 1.60, False), ("RR", 0.6825, 1.60, False)):
        build_wheel(code, (x, y, 0.44), p, root, wheel_coll, front)
        if not front:
            cylinder(f"RB_Rear_Brake_Drum_{code}", (x * 0.78, y, 0.44), 0.15, 0.08, p["iron"], chassis, chassis_coll, "X", 24)
    build_wheel("SPARE", (0, 2.24, 0.73), p, root, wheel_coll, False, True)

    collision_coll = collection("RB_C24_55_COLLISION")
    collision_root = empty("RB_C24_55_Collision_Root", coll=collision_coll)
    box("UCX_RussoBalt_C24_55_Hull", (0, 0, 0.64), (1.48, 4.30, 1.05), p["steel"], collision_root, collision_coll, 0)
    for code, x, y in (("FL", -0.6825, -1.60), ("FR", 0.6825, -1.60), ("RL", -0.6825, 1.60), ("RR", 0.6825, 1.60)):
        cylinder(f"UCX_RussoBalt_C24_55_Wheel_{code}", (x, y, 0.44), 0.44, 0.13, p["steel"], collision_root, collision_coll, "X", 16, 0)

    bpy.context.view_layer.update()
    ensure_uvs(root)
    detailed_stats = asset_stats(root)
    add_studio(target=(0, 0, 0.72), camera=(6.3, -7.8, 3.7), size=7.5)
    export_fbx(root, ASSET_DIR / "RussoBalt_C24_55_Detailed.fbx")
    export_fbx(collision_root, ASSET_DIR / "RussoBalt_C24_55_Collision.fbx")
    optimized0 = optimize_hierarchy(root, "_OPT", 0.72)
    stats = asset_stats(optimized0)
    export_fbx_clean_nodes(optimized0, ASSET_DIR / "RussoBalt_C24_55_LOD0.fbx", ("_OPT",))
    lod1 = duplicate_lod(optimized0, "_LOD1", 0.55)
    export_fbx_clean_nodes(lod1, ASSET_DIR / "RussoBalt_C24_55_LOD1.fbx", ("_OPT", "_LOD1"))
    lod2 = duplicate_lod(optimized0, "_LOD2", 0.28)
    export_fbx_clean_nodes(lod2, ASSET_DIR / "RussoBalt_C24_55_LOD2.fbx", ("_OPT", "_LOD2"))
    for hidden_root in (optimized0, lod1, lod2):
        for obj in descendants(hidden_root):
            obj.hide_render = True
            obj.hide_viewport = True
    for obj in descendants(collision_root):
        obj.hide_render = True
        obj.hide_viewport = True
    render_preview(PREVIEW_DIR / "RussoBalt_C24_55_preview.png", (1400, 900), 48)
    save_blend(ASSET_DIR / "RussoBalt_C24_55.blend")
    metadata = {
        "asset_id": "russo_balt_c24_55_1912",
        "display_name": "Russo-Balt C24/55 «Monako» — research reconstruction",
        "historical_status": "reconstruction",
        "source_basis": ["1912 Monte Carlo public-domain photograph", "1913 C24/58 race photograph", "CC0 film replicas", "museum and motorsport histories"],
        "confirmed": {"engine_cc": 4939, "power_hp_approx": 55, "drive": "RWD", "gears": 3, "brakes": "rear mechanical drums", "steering": "right-hand drive", "suspension": "solid axles, semi-elliptic leaf springs", "tire_size_mm": "880x120"},
        "assumptions": {"length_m": 4.435, "width_body_m": 1.544, "height_m": 1.23, "wheelbase_m": 3.20, "mass_kg": 1300, "paint": "film-inspired racing green; original rally color unproven"},
        "coordinate_system": {"blender": "meters, Z up, front -Y", "unity_fbx": "meters, Y up, forward +Z", "pivot": "ground center between axles"},
        "unity_nodes": ["Wheel_FL_Steer", "Wheel_FR_Steer", "Wheel_FL_Spin", "Wheel_FR_Spin", "Wheel_RL_Spin", "Wheel_RR_Spin", "SteeringWheel_Pivot", "Clutch_Pedal_Pivot", "Brake_Pedal_Pivot", "Throttle_Pedal_Pivot", "GearLever_Pivot", "Windshield_Pivot"],
        "stats": stats,
        "stats_detailed": detailed_stats,
        "exports": ["RussoBalt_C24_55_LOD0.fbx", "RussoBalt_C24_55_LOD1.fbx", "RussoBalt_C24_55_LOD2.fbx", "RussoBalt_C24_55_Detailed.fbx", "RussoBalt_C24_55_Collision.fbx"],
        "textures": "game_assets/textures/shared (CC0 procedural maps, packed into BLEND/FBX)",
        "notes": "Windshield is folded for rally configuration; rotate RB_Windshield_Pivot to 0 radians for road configuration. Spare wheel is rear-mounted."
    }
    (ASSET_DIR / "asset_metadata.json").write_text(json.dumps(metadata, ensure_ascii=False, indent=2), encoding="utf-8")
    print("RUSSOBALT_BUILD_OK", json.dumps({"detailed": detailed_stats, "optimized": stats}, ensure_ascii=False))


try:
    build_russobalt()
except Exception as exc:
    print("RUSSOBALT_BUILD_ERROR", type(exc).__name__, str(exc))
    traceback.print_exc()
