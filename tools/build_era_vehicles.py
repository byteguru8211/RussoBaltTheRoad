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

BASE = ROOT / "game_assets" / "vehicles"
PREVIEW_DIR = ROOT / "previews"
RECORDS = []


def wheel(prefix, code, center, radius, width, spokes, p, root, coll, front=False, wire=False):
    steer = empty(f"{prefix}_Wheel_{code}_Steer", center, root, coll, "CIRCLE") if front else None
    spin = empty(f"{prefix}_Wheel_{code}_Spin", (0, 0, 0), steer or root, coll, "CIRCLE")
    if not front:
        spin.location = center
    lathe_x(f"{prefix}_Wheel_{code}_Tire", [(-width/2, radius*.70), (-width/2, radius*.86), (-width*.38, radius*.97), (-width*.22, radius), (width*.22, radius), (width*.38, radius*.97), (width/2, radius*.86), (width/2, radius*.70)], p["rubber"], spin, coll, 44)
    lathe_x(f"{prefix}_Wheel_{code}_Rim", [(-width*.42, radius*.66), (-width*.48, radius*.72), (-width*.36, radius*.76), (width*.36, radius*.76), (width*.48, radius*.72), (width*.42, radius*.66)], p["wood"] if not wire else p["steel"], spin, coll, 40)
    torus(f"{prefix}_Wheel_{code}_Band", (0,0,0), radius*.74, width*.10, p["brass"] if not wire else p["steel"], spin, coll, "X", 40, 8)
    for i in range(spokes):
        angle = i / spokes * math.tau
        cylinder_between(f"{prefix}_Wheel_{code}_Spoke_{i+1:02d}", (0,0,0), (0,radius*.70*math.cos(angle),radius*.70*math.sin(angle)), 0.008 if wire else 0.012, p["steel"] if wire else p["wood"], spin, coll, 8)
    cylinder(f"{prefix}_Wheel_{code}_Hub", (0,0,0), radius*.16, width*1.35, p["brass"], spin, coll, "X", 20)
    return steer, spin


def add_common_chassis(prefix, wheelbase, track, radius, p, root, chassis_coll):
    chassis = empty(f"{prefix}_Chassis_Root", parent=root, coll=chassis_coll)
    front_y = -wheelbase / 2
    rear_y = wheelbase / 2
    for side in (-1,1):
        box(f"{prefix}_Chassis_Rail_{side}", (side*track*.38,0,.47), (.09,wheelbase+.55,.13), p["steel"], chassis, chassis_coll, .01)
        for y in (front_y, rear_y):
            leaf_spring(f"{prefix}_LeafSpring_{side}_{y}", y, .36, side*track*.43, .95, .07, p["steel"], chassis, chassis_coll, 3)
    cylinder(f"{prefix}_Front_Axle", (0,front_y,radius), .045, track, p["steel"], chassis, chassis_coll, "X", 18)
    cylinder(f"{prefix}_Rear_Axle", (0,rear_y,radius), .052, track, p["steel"], chassis, chassis_coll, "X", 18)
    sphere(f"{prefix}_Differential", (0,rear_y,radius), (.15,.14,.14), p["iron"], chassis, chassis_coll)
    cylinder_between(f"{prefix}_Driveshaft", (0,-.25,.43), (0,rear_y-.18,.43), .03, p["steel"], chassis, chassis_coll, 14)
    return chassis, front_y, rear_y


def collision(prefix, length, width, height, track, wheelbase, radius, p):
    coll = collection(f"{prefix}_COLLISION")
    root = empty(f"{prefix}_Collision_Root", coll=coll)
    box(f"UCX_{prefix}_Hull", (0,0,height*.48), (width*.92,length*.94,height), p["steel"], root, coll, 0)
    for code,x,y in (("FL",-track/2,-wheelbase/2),("FR",track/2,-wheelbase/2),("RL",-track/2,wheelbase/2),("RR",track/2,wheelbase/2)):
        cylinder(f"UCX_{prefix}_Wheel_{code}", (x,y,radius), radius, .14, p["steel"], root, coll, "X", 14, 0)
    return root


