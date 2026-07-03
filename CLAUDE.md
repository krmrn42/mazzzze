# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

A Godot 4.6 3D maze game prototype using C# (.NET 8.0) with the Jolt Physics engine. The project uses the Forward Plus renderer and targets Windows (D3D12) primarily.

## Build & Run

The Godot 4.6.3 mono editor lives at:
`/home/user13/Apps/Godot_v4.6.3-stable_mono_linux_x86_64/Godot_v4.6.3-stable_mono_linux.x86_64`

```bash
# 0. Build the C# project (MUST run after any .cs change before launching Godot)
dotnet build
dotnet build -c ExportRelease          # release configuration

# Convenience handle used in the examples below
GODOT="/home/user13/Apps/Godot_v4.6.3-stable_mono_linux_x86_64/Godot_v4.6.3-stable_mono_linux.x86_64"

# 1. Import assets (only needed after adding/changing .tscn/.tres/.glb, or a fresh checkout)
"$GODOT" --headless --import

# 2. Launch the editor / run the game by hand
"$GODOT" --editor --path .             # open the editor (F5 runs the game)
"$GODOT" --path .                      # run the game windowed (needs DISPLAY)
```

The main scene is `main.tscn` (set in project.godot).

## How to verify changes (no GUI needed)

There are no unit tests, integration tests, or CI. All verification is CLI-based. Never add test files unless explicitly asked.

Always `dotnet build` first, then:

**A. Logic / physics check (headless, reads `GD.Print` logs):**

```bash
timeout 8 "$GODOT" --headless --path . 2>&1 | grep -iE "Player|Chunk|error|exception"
```

Expected output: `[Player] Start cell=…` and `[ChunkManager] LOAD` lines, zero error/exception lines. For physics questions add a temporary throttled `GD.Print` in `Player._PhysicsProcess`, run the command, then **remove the debug code**.

**B. Visual check (real render → screenshot).** A real GPU + `DISPLAY=:0` are available. Temporarily add to `Player._PhysicsProcess`:

```csharp
// TEMP — remove after verifying
if (++_dbgFrame == 90) {
    GetViewport().GetTexture().GetImage().SavePng("res://shot.png");
    GetTree().Quit();
}
```

```bash
dotnet build && DISPLAY=:0 timeout 20 "$GODOT" --path . --resolution 1280x720
# -> shot.png in the project root; view it, then delete it and the temp code
```

Always delete `shot.png` and revert debug code before finishing.

## Architecture

`requirements/TECH_SPEC.md` is the authoritative technical reference. Key facts below.

### Runtime scene hierarchy

```
Main (Node3D)
├── Ground (StaticBody3D, Y=-0.5)          — fallback floor, 256×1×256 box
├── DirectionalLight3D                      — "sun", ~42° elevation, ahead (-Z), energy 2.4
├── MazeData (Node + MazeData.cs)          — singleton, procedural world data
├── Player (CharacterBody3D)               — player.tscn
│   ├── ModelPivot (Node3D)                — faces movement direction
│   │   └── Character (AnimationLibrary_Godot_Standard.glb)  — AnimationPlayer with 46 clips
│   ├── CollisionShape3D (Y=0.35)          — SphereShape3D, radius=0.3
│   ├── HeadLight (OmniLight3D, Y=4.0)     — local fill light
│   └── CameraYaw (Node3D, Y=2.0)
│       └── CameraPitch (Node3D, -60°)
│           └── Camera3D (Z=10)            — spring-arm shortened to avoid walls
├── ChunkManager (Node3D)                  — orchestrates chunk lifecycle
│   └── Chunk ×N (dynamic)                — chunk.tscn instances, each a 16×16 GridMap
├── WorldEnvironment                        — procedural sky + bloom
└── HUD (CanvasLayer)
    ├── Minimap (Control)                  — top-left overlay, procedural _Draw
    └── Inventory (Control + InventoryHud.cs) — bottom-right overlay, procedural _Draw
```

### Maze geometry

