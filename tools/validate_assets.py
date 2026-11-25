from pathlib import Path
import hashlib
import json
import math
import wave
import numpy as np
from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parents[1]
DOCS = ROOT / "docs"
PREVIEWS = ROOT / "previews"
ERRORS = []
WARNINGS = []
CHECKS = []


def check(condition, message):
    CHECKS.append({"ok": bool(condition), "message": message})
    if not condition:
        ERRORS.append(message)


def sha256(path):
    h = hashlib.sha256()
    with path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b""):
            h.update(chunk)
    return h.hexdigest()


def validate_fbx(path):
    with path.open("rb") as handle:
        header = handle.read(32)
    check(header.startswith(b"Kaydara FBX Binary"), f"FBX header: {path.relative_to(ROOT)}")
    check(path.stat().st_size > 1000, f"FBX non-empty: {path.relative_to(ROOT)}")


def validate_blend(path):
    with path.open("rb") as handle:
        header = handle.read(12)
    check(header.startswith(b"BLENDER") or header.startswith(b"(\xb5/\xfd"), f"BLEND header: {path.relative_to(ROOT)}")


def validate_png(path):
    with Image.open(path) as img:
        img.verify()
    with Image.open(path) as img:
        check(img.width >= 256 and img.height >= 256, f"PNG resolution: {path.relative_to(ROOT)} {img.width}x{img.height}")


def validate_wav(path):
    with wave.open(str(path), "rb") as wf:
        channels = wf.getnchannels()
        rate = wf.getframerate()
        width = wf.getsampwidth()
        frames = wf.getnframes()
        raw = wf.readframes(frames)
    check(rate == 48000, f"WAV sample rate: {path.relative_to(ROOT)}")
    check(width == 2 and channels == 1, f"WAV format: {path.relative_to(ROOT)}")
    data = np.frombuffer(raw, dtype="<i2").astype(np.float32) / 32768.0
    peak = float(np.max(np.abs(data))) if len(data) else 0.0
    rms = float(np.sqrt(np.mean(data * data))) if len(data) else 0.0
    check(peak < 0.98, f"WAV peak below clipping: {path.relative_to(ROOT)} peak={peak:.3f}")
    check(rms > 0.0005, f"WAV not silent: {path.relative_to(ROOT)} rms={rms:.4f}")
    return {"duration_s": round(frames / rate, 3), "peak": round(peak, 4), "rms": round(rms, 4)}


def contact_sheet():
    entries = [
        ("RussoBalt_C24_55_preview.png", "Russo-Balt C24/55 — реконструкция"),
        ("Benz_1912_Racer_Concept_preview.png", "Benz 1912 Racer — концепт"),
        ("Russia_1912_Modular_Kit_preview.png", "Россия 1912 — модульный kit"),
        ("Historical_Characters_preview.png", "Персонажи: водитель, механик, дама"),
        ("RussoBalt_The_Road_Hero.png", "Интегрированная hero-сцена"),
    ]
    canvas = Image.new("RGB", (1600, 1500), (24, 27, 26))
    draw = ImageDraw.Draw(canvas)
    font_path = Path("C:/Windows/Fonts/arial.ttf")
    font = ImageFont.truetype(str(font_path), 24) if font_path.exists() else ImageFont.load_default()
    for index, (filename, label) in enumerate(entries):
        path = PREVIEWS / filename
        if not path.exists():
            WARNINGS.append(f"Missing preview for contact sheet: {filename}")
            continue
        with Image.open(path) as source:
            source = source.convert("RGB")
            source.thumbnail((760, 410), Image.Resampling.LANCZOS)
        col = index % 2
        row = index // 2
        x = 20 + col * 790
        y = 20 + row * 490
        canvas.paste(source, (x, y + 45))
        draw.text((x, y + 5), label, font=font, fill=(230, 226, 210))
    out = PREVIEWS / "Contact_Sheet.png"
    canvas.save(out, optimize=True)
    return out


