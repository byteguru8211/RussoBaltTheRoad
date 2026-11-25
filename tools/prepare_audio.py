from pathlib import Path
import json
import math
import wave
import hashlib
import subprocess
from urllib.request import Request, urlopen
import numpy as np

ROOT = Path(__file__).resolve().parents[1]
try:
    import imageio_ffmpeg
except ImportError as exc:
    raise SystemExit("Install audio tools first: python -m pip install -r tools/requirements.txt") from exc

AUDIO = ROOT / "audio"
SOURCE = AUDIO / "source"
PROCESSED = AUDIO / "processed"
ENGINE = AUDIO / "engine"
SOURCE.mkdir(parents=True, exist_ok=True)
PROCESSED.mkdir(parents=True, exist_ok=True)
ENGINE.mkdir(parents=True, exist_ok=True)
SR = 48000

SOURCES = [
    {"id": "model_t_idle", "name": "Model T steady idle", "url": "https://archive.org/download/SSE_Library_VEHICLES/ANTIQUE/VEHAntq_Model%20T%20Ford%20Car%20steady%20idle_CS_USC.wav", "ext": "wav", "creator": "USC Cinema / Sunset Editorial Collection; digitized by Craig Smith, USC; uploaded by Jason Scott", "license": "CC0-1.0", "source_page": "https://archive.org/details/SSE_Library_VEHICLES", "use": "period gasoline reference; not a Russo-Balt recording"},
    {"id": "model_t_crank", "name": "Model T crank and start", "url": "https://archive.org/download/Red_Library_Cars_3/R04-24-Model%20T%20Crank%20and%20Start.wav", "ext": "wav", "creator": "USC Cinema / Sunset Editorial Collection; digitized by Craig Smith, USC; uploaded by Jason Scott", "license": "CC0-1.0", "source_page": "https://archive.org/details/Red_Library_Cars_3", "use": "hand-crank starter one-shot"},
    {"id": "hooves", "name": "Clatter of hooves", "url": "https://cdn.freesound.org/previews/204/204961_3825705-hq.mp3", "ext": "mp3", "creator": "skitchscharff", "license": "CC0-1.0", "source_page": "https://freesound.org/people/skitchscharff/sounds/204961/", "use": "horse-drawn traffic loop"},
    {"id": "gravel", "name": "Tires on Gravel Road 1", "url": "https://cdn.freesound.org/previews/251/251661_3719168-hq.mp3", "ext": "mp3", "creator": "OBXJohn", "license": "CC0-1.0", "source_page": "https://freesound.org/people/OBXJohn/sounds/251661/", "use": "gravel/dirt rolling loop"},
    {"id": "wind", "name": "Strong Wind Sound Scape", "url": "https://cdn.freesound.org/previews/567/567887_1866048-hq.mp3", "ext": "mp3", "creator": "rentless", "license": "CC0-1.0", "source_page": "https://freesound.org/people/rentless/sounds/567887/", "use": "open-cockpit wind loop"},
    {"id": "crowd", "name": "Crowd talking / murmur", "url": "https://cdn.freesound.org/previews/546/546676_5637404-hq.mp3", "ext": "mp3", "creator": "trezz77", "license": "CC0-1.0", "source_page": "https://freesound.org/people/trezz77/sounds/546676/", "use": "race-side crowd ambience"},
    {"id": "birds", "name": "Sussex Woodland & Meadow Bird Sounds, Evening", "url": "https://cdn.freesound.org/previews/269/269559_5126800-hq.mp3", "ext": "mp3", "creator": "Vonora", "license": "CC0-1.0", "source_page": "https://freesound.org/people/Vonora/sounds/269559/", "use": "rural evening ambience"},
    {"id": "gear", "name": "Car gear shift lever", "url": "https://archive.org/download/SSE_Library_VEHICLES/MISC/VEHMisc_Car%20gear%20shift%20lever_CS_USC.wav", "ext": "wav", "creator": "USC Cinema / Sunset Editorial Collection; digitized by Craig Smith, USC; uploaded by Jason Scott", "license": "CC0-1.0", "source_page": "https://archive.org/details/SSE_Library_VEHICLES", "use": "mechanical gear lever reference"},
    {"id": "metal", "name": "Metallic attach, release", "url": "https://cdn.freesound.org/previews/575/575907_3036029-hq.mp3", "ext": "mp3", "creator": "zeteny_zpx", "license": "CC0-1.0", "source_page": "https://freesound.org/people/zeteny_zpx/sounds/575907/", "use": "metal attachment/release one-shot"},
    {"id": "horn", "name": "1933 Austin 10 Clifton Roadster horn", "url": "https://cdn.freesound.org/previews/828/828939_1480854-hq.mp3", "ext": "mp3", "creator": "SpliceSound", "license": "CC0-1.0", "source_page": "https://freesound.org/people/SpliceSound/sounds/828939/", "use": "close-era horn stylization; not 1912-authentic"},
]


