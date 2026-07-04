# REQ-0021-tennis-ball — Реализация (Design / HOW)

> **Фича:** REQ-0021-tennis-ball · **Тип документа:** реализация (HOW, набросок — фича не реализована)
> **Глобальный контекст:** [TECH_SPEC.md](../TECH_SPEC.md).

Фича не реализована. Ниже — предварительный технический набросок на текущую архитектуру; уточняется при написании кода.

---

## Что принципиально нового

1. **Первый многоразовый предмет.** В отличие от фотоаппарата/фотографии (`ConsumeActivated`), бросок не уничтожает мяч: переход `Activated → InWorld`, слот освобождается (как `DropActivated`), мяч возвращается через авто-подбор (REQ-0016).
2. **Первый реальный физический снаряд.** Существующий `src/DropProjectile.cs` — детерминированная дуга A→B **без коллизий** (`_start.Lerp(_land,_t)` + парабола; на финише `new WorldItem`). Для броска нужен **`RigidBody3D`** с гравитацией, отскоками и коллизией о стены/монстров.

## Структура кода (предварительно)

- **`src/Item.cs`** — мяч как обычный `Item` с `Usage = ActivatedB`, `TypeId = "tennis_ball"`, `ModelPath = res://art/base_ball.glb`. `BuildModel()` по умолчанию (инстанцирует glb) — без подкласса (как фотоаппарат, не как `PhotoItem`).
- **`src/ThrowProjectile.cs`** (новый, `RigidBody3D`) — снаряд броска:
  - `Setup(dir, force, item, targetHeight)`: задаёт начальную скорость (`dir * force + up*UpwardBias`), несёт `Item` и `targetHeight`.
  - Физика: `GravityScale`/стандартная гравитация; коллизионные слои — стены (для отскока) и монстры (для попадания, [F-49](./03-monster-effect-logic.md)).
  - На коллизии со стеной — `BounceDamping` (через физический материал `bounce`).
  - На коллизию с телом монстра — вызвать эффект ([F-49](./03-monster-effect-logic.md)) и «погасить» снаряд (одна цель).
  - Остановка: в `_PhysicsProcess` при `LinearVelocity.Length() < StopThreshold` накапливать `StopTime` → `Land()` (как в `DropProjectile.Land`): создать `WorldItem` в точке, `QueueFree`.
  - Предохранитель по `MaxAirborneTime`.
- **`src/InventoryHud.cs`** — заряд и бросок (диспетч по типу в обработке `use_activated`, рядом с камерой):
  - Зажать ЛКМ (`InputEventMouseButton` Button1, `pressed`) при активированном мяче → начать заряд (`_chargeT` копится в `_PhysicsProcess`).
  - Отпустить ЛКМ (`!pressed`) → `SpawnThrow(dir, force)`; `force = lerp(Min,Max, clamp(_chargeT/MaxChargeTime))`; `dir = player.CameraYawForward`.
  - RMB (Button2, `pressed`) во время заряда → сброс заряда, без броска.
  - На бросок: создать `ThrowProjectile`, прицепить к `Main`; `ConsumeActivated(null)` **НЕ** звать — мяч не расходуется, но слот освобождается (`DropActivated`-подобно: убрать `_activatedItem`/`_reservedSlot`, не создавая замену).
- **`src/Player.cs`** — `CameraYawForward` (есть) для направления броска; `MoveSpeedFactorWhileCharging` — множитель скорости при заряде (экспорт-флаг `IsChargingThrow` читается в `_PhysicsProcess`).
- **Визуал в руке:** модель мяча как дочерний узел `HandAnchor`/`HeadAnchor` игрока, видимая при `Activated` (включается/выключается с активацией). Пульсация масштаба во время заряда — обратная связь.

## Интеграция с монстром ([F-49](./03-monster-effect-logic.md))

Зависит от **REQ-0019** (не реализован). Точки:

- На **выпуске мяча** — снять по реестру монстров ([F-43](../REQ-0019-base-monster/04-data.md), аналог `WorldItem.All`) «видел ли монстр момент броска»: для каждого живого монстра проверить, видит ли он игрока сейчас (конус+LoS, [F-40](../REQ-0019-base-monster/02-perception-logic.md)). Список «aware-at-throw» хранится в `ThrowProjectile` (или в монстре: флаг `_awareOfThrow` на момент броска).
- На **коллизии снаряда с монстром** — если монстр был «aware-at-throw» → запросить у монстра `DisruptStun(StunDuration)` (стан); иначе — попадание без стана (монстр увидит мяч позже → отвлечение).
- **Отвлечение** обслуживается самим монстром: его восприятие ([F-40](../REQ-0019-base-monster/02-perception-logic.md)) сканирует «привлекающие» предметы. Мяч должен быть в списке сканируемых (реестр `WorldItem.All` + признак «distracting» либо по типу `tennis_ball`). Достигнув мяча (`InspectRadius`), монстр держит `InspectDuration` и возвращается к Cycle; мяч остаётся.

## Порядок реализации (зависимости)

- **Можно сделать сразу (без REQ-0019):** модель/тип предмета, заряд/бросок/физика снаряда (`ThrowProjectile`), переход `Activated → InWorld`, возврат через подбор, модель в руке. На коллизию с монстром — заглушка (лог/нет эффекта).
- **После REQ-0019:** стан/отвлечение, реестр «aware-at-throw», отвлечение через восприятие монстра.

## Что НЕ входит (границы)

- **Убийство маленького монстра** — будущая специализация (маленьких монстров нет).
- **Пробивание (piercing)** — нет, одна цель на бросок.
- **Прицельная дуга** — отсутствует (возможна через будущие артефакты).
- **Трейл снаряда / звук отскока** — отложены.

## Открытые вопросы

- Должен ли мяч в полёте учитываться реестром монстров как «вижу мяч» (для раннего отвлечения в полёте), или только после остановки — оставить настраиваемым.
- Хранить «aware-at-throw» в `ThrowProjectile` (per-throw список) vs. в монстре (`_awareOfThrow` на момент броска) — на реализации.
- Коллизионная маска: отдельный слой для «monster body» или переиспользовать существующий (как `FocusDistanceOk` через `IntersectRay`).

## Связи

[REQ-0012-base-item](../REQ-0012-base-item/README.md); [REQ-0014 design.md](../REQ-0012-base-item/REQ-0014-base-item-item-in-world/design.md) (`WorldItem.Setup`/`ComputeSceneAabb` — образец для посадки мяча на пол и масштаба); [REQ-0015 DropProjectile.cs](../REQ-0012-base-item/REQ-0015-base-item-drop/design.md) (образец `Land()` и contrasts); [REQ-0016](../REQ-0012-base-item/REQ-0016-base-item-pickup/README.md) (возврат); [REQ-0019-base-monster](../REQ-0019-base-monster/README.md) (стан/отвлечение); [IDEA-0025](../../ideas/items/IDEA-0025-tennis-ball.md).
