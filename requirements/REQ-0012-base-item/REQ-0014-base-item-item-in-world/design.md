# REQ-0014-base-item-item-in-world — Реализация (Design / HOW)

> **Фича:** REQ-0014-base-item-item-in-world · **Тип документа:** реализация (HOW) · **Статус:** ✅ базово реализовано
> **Глобальный контекст:** [TECH_SPEC.md](../../TECH_SPEC.md).

---

## Структура кода

- **`src/WorldItem.cs`** (`Node3D`) — представление предмета в мире:
  - `Setup(modelPath, targetHeight)`: грузит `.glb` как `PackedScene`, инстанцирует, считает общий AABB модели (`ComputeSceneAabb`), масштабирует так, что высота (`Aabb.Size.Y`) = `targetHeight`, и сдвигает модель вверх так, что её низ касается `y=0` этого узла. Узел ставится в точку приземления → модель стоит на полу.
  - Анимация появления (F-25): в `_PhysicsProcess` масштаб узла растёт от малого к 1 за `SpawnPopDuration`.
- **`ComputeSceneAabb(Node3D)`** — статический помощник (в `WorldItem`), объединяет мировые AABB всех `VisualInstance3D`. Используется также `InventoryHud` для кадрирования иконки.
- **Создание:** `WorldItem` создаётся из `DropProjectile` при приземлении (см. [REQ-0015](../REQ-0015-base-item-drop/design.md)) и цепляется к `Main` → живёт независимо от стриминга чанков.

## Конфигурация

Размерные параметры (`WorldItemSizeFraction`, `PlayerHeight`) заданы как экспортируемые
поля `InventoryHud` и передаются в `WorldItem.Setup` через вычисленную `targetHeight`.

## Границы

- Коллизия/подбор не реализованы: `WorldItem` — чисто визуальный `Node3D` без физического тела.
- idle-анимация и реакция на подбор (F-25) — задел.
