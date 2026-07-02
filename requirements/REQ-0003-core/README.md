# REQ-0003-core — Ядро игры (Core Maze Game)

> **Фича:** REQ-0003-core · **User Stories:** US-01 … US-08 · **Functional:** F-01 … F-08
> **Статус:** ✅ Реализовано
> **Тип документа:** обзор фичи (WHAT)

Базовый геймплей исследования процедурного лабиринта: перемещение, камера от третьего лица сверху-сзади, генерация мира, стриминг чанков, вход/выход, визуальный стиль. Нефункциональные требования (производительность и т.п.) — в [REQ-0002-non-functional.md](../REQ-0002-non-functional.md).

---

## Карта ID → файлы

| ID | Заголовок | Файл |
|----|-----------|------|
| US-01 | Перемещение по лабиринту | [01-movement-logic.md](./01-movement-logic.md) |
| US-02 | Обзор и ориентация | [02-camera-logic.md](./02-camera-logic.md) |
| US-03 | Приближение и удаление камеры | [02-camera-logic.md](./02-camera-logic.md) |
| US-04 | Камера не показывает карту | [02-camera-logic.md](./02-camera-logic.md) |
| US-05 | Процедурная генерация лабиринта | [03-maze-logic.md](./03-maze-logic.md) |
| US-06 | Загрузка мира по мере продвижения | [04-chunking-logic.md](./04-chunking-logic.md) |
| US-07 | Вход и выход | [03-maze-logic.md](./03-maze-logic.md) |
| US-08 | Визуальное оформление | [06-visual.md](./06-visual.md) |
| F-01 | Игровой мир | [03-maze-logic.md](./03-maze-logic.md) |
| F-02 | Лабиринт — правила генерации | [03-maze-logic.md](./03-maze-logic.md) |
| F-03 | Персонаж игрока | [01-movement-logic.md](./01-movement-logic.md) |
| F-04 | Камера | [02-camera-logic.md](./02-camera-logic.md) |
| F-05 | Управление | [05-input.md](./05-input.md) |
| F-06 | Области видимости (Chunking) | [04-chunking-logic.md](./04-chunking-logic.md) |
| F-07 | Визуальный стиль | [06-visual.md](./06-visual.md) |
| F-08 | Ограничения | [07-constraints.md](./07-constraints.md) |

**Реализация (HOW):** [design.md](./design.md) · глобальный контекст — [TECH_SPEC.md](../TECH_SPEC.md)