def export_vehicle(root, prefix, coll_root, p, display_location, metadata):
    ensure_uvs(root)
    export_fbx(root, BASE / prefix / f"{prefix}_Detailed.fbx")
    export_fbx(coll_root, BASE / prefix / f"{prefix}_Collision.fbx")
    opt = optimize_hierarchy(root, "_OPT", .72)
    export_fbx_clean_nodes(opt, BASE / prefix / f"{prefix}_LOD0.fbx", ("_OPT",))
    lod1 = duplicate_lod(opt, "_LOD1", .55)
    export_fbx_clean_nodes(lod1, BASE / prefix / f"{prefix}_LOD1.fbx", ("_OPT","_LOD1"))
    lod2 = duplicate_lod(opt, "_LOD2", .28)
    export_fbx_clean_nodes(lod2, BASE / prefix / f"{prefix}_LOD2.fbx", ("_OPT","_LOD2"))
    for hidden in (opt,lod1,lod2):
        for obj in descendants(hidden):
            obj.hide_render = True
            obj.hide_viewport = True
    for obj in descendants(coll_root):
        obj.hide_render = True
        obj.hide_viewport = True
    root.location = display_location
    metadata["stats_detailed"] = asset_stats(root)
    metadata["exports"] = [f"{prefix}_LOD0.fbx",f"{prefix}_LOD1.fbx",f"{prefix}_LOD2.fbx",f"{prefix}_Detailed.fbx",f"{prefix}_Collision.fbx"]
    (BASE / prefix / "asset_metadata.json").write_text(json.dumps(metadata,ensure_ascii=False,indent=2),encoding="utf-8")
    RECORDS.append(metadata)
    return root


