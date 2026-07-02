# REQ-0015-base-item-drop — Реализация (Design / HOW)

> **Фича:** REQ-0015-base-item-drop · **Тип документа:** реализация (HOW) · **Статус:** ✅ реализовано
> **Глобальный контекст:** [TECH_SPEC.md](../../TECH_SPEC.md).

---

## Структура кода

- **`src/InventoryHud.cs`** — источник выброса и вся конфигурация:
  - Экспортируемые параметры: `DropDistanceBodies` (3), `PlayerBodyDiameter` (0.6), `WorldItemSizeFraction` (0.125), `PlayerHeight` (1.8), `DropFlightDuration` (0.6), `DropArcHeight` (1.2).
  - Ввод (F-27): в `_Input` цифры/G читаются по `PhysicalKeycode` (устойчиво к раскладке и Shift). Состояние: `_selectedRow`, персистентный `_cursorSlot`. `Shift + колонка` → `DropSlot`; `G` → `DropSlot(_cursorSlot)`.
  - `DropSlot(slot)`: убирает предмет из `Inventory`, вычисляет точку приземления (луч из игрока по `PlanarFacing` на дистанцию, кламп к стене через `DirectSpaceState.IntersectRay`, маска 1), создаёт `DropProjectile` и цепляет к `Main`.
- **`src/DropProjectile.cs`** (`Node3D`) — летящая звёздочка (F-28):
  - Ярко-emissive `SphereMesh` + `OmniLight3D`; свечение сцены (`WorldEnvironment.glow`) даёт эффект звезды. Лёгкая пульсация размера — «мерцание».
  - `_PhysicsProcess`: параметр `t: 0→1`, горизонталь — линейная интерполяция старт→цель, вертикаль — та же интерполяция плюс парабола `4·h·t·(1−t)`.
  - По завершении создаёт `WorldItem` в точке приземления, добавляет к `Main` и вызывает `QueueFree`.
- **Модель предмета:** путь к `.glb` берётся из `Item.ModelPath` (для фотоаппарата — `res://art/old_kodak_camera.glb`).

## Почему так

- **PhysicalKeycode** вместо `Keycode`: при зажатом Shift верхний цифровой ряд даёт символы (`!@#$`), а физический код остаётся `Key.Key1..4`.
- **Снаряд в мире, а не в HUD:** цепляется к `Main`, чтобы жить независимо от инвентаря и продолжить полёт даже при закрытии панели.
- **_PhysicsProcess:** фиксированный шаг (соглашение проекта), детерминированная траектория.

## Границы

- `Activated → InWorld` (выброс из руки) не реализован — активация в руку относится к REQ-0012 (F-18/B) и не сделана.
- Подбор из мира (`InWorld → InInventory`) — отдельная задача.
