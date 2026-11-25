from pathlib import Path
import sys
import json
import math
import traceback
import bpy
sys.path.insert(0, str(Path(__file__).resolve().parent))
import blender_assetlib
from blender_assetlib import asset_stats, descendants, mesh_triangle_count

ROOT = Path(__file__).resolve().parents[1]
REPORT_PATH = ROOT / "docs" / "FBX_VALIDATION.json"


def validate():
    records = []
    errors = []
    files = sorted((ROOT / "game_assets").rglob("*.fbx"))
    for path in files:
        before_objects = set(bpy.data.objects)
        before_actions = set(bpy.data.actions)
        try:
            bpy.ops.import_scene.fbx(filepath=str(path), use_anim=True, ignore_leaf_bones=True, automatic_bone_orientation=False)
            new_objects = [obj for obj in bpy.data.objects if obj not in before_objects]
            roots = [obj for obj in new_objects if obj.type == "EMPTY" and obj.parent is None]
            if not roots:
                raise RuntimeError("No root empty after import")
            root = roots[0]
            stats = asset_stats(root)
            meshes = [obj for obj in descendants(root) if obj.type == "MESH"]
            stats["triangles"] = sum(mesh_triangle_count(obj) for obj in meshes)
            missing_uv = [obj.name for obj in meshes if not obj.data.uv_layers]
            invalid_values = []
            for obj in meshes:
                if any(not math.isfinite(value) for vertex in obj.data.vertices for value in vertex.co):
                    invalid_values.append(obj.name)
                if any(not math.isfinite(value) for value in obj.scale):
                    invalid_values.append(obj.name)
            armatures = [obj for obj in descendants(root) if obj.type == "ARMATURE"]
            actions = sorted(action.name for action in bpy.data.actions if action not in before_actions)
            names = {obj.name for obj in descendants(root)}
            record = {
                "file": path.relative_to(ROOT).as_posix(),
                "root": root.name,
                "stats": stats,
                "materials": len({mat.name for obj in meshes for mat in obj.data.materials}),
                "missing_uv": missing_uv,
                "invalid_values": invalid_values,
                "armatures": [{"name": arm.name, "bones": len(arm.data.bones)} for arm in armatures],
                "actions": actions,
            }
            if path.name.startswith("RussoBalt_C24_55") and "Collision" not in path.name:
                required = {"RB_Wheel_FL_Steer", "RB_Wheel_FR_Steer", "RB_Wheel_FL_Spin", "RB_Wheel_FR_Spin", "RB_Wheel_RL_Spin", "RB_Wheel_RR_Spin", "RB_SteeringWheel_Pivot"}
                record["missing_required_nodes"] = sorted(required - names)
                if record["missing_required_nodes"]:
                    errors.append(f"Missing car nodes in {path.name}: {record['missing_required_nodes']}")
            if path.parent.name == "characters":
                for clip in ("Idle", "Walk", "Drive"):
                    if not any(clip in action for action in actions):
                        errors.append(f"Missing {clip} action in {path.name}; imported actions={actions}")
            if invalid_values:
                errors.append(f"Invalid numeric values in {path.name}: {invalid_values}")
            records.append(record)
            print("VALIDATED", path.name, stats["meshes"], stats["polygons"], "actions", len(actions))
        except Exception as exc:
            errors.append(f"{path.relative_to(ROOT)}: {type(exc).__name__}: {exc}")
            print("FAILED", path.name, type(exc).__name__, str(exc))
        finally:
            for obj in list(bpy.data.objects):
                if obj not in before_objects:
                    bpy.data.objects.remove(obj, do_unlink=True)
    report = {"validated": "2026-11-24", "blender": bpy.app.version_string, "status": "passed" if not errors else "failed", "fbx_count": len(files), "errors": errors, "records": records}
    REPORT_PATH.write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")
    print("FBX_VALIDATION", report["status"], len(records), "errors", len(errors))
    if errors:
        for error in errors:
            print("ERROR", error)


try:
    validate()
except Exception as exc:
    print("FBX_VALIDATOR_ERROR", type(exc).__name__, str(exc))
    traceback.print_exc()
