#!/usr/bin/env python3
"""Validate Unity track assets and build their Blender presentation layouts.

Run from the repository root:
    "D:/Program Files/Blender/blender.exe" --background --factory-startup -t 1 \
        --python tools/build_track_layouts.py

Validation can also run without Blender:
    python tools/build_track_layouts.py --validate-only

The route math mirrors TrackRoute.cs: closed, uniform Catmull-Rom, with 48
runtime samples per control segment.  Horizontal radius and grade are measured
from analytic derivatives.  Clearance is the minimum 3D distance between
centerline portions separated by at least 60 m along the route; this excludes
ordinary neighbouring samples while still catching nearby parallel sections.
"""

from __future__ import annotations

import argparse
import json
import math
import re
import sys
from dataclasses import dataclass
from pathlib import Path
from typing import Iterable, Sequence

ROOT = Path(__file__).resolve().parents[1]
TRACK_DIR = ROOT / "unity-project" / "Assets" / "Resources" / "Tracks"
ASSET_DIR = ROOT / "game_assets" / "tracks"
PREVIEW_DIR = ROOT / "previews" / "tracks"
RUNTIME_STEPS = 48
CLEARANCE_ARC_SEPARATION = 60.0

TRACK_ORDER = (
    "winter_departure",
    "alps_pass",
    "monaco_finish",
    "moscow_highway",
    "finland_coast",
    "tver_forest",
    "riga_speedway",
    "crimean_coast",
)

LIMITS = {
    "winter_departure": {"length": (550.0, 650.0), "radius": 18.0, "start": 70.0},
    "alps_pass": {"length": (750.0, 900.0), "radius": 18.0, "start": 70.0},
    "monaco_finish": {"length": (550.0, 650.0), "radius": 18.0, "start": 70.0},
    "moscow_highway": {"length": (850.0, 1000.0), "radius": 18.0, "start": 70.0},
    "finland_coast": {"length": (900.0, 1100.0), "radius": 18.0, "start": 70.0},
    "tver_forest": {"length": (700.0, 850.0), "radius": 18.0, "start": 70.0},
    "riga_speedway": {"length": (900.0, 1100.0), "radius": 30.0, "start": 100.0},
    "crimean_coast": {"length": (800.0, 1000.0), "radius": 18.0, "start": 70.0},
}

Point3 = tuple[float, float, float]
Point2 = tuple[float, float]


@dataclass(frozen=True)
class TrackAsset:
    track_id: str
    path: Path
    waypoints: tuple[Point3, ...]
    display_name: str
    road_width: float
    scenery: str
    road_color: tuple[float, float, float]
    ground_color: tuple[float, float, float]
    curb_color: tuple[float, float, float]


@dataclass(frozen=True)
class TrackMetrics:
    length: float
    dense_length: float
    minimum_radius: float
    maximum_grade_percent: float
    nonlocal_clearance: float
    self_intersections: int
    start_straight: float
    start_deviation: float
    width: float
    depth: float
    elevation_range: float
    control_count: int


def _number(value: str) -> float:
    return float(value.strip().replace("_", ""))


def _flow_map(text: str) -> dict[str, str]:
    body = text.strip()
    if body.startswith("{") and body.endswith("}"):
        body = body[1:-1]
    result: dict[str, str] = {}
    for item in body.split(","):
        if ":" not in item:
            continue
        key, value = item.split(":", 1)
        result[key.strip()] = value.strip()
    return result


def _yaml_double_quoted(value: str) -> str:
    """Decode the JSON-like double-quoted scalars used by Unity YAML.

    Unity also emits YAML ``\\xNN`` escapes, which JSON does not accept.
    """
    value = value.strip()
    if len(value) < 2 or value[0] != '"' or value[-1] != '"':
        return value
    body = value[1:-1]
    escapes = {
        "0": "\0", "a": "\a", "b": "\b", "t": "\t", "n": "\n",
        "v": "\v", "f": "\f", "r": "\r", "e": "\x1b", " ": " ",
        '"': '"', "/": "/", "\\": "\\", "N": "\u0085",
        "_": "\u00a0", "L": "\u2028", "P": "\u2029",
    }

    def replace(match: re.Match[str]) -> str:
        token = match.group(1)
        if token in escapes:
            return escapes[token]
        if token.startswith("x"):
            return chr(int(token[1:], 16))
        if token.startswith("u"):
            return chr(int(token[1:], 16))
        if token.startswith("U"):
            return chr(int(token[1:], 16))
        return match.group(0)

    return re.sub(r"\\(x[0-9a-fA-F]{2}|u[0-9a-fA-F]{4}|U[0-9a-fA-F]{8}|.)", replace, body)


def _top_level_scalar(lines: Sequence[str], key: str) -> str | None:
    pattern = re.compile(rf"^  {re.escape(key)}:\s*(.*?)\s*$")
    for line in lines:
        match = pattern.match(line)
        if match:
            return match.group(1)
    return None


def parse_track_asset(path: Path) -> TrackAsset:
    """Parse only the TrackData fields needed here, without a YAML dependency.

    Unity serializes Vector3 waypoints as one-line flow mappings.  The parser
    deliberately anchors at the two-space MonoBehaviour field indentation and
    stops at the next field, so similarly named nested data cannot be mistaken
    for the route.
    """
    text = path.read_text(encoding="utf-8-sig")
    lines = text.splitlines()
    waypoint_start = None
    for index, line in enumerate(lines):
        if line.rstrip() == "  waypoints:":
            waypoint_start = index + 1
            break
    if waypoint_start is None:
        raise ValueError(f"{path}: missing waypoints field")

    waypoints: list[Point3] = []
    for line in lines[waypoint_start:]:
        if not line.startswith("  - "):
            if line.strip() and not line.startswith("    "):
                break
            continue
        values = _flow_map(line[4:])
        missing = {"x", "y", "z"} - values.keys()
        if missing:
            raise ValueError(f"{path}: waypoint is missing {sorted(missing)}: {line}")
        waypoints.append(tuple(_number(values[axis]) for axis in ("x", "y", "z")))

    if len(waypoints) < 4:
        raise ValueError(f"{path}: expected at least four waypoints, got {len(waypoints)}")

    track_id = _yaml_double_quoted(_top_level_scalar(lines, "id") or path.stem)
    display_name = _yaml_double_quoted(_top_level_scalar(lines, "displayName") or track_id)
    scenery = _yaml_double_quoted(_top_level_scalar(lines, "scenery") or "forest")
    road_width = _number(_top_level_scalar(lines, "roadWidth") or "9")

    def color(key: str, fallback: tuple[float, float, float]) -> tuple[float, float, float]:
        raw = _top_level_scalar(lines, key)
        if not raw:
            return fallback
        values = _flow_map(raw)
        return tuple(_number(values.get(channel, str(fallback[index]))) for index, channel in enumerate("rgb"))

    return TrackAsset(
        track_id=track_id,
        path=path,
        waypoints=tuple(waypoints),
        display_name=display_name,
        road_width=road_width,
        scenery=scenery,
        road_color=color("roadColor", (0.34, 0.25, 0.15)),
        ground_color=color("groundColor", (0.20, 0.27, 0.16)),
        curb_color=color("curbColor", (0.72, 0.68, 0.55)),
    )