def read_wav(path):
    with wave.open(str(path), "rb") as wf:
        channels = wf.getnchannels()
        width = wf.getsampwidth()
        rate = wf.getframerate()
        raw = wf.readframes(wf.getnframes())
    if width != 2:
        raise ValueError(f"Expected 16-bit WAV, got {path}")
    data = np.frombuffer(raw, dtype="<i2").astype(np.float32) / 32768.0
    if channels > 1:
        data = data.reshape(-1, channels).mean(axis=1)
    if rate != SR:
        raise ValueError(f"Expected {SR} Hz, got {rate}")
    return data


def write_wav(path, data):
    data = np.asarray(data, dtype=np.float32)
    peak = float(np.max(np.abs(data))) if len(data) else 0.0
    if peak > 0:
        data = data / peak * 0.89
    pcm = np.int16(np.clip(data, -1, 1) * 32767)
    with wave.open(str(path), "wb") as wf:
        wf.setnchannels(1)
        wf.setsampwidth(2)
        wf.setframerate(SR)
        wf.writeframes(pcm.tobytes())
    return {"file": path.relative_to(ROOT).as_posix(), "samples": len(pcm), "duration_s": round(len(pcm) / SR, 3), "sample_rate": SR, "channels": 1, "sha256": hashlib.sha256(path.read_bytes()).hexdigest()}


def moving_average(data, width):
    kernel = np.ones(width, dtype=np.float32) / width
    return np.convolve(data, kernel, mode="same")