def main():
    expected = [
        ROOT / "game_assets/vehicles/RussoBalt_C24_55/RussoBalt_C24_55.blend",
        ROOT / "game_assets/vehicles/RussoBalt_C24_55/RussoBalt_C24_55_LOD0.fbx",
        ROOT / "game_assets/vehicles/RussoBalt_C24_55/RussoBalt_C24_55_LOD1.fbx",
        ROOT / "game_assets/vehicles/RussoBalt_C24_55/RussoBalt_C24_55_LOD2.fbx",
        ROOT / "game_assets/vehicles/RussoBalt_C24_55/RussoBalt_C24_55_Detailed.fbx",
        ROOT / "game_assets/vehicles/RussoBalt_C24_55/RussoBalt_C24_55_Collision.fbx",
        ROOT / "game_assets/vehicles/Benz_1912_Prototype/Benz_1912_Racer_Concept.blend",
        ROOT / "game_assets/vehicles/Benz_1912_Prototype/Benz_1912_Racer_Concept_LOD0.fbx",
        ROOT / "game_assets/environment/Russia_1912/Russia_1912_Modular_Kit.blend",
        ROOT / "game_assets/characters/Historical_Characters.blend",
        ROOT / "game_assets/characters/Driver_1912.fbx",
        ROOT / "game_assets/characters/Mechanic_1912.fbx",
        ROOT / "game_assets/characters/Lady_1912.fbx",
        ROOT / "game_assets/environment/Russia_1912/RussoBalt_The_Road_Hero.blend",
        ROOT / "previews/RussoBalt_The_Road_Hero.png",
        ROOT / "audio/manifest.json",
    ]
    for path in expected:
        check(path.exists() and path.stat().st_size > 0, f"Expected file: {path.relative_to(ROOT)}")

    fbx_files = sorted((ROOT / "game_assets").rglob("*.fbx"))
    blend_files = sorted((ROOT / "game_assets").rglob("*.blend"))
    png_files = sorted((ROOT / "game_assets/textures").rglob("*.png")) + sorted(PREVIEWS.glob("*.png"))
    wav_files = sorted((ROOT / "audio/processed").glob("*.wav")) + sorted((ROOT / "audio/engine").glob("*.wav"))
    for path in fbx_files:
        validate_fbx(path)
    for path in blend_files:
        validate_blend(path)
    for path in png_files:
        validate_png(path)
    wav_stats = {path.relative_to(ROOT).as_posix(): validate_wav(path) for path in wav_files}

    for path in sorted((ROOT / "game_assets").rglob("asset_metadata.json")) + [ROOT / "audio/manifest.json", ROOT / "references/history/download_manifest.json"]:
        try:
            json.loads(path.read_text(encoding="utf-8"))
            check(True, f"JSON parse: {path.relative_to(ROOT)}")
        except Exception as exc:
            check(False, f"JSON parse: {path.relative_to(ROOT)} {exc}")

    sheet = contact_sheet()
    roots = [ROOT / "game_assets", ROOT / "audio", ROOT / "previews", ROOT / "docs", ROOT / "references"]
    files = []
    for root in roots:
        for path in sorted(root.rglob("*")):
            if path.is_file() and path.name not in {"ASSET_MANIFEST.json", "VALIDATION_REPORT.json"}:
                files.append({"file": path.relative_to(ROOT).as_posix(), "bytes": path.stat().st_size, "sha256": sha256(path)})
    report = {
        "validated": "2026-11-24",
        "status": "passed" if not ERRORS else "failed",
        "counts": {"fbx": len(fbx_files), "blend": len(blend_files), "png": len(png_files), "wav": len(wav_files), "manifest_files": len(files)},
        "total_bytes": sum(item["bytes"] for item in files),
        "errors": ERRORS,
        "warnings": WARNINGS,
        "wav_stats": wav_stats,
        "checks": CHECKS,
        "limitations": ["Unity is not installed; in-engine import, physics and rendering were not tested.", "FBX animation clips are validated by Blender import in the companion headless report.", "Historical reconstruction contains documented assumptions."]
    }
    (DOCS / "VALIDATION_REPORT.json").write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")
    manifest = {"generated": "2026-11-24", "root": ROOT.as_posix(), "counts": report["counts"], "total_bytes": report["total_bytes"], "files": files}
    (DOCS / "ASSET_MANIFEST.json").write_text(json.dumps(manifest, ensure_ascii=False, indent=2), encoding="utf-8")
    print(json.dumps({"status": report["status"], "counts": report["counts"], "total_mb": round(report["total_bytes"] / 1024 / 1024, 1), "errors": ERRORS, "warnings": WARNINGS, "contact_sheet": sheet.relative_to(ROOT).as_posix()}, ensure_ascii=False, indent=2))
    if ERRORS:
        raise SystemExit(1)


if __name__ == "__main__":
    main()