def load_tracks() -> list[TrackAsset]:
    tracks = [parse_track_asset(TRACK_DIR / f"{track_id}.asset") for track_id in TRACK_ORDER]
    ids = [track.track_id for track in tracks]
    if ids != list(TRACK_ORDER):
        raise ValueError(f"Unexpected track ids/order: {ids}")
    return tracks


def catmull_rom(points: Sequence[Point3], segment: int, t: float) -> Point3:
    count = len(points)
    p0 = points[(segment - 1) % count]
    p1 = points[segment]
    p2 = points[(segment + 1) % count]
    p3 = points[(segment + 2) % count]
    return tuple(
        0.5 * (
            2.0 * p1[axis]
            + (-p0[axis] + p2[axis]) * t
            + (2.0 * p0[axis] - 5.0 * p1[axis] + 4.0 * p2[axis] - p3[axis]) * t * t
            + (-p0[axis] + 3.0 * p1[axis] - 3.0 * p2[axis] + p3[axis]) * t * t * t
        )
        for axis in range(3)
    )


def catmull_derivatives(points: Sequence[Point3], segment: int, t: float) -> tuple[Point3, Point3]:
    count = len(points)
    p0 = points[(segment - 1) % count]
    p1 = points[segment]
    p2 = points[(segment + 1) % count]
    p3 = points[(segment + 2) % count]
    first = tuple(
        0.5 * (
            (-p0[axis] + p2[axis])
            + 2.0 * (2.0 * p0[axis] - 5.0 * p1[axis] + 4.0 * p2[axis] - p3[axis]) * t
            + 3.0 * (-p0[axis] + 3.0 * p1[axis] - 3.0 * p2[axis] + p3[axis]) * t * t
        )
        for axis in range(3)
    )
    second = tuple(
        0.5 * (
            2.0 * (2.0 * p0[axis] - 5.0 * p1[axis] + 4.0 * p2[axis] - p3[axis])
            + 6.0 * (-p0[axis] + 3.0 * p1[axis] - 3.0 * p2[axis] + p3[axis]) * t
        )
        for axis in range(3)
    )
    return first, second


def distance(a: Point3, b: Point3) -> float:
    return math.sqrt(sum((a[axis] - b[axis]) ** 2 for axis in range(3)))


def sample_runtime(points: Sequence[Point3], steps: int = RUNTIME_STEPS) -> list[Point3]:
    return [catmull_rom(points, segment, step / steps) for segment in range(len(points)) for step in range(steps)]


def sample_dense(points: Sequence[Point3], spacing: float = 2.0) -> list[Point3]:
    result: list[Point3] = []
    for segment in range(len(points)):
        estimate = distance(points[segment], points[(segment + 1) % len(points)])
        steps = max(12, int(math.ceil(estimate / spacing)))
        result.extend(catmull_rom(points, segment, step / steps) for step in range(steps))
    return result


def polyline_length(points: Sequence[Point3]) -> float:
    return sum(distance(points[index], points[(index + 1) % len(points)]) for index in range(len(points)))


def _orientation(a: Point2, b: Point2, c: Point2) -> float:
    return (b[0] - a[0]) * (c[1] - a[1]) - (b[1] - a[1]) * (c[0] - a[0])


def _segments_cross(a: Point2, b: Point2, c: Point2, d: Point2) -> bool:
    o1 = _orientation(a, b, c)
    o2 = _orientation(a, b, d)
    o3 = _orientation(c, d, a)
    o4 = _orientation(c, d, b)
    return o1 * o2 < -1e-10 and o3 * o4 < -1e-10


def count_self_intersections(points: Sequence[Point3]) -> int:
    planar = [(point[0], point[2]) for point in points]
    count = 0
    total = len(planar)
    for first in range(total):
        a, b = planar[first], planar[(first + 1) % total]
        for second in range(first + 2, total):
            if first == 0 and second == total - 1:
                continue
            c, d = planar[second], planar[(second + 1) % total]
            if _segments_cross(a, b, c, d):
                count += 1
    return count


def nonlocal_clearance(points: Sequence[Point3], length: float) -> float:
    arc = [0.0]
    for index in range(len(points)):
        arc.append(arc[-1] + distance(points[index], points[(index + 1) % len(points)]))
    minimum = math.inf
    for first in range(len(points)):
        for second in range(first + 1, len(points)):
            separation = arc[second] - arc[first]
            separation = min(separation, length - separation)
            if separation < CLEARANCE_ARC_SEPARATION:
                continue
            minimum = min(minimum, distance(points[first], points[second]))
    return minimum


def _start_straight(points: Sequence[Point3], dense: Sequence[Point3], required: float) -> tuple[float, float]:
    velocity, _ = catmull_derivatives(points, 0, 0.0)
    tangent_length = math.hypot(velocity[0], velocity[2])
    if tangent_length < 1e-8:
        return 0.0, math.inf
    tx, tz = velocity[0] / tangent_length, velocity[2] / tangent_length
    start = points[0]
    travelled = 0.0
    previous = start
    maximum_deviation = 0.0
    for point in dense[1:]:
        travelled += distance(previous, point)
        previous = point
        dx, dz = point[0] - start[0], point[2] - start[2]
        along = dx * tx + dz * tz
        lateral = abs(-dx * tz + dz * tx)
        if along <= required + 1e-6:
            maximum_deviation = max(maximum_deviation, lateral)
        if along > required:
            break
        if lateral > 0.05:
            return travelled, maximum_deviation
    return travelled, maximum_deviation


def measure_track(track: TrackAsset) -> TrackMetrics:
    points = track.waypoints
    runtime = sample_runtime(points)
    dense = sample_dense(points)
    runtime_length = polyline_length(runtime)
    dense_length = polyline_length(dense)

    minimum_radius = math.inf
    maximum_grade = 0.0
    for segment in range(len(points)):
        for step in range(257):
            t = step / 256.0
            first, second = catmull_derivatives(points, segment, t)
            horizontal_speed_squared = first[0] ** 2 + first[2] ** 2
            cross = first[0] * second[2] - first[2] * second[0]
            if horizontal_speed_squared > 1e-10 and abs(cross) > 1e-10:
                radius = horizontal_speed_squared ** 1.5 / abs(cross)
                minimum_radius = min(minimum_radius, radius)
            horizontal_speed = math.sqrt(horizontal_speed_squared)
            if horizontal_speed > 1e-8:
                maximum_grade = max(maximum_grade, abs(first[1]) / horizontal_speed)

    required_start = LIMITS[track.track_id]["start"]
    start_straight, start_deviation = _start_straight(points, dense, required_start)
    xs = [point[0] for point in dense]
    zs = [point[2] for point in dense]
    ys = [point[1] for point in dense]
    return TrackMetrics(
        length=runtime_length,
        dense_length=dense_length,
        minimum_radius=minimum_radius,
        maximum_grade_percent=maximum_grade * 100.0,
        nonlocal_clearance=nonlocal_clearance(dense, dense_length),
        self_intersections=count_self_intersections(dense),
        start_straight=start_straight,
        start_deviation=start_deviation,
        width=max(xs) - min(xs),
        depth=max(zs) - min(zs),
        elevation_range=max(ys) - min(ys),
        control_count=len(points),
    )


