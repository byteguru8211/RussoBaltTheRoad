import argparse
import os
import shutil
import subprocess
from pathlib import Path


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--unity", type=Path, default=Path("C:/Program Files/Unity/Hub/Editor/6000.6.10f1/Editor/Unity.exe"))
    parser.add_argument("--timeout", type=int, default=1800)
    args = parser.parse_args()

    root = Path(__file__).resolve().parents[1]
    project = root / "unity-project"
    builds = root / "Builds"
    if builds.name.lower() != "builds":
        raise RuntimeError("Refusing to clean an unexpected build directory")
    if not args.unity.is_file():
        raise FileNotFoundError(args.unity)
    if os.name == "nt":
        processes = subprocess.run(["tasklist", "/FI", "IMAGENAME eq Unity.exe"], capture_output=True, text=True)
        if "Unity.exe" in processes.stdout:
            raise RuntimeError("Close the Unity Editor before cleaning Builds and starting a release build")

    if builds.exists():
        shutil.rmtree(builds)
    builds.mkdir(parents=True)
    executable = builds / "RussoBaltTheRoad.exe"
    log = builds / "build.log"
    command = [
        str(args.unity),
        "-batchmode",
        "-nographics",
        "-projectPath",
        str(project),
        "-buildWindows64Player",
        str(executable),
        "-quit",
        "-logFile",
        str(log),
    ]
    result = subprocess.run(command, timeout=args.timeout)
    log_text = log.read_text(encoding="utf-8", errors="replace") if log.exists() else ""
    if result.returncode != 0 or not executable.is_file() or "Build Finished, Result: Success." not in log_text:
        raise RuntimeError(f"Release build failed; inspect {log}")
    print(f"Release build ready: {executable}")


if __name__ == "__main__":
    main()