def ford(p):
    prefix = "Ford_ModelT_1912"
    coll = collection(prefix)
    root = empty(f"{prefix}_Root", coll=coll)
    set_metadata(root,"ford_model_t_1912","research_based_concept","1912 Model T touring; individual game-art reconstruction.")
    body_coll = collection(f"{prefix}_Body",coll)
    wheel_coll = collection(f"{prefix}_Wheels",coll)
    cockpit_coll = collection(f"{prefix}_Cockpit",coll)
    chassis,front_y,rear_y = add_common_chassis(prefix,2.54,1.43,.381,p,root,coll)
    black = material("FORD_Paint_Black",(.018,.021,.019),metallic=.42,roughness=.34,coat=.45)
    body = empty(f"{prefix}_Body_Root",parent=root,coll=body_coll)
    box(f"{prefix}_Hood",(0,-.92,.93),(.58,1.38,.72),black,body,body_coll,.05)
    box(f"{prefix}_Hood_Top",(0,-.92,1.30),(.54,1.34,.035),black,body,body_coll,.02)
    for y in (-1.35,-.55):
        box(f"{prefix}_Hood_Strap_{y}",(0,y,1.325),(.08,.045,.02),p["leather"],body,body_coll,.004)
    box(f"{prefix}_Radiator",(0,-1.64,.93),(.48,.12,.82),p["brass"],body,body_coll,.06)
    box(f"{prefix}_Radiator_Grid",(0,-1.71,.93),(.39,.018,.66),p["iron"],body,body_coll,.02)
    for i in range(11):
        cylinder_between(f"{prefix}_Radiator_V_{i}",(-.17+i*.034,-1.725,.64),(-.17+i*.034,-1.725,1.22),.004,p["brass"],body,body_coll,6)
    cylinder(f"{prefix}_Radiator_Cap",(0,-1.63,1.39),.045,.06,p["brass"],body,body_coll,"Z",16)
    box(f"{prefix}_Cowl",(0,-.18,1.12),(.72,.22,.35),black,body,body_coll,.04)
    box(f"{prefix}_Firewall",(0,.02,1.05),(.78,.10,.70),black,body,body_coll,.03)
    box(f"{prefix}_Rear_Body",(0,1.02,.78),(.92,1.58,.42),black,body,body_coll,.08)
    for side in (-1,1):
        box(f"{prefix}_Side_Step_{side}",(side*.72,.12,.43),(.24,1.55,.05),p["steel"],body,body_coll,.01)
        box(f"{prefix}_Foredoor_{side}",(side*.46,-.02,1.05),(.06,.58,.62),black,body,body_coll,.025)
        box(f"{prefix}_Rear_Door_{side}",(side*.47,1.05,1.06),(.055,.62,.62),black,body,body_coll,.025)
        for y in (front_y,rear_y):
            mudguard(f"{prefix}_Mudguard_{side}_{y}",(side*.73,y,.381),.425,.22,.028,black,body,body_coll,10,170)
        for x in (side*.30,):
            cylinder(f"{prefix}_Cowl_Lamp_{side}",(x,-.22,1.43),.075,.14,p["brass"],body,body_coll,"Y",20)
            cylinder(f"{prefix}_Cowl_Lamp_Glass_{side}",(x,-.30,1.43),.062,.008,p["glass"],body,body_coll,"Y",20)
        cylinder(f"{prefix}_Headlamp_{side}",(side*.50,-1.52,1.04),.12,.16,p["brass"],body,body_coll,"Y",24)
        cylinder(f"{prefix}_Headlamp_Glass_{side}",(side*.50,-1.61,1.04),.10,.008,p["glass"],body,body_coll,"Y",24)
    for y in (.45,1.25):
        box(f"{prefix}_Bench_{y}",(0,y,.92),(.78,.42,.16),p["leather"],body,body_coll,.05)
        box(f"{prefix}_Bench_Back_{y}",(0,y+.22,1.15),(.78,.10,.42),p["leather"],body,body_coll,.04,(-.12,0,0))
    for side in (-1,1):
        tube(f"{prefix}_Roof_Frame_{side}",[(side*.42,-.12,1.43),(side*.43,.55,1.95),(side*.43,1.62,1.90)],.018,p["steel"],body,body_coll,8)
    tube(f"{prefix}_Roof_Front",[(-.42,-.12,1.43),(0,-.14,1.97),(.42,-.12,1.43)],.018,p["steel"],body,body_coll,8)
    tube(f"{prefix}_Roof_Rear",[(-.43,1.62,1.90),(0,1.66,1.97),(.43,1.62,1.90)],.018,p["steel"],body,body_coll,8)
    cockpit = empty(f"{prefix}_Cockpit_Root",parent=root,coll=cockpit_coll)
    steering = empty(f"{prefix}_SteeringWheel_Pivot",(-.24,.16,1.31),cockpit,cockpit_coll,"CIRCLE")
    torus(f"{prefix}_SteeringWheel_Rim",(0,0,0),.17,.014,p["wood"],steering,cockpit_coll,"Z",32,8).rotation_euler=(math.radians(72),0,0)
    cylinder_between(f"{prefix}_Steering_Column",(-.24,-.05,1.08),(-.24,.16,1.31),.016,p["steel"],cockpit,cockpit_coll,12)
    for code,x in (("Clutch",-.12),("Brake",.02),("Throttle",.16)):
        pedal=empty(f"{prefix}_{code}_Pedal_Pivot",(x,.10,.62),cockpit,cockpit_coll)
        box(f"{prefix}_{code}_Pedal",(0,-.06,.03),(.07,.16,.025),p["steel"],pedal,cockpit_coll,.006,(.3,0,0))
    for code,x,y,front in (("FL",-.715,front_y,True),("FR",.715,front_y,True),("RL",-.715,rear_y,False),("RR",.715,rear_y,False)):
        wheel(prefix,code,(x,y,.381),.381,.10,12,p,root,wheel_coll,front,False)
    coll_root=collision(prefix,3.42,1.73,1.95,1.43,2.54,.381,p)
    return root,coll_root,{"asset_id":"ford_model_t_1912","display_name":"Ford Model T 1912 Touring","historical_status":"research_based_concept","confirmed":{"engine_cc":2892,"power_hp_approx":20,"drive":"RWD planetary 2-speed + reverse","steering":"LHD","brakes":"transmission band brake + rear drums","wheelbase_m":2.54},"assumptions":{"color":"black with brass","body":"five-seat touring","dimensions_m":[3.42,1.73,1.95]}}


