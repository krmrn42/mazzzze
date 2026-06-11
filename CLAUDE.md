# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

A Godot 4.6 3D maze game prototype using C# (.NET 8.0) with the Jolt Physics engine. The project uses the Forward Plus renderer and targets Windows (D3D12) primarily.

## Build & Run

```bash
# Build the C# project
dotnet build

# Build release configuration
dotnet build -c ExportRelease

# Run the game from the command line (if Godot is installed)
godot --headless    # run without graphics
godot               # launch the editor
godot --editor       # force editor mode
```

The main scene is `main.tscn` (set in project.godot). Press F5 in the Godot editor to run.

## Architecture

### Scene Hierarchy

```
Main (Node3D) — main.tscn
├── Ground (StaticBody3D) — 60×2×60 box with collision
├── DirectionalLight3D — overhead light with shadows enabled
├── Player (CharacterBody3D) — instance of player.tscn
│   ├── Pivot (Node3D)
│   │   └── Character (GLB model from art/player.glb)
│   └── CollisionShape3D — sphere radius 0.8
└── CameraPivot (Marker3D) — positioned above/behind
    └── Camera3D — orthographic projection, size 19
```

### Player Controller (`src/Player.cs`)

`CharacterBody3D`-based controller with WASD movement and spacebar jumping. Key details:
- Movement uses `Input.GetVector()` mapped to `move_left`, `move_right`, `move_forward`, `move_back` input actions
- The `Pivot` child node rotates to face the movement direction via `Basis.LookingAt(direction)`
- Gravity applied when not on floor; `JumpVelocity` applied on jump input
- Exported properties: `Speed` (default 5.0), `JumpVelocity` (default 4.5)

### Input Map

WASD + arrow keys for movement, spacebar for jump. Gamepad left stick and D-pad also mapped. See `[input]` section in `project.godot`.

### Art Pipeline

Source files are `.blend` files in `art/`. They are imported as `.glb` scenes. Materials (`.tres`) are defined separately:
- `player.glb` — player character model with body, eye, and pupil materials
- `mob.glb` — enemy model (not yet placed in any scene)
- `House In a Forest Loop.ogg` — background music

Fonts: Montserrat-Medium.ttf (SIL Open Font License).

### Placeholder Code

- `game_object.cs` is an empty `Node`-derived class — not used in any scene, likely for future use.
- The `3d_squash_the_creeps_starter/` directory and its `.zip` are reference/tutorial assets, not part of this project.
