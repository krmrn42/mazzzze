# AGENTS.md

Compact instruction file for OpenCode sessions. Every line answers: "Would an agent likely
miss this without help?" If not, it's left out. See also `CLAUDE.md` for the original AI
guidance (most technical detail lives there).

## Quick start (command order matters)

```bash
# 1. Build C# (MUST run after any .cs change before Godot)
dotnet build

# 2. Import assets (only after new .tscn/.tres/.glb or fresh clone)
/home/user13/Apps/Godot_v4.6.3-stable_mono_linux_x86_64/Godot_v4.6.3-stable_mono_linux.x86_64 --headless --import

# 3. Run (DISPLAY needed)
DISPLAY=:0 /home/user13/Apps/Godot_v4.6.3-stable_mono_linux_x86_64/Godot_v4.6.3-stable_mono_linux.x86_64 --path .
```

## Verification (no GUI)

After any behaviour change, verify with:

```bash
dotnet build && timeout 8 /home/user13/Apps/Godot_v4.6.3-stable_mono_linux_x86_64/Godot_v4.6.3-stable_mono_linux.x86_64 --headless --path . 2>&1 | grep -iE "Player|Chunk|error|exception"
```

Expected output includes `[Player] Start cell=…` and `[ChunkManager] LOAD` lines, with no
error/exception lines. For physics questions add a temporary throttled `GD.Print` inside
`Player._PhysicsProcess`, run the command, then remove the debug code.

For visual changes, temporarily save a screenshot from `_PhysicsProcess` (see `CLAUDE.md` for
the snippet), run windowed with `DISPLAY=:0`, then delete the temp code and `shot.png`.

## No tests — all verification is CLI-based

There are no unit tests, integration tests, or CI. Change verification is the headless
`grep` above for logic, or a temporary screenshot for visuals. Never add test files unless
the user asks for them.

## Critical gotchas

**GridMap cell centering.** `chunk.tscn` sets `cell_center_y = false` while X/Z stay true.
This keeps the floor on Y=0. Do NOT change `cell_size.y` to wall height — wall mesh is 30
tall with an explicit `mesh_transform` Y=+15 offset. Changing `cell_size.y` or
`cell_center_y` without understanding this will push the floor up and drop the player.

**Tile overlap (seam fix).** Floor and wall *meshes* are 3.66 wide in `MazeTiles.tres` but
GridMap `cell_size` is 3.6 and collision shapes are 3.6. This 0.03 overlap per side hides
float32 precision cracks at the huge world coords (~-18000). Never resize collision shapes
to match the meshes.

**Floor collision requires Transform3D in MeshLibrary shapes.** The `shapes` array in
`MazeTiles.tres` is a flat `[shape, transform, ...]` list. The floor item must include
`Transform3D identity` after its `BoxShape3D` — without it the floor has no collision and
the player falls through.

**Chunk load order: AddChild before Setup.** `ChunkManager.LoadChunk` calls
`chunk.AddChild(gridmap)` then `chunk.Setup()`. `_Ready` only fires after entering the
scene tree, so `Setup` must come after `AddChild` or `gridmap` will be null.

**Input.UseAccumulatedInput = false.** Set in `Player._Ready`. Held-key auto-repeat on
Linux/X11 starves queued `InputEventMouseMotion` events; accumulation off fixes mouse look
while movement keys are held.

**Character faces the right way via 180° Y rotation.** `Basis.LookingAt` points −Z at the
target direction. The character model's own forward is +Z, so `player.tscn` rotates the
`Character` node 180° around Y. Without this the player walks backward.

**Mini-map cell detection MUST stay in `_PhysicsProcess`** (fixed 60 Hz), not `_Process`.
At the variable render rate a cell can be skipped in a single frame.

**Wall `uv1_scale.y` floor.** Don't push `uv1_scale.y` below ~0.05 in `MazeTiles.tres` —
the vertical noise streaks fan out into "fur".

## Architecture (what filenames don't tell you)

- **MazeData.cs** is the central authority for world layout. The maze is 10000×10000 cells,
  stateless and deterministic — `IsFloor(wx, wz)` computes cell type in O(1) via a murmur3
  variant hash. There is no stored world array.
- **Chunk streaming:** only 9 chunks (3×3, `LoadDistance=1`) are loaded. Load iterates a
  full `[-1,1]×[-1,1]` square and unload uses per-axis `Abs > LoadDistance` (Chebyshev, not
  Manhattan). Each chunk is 16×16 cells = 57.6×57.6 world units. `ChunkManager.UpdateChunks()`
  is called every `_PhysicsProcess` from `Player.cs`.
- **MazeTiles.tres** is a `MeshLibrary` with exactly 2 items: id 0 = Floor, id 1 = Wall.
- **Item system** hubs in `InventoryHud.cs` (`HUD/Inventory`). `Inventory`/`Item` = 12-slot model; slot icons render the item's glb into a `SubViewport`. Drop (`DropProjectile`) flings a glowing "star" that lands as a `WorldItem` (glb on floor, kept in a static `WorldItem.All` registry). Pickup is **automatic** (no key): `InventoryHud._PhysicsProcess` scans the registry for the nearest armed item in range with line-of-sight; `PickupProjectile` flies the star back. `ItemStar` is the shared star visual. Requirements: `REQ-0011-inventory/`, and sub-features `REQ-0012-base-item/REQ-001{4,5,6}-...`.
- **Mob.cs** exists but mobs are not spawned — no spawner is implemented.
- **game_object.cs** is an empty unused placeholder. Ignore it.

## Conventions

- No comments unless asked. The codebase has none; don't add them.
- Use `GD.Print` for debug logging, not `GD.PrintErr` (the headless verification greps
  both stdout and stderr anyway).
- All game-critical behaviour is in `_PhysicsProcess`, not `_Process`. The mini-map is the
  exception — drawing lives in `_Process` but cell-visit detection stays in
  `_PhysicsProcess`.
- Editor path is absolute, not in PATH. Always use the full path to the Godot binary.
- `.claude/` is in `.gitignore` — don't put OpenCode config there.
- Requirements catalog in `requirements/` (index: `requirements/README.md`): WHAT in Russian,
  one `REQ-NNNN-<slug>/` folder per feature (README + facet files `NN-logic/ui/visual/input.md`
  + `design.md`). `requirements/TECH_SPEC.md` is the authoritative technical reference (HOW) in English.
  Full rules below.

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
