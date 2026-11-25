from pathlib import Path
import json
import math
import numpy as np
from PIL import Image, ImageDraw, ImageFilter

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / "game_assets" / "textures" / "shared"
OUT.mkdir(parents=True, exist_ok=True)
SIZE = 512
RNG = np.random.default_rng(1912)
MANIFEST = []


def multiscale_noise(size=SIZE, seed=1):
    rng = np.random.default_rng(seed)
    result = np.zeros((size, size), dtype=np.float32)
    weights = [(4, 0.42), (12, 0.28), (48, 0.18), (160, 0.12)]
    for cells, weight in weights:
        grid = rng.random((cells, cells), dtype=np.float32)
        img = Image.fromarray(np.uint8(grid * 255), "L").resize((size, size), Image.Resampling.BICUBIC)
        result += np.asarray(img, dtype=np.float32) / 255.0 * weight
    return result


def normal_from_height(height, strength=2.0):
    dx = np.zeros_like(height)
    dy = np.zeros_like(height)
    dx[:, 1:-1] = height[:, 2:] - height[:, :-2]
    dx[:, 0] = height[:, 1] - height[:, 0]
    dx[:, -1] = height[:, -1] - height[:, -2]
    dy[1:-1, :] = height[2:, :] - height[:-2, :]
    dy[0, :] = height[1, :] - height[0, :]
    dy[-1, :] = height[-1, :] - height[-2, :]
    n = np.stack([-dx * strength, -dy * strength, np.ones_like(height)], axis=-1)
    n /= np.linalg.norm(n, axis=-1, keepdims=True)
    return np.uint8(np.clip((n * 0.5 + 0.5) * 255, 0, 255))


def save_set(name, color, height, roughness, normal_strength=2.0):
    color_img = Image.fromarray(np.uint8(np.clip(color, 0, 255)), "RGB")
    rough_img = Image.fromarray(np.uint8(np.clip(np.broadcast_to(roughness, height.shape), 0, 255)), "L")
    normal_img = Image.fromarray(normal_from_height(height, normal_strength), "RGB")
    for suffix, img in [("basecolor", color_img), ("roughness", rough_img), ("normal", normal_img)]:
        path = OUT / f"{name}_{suffix}.png"
        img.save(path, optimize=True)
        MANIFEST.append({"file": path.relative_to(ROOT).as_posix(), "kind": suffix, "license": "CC0-1.0", "generator": "tools/generate_textures.py"})


def color_field(base, variation):
    return np.clip(np.asarray(base)[None, None, :] + variation[:, :, None], 0, 255)


noise = multiscale_noise(seed=24)
fine = RNG.normal(0, 1, (SIZE, SIZE)).astype(np.float32)

paint_noise = (noise - 0.5) * 7 + fine * 0.7
save_set("paint_racing_green", color_field((30, 67, 55), paint_noise), noise + fine * 0.015, 175 + noise * 35, 0.7)

leather_height = np.clip(0.45 + (multiscale_noise(seed=55) - 0.5) * 0.7 + fine * 0.08, 0, 1)
leather_color = color_field((79, 43, 25), (leather_height - 0.5) * 45)
save_set("leather_saddle", leather_color, leather_height, 185 + leather_height * 35, 2.4)

x = np.linspace(0, 1, SIZE, dtype=np.float32)[None, :]
y = np.linspace(0, 1, SIZE, dtype=np.float32)[:, None]
grain = np.sin((x * 34 + np.sin(y * 9) * 1.7 + noise * 2.5) * math.pi)
wood_height = np.clip(0.5 + grain * 0.22 + (noise - 0.5) * 0.45, 0, 1)
wood_color = color_field((101, 55, 27), grain * 17 + (noise - 0.5) * 25)
save_set("wood_walnut", wood_color, wood_height, 160 + noise * 40, 1.8)

dirt_height = np.clip(0.25 + noise * 0.7 + fine * 0.08, 0, 1)
dirt_color = color_field((111, 87, 59), (noise - 0.5) * 50 + fine * 4)
stone_mask = RNG.random((SIZE, SIZE)) > 0.988
dirt_color[stone_mask] = (135, 126, 105)
dirt_height[stone_mask] += 0.22
save_set("road_dirt", dirt_color, dirt_height, 205 + noise * 35, 3.2)

snow_height = np.clip(0.55 + noise * 0.35 + fine * 0.025, 0, 1)
snow_color = color_field((224, 231, 232), (noise - 0.5) * 18)
save_set("road_snow", snow_color, snow_height, 210 + noise * 25, 1.0)

plaster_height = np.clip(0.5 + (noise - 0.5) * 0.65 + fine * 0.035, 0, 1)
plaster_color = color_field((196, 183, 153), (noise - 0.5) * 28)
save_set("plaster_ochre", plaster_color, plaster_height, 205 + noise * 30, 1.7)

fabric_height = 0.5 + 0.08 * np.sin(x * math.pi * 180) + 0.08 * np.sin(y * math.pi * 180) + fine * 0.025
fabric_color = color_field((54, 48, 39), (fabric_height - 0.5) * 38)
save_set("fabric_charcoal", fabric_color, fabric_height, 220, 1.2)