- **MazeData** is a stateless singleton. `IsFloor(wx, wz)` determines any cell in O(1) via a murmur3 hash — no world array is stored. The maze is 10 000×10 000 cells.
- **CellWorldSize = 3.6** world units (corridor width = 6× player diameter). **WallHeight = 30**.
- **WorldOffset = −18 000** in X and Z (maze centred at origin). Player start cell (1, 1) → world (−17994.6, 0.3, −17994.6).
- **Chunk streaming**: `ChunkManager` keeps a 3×3 (LoadDistance=1) grid of 16×16-cell chunks loaded. `UpdateChunks()` is called every `Player._PhysicsProcess`.
- **MazeTiles.tres**: `MeshLibrary` with exactly 2 items — id 0 = Floor, id 1 = Wall.
- **Tile overlap (seam fix):** floor/wall *meshes* are 3.66 wide; GridMap `cell_size` and collision shapes stay 3.6. The 0.03 overlap hides float32 precision cracks at the −18 000 world coords. World-space triplanar mapping makes the coplanar z-fight invisible.

### Player controller (`src/Player.cs`)

- Camera-relative WASD movement via `Input.GetVector()`. No jump; gravity only.
- **Dual-node orbit camera**: `CameraYaw` (mouse X) → `CameraPitch` (mouse Y, clamped [−85°, −25°]) → `Camera3D`. Spring-arm raycast shortens the camera distance each physics frame to avoid wall clips.
- **HeadLight** (OmniLight3D, Y=4) travels with the player for fill light at the canyon floor.
- Animation: `ModelPivot/Character/AnimationPlayer` cross-fades `Idle` ↔ `Jog_Fwd`; speed-scaled by `planarSpeed / WalkAnimRefSpeed` to match foot pace.
- **Character model forward is +Z** but `Basis.LookingAt` points −Z at the target, so `player.tscn` carries a **180° Y rotation on the Character node** — without it the player walks backward.
- Exported: `Speed` (5.0), `MouseSensitivity`, `Gravity` (15.0), zoom (`MinZoom`/`MaxZoom`/`ZoomStep`), pitch (`DefaultPitchDeg`/`MinPitchDeg`/`MaxPitchDeg`), anim clip names (`IdleAnim`/`WalkAnim`/`WalkAnimRefSpeed`/`AnimBlend`).

### Mini-map (`src/Minimap.cs`, `src/MinimapState.cs`)

Top-left HUD overlay, procedurally drawn — no textures. Implements `requirements/REQ-0010-minimap/` (US-10/F-09/F-10/F-11).
- **Fog of war** (`MinimapState`): FIFO of last 1000 entered cells; each reveals a 3×3 neighbourhood. Entrance/exit permanently revealed once entered.
- **Two draw zones**: near (Chebyshev ≤ 7 → 15×15) = per-cell floor/wall detail; far revealed = flat schematic silhouette.
- **Cell-visit detection is in `_PhysicsProcess`** — do not move it to `_Process` (cells can be skipped at variable render rate).
- Tab toggles orientation (camera-forward-up ↔ world-north-up). Ctrl+wheel zooms the map; player ignores wheel while Ctrl is held.

### Item system (inventory · drop · world item · pickup)

`InventoryHud` (`Control`, HUD bottom-right) is the hub; procedurally drawn like the mini-map.
- **`Inventory` / `Item`** — 12-slot model (3×4, pure data) and a minimal item entity (`TypeId`, `DisplayName`, `Category` = Consumable/Key, `ModelPath`, `Icon`, `Use()`). A subset of REQ-0012 (`requirements/REQ-0012-base-item/`); one camera is seeded into slot 0 in `_Ready`.
- **Inventory HUD** (REQ-0011, US-11): compact (bag + "N/12") vs expanded (3×4 grid). **double-I** toggles; when open, digit **1–3** picks a row then **1–4** a column → apply (`Item.Use`, pattern A). Game **not paused**, mouse stays captured.
- **Item icons render the 3D model:** the item's `ModelPath` glb is rendered in a `SubViewport` (`OwnWorld3D`, camera auto-framed from the model AABB) → `ViewportTexture` drawn into the slot.
- **Drop** (`REQ-0015-base-item-drop`, US-15): **Shift+column** or **G** → `DropProjectile` (a glowing parabolic "star") flies out and lands as a **`WorldItem`** on the floor (glb scaled to 1/8 player height).
- **Pickup** (`REQ-0016-base-item-pickup`, US-16): **automatic, no key** — `InventoryHud._PhysicsProcess` scans `WorldItem.All` for the nearest *armed* item within `PickupRange` with clear line-of-sight (raycast, mask 1), inventory not full. `PickupProjectile` flies the star back to the player; the slot fills with a flash. A `WorldItem` **arms** only after the player has once been outside its radius (prevents instantly re-grabbing a just-dropped item).
- **Shared:** `ItemStar` builds the identical emissive star for both projectiles. `WorldItem` keeps a static registry (`All`) and holds its `Item` so pickup restores it.
- **Not implemented:** Pattern B (activate-to-hand / LMB), slot reservation (F-19a).

