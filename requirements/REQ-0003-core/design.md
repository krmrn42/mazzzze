# REQ-0003-core — Реализация (Design / HOW)

> **Фича:** REQ-0003-core · **Тип документа:** реализация (HOW)
> **Глобальный контекст:** [TECH_SPEC.md](../TECH_SPEC.md). Этот файл — компас по реализованным подсистемам ядра, не дублирует глобальный HOW.

---

## Соответствие требований и кода

| Требование | Источник (HOW) | Код |
|-----------|----------------|-----|
| US-01, F-03 — перемещение/персонаж | [TECH_SPEC §5.5](../TECH_SPEC.md#55-player---character-controller) | `src/Player.cs` |
| US-02, US-03, US-04, F-04 — камера | [TECH_SPEC §5.5 (Camera System)](../TECH_SPEC.md#55-player---character-controller) | `src/Player.cs` (dual-node orbit + spring arm), `player.tscn` |
| US-05, US-07, F-01, F-02 — мир и генерация | [TECH_SPEC §5.1](../TECH_SPEC.md#51-mazedata---world-data-and-procedural-generation) | `src/MazeData.cs` |
| US-06, F-06 — стриминг чанков | [TECH_SPEC §5.2](../TECH_SPEC.md#52-chunkmanager---dynamic-chunk-streaming), [§5.3](../TECH_SPEC.md#53-chunk---gridmap-tile-filler) | `src/ChunkManager.cs`, `src/Chunk.cs`, `chunk.tscn` |
| F-05 — ввод | [TECH_SPEC §5.5 (Input Map)](../TECH_SPEC.md#55-player---character-controller) | `project.godot` `[input]`, `src/Player.cs._Input` |
| US-08, F-07 — визуал | [TECH_SPEC §5.4](../TECH_SPEC.md#54-meshlibrary---maze-tiles), [§5.7](../TECH_SPEC.md#57-lighting-and-environment) | `MazeTiles.tres`, `main.tscn` (освещение/окружение) |

## Ключевые архитектурные решения (кратко)

Полное обоснование — [TECH_SPEC §8 «Key Design Decisions»](../TECH_SPEC.md#8-key-design-decisions). Самые важные для понимания ядра:

1. **Процедурный лабиринт без хранения:** `MazeData.IsFloor(wx,wz)` — детерминированный O(1) хэш, без массива мира.
2. **Нечёт-нечёт клетки = коридоры** → глобальная связность.
3. **Стриминг 3×3 чанков** (`LoadDistance=1`), `UpdateChunks()` вызывается из `Player._PhysicsProcess` каждый фиксированный тик.

## Эксплуатационные ограничения (гатчи)

См. [AGENTS.md «Critical gotchas»](../../AGENTS.md) — `cell_center_y=false`, `AddChild` перед `Setup`, порядок `cell_size`/mesh, `Input.UseAccumulatedInput=false`, 180° Y-поворот модели. Эти ограничения обязательно учитывать при правках ядра.