def fiat(p):
    prefix="Fiat_S76_1910"
    coll=collection(prefix)
    root=empty(f"{prefix}_Root",coll=coll)
    set_metadata(root,"fiat_s76_1910","research_based_concept","Fiat S76 record car concept; original color unproven.")
    body_coll=collection(f"{prefix}_Body",coll)
    wheel_coll=collection(f"{prefix}_Wheels",coll)
    cockpit_coll=collection(f"{prefix}_Cockpit",coll)
    chassis,front_y,rear_y=add_common_chassis(prefix,3.70,1.30,.46,p,root,coll)
    red=material("FIAT_Paint_RecordRed",(.30,.035,.022),metallic=.38,roughness=.32,coat=.5)
    body=empty(f"{prefix}_Body_Root",parent=root,coll=body_coll)
    loft_y(f"{prefix}_High_Hood",[(-1.94,.25,.58,.92),(-1.72,.34,.56,1.34),(-1.18,.38,.55,1.45),(-.35,.40,.56,1.42),(.02,.39,.58,1.24)],red,body,body_coll)
    box(f"{prefix}_Blade_Radiator",(0,-2.00,.99),(.34,.12,1.20),p["brass"],body,body_coll,.09)
    box(f"{prefix}_Radiator_Core",(0,-2.075,.99),(.25,.02,1.02),p["iron"],body,body_coll,.04)
    for i in range(9):
        cylinder_between(f"{prefix}_Radiator_V_{i}",(-.11+i*.0275,-2.09,.52),(-.11+i*.0275,-2.09,1.46),.004,p["brass"],body,body_coll,6)
    cylinder(f"{prefix}_Radiator_Cap",(0,-1.99,1.64),.06,.08,p["brass"],body,body_coll,"Z",18)
    panel_x(f"{prefix}_Side_{-1}",[(-.02,.58),(.02,1.05),(.35,1.10),(1.25,.98),(1.62,.75),(1.55,.56)],-.405,.04,red,body,body_coll)
    panel_x(f"{prefix}_Side_{1}",[(-.02,.58),(.02,1.05),(.35,1.10),(1.25,.98),(1.62,.75),(1.55,.56)],.405,.04,red,body,body_coll)
    box(f"{prefix}_Seat_Base",(.12,.92,.76),(.62,.52,.14),p["leather"],body,body_coll,.05)
    box(f"{prefix}_Seat_Back",(.12,1.20,1.02),(.62,.12,.46),p["leather"],body,body_coll,.05,(-.12,0,0))
    for side in (-1,1):
        for i in range(4):
            y=-1.45+i*.36
            box(f"{prefix}_Engine_Cylinder_{side}_{i}",(side*.39,y,.90),(.24,.28,.48),p["iron"],body,body_coll,.025)
            cylinder(f"{prefix}_Cylinder_Head_{side}_{i}",(side*.39,y,1.17),.13,.06,p["steel"],body,body_coll,"Z",20)
            tube(f"{prefix}_Exhaust_{side}_{i}",[(side*.49,y,1.05),(side*.64,y,1.18),(side*.68,y+.12,1.35)],.035,p["steel"],body,body_coll,10)
        tube(f"{prefix}_Exhaust_Header_{side}",[(side*.68,-1.50,1.30),(side*.70,-.70,1.34),(side*.72,.20,1.32),(side*.78,1.40,1.22)],.045,p["steel"],body,body_coll,12)
        cylinder(f"{prefix}_Headlamp_{side}",(side*.40,-1.88,1.24),.11,.15,p["brass"],body,body_coll,"Y",22)
        torus(f"{prefix}_Rear_Sprocket_{side}",(side*.22,rear_y,.46),.22,.028,p["steel"],chassis,coll,"X",28,8)
        tube(f"{prefix}_Chain_{side}",[(side*.22,-.15,.62),(side*.22,rear_y,.68),(side*.22,rear_y+.18,.46),(side*.22,rear_y,.24),(side*.22,-.15,.30),(side*.22,-.30,.46),(side*.22,-.15,.62)],.018,p["steel"],chassis,coll,8)
    cockpit=empty(f"{prefix}_Cockpit_Root",parent=root,coll=cockpit_coll)
    steering=empty(f"{prefix}_SteeringWheel_Pivot",(-.23,.48,1.25),cockpit,cockpit_coll,"CIRCLE")
    torus(f"{prefix}_SteeringWheel_Rim",(0,0,0),.18,.014,p["wood"],steering,cockpit_coll,"Z",34,8).rotation_euler=(math.radians(74),0,0)
    cylinder_between(f"{prefix}_Steering_Column",(-.23,.02,1.04),(-.23,.48,1.25),.018,p["steel"],cockpit,cockpit_coll,12)
    for code,x,y,front in (("FL",-.65,front_y,True),("FR",.65,front_y,True),("RL",-.65,rear_y,False),("RR",.65,rear_y,False)):
        wheel(prefix,code,(x,y,.46),.46,.12,16,p,root,wheel_coll,front,True)
    coll_root=collision(prefix,3.75,1.30,1.75,1.30,3.70,.46,p)
    return root,coll_root,{"asset_id":"fiat_s76_1910","display_name":"Fiat S76 1910 Record Car","historical_status":"research_based_concept","confirmed":{"engine_cc":28353,"power_hp_approx":"290–300","drive":"RWD double chain","gears":4,"steering":"RHD by photographs","brakes":"rear drums","wheelbase_m":3.70},"assumptions":{"color":"dark red game-art choice; original unproven","dimensions_m":[3.75,1.30,1.75],"mass_kg":1650}}