def validate_track(track: TrackAsset, metrics: TrackMetrics) -> list[str]:
    limits = LIMITS[track.track_id]
    low, high = limits["length"]
    issues: list[str] = []
    if not low <= metrics.length <= high:
        issues.append(f"length {metrics.length:.2f} m outside [{low:.0f}, {high:.0f}]")
    if metrics.minimum_radius < limits["radius"]:
        issues.append(f"radius {metrics.minimum_radius:.2f} m below {limits['radius']:.0f}")
    if metrics.maximum_grade_percent > 14.0:
        issues.append(f"grade {metrics.maximum_grade_percent:.2f}% above 14%")
    if metrics.nonlocal_clearance < 25.0:
        issues.append(f"non-local clearance {metrics.nonlocal_clearance:.2f} m below 25")
    if metrics.self_intersections:
        issues.append(f"{metrics.self_intersections} self-intersection(s)")
    if metrics.start_straight + 1e-6 < limits["start"] or metrics.start_deviation > 0.05:
        issues.append(
            f"start straight {metrics.start_straight:.2f} m / deviation {metrics.start_deviation:.3f} m "
            f"below required {limits['start']:.0f} m"
        )
    if metrics.width > 700.0 or metrics.depth > 700.0:
        issues.append(f"bounds {metrics.width:.1f} x {metrics.depth:.1f} m exceed 700 x 700")
    if track.track_id == "alps_pass" and not 35.0 <= metrics.elevation_range <= 55.0:
        issues.append(f"Alps elevation range {metrics.elevation_range:.2f} m outside [35, 55]")
    return issues


def metrics_dict(track: TrackAsset, metrics: TrackMetrics) -> dict[str, object]:
    return {
        "id": track.track_id,
        "length_m": round(metrics.length, 3),
        "dense_length_m": round(metrics.dense_length, 3),
        "minimum_radius_m": round(metrics.minimum_radius, 3),
        "maximum_grade_percent": round(metrics.maximum_grade_percent, 3),
        "nonlocal_clearance_m": round(metrics.nonlocal_clearance, 3),
        "self_intersections": metrics.self_intersections,
        "start_straight_m": round(metrics.start_straight, 3),
        "start_max_deviation_m": round(metrics.start_deviation, 4),
        "bounds_m": [round(metrics.width, 3), round(metrics.depth, 3)],
        "elevation_range_m": round(metrics.elevation_range, 3),
        "control_points": metrics.control_count,
        "road_width_m": track.road_width,
    }


def print_metrics(tracks: Sequence[TrackAsset], all_metrics: Sequence[TrackMetrics]) -> None:
    print("Track                 Length   Dense    Min R   Grade   Clear   Cross  Start   Bounds       Elev")
    for track, metrics in zip(tracks, all_metrics):
        print(
            f"{track.track_id:20} {metrics.length:7.1f} {metrics.dense_length:8.1f} "
            f"{metrics.minimum_radius:7.2f} {metrics.maximum_grade_percent:6.2f}% "
            f"{metrics.nonlocal_clearance:7.2f} {metrics.self_intersections:6d} "
            f"{metrics.start_straight:7.1f} {metrics.width:5.0f}x{metrics.depth:<5.0f} "
            f"{metrics.elevation_range:6.1f}"
        )


def validate_all() -> tuple[list[TrackAsset], list[TrackMetrics]]:
    tracks = load_tracks()
    all_metrics = [measure_track(track) for track in tracks]
    print_metrics(tracks, all_metrics)
    failed = False
    for track, metrics in zip(tracks, all_metrics):
        for issue in validate_track(track, metrics):
            failed = True
            print(f"ERROR {track.track_id}: {issue}", file=sys.stderr)
    if failed:
        raise ValueError("Track layout validation failed")
    return tracks, all_metrics


