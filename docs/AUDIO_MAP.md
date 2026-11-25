# Звуковая карта RussoBalt The Road

## Формат

- 48 000 Гц, 16 бит PCM, моно WAV.
- Исходники и обработанные файлы: `audio/`.
- Метаданные, авторы, ссылки и SHA-256: `audio/manifest.json`.

## Двигатель

| Файл | Обороты | Назначение |
|---|---:|---|
| `engine/engine_synthetic_4cyl_600rpm.wav` | 600 | холостой ход/пуск |
| `engine/engine_synthetic_4cyl_900rpm.wav` | 900 | трогание |
| `engine/engine_synthetic_4cyl_1200rpm.wav` | 1200 | город |
| `engine/engine_synthetic_4cyl_1500rpm.wav` | 1500 | трасса |
| `engine/engine_synthetic_4cyl_1800rpm.wav` | 1800 | нагрузка |
| `engine/engine_synthetic_4cyl_2100rpm.wav` | 2100 | максимум |

Алгоритм: найти два соседних слоя, плавно смешать их по RPM в пределах 0,25–0,75. Добавить нагрузку через gain и лёгкое изменение pitch. Не проигрывать слои одновременно без кроссфейда.

`processed/engine_period_idle_reference_loop.wav` — реальная архивная запись Model T, полезная как дополнительная текстура, но не как утверждение об аутентичном звуке Russo-Balt.

`processed/starter_hand_crank.wav` — ручной пуск; проигрывать перед зажиганием.

## Дорога и ветер

- `processed/road_gravel_loop.wav` — гравий/грунт, gain и pitch от скорости.
- `processed/wind_open_cockpit_loop.wav` — ветер, усиление со скоростью, low-pass на малой скорости.
- Для снега и булыжника позже добавить отдельные loops; текущий гравий не должен выдаваться за запись этих поверхностей.

## Мир

- `processed/ambience_hooves_loop.wav` — повозки и лошади.
- `processed/ambience_crowd_loop.wav` — старт, финиш, площадь.
- `processed/ambience_birds_loop.wav` — сельская местность, вечер.

## Разовые звуки

- `processed/gear_lever_clack.wav` — рычаг коробки.
- `processed/metal_release_clunk.wav` — металлический люфт/фиксатор.
- `processed/horn_close_era.wav` — гудок Austin 1933 как близкая по эпохи стилизация.

## Историческая оговорка

Аутентичной записи двигателя Russo-Balt 1912 года не найдено. RPM-слои — оригинальный синтез четырёхцилиндрового мотора; Model T — периодный референс; гудок 1933 года — сознательная стилизация. В финальной игре эти звуки можно заменить новыми полевыми записями, сохранив текущую структуру микса.