def mercedes(p):
    prefix="Mercedes_37_90_1912"
    coll=collection(prefix)
    root=empty(f"{prefix}_Root",coll=coll)
    set_metadata(root,"mercedes_37_90_1912","research_based_concept","Mercedes 37/90 Double Phaeton concept.")
    body_coll=collection(f"{prefix}_Body",coll)
    wheel_coll=collection(f"{prefix}_Wheels",coll)
    cockpit_coll=collection(f"{prefix}_Cockpit",coll)
    chassis,front_y,rear_y=add_common_chassis(prefix,3.45,1.55,.46,p,root,coll)
    green=material("MERCEDES_Paint_DeepGreen",(.025,.075,.052),metallic=.38,roughness=.30,coat=.6)
    body=empty(f"{prefix}_Body_Root",parent=root,coll=body_coll)
    loft_y(f"{prefix}_Long_Hood",[(-2.28,.34,.58,.88),(-2.05,.43,.56,1.10),(-1.45,.47,.56,1.16),(-.55,.49,.57,1.15),(-.12,.50,.58,1.08)],green,body,body_coll)
    cone(f"{prefix}_Pointed_Radiator",(0,-2.31,1.00),.42,.20,.38,p["brass"],body,body_coll,4,(math.pi/2,0,math.pi/4))
    box(f"{prefix}_Radiator_Core",(0,-2.49,.98),(.40,.025,.70),p["iron"],body,body_coll,.04)
    for i in range(12):
        cylinder_between(f"{prefix}_Radiator_V_{i}",(-.18+i*.0327,-2.51,.66),(-.18+i*.0327,-2.51,1.30),.004,p["brass"],body,body_coll,6)
    cylinder(f"{prefix}_Radiator_Cap",(0,-2.28,1.26),.065,.08,p["brass"],body,body_coll,"Z",18)
    sphere(f"{prefix}_Radiator_Emblem",(0,-2.34,1.39),(.06,.03,.06),p["brass_bright"],body,body_coll,16,8)
    box(f"{prefix}_Cabin_Floor",(0,.85,.64),(1.02,2.55,.12),p["wood"],body,body_coll,.03)
    for side in (-1,1):
        panel_x(f"{prefix}_Side_{side}",[(-.10,.62),(-.10,1.05),(.10,1.12),(.25,1.00),(2.00,1.00),(2.18,.88),(2.20,.62)],side*.51,.05,green,body,body_coll)
        box(f"{prefix}_Rear_Door_{side}",(side*.53,1.55,.99),(.045,.72,.66),green,body,body_coll,.02)
        box(f"{prefix}_Running_Board_{side}",(side*.73,.55,.44),(.25,2.75,.05),p["steel"],body,body_coll,.01)
        for y in (front_y,rear_y):
            mudguard(f"{prefix}_Mudguard_{side}_{y}",(side*.76,y,.46),.50,.25,.03,green,body,body_coll,8,172)
        cylinder(f"{prefix}_Headlamp_{side}",(side*.62,-2.10,1.12),.16,.22,p["brass"],body,body_coll,"Y",28)
        cylinder(f"{prefix}_Headlamp_Glass_{side}",(side*.62,-2.22,1.12),.138,.01,p["glass"],body,body_coll,"Y",28)
        tube(f"{prefix}_External_Exhaust_{side}",[(side*.50,-1.20,.56),(side*.62,-.65,.48),(side*.65,.50,.45),(side*.72,1.75,.50)],.028,p["steel"],body,body_coll,10)
        tube(f"{prefix}_Roof_Frame_{side}",[(side*.48,-.05,1.15),(side*.49,.70,1.95),(side*.49,2.02,1.91)],.018,p["brass"],body,body_coll,8)
    for y in (.38,1.45):
        box(f"{prefix}_Seat_{y}",(0,y,.88),(.88,.48,.16),p["leather"],body,body_coll,.05)
        box(f"{prefix}_Seat_Back_{y}",(0,y+.25,1.12),(.88,.12,.45),p["leather"],body,body_coll,.05,(-.12,0,0))
    tube(f"{prefix}_Roof_Front",[(-.48,-.05,1.15),(0,-.08,1.97),(.48,-.05,1.15)],.018,p["brass"],body,body_coll,8)
    tube(f"{prefix}_Roof_Rear",[(-.49,2.02,1.91),(0,2.06,1.98),(.49,2.02,1.91)],.018,p["brass"],body,body_coll,8)
    cockpit=empty(f"{prefix}_Cockpit_Root",parent=root,coll=cockpit_coll)
    steering=empty(f"{prefix}_SteeringWheel_Pivot",(-.27,.06,1.25),cockpit,cockpit_coll,"CIRCLE")
    torus(f"{prefix}_SteeringWheel_Rim",(0,0,0),.19,.015,p["wood"],steering,cockpit_coll,"Z",36,8).rotation_euler=(math.radians(74),0,0)
    cylinder_between(f"{prefix}_Steering_Column",(-.27,-.18,1.02),(-.27,.06,1.25),.018,p["steel"],cockpit,cockpit_coll,12)
    for code,x,y,front in (("FL",-.775,front_y,True),("FR",.775,front_y,True),("RL",-.775,rear_y,False),("RR",.775,rear_y,False)):
        wheel(prefix,code,(x,y,.46),.46,.13,16,p,root,wheel_coll,front,False)
    coll_root=collision(prefix,4.95,1.75,1.75,1.55,3.45,.46,p)
    return root,coll_root,{"asset_id":"mercedes_37_90_1912","display_name":"Mercedes 37/90 1912 Double Phaeton","historical_status":"research_based_concept","confirmed":{"engine_cc":9530,"power_hp_approx":90,"drive":"RWD enclosed chain","gears":4,"steering":"RHD","brakes":"transmission brake + rear drums","wheelbase_m":"3.38–3.58"},"assumptions":{"color":"deep green with brass","dimensions_m":[4.95,1.75,1.75],"mass_kg":1950}}


