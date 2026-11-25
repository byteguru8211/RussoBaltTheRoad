from pathlib import Path
import shutil

ROOT = Path(__file__).resolve().parents[1]
UNITY = ROOT / "unity-project" / "Assets"
TARGET = UNITY / "GameAssets"


def copy_tree(source, destination):
    if not source.exists():
        return
    destination.mkdir(parents=True, exist_ok=True)
    for path in source.rglob("*"):
        if path.is_file() and path.suffix.lower() in {".fbx", ".png", ".wav", ".json", ".md"}:
            relative = path.relative_to(source)
            output = destination / relative
            output.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(path, output)


def main():
    copy_tree(ROOT / "game_assets" / "vehicles", TARGET / "Vehicles")
    copy_tree(ROOT / "game_assets" / "environment", TARGET / "Environment")
    copy_tree(ROOT / "game_assets" / "characters", TARGET / "Characters")
    copy_tree(ROOT / "game_assets" / "textures", TARGET / "Textures")
    copy_tree(ROOT / "audio", TARGET / "Audio")
    images = UNITY / "UI" / "Images"
    images.mkdir(parents=True, exist_ok=True)
    resource_images = UNITY / "Resources" / "UI"
    resource_images.mkdir(parents=True, exist_ok=True)
    resource_names = {"RussoBalt_The_Road_Hero.png": "Hero.png", "RussoBalt_C24_55_preview.png": "RussoBalt.png", "Benz_1912_Racer_Concept_preview.png": "Benz.png", "Era_Vehicles_preview.png": "Era.png", "Historical_Characters_preview.png": "Characters.png", "Historical_Characters_detail.png": "CharactersDetail.png", "Russia_1912_Modular_Kit_preview.png": "Russia.png"}
    for name in ("RussoBalt_The_Road_Hero.png", "Contact_Sheet.png", "RussoBalt_C24_55_preview.png", "Benz_1912_Racer_Concept_preview.png", "Era_Vehicles_preview.png", "Historical_Characters_preview.png", "Historical_Characters_detail.png", "Russia_1912_Modular_Kit_preview.png"):
        source = ROOT / "previews" / name
        if source.exists():
            shutil.copy2(source, images / name)
            if name in resource_names:
                shutil.copy2(source, resource_images / resource_names[name])
    resources_audio = UNITY / "Resources" / "Audio"
    resources_audio.mkdir(parents=True, exist_ok=True)
    for folder in (ROOT / "audio" / "engine", ROOT / "audio" / "processed"):
        for path in folder.glob("*.wav"):
            shutil.copy2(path, resources_audio / path.name)
    resources_textures = UNITY / "Resources" / "Textures"
    resources_textures.mkdir(parents=True, exist_ok=True)
    for name in ("road_dirt_basecolor.png", "road_dirt_normal.png", "road_dirt_roughness.png", "road_snow_basecolor.png", "road_snow_normal.png", "road_snow_roughness.png", "road_cobblestone_basecolor.png", "road_cobblestone_normal.png", "road_cobblestone_roughness.png", "particle_dust.png"):
        source = ROOT / "game_assets" / "textures" / "shared" / name
        if source.exists():
            shutil.copy2(source, resources_textures / name)
    resources_fonts = UNITY / "Resources" / "Fonts"
    resources_fonts.mkdir(parents=True, exist_ok=True)
    for system_name, asset_name in (("arial.ttf", "Arial.ttf"), ("georgia.ttf", "Georgia.ttf")):
        source = Path("C:/Windows/Fonts") / system_name
        if source.exists():
            shutil.copy2(source, resources_fonts / asset_name)
    print("Unity assets synchronized")


if __name__ == "__main__":
    main()