### Input map

WASD / arrow keys, **Tab** (minimap orientation), **double-I** (inventory), **1–4** (inventory row/cell), **Shift+1–4 / G** (drop), **Ctrl+Q** quits, mouse look, wheel zoom, gamepad left stick. Pickup is automatic (no key). See `[input]` in `project.godot`.

### Art pipeline

Source `.blend` files live in `art/`, imported as `.glb`. Materials are separate `.tres` resources.

| File | Status | Purpose |
|------|--------|---------|
| `art/AnimationLibrary_Godot_Standard.glb` | **Active** | Player: rigged humanoid + AnimationPlayer (46 clips) |
| `art/old_kodak_camera.glb` | **Active** | 3D model for the camera item (US-13), seeded in inventory + dropped in world |
| `art/vintage_camera.glb` | Asset (unused in scene) | Alternate camera model candidate |
| `art/mob.glb` | Asset (unused in scene) | Enemy model; `Mob.cs` exists but no spawner |
| `art/player.glb` | **Deprecated** | Old sphere-based player; no longer in any scene |
| `art/House In a Forest Loop.ogg` | Asset (unused) | Background music, not yet integrated |

### Requirements

`requirements/` docs are **in Russian**. They describe WHAT the game does, not HOW. `requirements/TECH_SPEC.md` is the authoritative technical reference in English. Catalog index: `requirements/README.md` (feature → US → F-ID → status → file).

Reserved low IDs (0000–0009) are core/meta docs; feature IDs match their US number (US-10 → REQ-0010):
- `requirements/REQ-0000-vision.md`, `REQ-0001-user-journey.md`, `REQ-0002-non-functional.md` — context/vision (US-09).
- `requirements/REQ-0003-core/` — US-01..08/F-01..08 (**the core game**: movement, camera, maze, chunking, input, visual — what all of `src/*.cs` implements). Start here to map a C# file to its requirement.

Feature folders (`REQ-NNNN-<slug>/`, each with README + facet files + `design.md`):
- `requirements/REQ-0010-minimap/` — US-10/F-09..F-11 (mini-map, implemented)
- `requirements/REQ-0011-inventory/` — US-11/F-12..F-14 (inventory, 3×4 grid, implemented; pattern A only — see its `design.md` for scope)
- `requirements/REQ-0012-base-item/` — US-12/F-15..F-19a (base item entity, shared by all items; only the inventory-facing subset exists in `Item.cs`)
- `requirements/REQ-0013-vintage-camera/` — US-13/F-20..F-23 (vintage camera item, not yet implemented)

### Placeholder / unused code

- `game_object.cs` — empty `Node` subclass, unused placeholder.
- `src/Mob.cs` — enemy controller stub; `mob.tscn` exists but is never spawned.
- `3d_squash_the_creeps_starter/` — tutorial reference assets, not part of this project.

## Documentation & requirements rules (MANDATORY)

Binding rules. Any change that touches behaviour, input, or architecture MUST update the docs in the **same change** — docs are part of "done", not a follow-up.

**Where docs live**
- `requirements/` — the requirements catalog. Docs are in **Russian** and describe **WHAT** the game does, never HOW.
- `requirements/TECH_SPEC.md` — the single authoritative technical reference (**HOW**), in **English**.
- `requirements/README.md` — the registry/index: one row per feature (ID · name · US · F-ID · status · path) plus a "Связи между фичами" section. Update it whenever a feature is added, moved, or changes status.
- `requirements/REQ-0004-keybindings.md` — a live snapshot of every working key. Update on ANY input-map or hardwired-key change.