def build_blender_layouts(tracks: Sequence[TrackAsset], all_metrics: Sequence[TrackMetrics]) -> None:
    """Build one self-contained, metric-scale Blender layout per track."""
    import bpy
    from mathutils import Matrix, Vector

    ASSET_DIR.mkdir(parents=True, exist_ok=True)
    PREVIEW_DIR.mkdir(parents=True, exist_ok=True)
    font_path = Path("C:/Windows/Fonts/arial.ttf")
    font = None

    def collection(name: str):
        coll = bpy.data.collections.new(name)
        bpy.context.scene.collection.children.link(coll)
        return coll

    def link(obj, coll):
        for old in list(obj.users_collection):
            old.objects.unlink(obj)
        coll.objects.link(obj)
        return obj

    def pbr(name: str, color: tuple[float, float, float], roughness: float = 0.8,
            metallic: float = 0.0, bump: float = 0.12, scale: float = 3.0,
            emission: float = 0.0):
        mat = bpy.data.materials.new(name)
        mat.use_nodes = True
        mat.diffuse_color = (*color, 1.0)
        nodes = mat.node_tree.nodes
        links = mat.node_tree.links
        shader = nodes.get("Principled BSDF")
        shader.inputs["Base Color"].default_value = (*color, 1.0)
        shader.inputs["Roughness"].default_value = roughness
        shader.inputs["Metallic"].default_value = metallic
        if emission:
            shader.inputs["Emission Color"].default_value = (*color, 1.0)
            shader.inputs["Emission Strength"].default_value = emission
        coord = nodes.new("ShaderNodeTexCoord")
        noise = nodes.new("ShaderNodeTexNoise")
        noise.inputs["Scale"].default_value = scale
        noise.inputs["Detail"].default_value = 3.0
        noise.inputs["Roughness"].default_value = 0.72
        links.new(coord.outputs["Object"], noise.inputs["Vector"])
        relief = nodes.new("ShaderNodeBump")
        relief.inputs["Strength"].default_value = bump
        relief.inputs["Distance"].default_value = 0.045
        links.new(noise.outputs["Fac"], relief.inputs["Height"])
        links.new(relief.outputs["Normal"], shader.inputs["Normal"])
        rough_ramp = nodes.new("ShaderNodeMapRange")
        rough_ramp.inputs["From Min"].default_value = 0.0
        rough_ramp.inputs["From Max"].default_value = 1.0
        rough_ramp.inputs["To Min"].default_value = max(0.05, roughness - 0.12)
        rough_ramp.inputs["To Max"].default_value = min(1.0, roughness + 0.12)
        links.new(noise.outputs["Fac"], rough_ramp.inputs["Value"])
        links.new(rough_ramp.outputs["Result"], shader.inputs["Roughness"])
        return mat

    def mesh_object(name, verts, faces, materials, coll, smooth=True, material_indices=None, uvs=None):
        data = bpy.data.meshes.new(name)
        data.from_pydata(verts, [], faces)
        data.update()
        obj = bpy.data.objects.new(name, data)
        coll.objects.link(obj)
        for mat in materials:
            data.materials.append(mat)
        if material_indices:
            for polygon, index in zip(data.polygons, material_indices):
                polygon.material_index = index
        if smooth:
            for polygon in data.polygons:
                polygon.use_smooth = True
        if uvs:
            layer = data.uv_layers.new(name="UVMap")
            for loop in data.loops:
                layer.data[loop.index].uv = uvs[loop.vertex_index]
        return obj

    def bevel(obj, width=0.025, segments=2):
        modifier = obj.modifiers.new("WearAndSoftEdges", "BEVEL")
        modifier.width = width
        modifier.segments = segments
        modifier.limit_method = "ANGLE"
        try:
            modifier.harden_normals = True
        except Exception:
            pass
        return obj

    def make_box(name, location, dimensions, mat, coll, rotation=None, parent=None, edge=0.025):
        hx, hy, hz = (dimension * 0.5 for dimension in dimensions)
        verts = [
            (-hx, -hy, -hz), (hx, -hy, -hz), (hx, hy, -hz), (-hx, hy, -hz),
            (-hx, -hy, hz), (hx, -hy, hz), (hx, hy, hz), (-hx, hy, hz),
        ]
        faces = [(0, 3, 2, 1), (4, 5, 6, 7), (0, 1, 5, 4), (1, 2, 6, 5), (2, 3, 7, 6), (3, 0, 4, 7)]
        obj = mesh_object(name, verts, faces, [mat], coll, smooth=False)
        obj.location = location
        if rotation is not None:
            obj.rotation_mode = "QUATERNION"
            obj.rotation_quaternion = rotation
        if parent is not None:
            obj.parent = parent
        if edge > 0:
            bevel(obj, edge)
        return obj

    def make_cylinder(name, location, radius, depth, mat, coll, parent=None, vertices=16, edge=0.012):
        bpy.ops.mesh.primitive_cylinder_add(vertices=vertices, radius=radius, depth=depth, location=location)
        obj = bpy.context.object
        obj.name = name
        link(obj, coll)
        obj.data.materials.append(mat)
        for polygon in obj.data.polygons:
            polygon.use_smooth = True
        if parent is not None:
            obj.parent = parent
        if edge:
            bevel(obj, edge)
        return obj

    def make_cone(name, location, radius1, radius2, depth, mat, coll, parent=None, vertices=14):
        bpy.ops.mesh.primitive_cone_add(vertices=vertices, radius1=radius1, radius2=radius2, depth=depth, location=location)
        obj = bpy.context.object
        obj.name = name
        link(obj, coll)
        obj.data.materials.append(mat)
        for polygon in obj.data.polygons:
            polygon.use_smooth = True
        if parent is not None:
            obj.parent = parent
        bevel(obj, 0.018)
        return obj

    def make_text(name, body, location, size, mat, coll, rotation=(0.0, 0.0, 0.0), parent=None, extrude=0.015):
        data = bpy.data.curves.new(name, "FONT")
        data.body = body
        data.align_x = "CENTER"
        data.align_y = "CENTER"
        data.size = size
        data.extrude = extrude
        data.bevel_depth = 0.004
        data.bevel_resolution = 1
        if font is not None:
            data.font = font
        obj = bpy.data.objects.new(name, data)
        coll.objects.link(obj)
        obj.location = location
        obj.rotation_euler = rotation
        if parent is not None:
            obj.parent = parent
        data.materials.append(mat)
        return obj

    def closed_curve(name, points, radius, mat, coll):
        data = bpy.data.curves.new(name, "CURVE")
        data.dimensions = "3D"
        data.resolution_u = 1
        data.bevel_depth = radius
        data.bevel_resolution = 2
        data.use_fill_caps = True
        spline = data.splines.new("POLY")
        spline.points.add(len(points) - 1)
        for point, coordinate in zip(spline.points, points):
            point.co = (*coordinate, 1.0)
        spline.use_cyclic_u = True
        obj = bpy.data.objects.new(name, data)
        coll.objects.link(obj)
        data.materials.append(mat)
        return obj

    def basis(right, tangent):
        up = Vector((0.0, 0.0, 1.0))
        return Matrix((right, tangent, up)).transposed()

    def frames_for(centers):
        frames = []
        cumulative = [0.0]
        for index, center in enumerate(centers):
            previous = centers[(index - 1) % len(centers)]
            following = centers[(index + 1) % len(centers)]
            tangent = (following - previous).normalized()
            horizontal = Vector((tangent.x, tangent.y, 0.0))
            if horizontal.length < 1e-6:
                horizontal = Vector((1.0, 0.0, 0.0))
            horizontal.normalize()
            right = Vector((horizontal.y, -horizontal.x, 0.0))
            if index:
                cumulative.append(cumulative[-1] + (center - centers[index - 1]).length)
            frames.append((center, right, tangent, cumulative[-1]))
        return frames

    def strip(name, frames, lateral_positions, vertical_offsets, materials, coll,
              material_indices=None, uv_scale=4.0):
        verts = []
        uvs = []
        for center, right, _, distance_along in frames:
            for lateral, vertical in zip(lateral_positions, vertical_offsets):
                verts.append(tuple(center + right * lateral + Vector((0.0, 0.0, vertical))))
                uvs.append((distance_along / uv_scale, lateral * 0.25 + 0.5))
        faces = []
        indices = []
        columns = len(lateral_positions)
        for index in range(len(frames)):
            following = (index + 1) % len(frames)
            for column in range(columns - 1):
                faces.append((index * columns + column, following * columns + column,
                              following * columns + column + 1, index * columns + column + 1))
                indices.append(material_indices[index] if material_indices else 0)
        return mesh_object(name, verts, faces, materials, coll, smooth=True, material_indices=indices, uvs=uvs)

    def ribbon(name, frames, width, vertical, mat, coll):
        return strip(name, frames, [-width * 0.5, 0.0, width * 0.5],
                     [vertical - 0.018, vertical + 0.028, vertical - 0.018], [mat], coll)

    def terrain_follow(name, frames, inner_width, outer_width, base_z, mat, coll):
        verts = []
        lateral = [-outer_width * 0.5, -inner_width * 0.5, 0.0, inner_width * 0.5, outer_width * 0.5]
        for center, right, _, _ in frames:
            heights = [base_z + 0.02, center.z - 0.16, center.z - 0.10, center.z - 0.16, base_z + 0.02]
            verts.extend(tuple(center + right * offset + Vector((0.0, 0.0, height - center.z))) for offset, height in zip(lateral, heights))
        faces = []
        columns = len(lateral)
        for index in range(len(frames)):
            following = (index + 1) % len(frames)
            for column in range(columns - 1):
                faces.append((index * columns + column, following * columns + column,
                              following * columns + column + 1, index * columns + column + 1))
        obj = mesh_object(name, verts, faces, [mat], coll, smooth=True)
        bevel(obj, 0.08, 2)
        return obj

    def frame_at(frames, distance_along):
        total = frames[-1][3] + (frames[0][0] - frames[-1][0]).length
        distance_along %= total
        for index in range(len(frames)):
            following = (index + 1) % len(frames)
            start_distance = frames[index][3]
            end_distance = frames[following][3] if following else total
            if start_distance <= distance_along <= end_distance:
                span = max(1e-6, end_distance - start_distance)
                t = (distance_along - start_distance) / span
                center = frames[index][0].lerp(frames[following][0], t)
                tangent = (frames[following][0] - frames[index][0]).normalized()
                horizontal = Vector((tangent.x, tangent.y, 0.0)).normalized()
                right = Vector((horizontal.y, -horizontal.x, 0.0))
                return center, right, tangent
        return frames[0]

    def add_gate(track_id, label, frames, distance_along, road_width, coll, mats, start=False):
        center, right, tangent = frame_at(frames, distance_along)
        root = bpy.data.objects.new(f"{track_id}_{label.replace(' ', '_')}", None)
        coll.objects.link(root)
        root.location = center
        root.rotation_mode = "QUATERNION"
        root.rotation_quaternion = basis(right, tangent).to_quaternion()
        root["kind"] = "start_arch" if start else "checkpoint_gate"
        root["route_distance_m"] = distance_along
        half = road_width * 0.5 + 1.35
        height = 5.8 if start else 4.6
        for side in (-1, 1):
            make_box(f"{label}_Pillar_{side}", (side * half, 0.0, height * 0.5),
                     (0.48, 0.58, height), mats["structure"], coll, parent=root, edge=0.055)
            make_cylinder(f"{label}_Finial_{side}", (side * half, 0.0, height + 0.18),
                          0.20, 0.38, mats["brass"], coll, parent=root, vertices=12)
        make_box(f"{label}_Header", (0.0, 0.0, height),
                 (road_width + 3.4, 0.52, 0.42), mats["structure"], coll, parent=root, edge=0.045)
        banner_height = 1.25 if start else 0.95
        make_box(f"{label}_Banner", (0.0, -0.29, height - 0.72),
                 (road_width + 2.8, 0.09, banner_height), mats["banner"], coll, parent=root, edge=0.035)
        make_text(f"{label}_Text", label, (0.0, -0.35, height - 0.72),
                  0.62 if start else 0.55, mats["lettering"], coll,
                  rotation=(math.pi / 2.0, 0.0, 0.0), parent=root, extrude=0.008)
        if start:
            columns = 12
            cell = road_width / columns
            for column in range(columns):
                for row in range(2):
                    mat = mats["ivory"] if (column + row) % 2 == 0 else mats["asphalt"]
                    make_box(f"StartChecker_{column}_{row}", (-road_width * 0.5 + cell * (column + 0.5), -0.65 - row * cell, 0.055),
                             (cell, cell, 0.035), mat, coll, parent=root, edge=0.0)
            for row in range(2):
                for column in range(4):
                    lateral = (column - 1.5) * max(2.2, road_width * 0.22)
                    make_box(f"StartGrid_{row}_{column}", (lateral, -4.0 - row * 3.0, 0.05),
                             (1.6, 0.14, 0.025), mats["ivory"], coll, parent=root, edge=0.0)

    def add_barriers(track_id, frames, road_width, coll, mats, scenic):
        offset = road_width * 0.5 + 2.15
        for side in (-1, 1):
            rail_points = []
            lower_points = []
            post_verts = []
            post_faces = []
            base_faces = [(0, 3, 2, 1), (4, 5, 6, 7), (0, 1, 5, 4), (1, 2, 6, 5), (2, 3, 7, 6), (3, 0, 4, 7)]
            next_post = 0.0
            for center, right, tangent, distance_along in frames:
                position = center + right * offset * side
                rail_points.append(tuple(position + Vector((0.0, 0.0, 1.18))))
                lower_points.append(tuple(position + Vector((0.0, 0.0, 0.56))))
                if distance_along + 1e-5 >= next_post:
                    rotation = basis(right, tangent).to_quaternion()
                    hx, hy, hz = 0.11, 0.16, 0.68
                    local = [(-hx, -hy, -hz), (hx, -hy, -hz), (hx, hy, -hz), (-hx, hy, -hz),
                             (-hx, -hy, hz), (hx, -hy, hz), (hx, hy, hz), (-hx, hy, hz)]
                    origin = position + Vector((0.0, 0.0, 0.68))
                    start_index = len(post_verts)
                    post_verts.extend(tuple(origin + rotation @ Vector(vertex)) for vertex in local)
                    post_faces.extend(tuple(start_index + index for index in face) for face in base_faces)
                    next_post += 6.5
            closed_curve(f"{track_id}_BarrierTop_{side}", rail_points, 0.065, mats["barrier"], coll)
            closed_curve(f"{track_id}_BarrierLower_{side}", lower_points, 0.045, mats["barrier"], coll)
            posts = mesh_object(f"{track_id}_BarrierPosts_{side}", post_verts, post_faces, [mats["barrier"]], coll, smooth=False)
            bevel(posts, 0.018)
            if scenic == "town":
                wall_verts = []
                wall_faces = []
                for center, right, _, _ in frames:
                    position = center + right * offset * side
                    wall_verts.extend([tuple(position + Vector((0.0, 0.0, 0.12))), tuple(position + Vector((0.0, 0.0, 0.82)))])
                for index in range(len(frames)):
                    following = (index + 1) % len(frames)
                    wall_faces.append((index * 2, following * 2, following * 2 + 1, index * 2 + 1))
                wall = mesh_object(f"{track_id}_ConcreteBarrier_{side}", wall_verts, wall_faces, [mats["concrete"]], coll, smooth=False)
                bevel(wall, 0.035)

    def add_context(track_id, scenery, frames, road_width, bounds, base_z, coll, mats):
        import random
        rng = random.Random(1912 + TRACK_ORDER.index(track_id) * 97)
        min_x, max_x, min_y, max_y = bounds
        centers = [frame[0] for frame in frames]

        def clear_position(x, y, margin=9.0):
            return min((Vector((x, y, 0.0)) - Vector((center.x, center.y, 0.0))).length for center in centers) > road_width * 0.5 + margin

        if scenery in {"forest", "speedway"} or track_id in {"alps_pass", "tver_forest", "winter_departure"}:
            placed = 0
            attempts = 0
            while placed < 22 and attempts < 500:
                attempts += 1
                x = rng.uniform(min_x - 28.0, max_x + 28.0)
                y = rng.uniform(min_y - 28.0, max_y + 28.0)
                if not clear_position(x, y, 10.0):
                    continue
                height = rng.uniform(5.0, 8.5)
                make_cylinder(f"TreeTrunk_{placed}", (x, y, base_z + height * 0.28),
                              0.16, height * 0.56, mats["wood"], coll, vertices=10)
                for level in range(3):
                    radius = height * (0.30 - level * 0.065)
                    make_cone(f"TreeCrown_{placed}_{level}", (x, y, base_z + height * (0.48 + level * 0.18)),
                              radius, 0.04, height * 0.46, mats["leaf"] if level % 2 else mats["leaf_light"], coll, vertices=12)
                if track_id == "winter_departure":
                    make_cone(f"TreeSnow_{placed}", (x, y, base_z + height * 0.82),
                              height * 0.15, 0.02, height * 0.28, mats["snow"], coll, vertices=12)
                placed += 1

        if track_id in {"alps_pass", "crimean_coast"}:
            placed = 0
            attempts = 0
            while placed < 12 and attempts < 400:
                attempts += 1
                x = rng.uniform(min_x - 20.0, max_x + 20.0)
                y = rng.uniform(min_y - 20.0, max_y + 20.0)
                if not clear_position(x, y, 12.0):
                    continue
                bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=2, radius=1.0, location=(x, y, base_z + 1.2))
                rock = bpy.context.object
                rock.name = f"ContextRock_{placed}"
                link(rock, coll)
                rock.scale = (rng.uniform(1.8, 4.2), rng.uniform(1.5, 3.4), rng.uniform(1.2, 3.0))
                for vertex in rock.data.vertices:
                    vertex.co *= rng.uniform(0.88, 1.12)
                rock.data.materials.append(mats["rock"])
                for polygon in rock.data.polygons:
                    polygon.use_smooth = True
                if track_id == "alps_pass":
                    bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=1, radius=1.0, location=(x - 0.2, y, base_z + 2.6))
                    cap = bpy.context.object
                    cap.name = f"ContextSnowCap_{placed}"
                    link(cap, coll)
                    cap.scale = (rock.scale.x * 0.72, rock.scale.y * 0.70, 0.55)
                    cap.data.materials.append(mats["snow"])
                    for polygon in cap.data.polygons:
                        polygon.use_smooth = True
                placed += 1

        if scenery == "coast":
            water_width = max_x - min_x + 150.0
            make_box("CoastalWater", ((min_x + max_x) * 0.5, max_y + 75.0, base_z - 0.12),
                     (water_width, 105.0, 0.18), mats["water"], coll, edge=0.08)
            for index in range(9):
                x = rng.uniform(min_x, max_x)
                y = max_y + rng.uniform(20.0, 55.0)
                bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=1, radius=1.0, location=(x, y, base_z + 0.25))
                rock = bpy.context.object
                rock.name = f"CoastalRock_{index}"
                link(rock, coll)
                rock.scale = (rng.uniform(1.0, 2.8), rng.uniform(0.8, 2.0), rng.uniform(0.5, 1.3))
                rock.data.materials.append(mats["rock"])
                for polygon in rock.data.polygons:
                    polygon.use_smooth = True

        if scenery == "town":
            placed = 0
            attempts = 0
            while placed < 7 and attempts < 500:
                attempts += 1
                x = rng.uniform(min_x - 18.0, max_x + 18.0)
                y = rng.uniform(min_y - 18.0, max_y + 18.0)
                if not clear_position(x, y, 14.0):
                    continue
                width, depth, height = rng.uniform(8.0, 13.0), rng.uniform(7.0, 11.0), rng.uniform(5.0, 9.0)
                root = bpy.data.objects.new(f"TownBuilding_{placed}", None)
                coll.objects.link(root)
                root.location = (x, y, base_z)
                root.rotation_euler.z = rng.uniform(-0.35, 0.35)
                make_box(f"TownWalls_{placed}", (0.0, 0.0, height * 0.5), (width, depth, height),
                         mats["plaster"], coll, parent=root, edge=0.08)
                roof_verts = [(-width * 0.58, -depth * 0.58, height), (width * 0.58, -depth * 0.58, height),
                              (width * 0.58, depth * 0.58, height), (-width * 0.58, depth * 0.58, height),
                              (0.0, -depth * 0.58, height + 2.4), (0.0, depth * 0.58, height + 2.4)]
                roof_faces = [(0, 1, 4), (3, 5, 2), (0, 4, 5, 3), (1, 2, 5, 4), (0, 3, 2, 1)]
                mesh_object(f"TownRoof_{placed}", roof_verts, roof_faces, [mats["roof"]], coll, smooth=False).parent = root
                for wx in (-width * 0.28, width * 0.28):
                    for wz in (1.8, 3.8):
                        if wz < height - 0.5:
                            make_box(f"TownWindow_{placed}_{wx}_{wz}", (wx, -depth * 0.5 - 0.035, wz),
                                     (1.15, 0.07, 1.35), mats["glass"], coll, parent=root, edge=0.015)
                placed += 1

        if track_id == "riga_speedway":
            center, right, tangent = frame_at(frames, 55.0)
            root = bpy.data.objects.new("RigaGrandstand", None)
            coll.objects.link(root)
            root.location = center + right * -(road_width * 0.5 + 10.0)
            root.rotation_mode = "QUATERNION"
            root.rotation_quaternion = basis(right, tangent).to_quaternion()
            for row in range(5):
                make_box(f"GrandstandStep_{row}", (0.0, row * 0.9, 0.28 + row * 0.28),
                         (20.0, 0.95, 0.56 + row * 0.56), mats["wood"], coll, parent=root, edge=0.035)
            for x in (-9.4, 9.4):
                for y in (-0.5, 4.4):
                    make_cylinder(f"GrandstandPost_{x}_{y}", (x, y, 2.8), 0.09, 5.6,
                                  mats["structure"], coll, parent=root, vertices=10)
            make_box("GrandstandRoof", (0.0, 1.8, 5.65), (21.0, 7.2, 0.22),
                     mats["banner"], coll, parent=root, edge=0.045)

    def add_lighting_and_cameras(track_id, metrics, bounds, min_y, max_y):
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
        scene.render.image_settings.color_mode = "RGB"
        scene.render.resolution_x = 1600
        scene.render.resolution_y = 1200
        scene.render.resolution_percentage = 100
        scene.render.film_transparent = False
        try:
            scene.eevee.taa_render_samples = 48
            scene.eevee.use_gtao = True
            scene.eevee.gtao_distance = 3.0
            scene.eevee.gtao_factor = 1.15
            scene.eevee.use_soft_shadows = True
        except Exception:
            pass
        world = bpy.data.worlds.new(f"World_{track_id}")
        world.use_nodes = True
        world.node_tree.nodes["Background"].inputs["Color"].default_value = (0.16, 0.20, 0.25, 1.0)
        world.node_tree.nodes["Background"].inputs["Strength"].default_value = 0.55
        scene.world = world
        min_x, max_x, min_z, max_z = bounds
        center = Vector(((min_x + max_x) * 0.5, (min_z + max_z) * 0.5, (min_y + max_y) * 0.5))
        sun_data = bpy.data.lights.new(f"Sun_{track_id}", "SUN")
        sun_data.energy = 2.2
        sun_data.angle = math.radians(18.0)
        sun = bpy.data.objects.new(f"Sun_{track_id}", sun_data)
        scene.collection.objects.link(sun)
        sun.location = center + Vector((-180.0, -240.0, 420.0))
        sun.rotation_euler = (math.radians(28.0), math.radians(-22.0), math.radians(-38.0))
        fill_data = bpy.data.lights.new(f"Fill_{track_id}", "AREA")
        fill_data.energy = 4500.0
        fill_data.shape = "DISK"
        fill_data.size = max(120.0, max(metrics.width, metrics.depth) * 0.65)
        fill = bpy.data.objects.new(f"Fill_{track_id}", fill_data)
        scene.collection.objects.link(fill)
        fill.location = center + Vector((150.0, -180.0, 260.0))

        top_data = bpy.data.cameras.new(f"Camera_{track_id}_Top")
        top_data.type = "ORTHO"
        top_data.ortho_scale = max(metrics.width, metrics.depth) + 115.0
        top_data.clip_end = 4000.0
        top = bpy.data.objects.new(f"Camera_{track_id}_Top", top_data)
        scene.collection.objects.link(top)
        top.location = Vector((center.x, center.y, max_y + max(metrics.width, metrics.depth) * 1.2 + 160.0))
        top.rotation_euler = (0.0, 0.0, 0.0)

        perspective_data = bpy.data.cameras.new(f"Camera_{track_id}_Perspective")
        perspective_data.type = "PERSP"
        perspective_data.lens = 48.0
        perspective_data.clip_end = 5000.0
        perspective = bpy.data.objects.new(f"Camera_{track_id}_Perspective", perspective_data)
        scene.collection.objects.link(perspective)
        span = max(metrics.width, metrics.depth)
        perspective.location = center + Vector((span * 0.88, -span * 1.18, span * 0.72 + max_y + 55.0))
        direction = center - perspective.location
        perspective.rotation_euler = direction.to_track_quat("-Z", "Y").to_euler()
        scene.camera = perspective
        return top, perspective

    for track, metrics in zip(tracks, all_metrics):
        print(f"BUILD {track.track_id}: {metrics.control_count} controls, {metrics.length:.1f} m", flush=True)
        bpy.ops.wm.read_factory_settings(use_empty=True)
        bpy.context.preferences.filepaths.save_version = 0
        font = bpy.data.fonts.load(str(font_path), check_existing=True) if font_path.exists() else None
        scene = bpy.context.scene
        scene.name = track.track_id
        road_coll = collection(f"{track.track_id}_Road")
        structure_coll = collection(f"{track.track_id}_Structures")
        terrain_coll = collection(f"{track.track_id}_Terrain")
        scenery_coll = collection(f"{track.track_id}_Context")
        label_coll = collection(f"{track.track_id}_Labels")
        root = bpy.data.objects.new(f"TRACK_{track.track_id}", None)
        scene.collection.objects.link(root)
        root["units"] = "meters"
        root["unity_to_blender"] = "(x, y, z) -> (x, z, y)"
        root["validated_length_m"] = metrics.length
        root["minimum_radius_m"] = metrics.minimum_radius
        root["maximum_grade_percent"] = metrics.maximum_grade_percent
        root["nonlocal_clearance_m"] = metrics.nonlocal_clearance

        mats = {
            "asphalt": pbr(f"{track.track_id}_Asphalt", track.road_color, 0.88, 0.0, 0.24, 7.5),
            "shoulder": pbr(f"{track.track_id}_GravelShoulder", tuple(min(1.0, c * 1.28) for c in track.road_color), 0.96, 0.0, 0.32, 5.0),
            "ground": pbr(f"{track.track_id}_Terrain", track.ground_color, 0.98, 0.0, 0.30, 1.8),
            "curb": pbr(f"{track.track_id}_CurbPaint", track.curb_color, 0.72, 0.0, 0.10, 4.0),
            "ivory": pbr(f"{track.track_id}_IvoryPaint", (0.82, 0.80, 0.70), 0.66, 0.0, 0.08, 5.0),
            "red": pbr(f"{track.track_id}_RedPaint", (0.36, 0.045, 0.035), 0.62, 0.05, 0.10, 5.0),
            "barrier": pbr(f"{track.track_id}_Barrier", (0.12, 0.14, 0.13) if track.scenery != "forest" else (0.22, 0.13, 0.065),
                           0.58 if track.scenery != "forest" else 0.86, 0.62 if track.scenery != "forest" else 0.0, 0.12, 4.0),
            "structure": pbr(f"{track.track_id}_Structure", (0.075, 0.095, 0.085), 0.48, 0.65, 0.10, 5.0),
            "brass": pbr(f"{track.track_id}_Brass", (0.56, 0.34, 0.10), 0.30, 0.85, 0.06, 6.0),
            "banner": pbr(f"{track.track_id}_Banner", (0.10, 0.19, 0.12), 0.92, 0.0, 0.08, 12.0),
            "lettering": pbr(f"{track.track_id}_Lettering", (0.90, 0.83, 0.60), 0.50, 0.15, 0.03, 8.0, emission=0.12),
            "wood": pbr(f"{track.track_id}_Wood", (0.20, 0.105, 0.052), 0.90, 0.0, 0.26, 3.0),
            "leaf": pbr(f"{track.track_id}_Leaves", (0.055, 0.16, 0.075), 0.94, 0.0, 0.18, 4.0),
            "leaf_light": pbr(f"{track.track_id}_LeavesLight", (0.09, 0.23, 0.10), 0.92, 0.0, 0.16, 4.0),
            "snow": pbr(f"{track.track_id}_Snow", (0.88, 0.93, 0.97), 0.84, 0.0, 0.10, 8.0),
            "rock": pbr(f"{track.track_id}_Rock", (0.30, 0.33, 0.31), 0.96, 0.0, 0.38, 3.0),
            "water": pbr(f"{track.track_id}_Water", (0.035, 0.16, 0.21), 0.22, 0.10, 0.28, 2.2),
            "concrete": pbr(f"{track.track_id}_Concrete", (0.48, 0.47, 0.42), 0.94, 0.0, 0.20, 5.0),
            "plaster": pbr(f"{track.track_id}_Plaster", (0.64, 0.55, 0.40), 0.94, 0.0, 0.18, 4.0),
            "roof": pbr(f"{track.track_id}_Roof", (0.22, 0.095, 0.055), 0.78, 0.15, 0.16, 5.0),
            "glass": pbr(f"{track.track_id}_Glass", (0.10, 0.22, 0.24), 0.18, 0.20, 0.03, 7.0),
        }

        unity_dense = sample_dense(track.waypoints, spacing=1.5)
        centers = [Vector((point[0], point[2], point[1])) for point in unity_dense]
        frames = frames_for(centers)
        min_x, max_x = min(center.x for center in centers), max(center.x for center in centers)
        min_y, max_y = min(center.y for center in centers), max(center.y for center in centers)
        min_z, max_z = min(center.z for center in centers), max(center.z for center in centers)
        bounds = (min_x, max_x, min_y, max_y)
        pad_width = metrics.width + 86.0
        pad_depth = metrics.depth + 86.0
        pad_top = min_z - 0.34
        pad_center = ((min_x + max_x) * 0.5, (min_y + max_y) * 0.5, pad_top - 0.65)
        make_box(f"{track.track_id}_TerrainPad", pad_center, (pad_width, pad_depth, 1.30),
                 mats["ground"], terrain_coll, edge=3.0)
        terrain_follow(f"{track.track_id}_ElevatedTerrainFollow", frames, track.road_width + 8.0,
                       track.road_width + 30.0, pad_top + 0.03, mats["ground"], terrain_coll)
        # Fine contour loops make the pad read as a surveyed terrain model.
        for index in range(3):
            inset = 10.0 + index * 7.0
            half_x = pad_width * 0.5 - inset
            half_y = pad_depth * 0.5 - inset
            cx, cy = pad_center[0], pad_center[1]
            contour_points = [
                (cx - half_x, cy - half_y, pad_top + 0.025), (cx + half_x, cy - half_y, pad_top + 0.025),
                (cx + half_x, cy + half_y, pad_top + 0.025), (cx - half_x, cy + half_y, pad_top + 0.025),
            ]
            closed_curve(f"{track.track_id}_Contour_{index}", contour_points, 0.035,
                         mats["curb"], terrain_coll)

        ribbon(f"{track.track_id}_Shoulder", frames, track.road_width + 2.4, -0.035, mats["shoulder"], road_coll)
        ribbon(f"{track.track_id}_Road", frames, track.road_width, 0.035, mats["asphalt"], road_coll)
        for side in (-1, 1):
            edge_offset = side * (track.road_width * 0.5 - 0.22)
            strip(f"{track.track_id}_EdgeLine_{side}", frames, [edge_offset - 0.055, edge_offset + 0.055],
                  [0.073, 0.073], [mats["ivory"]], road_coll)
            curb_offset = side * (track.road_width * 0.5 + 0.28)
            curb_indices = [int(distance_along / 2.0) % 2 for _, _, _, distance_along in frames]
            curb = strip(f"{track.track_id}_Curb_{side}", frames,
                         [curb_offset - 0.28, curb_offset + 0.28], [0.105, 0.105],
                         [mats["curb"], mats["red"]], road_coll, material_indices=curb_indices)
            bevel(curb, 0.022)
        # Dashed centre guides are deliberately subtle and improve aerial readability.
        dash_verts, dash_faces = [], []
        for distance_along in range(8, int(metrics.length), 13):
            center, right, tangent = frame_at(frames, float(distance_along))
            start_index = len(dash_verts)
            for lateral, longitudinal in ((-0.055, -1.8), (0.055, -1.8), (0.055, 1.8), (-0.055, 1.8)):
                dash_verts.append(tuple(center + right * lateral + tangent * longitudinal + Vector((0.0, 0.0, 0.082))))
            dash_faces.append((start_index, start_index + 1, start_index + 2, start_index + 3))
        mesh_object(f"{track.track_id}_CentreDashes", dash_verts, dash_faces, [mats["ivory"]], road_coll, smooth=False)

        add_barriers(track.track_id, frames, track.road_width, structure_coll, mats, track.scenery)
        total_length = frames[-1][3] + (frames[0][0] - frames[-1][0]).length
        add_gate(track.track_id, track.track_id.upper().replace("_", " "), frames, 0.0,
                 track.road_width, structure_coll, mats, start=True)
        for index, fraction in enumerate((0.25, 0.50, 0.75), start=1):
            add_gate(track.track_id, f"SECTOR {index:02d}", frames, total_length * fraction,
                     track.road_width, structure_coll, mats)

        # Direction arrows are modelled meshes, not screen overlays.
        for index, distance_along in enumerate(range(55, int(metrics.length), 95)):
            center, right, tangent = frame_at(frames, float(distance_along))
            root_arrow = bpy.data.objects.new(f"DirectionArrow_{index}", None)
            road_coll.objects.link(root_arrow)
            root_arrow.location = center + Vector((0.0, 0.0, 0.09))
            root_arrow.rotation_mode = "QUATERNION"
            root_arrow.rotation_quaternion = basis(right, tangent).to_quaternion()
            arrow_verts = [(0.0, -1.0, 0.0), (0.62, 0.15, 0.0), (0.22, 0.15, 0.0),
                           (0.22, 1.25, 0.0), (-0.22, 1.25, 0.0), (-0.22, 0.15, 0.0), (-0.62, 0.15, 0.0)]
            arrow_faces = [(0, 1, 2, 3, 4, 5, 6)]
            mesh_object(f"DirectionArrowMesh_{index}", arrow_verts, arrow_faces, [mats["ivory"]], road_coll, smooth=False).parent = root_arrow

        add_context(track.track_id, track.scenery, frames, track.road_width, bounds, pad_top + 0.02,
                    scenery_coll, mats)
        label_x = min_x - 22.0
        label_y = max_y + 26.0
        make_text(f"{track.track_id}_Title", track.display_name, (label_x, label_y, pad_top + 0.06),
                  4.2, mats["lettering"], label_coll, rotation=(0.0, 0.0, 0.0), extrude=0.025)
        make_text(f"{track.track_id}_Metrics",
                  f"{track.track_id.upper()}  |  {metrics.length:.0f} M  |  RMIN {metrics.minimum_radius:.1f} M  |  GRADE {metrics.maximum_grade_percent:.1f}%",
                  (label_x, label_y - 6.0, pad_top + 0.06), 1.55, mats["ivory"], label_coll,
                  rotation=(0.0, 0.0, 0.0), extrude=0.012)

        top_camera, perspective_camera = add_lighting_and_cameras(track.track_id, metrics, bounds, min_z, max_z)
        track_dir = ASSET_DIR / track.track_id
        track_dir.mkdir(parents=True, exist_ok=True)
        blend_path = track_dir / f"{track.track_id}_layout.blend"
        top_path = PREVIEW_DIR / f"{track.track_id}_top.png"
        perspective_path = PREVIEW_DIR / f"{track.track_id}_perspective.png"

        scene.camera = top_camera
        scene.render.filepath = str(top_path)
        bpy.ops.render.render(write_still=True)
        scene.camera = perspective_camera
        scene.render.filepath = str(perspective_path)
        bpy.ops.render.render(write_still=True)
        scene.camera = perspective_camera
        bpy.ops.wm.save_as_mainfile(filepath=str(blend_path), check_existing=False, compress=True)
        print(f"DONE {track.track_id}: {blend_path.name}, {top_path.name}, {perspective_path.name}", flush=True)

    payload = {
        "generator": "tools/build_track_layouts.py",
        "blender_version": "5.2",
        "route_sampling": "closed uniform Catmull-Rom; 48 runtime samples per control segment",
        "clearance_rule": "minimum 3D centerline distance for portions >=60 m apart along route",
        "tracks": [metrics_dict(track, metrics) for track, metrics in zip(tracks, all_metrics)],
    }
    (ASSET_DIR / "layout_metrics.json").write_text(json.dumps(payload, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--validate-only", action="store_true", help="validate assets without starting Blender")
    if "--" in sys.argv:
        cli_args = sys.argv[sys.argv.index("--") + 1:]
    elif Path(sys.argv[0]).name.lower().startswith("blender"):
        cli_args = []
    else:
        cli_args = sys.argv[1:]
    args = parser.parse_args(cli_args)
    tracks, all_metrics = validate_all()
    if args.validate_only:
        print("All track layouts satisfy the route constraints.")
        return
    build_blender_layouts(tracks, all_metrics)
    print("All Blender track layouts and previews were generated.")


if __name__ == "__main__":
    main()