def build():
    reset_scene()
    p=palette()
    roots=[]
    for builder,location in ((ford,(-5.2,0,0)),(fiat,(0,0,0)),(mercedes,(5.4,0,0))):
        root,coll_root,metadata=builder(p)
        export_vehicle(root,metadata["asset_id"] and {"ford_model_t_1912":"Ford_ModelT_1912","fiat_s76_1910":"Fiat_S76_1910","mercedes_37_90_1912":"Mercedes_37_90_1912"}[metadata["asset_id"]],coll_root,p,location,metadata)
        roots.append(root)
    bpy.context.view_layer.update()
    add_studio(target=(0,0,.9),camera=(12,-18,8),size=18)
    bpy.data.lights["STUDIO_Key"].energy=15000
    bpy.data.lights["STUDIO_Fill"].energy=6500
    bpy.data.lights["STUDIO_Rim"].energy=10000
    render_preview(PREVIEW_DIR/"Era_Vehicles_preview.png",(1800,900),48)
    save_blend(BASE/"Era_Vehicles_1910_1914.blend")
    (BASE/"Era_Vehicles_Manifest.json").write_text(json.dumps({"generated":"2026-11-25","vehicles":RECORDS},ensure_ascii=False,indent=2),encoding="utf-8")
    print("ERA_VEHICLES_BUILD_OK",len(RECORDS))


try:
    build()
except Exception as exc:
    print("ERA_VEHICLES_BUILD_ERROR",type(exc).__name__,str(exc))
    traceback.print_exc()