**Feature folder structure** — one folder per feature: `requirements/REQ-NNNN-<slug>/`
- `README.md` — overview only: User Story (US-NN), acceptance criteria, an `ID → файл` map, status, related links.
- Numbered **facet files** `NN-<facet>.md`, one concern each. Facets: `logic`, `ui`, `visual`, `input`, `data`, `animation`. Split a concern into its own file when it is distinct (e.g. keep `animation` separate from `visual`) — never cram two facets into one file.
- `design.md` — **required**. The HOW for this feature: names the `src/*.cs` files, key decisions, and an explicit scope/limits ("границы") note. References TECH_SPEC.
- Single-page context/meta docs may be a flat `REQ-NNNN-<slug>.md` instead of a folder (reserved low IDs 0000–0009 = core/meta).

**WHAT vs HOW — do not mix**
- Facet files and README = WHAT. No file/class/method names, no implementation mechanics. Tunable values go in a "Параметры" table (name + default + meaning), not as code.
- `design.md` = HOW. Names the code, explains mechanics/trade-offs, and lists what is deliberately NOT implemented.

**Semantic IDs are permanent anchors**
- `US-NN` (user story) and `F-NN` (functional requirement) are referenced from code comments and TECH_SPEC. **Never renumber or reuse them.**
- New feature → next free `REQ-NNNN` folder + `US-NN`. New functional requirement → next free `F-NN`. IDs are globally flat, not per-parent.

**Sub-features nest inside their parent**
- A requirement that refines/extends an existing feature lives in a **subfolder of that feature**, named `REQ-NNNN-<parent-slug>-<slug>` (e.g. `REQ-0012-base-item/REQ-0014-base-item-item-in-world/`). Global `NNNN` numbering stays flat. The parent README lists its sub-features. An independent feature stays top-level.
- When you move/rename a folder, fix EVERY relative link into and out of it (paths are depth-sensitive) plus the registry, and verify no broken links remain.

**Status markers** (README and registry must agree): `ℹ️` context · `🟡` planned · `✅` implemented (add "базово"/"частично" when partial). On implementing something, flip the status in the feature README AND the registry row, and record any scope cut in `design.md`.

**Update checklist for any behaviour / input / architecture change**
1. Feature docs: relevant facet(s) + README + `design.md` (incl. scope/limits) + status.
2. `requirements/README.md` registry (row, path, "Связи").
3. `requirements/REQ-0004-keybindings.md` — if input changed.
4. `CLAUDE.md` **and** `AGENTS.md` — if scene tree, art pipeline, architecture, or conventions changed. Keep the two files' shared rule/architecture content in sync.

## Critical gotchas

**GridMap cell centering.** `chunk.tscn` sets `cell_center_y = false` while X/Z stay true. This keeps the floor on Y=0. Do NOT change `cell_size.y` to wall height — wall mesh is 30 tall with an explicit `mesh_transform` Y=+15 offset. Changing `cell_size.y` or `cell_center_y` without understanding this will push the floor up and drop the player.

**Floor collision requires Transform3D in MeshLibrary shapes.** The `shapes` array in `MazeTiles.tres` is a flat `[shape, transform, ...]` list. The floor item must include `Transform3D identity` after its `BoxShape3D` — without it the floor has no collision and the player falls through.

**Chunk load order: AddChild before Setup.** `ChunkManager.LoadChunk` must call `AddChild(chunk)` before `chunk.Setup()`. `_Ready` only fires after entering the scene tree, so `gridmap` is null if Setup runs first.

**Input.UseAccumulatedInput = false.** Set in `Player._Ready`. Held-key auto-repeat on Linux/X11 starves queued `InputEventMouseMotion` events; accumulation off fixes mouse look while movement keys are held.

**Wall `uv1_scale.y` floor.** Don't push `uv1_scale.y` below ~0.05 in `MazeTiles.tres` — the vertical noise streaks fan out into "fur". Current value 0.06 is the stable maximum for vertical fluting.

## Conventions

- **No comments** unless asked. Use `GD.Print` for debug logging (not `GD.PrintErr`).
- **All game-critical behaviour goes in `_PhysicsProcess`** (fixed 60 Hz), not `_Process`. The only exception is `Minimap._Draw` (drawing) — but its cell-visit detection stays in `_PhysicsProcess`.
- Editor path is absolute; never assume it is in `$PATH`.
