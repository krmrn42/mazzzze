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
