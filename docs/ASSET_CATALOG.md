# Каталог готовых ассетов

## Автомобили

### Russo-Balt C24/55

Папка: `game_assets/vehicles/RussoBalt_C24_55/`

- `RussoBalt_C24_55.blend` — редактируемая сцена с detailed, optimized и LOD.
- `RussoBalt_C24_55_Detailed.fbx` — 553 отдельных меша для изучения деталей.
- `RussoBalt_C24_55_LOD0.fbx` — игровой оптимизированный экспорт, 59 мешей.
- `RussoBalt_C24_55_LOD1.fbx` — упрощённый LOD.
- `RussoBalt_C24_55_LOD2.fbx` — дальний LOD.
- `RussoBalt_C24_55_Collision.fbx` — корпус и четыре колёсных коллайдера.
- `asset_metadata.json` — исторические факты, допущения, узлы и статистика.

Габариты optimized-модели: примерно **1,63 × 4,63 × 1,41 м**, включая фары, бампер, запасное колесо и орёл. Основной кузов ближе к историческим 4,435 × 1,544 × 1,23 м.

### Benz 1912 Racer Concept

Папка: `game_assets/vehicles/Benz_1912_Prototype/`

- `Benz_1912_Racer_Concept.blend`
- `Benz_1912_Racer_Concept_Detailed.fbx` — 355 мешей.
- `Benz_1912_Racer_Concept_LOD0.fbx` — 44 меша.
- `Benz_1912_Racer_Concept_LOD1.fbx`
- `Benz_1912_Racer_Concept_LOD2.fbx`
- `Benz_1912_Racer_Concept_Collision.fbx`
- `asset_metadata.json`

Это оригинальный концепт автомобиля эпохи, вдохновлённый немецкими гоночными машинами 1912 года, а не точная реплика Blitzen Benz.

### Дополнительные автомобили

Папка: `game_assets/vehicles/`

- `Ford_ModelT_1912/` — компактный чёрный touring с латунным радиатором, 12-спицевыми колёсами и складным верхом.
- `Fiat_S76_1910/` — рекордная машина с высоким капотом, радиатором-лезвием, гигантским двигателем и открытыми выхлопами.
- `Mercedes_37_90_1912/` — представительский double phaeton с двумя рядами сидений, остроконечным радиатором и закрытой цепной передачей.
- `Era_Vehicles_1910_1914.blend` — общая редактируемая сцена трёх машин.
- `Era_Vehicles_Manifest.json` — исторические факты, допущения и список экспортов.

Каждый автомобиль имеет Detailed, LOD0–LOD2 и Collision FBX.

## Окружение России 1912

Папка: `game_assets/environment/Russia_1912/`

- `Russia_1912_Modular_Kit.blend` — тестовая улица и все модули.
- `RussoBalt_The_Road_Hero.blend` — автомобиль, водитель, дорога, дом и знак в единой сцене.
- `asset_metadata.json`
- `modules/` — 18 самостоятельных FBX-модулей:
  - `Road_Dirt_100m_LOD0.fbx`
  - `Road_Snow_100m_LOD0.fbx`
  - `Road_Cobblestone_25m_LOD0.fbx`
  - `Building_Wood_House_01_LOD0/LOD1/Detailed/Collision.fbx`
  - `Building_Brick_Gatehouse_01_LOD0/LOD1/Detailed/Collision.fbx`
  - `Fence_Wood_10m_LOD0.fbx`
  - `Telegraph_Pole_01_LOD0/LOD1/Detailed.fbx`
  - `Signpost_Russia_1912_LOD0.fbx`
  - `Prop_Fuel_Can_01_LOD0.fbx`
  - `Prop_Tool_Chest_01_LOD0.fbx`
  - `Prop_Barrel_01_LOD0.fbx`
  - `Prop_Crate_01_LOD0.fbx`
  - `Prop_Hay_Bale_01_LOD0.fbx`
  - `Prop_Roadside_Lantern_01_LOD0.fbx`
  - `Prop_Milestone_01_LOD0.fbx`
  - `Prop_Horse_Cart_01_LOD0.fbx`
  - `Tree_Birch_01_LOD0/LOD1/Detailed.fbx`
  - `Tree_Pine_01_LOD0/LOD1/Detailed.fbx`

## Персонажи

Папка: `game_assets/characters/`

- `Driver_1912.fbx` — водитель в кожаной куртке, очках и кепке.
- `Mechanic_1912.fbx` — механик в куртке и фартуке.
- `Lady_1912.fbx` — дама в пальто и шляпе.
- `Historical_Characters.blend` — все три персонажа, риги и NLA-клипы.
- `asset_metadata.json`

Каждый персонаж: один skinned mesh, 21 кость, клипы **Idle**, **Walk**, **Drive**. FBX может создавать по два варианта на клип — для root и rig; оставлять вариант с костями.

## Текстуры

Папка: `game_assets/textures/shared/`

33 оригинальные PBR-карты: Base Color, Roughness и Normal для краски, кожи, дерева, ткани, грунта, снега, булыжника, кирпича, штукатурки, кровли и коры. Лицензия CC0.

## Звук

Папка: `audio/`

- 6 RPM-слоёв синтезированного четырёхцилиндрового двигателя.
- Архивный холостой ход Model T и ручной старт.
- Гравий, ветер, копыта, толпа, птицы.
- Рычаг коробки, металлический стук, близкий к эпохе гудок.
- `manifest.json` — источники, авторы, CC0, размеры и SHA-256.
- `LICENSES.md` — атрибуция и исторические оговорки.

## Референсы и превью

- `references/history/` — Public Domain/CC0 изображения и метаданные.
- `previews/Contact_Sheet.png` — общий контактный лист.
- `previews/RussoBalt_The_Road_Hero.png` — интегрированная сцена.
- `previews/RussoBalt_C24_55_preview.png`
- `previews/Benz_1912_Racer_Concept_preview.png`
- `previews/Era_Vehicles_preview.png`
- `previews/Russia_1912_Modular_Kit_preview.png`
- `previews/Historical_Characters_preview.png`

## Воспроизводимость

Скрипты находятся в `tools/`:

```bash
python -m pip install -r tools/requirements.txt
python tools/generate_textures.py
python tools/prepare_audio.py
"D:/Program Files/Blender/blender.exe" --background --factory-startup -t 1 --python tools/build_russobalt.py
"D:/Program Files/Blender/blender.exe" --background --factory-startup -t 1 --python tools/build_benz.py
"D:/Program Files/Blender/blender.exe" --background --factory-startup -t 1 --python tools/build_environment.py
"D:/Program Files/Blender/blender.exe" --background --factory-startup -t 1 --python tools/build_era_vehicles.py
"D:/Program Files/Blender/blender.exe" --background --factory-startup -t 1 --python tools/build_characters.py
"D:/Program Files/Blender/blender.exe" --background --factory-startup -t 1 --python tools/build_hero_scene.py
"D:/Program Files/Blender/blender.exe" --background --factory-startup -t 1 --python tools/validate_fbx.py
python tools/validate_assets.py
```

Blender-скрипты рассчитаны на Blender 5.2.1 и single-threaded headless-режим: он оказался стабильнее для данной конфигурации.
