# Импорт ассетов в Unity

## Единицы и оси

- Blender: метры, Z вверх, перед автомобиля −Y.
- FBX: метры, Y вверх, перед автомобиля +Z.
- Pivot автомобиля: центр между осями на уровне земли.
- При `Instantiate` окружения сохранять импортный поворот FBX: `desiredRotation * prefab.transform.rotation`; иначе теряется ось Z-up → Y-up и объекты ложатся.
- Вертикальные эмблемы заставки — исключение: для них нужен `Quaternion.identity`, но масштаб корня умножается на масштаб префаба.
- Scale Factor при импорте: **1**.
- Convert Units: включён; Bake Axis Conversion: включён.

## Структура префаба Russo-Balt

```text
RussoBalt_C24_55 (Rigidbody + VehicleController)
├── Visual_LOD0
│   ├── RB_Body_Root
│   ├── RB_Chassis_Root
│   ├── RB_Cockpit_Root
│   ├── RB_SteeringWheel_Pivot
│   ├── RB_Clutch_Pedal_Pivot
│   ├── RB_Brake_Pedal_Pivot
│   ├── RB_Throttle_Pedal_Pivot
│   ├── RB_GearLever_Pivot
│   └── RB_Windshield_Pivot
├── Wheel_FL_Steer
│   └── Wheel_FL_Spin
├── Wheel_FR_Steer
│   └── Wheel_FR_Spin
├── Wheel_RL_Spin
├── Wheel_RR_Spin
└── Collision (MeshCollider, Convex)
```

`Wheel_*_Steer` вращается вокруг вертикальной оси, `Wheel_*_Spin` — вокруг локальной оси X. В Unity после конвертации ось вращения колёс соответствует локальной X.

## FBX Import Settings

- Model > Scale Factor: 1.
- Model > Convert Units: On.
- Model > Bake Axis Conversion: On.
- Rig > Animation Type: None для автомобилей и окружения.
- Rig > Animation Type: Humanoid для персонажей после ремаппинга костей.
- Materials > Location: Use Embedded Materials.
- Materials > Import Materials: On.
- Meshes > Read/Write Enabled: On только там, где нужны MeshCollider или декодирование.
- Meshes > Optimize Mesh Data: On для статического окружения.
- Meshes > Weld Vertices: осторожно, чтобы не потерять UV-швы.
- Normals: Import; Tangent Space для обычных материалов, если нет проблем.
- Compression: высокая для LOD1/LOD2, умеренная для LOD0.

## LODGroup

Рекомендуемые экранные высоты:

- LOD0: 35–100%.
- LOD1: 12–35%.
- LOD2: 0–12%.
- Culled: ниже 2%.

Для автомобилей LOD должны иметь одинаковые pivot и иерархию колёс. Не заменять LOD0 автомобиля на модель без колёс, если рядом есть AI или физика.

## Коллайдеры

- `*_Collision.fbx` содержит упрощённый корпус и четыре колёса.
- Основной корпус: MeshCollider, Convex, на дочернем объекте.
- WheelCollider создаются в Unity отдельно; колёсные mesh-коллайдеры нужны для столкновений кузова и визуальной геометрии, а не как замена WheelCollider.
- Здания: отдельные `*_Collision.fbx` или составные Box/Convex MeshCollider.
- Дороги: MeshCollider по поверхности; для больших трасс предпочтительны Terrain/Heightmap и полоса движения.

## Материалы

В Blender используются PBR Principled BSDF и CC0-текстуры:

- Base Color sRGB.
- Roughness Linear.
- Normal Map Linear.

После импорта проверить, что Unity создал Standard или URP Lit материалы и что Normal Scale не завышен. Для URP переназначить шейдеры одним Master Material с текстурными параметрами.

## Анимации персонажей

Каждый FBX содержит три типа клипов: `Idle`, `Walk`, `Drive`, по 30 кадров при 30 fps. Blender может создать по два варианта каждого клипа — для root и rig; в Unity оставлять вариант с костями.

- Импортировать как Humanoid.
- Проверить T-pose/A-pose и ремаппинг.
- Создать Locomotion Blend Tree: Idle → Walk.
- `Drive` использовать для сидячего водителя; при необходимости добавить IK рук к рулю.
- В Blender весовая привязка ручная; в Unity проверить плечи, локти, бёдра и колени.

## Звук Unity

- Все игровые WAV: 48 кГц, 16 бит, моно.
- Двигатель: шесть слоёв 600/900/1200/1500/1800/2100 rpm.
- Кроссфейд между соседними слоями по нормализованным оборотам.
- Стартер проигрывается до появления устойчивого холостого хода.
- Гравий и ветер меняют pitch/gain по скорости.
- Ambient-зоны используют `AudioMixer` группы Engine, Road, Wind, World, OneShots.

## UI-изображения

Все изображения в `Assets/Resources/UI` импортируются как **Sprite** (`TextureImporterType.Sprite`). `UIUtils.LoadSprite` сначала загружает Sprite и только затем использует Texture2D fallback. Не создавать UI-фон через `Sprite.Create` из сжатой нечитаемой текстуры: в standalone-сборке это может дать сплошную заливку.

## Сетевые префабы

- `network_russo_balt.prefab` — NetworkObject, NetworkTransform (Owner), VehicleController, Input, Audio, Effects.
- `LobbyManager.prefab` — серверный NetworkObject с replicated roster.
- `LanSessionController` — persistent NetworkManager + UnityTransport, UDP 7777.

## Проверка перед коммитом

- [ ] Масштаб автомобиля примерно 4,4 м в длину.
- [ ] Колёса касаются земли, pivot на Z=0.
- [ ] Перед автомобиля смотрит в +Z Unity.
- [ ] LOD0–LOD2 не расходятся с pivot.
- [ ] Материалы и текстуры не фиолетовые.
- [ ] Нет отсутствующих нормалей/UV.
- [ ] Колёса и руль вращаются в правильные стороны.
- [ ] Коллайдер не начинает вращаться вместе с визуальным колесом.
- [ ] Клипы Idle/Walk/Drive импортируются и проигрываются.
- [ ] Звук без клиппинга и щелчков на loop-boundary.