bark_height = np.clip(0.5 + 0.2 * np.sin(x * math.pi * 44 + noise * 6) + (noise - 0.5) * 0.6, 0, 1)
bark_color = color_field((83, 70, 51), (bark_height - 0.5) * 55)
save_set("bark_birch", bark_color, bark_height, 215, 2.8)

cobble_color = np.zeros((SIZE, SIZE, 3), dtype=np.float32)
cobble_height = np.full((SIZE, SIZE), 0.12, dtype=np.float32)
img = Image.fromarray(np.uint8(cobble_color), "RGB")
height_img = Image.fromarray(np.uint8(cobble_height * 255), "L")
draw = ImageDraw.Draw(img)
hdraw = ImageDraw.Draw(height_img)
rows, cols = 10, 8
cell_w, cell_h = SIZE / cols, SIZE / rows
for row in range(rows):
    offset = (row % 2) * cell_w * 0.5
    for col in range(cols):
        x0 = col * cell_w + offset + 3
        x1 = (col + 1) * cell_w + offset - 3
        y0 = row * cell_h + 3
        y1 = (row + 1) * cell_h - 3
        tone = int(RNG.integers(92, 133))
        draw.rounded_rectangle((x0, y0, x1, y1), radius=8, fill=(tone, tone - 5, tone - 12))
        hdraw.rounded_rectangle((x0, y0, x1, y1), radius=8, fill=int(RNG.integers(170, 225)))
cobble_color = np.asarray(img, dtype=np.float32)
cobble_height = np.asarray(height_img, dtype=np.float32) / 255.0
save_set("road_cobblestone", cobble_color, cobble_height, 205, 3.4)

brick_color = np.zeros((SIZE, SIZE, 3), dtype=np.float32)
brick_height = np.zeros((SIZE, SIZE), dtype=np.float32)
img = Image.fromarray(np.uint8(brick_color), "RGB")
himg = Image.fromarray(np.uint8(brick_height * 255), "L")
draw = ImageDraw.Draw(img)
hdraw = ImageDraw.Draw(himg)
for row in range(16):
    offset = (row % 2) * 32
    for col in range(9):
        x0 = col * 64 + offset - 32
        x1 = x0 + 60
        y0 = row * 32
        y1 = y0 + 28
        tone = int(RNG.integers(105, 151))
        draw.rectangle((x0, y0, x1, y1), fill=(tone, tone - 34, tone - 43))
        hdraw.rectangle((x0 + 2, y0 + 2, x1 - 2, y1 - 2), fill=int(RNG.integers(175, 220)))
brick_color = np.asarray(img, dtype=np.float32)
brick_height = np.asarray(himg, dtype=np.float32) / 255.0
save_set("brick_red", brick_color, brick_height, 215, 2.6)

roof_color = np.zeros((SIZE, SIZE, 3), dtype=np.float32)
roof_height = np.zeros((SIZE, SIZE), dtype=np.float32)
img = Image.fromarray(np.uint8(roof_color), "RGB")
himg = Image.fromarray(np.uint8(roof_height * 255), "L")
draw = ImageDraw.Draw(img)
hdraw = ImageDraw.Draw(himg)
for row in range(12):
    for col in range(10):
        x0 = col * 52 + (row % 2) * 26 - 20
        x1 = x0 + 55
        y0 = row * 44
        y1 = y0 + 47
        tone = int(RNG.integers(68, 103))
        draw.rounded_rectangle((x0, y0, x1, y1), radius=5, fill=(tone, tone - 8, tone - 13))
        hdraw.rounded_rectangle((x0 + 3, y0 + 3, x1 - 3, y1 - 5), radius=5, fill=190)
        draw.line((x0 + 3, y1 - 4, x1 - 3, y1 - 4), fill=(45, 42, 37), width=2)
roof_color = np.asarray(img, dtype=np.float32)
roof_height = np.asarray(himg, dtype=np.float32) / 255.0
save_set("roof_iron", roof_color, roof_height, 150, 2.2)

particle_size = 256
yy, xx = np.mgrid[0:particle_size, 0:particle_size]
distance = np.sqrt((xx - particle_size / 2) ** 2 + (yy - particle_size / 2) ** 2) / (particle_size / 2)
alpha = np.uint8(np.clip((1.0 - distance) ** 2 * 255, 0, 255))
particle_noise = RNG.uniform(0.82, 1.0, (particle_size, particle_size))
particle = np.zeros((particle_size, particle_size, 4), dtype=np.uint8)
particle[:, :, :3] = np.uint8(np.array([210, 190, 150])[None, None, :] * particle_noise[:, :, None])
particle[:, :, 3] = alpha
particle_path = OUT / "particle_dust.png"
Image.fromarray(particle, "RGBA").save(particle_path, optimize=True)
MANIFEST.append({"file": particle_path.relative_to(ROOT).as_posix(), "kind": "particle", "license": "CC0-1.0", "generator": "tools/generate_textures.py"})

(OUT / "manifest.json").write_text(json.dumps({
    "generated": "2026-11-24",
    "license": "CC0-1.0",
    "resolution": SIZE,
    "maps": MANIFEST,
    "note": "Original procedural material maps generated for RussoBalt The Road; no third-party imagery."
}, ensure_ascii=False, indent=2), encoding="utf-8")
print(f"Generated {len(MANIFEST)} texture maps in {OUT}")