def seamless_loop(data, duration=12.0, fade=0.6):
    n = int(duration * SR)
    if len(data) < n + int(fade * SR):
        data = np.tile(data, int(math.ceil(n / len(data))) + 2)
    start = min(len(data) // 4, max(0, len(data) - n - 1))
    out = data[start:start + n].copy()
    f = min(int(fade * SR), n // 3)
    weights = np.linspace(0, 1, f, dtype=np.float32)
    out[-f:] = out[-f:] * (1 - weights) + out[0] * weights
    return out


def trim_active(data, max_duration=None, pad=0.15):
    frame = int(0.02 * SR)
    usable = len(data) // frame * frame
    rms = np.sqrt(np.mean(data[:usable].reshape(-1, frame) ** 2, axis=1))
    threshold = max(0.004, float(np.max(rms)) * 0.035)
    active = np.where(rms > threshold)[0]
    if len(active) == 0:
        return data[:int((max_duration or 5.0) * SR)]
    start = max(0, int(active[0] * frame - pad * SR))
    end = min(len(data), int((active[-1] + 1) * frame + pad * SR))
    if max_duration:
        end = min(end, start + int(max_duration * SR))
    return data[start:end]


def transient(data, duration=1.4):
    frame = int(0.02 * SR)
    usable = len(data) // frame * frame
    rms = np.sqrt(np.mean(data[:usable].reshape(-1, frame) ** 2, axis=1))
    peak_frame = int(np.argmax(rms))
    center = peak_frame * frame
    half = int(duration * SR / 2)
    start = max(0, center - half)
    end = min(len(data), center + half)
    return data[start:end]


def synthesize_engine(rpm, duration=4.0):
    rng = np.random.default_rng(1912 + rpm)
    n = int(duration * SR)
    t = np.arange(n, dtype=np.float32) / SR
    firing = 2.0 * rpm / 60.0
    phase = math.tau * firing * t
    tonal = np.sin(phase) + 0.52 * np.sin(2 * phase + 0.15) + 0.28 * np.sin(3 * phase + 0.4) + 0.16 * np.sin(4 * phase)
    chug = 0.68 + 0.32 * np.square(0.5 + 0.5 * np.sin(phase - math.pi / 2))
    noise = rng.normal(0, 1, n).astype(np.float32)
    low = moving_average(noise, 41)
    high = noise - moving_average(noise, 7)
    mechanical = 0.12 * high + 0.18 * low
    period = SR / firing
    knock_phase = np.mod(np.arange(n), period) / SR
    knock = np.exp(-knock_phase * 150.0) * (0.5 + 0.5 * np.sin(math.tau * 210 * t))
    output = tonal * chug * 0.28 + mechanical * (0.55 + rpm / 5000.0) + knock * 0.08
    output *= np.minimum(1.0, t / 0.08) * np.minimum(1.0, (duration - t) / 0.08)
    return output.astype(np.float32)


def main():
    ffmpeg = imageio_ffmpeg.get_ffmpeg_exe()
    records = []
    processed = {}
    for source in SOURCES:
        original = SOURCE / f"{source['id']}.{source['ext']}"
        if not original.exists():
            req = Request(source["url"], headers={"User-Agent": "RussoBaltTheRoad/1.0 educational asset preparation"})
            with urlopen(req, timeout=120) as response:
                original.write_bytes(response.read())
        wav_path = PROCESSED / f"{source['id']}_48k.wav"
        subprocess.run([ffmpeg, "-y", "-v", "error", "-i", str(original), "-ar", str(SR), "-ac", "1", "-c:a", "pcm_s16le", str(wav_path)], check=True)
        data = read_wav(wav_path)
        processed[source["id"]] = data
        records.append({**source, "original_file": original.relative_to(ROOT).as_posix(), "original_sha256": hashlib.sha256(original.read_bytes()).hexdigest(), "normalized_wav": wav_path.relative_to(ROOT).as_posix()})
        print("DOWNLOADED", source["id"], len(data) / SR)

    outputs = []
    outputs.append(write_wav(PROCESSED / "engine_period_idle_reference_loop.wav", seamless_loop(processed["model_t_idle"], 12.0)))
    outputs.append(write_wav(PROCESSED / "starter_hand_crank.wav", trim_active(processed["model_t_crank"], 18.0)))
    outputs.append(write_wav(PROCESSED / "ambience_hooves_loop.wav", seamless_loop(processed["hooves"], 14.0)))
    outputs.append(write_wav(PROCESSED / "road_gravel_loop.wav", seamless_loop(processed["gravel"], 12.0)))
    outputs.append(write_wav(PROCESSED / "wind_open_cockpit_loop.wav", seamless_loop(processed["wind"], 12.0)))
    outputs.append(write_wav(PROCESSED / "ambience_crowd_loop.wav", seamless_loop(processed["crowd"], 14.0)))
    outputs.append(write_wav(PROCESSED / "ambience_birds_loop.wav", seamless_loop(processed["birds"], 16.0)))
    outputs.append(write_wav(PROCESSED / "gear_lever_clack.wav", transient(processed["gear"], 1.6)))
    outputs.append(write_wav(PROCESSED / "metal_release_clunk.wav", transient(processed["metal"], 1.1)))
    outputs.append(write_wav(PROCESSED / "horn_close_era.wav", trim_active(processed["horn"], 3.0)))
    for rpm in (600, 900, 1200, 1500, 1800, 2100):
        outputs.append(write_wav(ENGINE / f"engine_synthetic_4cyl_{rpm}rpm.wav", synthesize_engine(rpm)))
    for output in outputs:
        print("WROTE", output["file"], output["duration_s"])

    for record in records:
        record.pop("normalized_wav", None)
    manifest = {
        "prepared": "2026-11-24",
        "format": {"sample_rate": SR, "bit_depth": 16, "channels": 1},
        "license_summary": "All third-party source recordings are CC0-1.0 per source pages; attribution retained for provenance.",
        "historical_caveat": "No surviving recording of a 1912 Russo-Balt engine was found. Model T and 1933 Austin files are close-era references; synthetic 4-cylinder layers are original stylization.",
        "sources": records,
        "outputs": outputs,
        "unity_mapping": {
            "engine": "Crossfade adjacent synthetic RPM layers by normalized RPM; add starter_hand_crank before engine loop.",
            "road": "Layer road_gravel_loop with speed and surface grip; pitch 0.85–1.20.",
            "wind": "Layer wind_open_cockpit_loop with speed; low-pass at low speed.",
            "ambience": "Use crowd near start/finish, hooves near NPC carriages, birds in rural chapters.",
            "one_shots": "gear_lever_clack, metal_release_clunk, horn_close_era, starter_hand_crank"
        }
    }
    (AUDIO / "manifest.json").write_text(json.dumps(manifest, ensure_ascii=False, indent=2), encoding="utf-8")
    for intermediate in PROCESSED.glob("*_48k.wav"):
        intermediate.unlink()
    print("AUDIO_PREP_OK", len(outputs), "processed files")


if __name__ == "__main__":
    main()
