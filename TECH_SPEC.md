# Maze Prototype 1 — Technical Specification

> **Target audience:** AI coding agents. This document describes the current implementation
> in sufficient detail to recreate an equivalent game from scratch.

---

## 1. Technology Stack

| Layer | Choice | Version |
|-------|--------|---------|
| Engine | Godot | 4.6 |
| Language | C# | .NET 8.0 |
| Physics | Jolt Physics | (built-in) |
| Renderer | Forward Plus | (built-in) |
| Platform | Windows (D3D12) | - |

**Project file:** `maze-prototype-1.csproj` — Godot SDK 4.6.3, nullable enabled.

## 2. Build and Run

```bash
dotnet build                          # Debug build
dotnet build -c ExportRelease         # Release build
godot                                 # Launch editor
godot --headless                      # Run headless
```

Main scene: `res://main.tscn` (configured in `project.godot`).

## 3. Project File Tree

```
maze-prototype-1/
├── project.godot              # Engine config, input map, physics, rendering
├── main.tscn                  # Main scene - root of the game
├── player.tscn                # Player character scene (instantiated into main)
├── chunk.tscn                 # Maze chunk scene (instantiated dynamically)
├── mob.tscn                   # Enemy scene (not yet instantiated)
├── MazeTiles.tres             # MeshLibrary: Floor + Wall tiles for GridMap
├── maze-prototype-1.csproj    # C# project file
├── maze-prototype-1.sln       # Solution file
├── icon.svg / icon.webp       # App icons
├── game_object.cs             # Placeholder (unused)
├── TECH_SPEC.md               # This file
├── CLAUDE.md                  # Original Claude guidance (outdated)
├── art/
│   ├── player.glb             # Player 3D model (GLTF binary)
│   ├── player.blend           # Player source (Blender)
│   ├── body.tres              # Player body material (orange)
│   ├── eye.tres               # Player eye material (white, emissive)
│   ├── pupil.tres             # Player pupil material (black, rim)
│   ├── mob.glb                # Enemy 3D model
│   ├── mob.blend              # Enemy source (Blender)
│   ├── mob_body.tres          # Enemy body material (blue)
│   ├── mob_eye.tres           # Enemy eye material (red, emissive)
│   └── House In a Forest Loop.ogg  # Background music
└── src/
    ├── Player.cs              # Player controller
    ├── MazeData.cs            # Maze world data & procedural generation
    ├── ChunkManager.cs        # Chunk loading/unloading orchestrator
    ├── Chunk.cs               # Single chunk - GridMap filler from cell data
    └── Mob.cs                 # Enemy controller (placeholder)
```

## 4. Scene Hierarchy (Runtime)

```
Main (Node3D)                              - main.tscn, root
├── Ground (StaticBody3D)                  - collision floor, Y=-0.5
│   ├── CollisionShape3D                   - BoxShape3D(256, 1, 256)
│   └── MeshInstance3D                     - BoxMesh(256, 1, 256), green-brown
├── DirectionalLight3D                     - Y=-60 tilt, energy=0.8
├── MazeData (Node + MazeData.cs)         - Singleton, procedural world data
├── Player (CharacterBody3D)              - instance of player.tscn
│   ├── ModelPivot (Node3D, Y=0.25)       - faces movement direction
│   │   └── Character (player.glb, scale=0.3) - 3D model
│   ├── CollisionShape3D (Y=0.35)         - SphereShape3D, radius=0.3
│   └── CameraYaw (Node3D, Y=0.5)         - horizontal orbit
│       └── CameraPitch (Node3D)          - vertical tilt
│           └── Camera3D (Z=2, current)   - perspective, default FOV
├── ChunkManager (Node3D + ChunkManager.cs) - orchestrates chunk lifecycle
│   └── Chunk (xN, dynamic)              - instances of chunk.tscn
│       └── GridMap (cell_size=2,1,2)     - renders Floor/Wall tiles
└── WorldEnvironment                      - procedural sky, ambient light
```

## 5. Subsystem Specifications

### 5.1 MazeData - World Data and Procedural Generation

**File:** `src/MazeData.cs`
**Type:** `Node` (singleton via `Instance` static property)
**Initialization:** `_EnterTree()` sets `Instance = this`; `_Ready()` prints debug info.

**Constants:**

| Constant | Value | Meaning |
|----------|-------|---------|
| WorldWidth | 10000 | Maze cells in X dimension |
| WorldHeight | 10000 | Maze cells in Z dimension |
| CellWorldSize | 2.0f | World units per maze cell |

**Computed Properties:**

| Property | Formula | Value |
|----------|---------|-------|
| WorldOffsetX | -WorldWidth * CellWorldSize / 2 | -10000 |
| WorldOffsetZ | -WorldHeight * CellWorldSize / 2 | -10000 |
| PlayerStartCell | Vector2I(1, 1) | Entry cell (maze coordinates) |

The world is centred at origin: cells [0, 9999] map to world X/Z approx [-10000, +9998].

**Procedural Maze Algorithm - IsFloor(wx, wz):**

Deterministic, stateless, O(1) per cell. No global array stored.

Cell classification rules (evaluated in order):

1. Border cells (wx<=0 or wz<=0 or wx>=9999 or wz>=9999) -> wall
2. Entrance: cell (1, 0) -> floor (top entrance)
3. Exit: cell (9998, 9999) -> floor (bottom exit)
4. Odd-Odd cells ((wx&1)==1 and (wz&1)==1) -> floor (corridor hubs, ensures global connectivity)
5. Even-Odd cells (vertical walls between adjacent corridors): murmur3-finalizer hash -> floor if hash%100 < 70 (70% open)
6. Odd-Even cells (horizontal walls): same hash -> floor if hash%100 < 70
7. Even-Even cells (pillars): floor if hash%100 < 5 (5% open)

**Hash function** (murmur3 finalizer variant):
```
h = wx * 0x45d9f3b + wz * 0x119de1f3
h = (h ^ (h >> 16)) * 0x85ebca6b
h = (h ^ (h >> 13)) * 0xc2b2ae35
h = h ^ (h >> 16)
```

**Chunk Data API - GetChunkData(chunkX, chunkZ, chunkSize):**

Returns `int[chunkSize, chunkSize]` where 0=floor, 1=wall. Iterates over the chunk's cell range and calls IsFloor() for each. Out-of-bounds chunks return all-1 (wall).

### 5.2 ChunkManager - Dynamic Chunk Streaming

**File:** `src/ChunkManager.cs`
**Type:** `Node3D`

**Constants:**

| Constant | Value | Meaning |
|----------|-------|---------|
| ChunkSize | 16 | Cells per chunk (16x16) |
| LoadDistance | 1 | Chunk load radius (Manhattan): 3x3 = 9 active chunks |

**State:**

| Field | Type | Purpose |
|-------|------|---------|
| activeChunks | Dictionary<string, Node3D> | Key = "{chunkX}_{chunkZ}", value = chunk instance |
| chunkScene | PackedScene | res://chunk.tscn |
| meshLibrary | MeshLibrary | res://MazeTiles.tres |

**UpdateChunks(Vector2 playerWorldPos):**

Called every frame from Player._PhysicsProcess(). Steps:

1. Convert world position -> maze cell coordinates -> chunk coordinates
2. Iterate chunk coords in range [center-LoadDistance, center+LoadDistance]
3. For each not-yet-loaded chunk -> LoadChunk()
4. Scan active chunks: if Manhattan distance > LoadDistance -> QueueFree() + remove from dict
5. Print "[ChunkManager] UNLOAD ..." for each removed chunk

**LoadChunk(Vector2 chunkPos) (private):**

1. Call MazeData.Instance.GetChunkData(chunkX, chunkZ, 16) -> int[16,16]
2. Instantiate chunk.tscn
3. Set world position:
   - chunk.Position.X = chunkX * 16 * CellWorldSize + WorldOffsetX
   - chunk.Position.Z = chunkZ * 16 * CellWorldSize + WorldOffsetZ
4. Assign MeshLibrary to chunk
5. AddChild(chunk) - enters scene tree, _Ready() fires
6. chunk.Setup(chunkPos, chunkData) - fills GridMap with tiles
7. Store in activeChunks dict
8. Print "[ChunkManager] LOAD chunk (X,Z) size=32x32 world=(wx,wz) totalActive=N"

Each chunk covers 16x16 cells = 32x32 world units (CellWorldSize=2).

### 5.3 Chunk - GridMap Tile Filler

**File:** `src/Chunk.cs`
**Type:** `Node3D` with [Export] int ChunkSize=16 and [Export] MeshLibrary MeshLibrary.

**Scene (chunk.tscn):**
```
Chunk (Node3D + Chunk.cs)
└── GridMap
    mesh_library = MazeTiles.tres
    cell_size = Vector3(2, 1, 2)
```

**Setup(Vector2 coord, int[,] chunkData):**

1. Store chunkCoord
2. gridmap.Clear() - remove previous tiles
3. Iterate x in [0, ChunkSize), z in [0, ChunkSize):
   - cellType = chunkData[x, z]
   - tileId = 0 if floor, 1 if wall, -1 if unknown
   - gridmap.SetCellItem(new Vector3I(x, 0, z), tileId)

GridMap places each tile centred at the cell's world position. cell_size=(2,1,2) means adjacent cells are 2 world-units apart in XZ.

### 5.4 MeshLibrary - Maze Tiles

**File:** `MazeTiles.tres`
**Type:** `MeshLibrary` with 2 items.

**Item 0 - Floor:**

| Property | Value |
|----------|-------|
| Mesh | BoxMesh(2, 0.2, 2) - flat square, 2x2 XZ, 0.2 thick |
| Material | StandardMaterial3D, albedo=Color(0.75, 0.70, 0.60) - warm sand |
| Collision | BoxShape3D(2, 0.2, 2) - centred at cell Y=0 |
| mesh_transform | Identity (Y=0, centred on floor) |
| Shadow casting | On |

**Item 1 - Wall:**

| Property | Value |
|----------|-------|
| Mesh | BoxMesh(2, 1, 2) - cube 2x2 XZ, 1 tall |
| Material | StandardMaterial3D, albedo=Color(0.35, 0.33, 0.30) - dark stone |
| Collision | BoxShape3D(2, 1, 2) with Transform3D Y=+0.5 |
| mesh_transform | Transform3D Y=+0.5 - wall SITS ON the floor (Y=0 to 1) |
| Shadow casting | On |

The Y=+0.5 offset is critical: without it, the wall BoxMesh would be centred at Y=0 (half below floor). With the offset, wall occupies Y=0 to Y=1, on top of floor tile (Y=-0.1 to Y=0.1).

### 5.5 Player - Character Controller

**File:** `src/Player.cs`
**Type:** `CharacterBody3D` (extends from player.tscn scene)

**Exported Properties:**

| Property | Default | Purpose |
|----------|---------|---------|
| Speed | 5.0f | Movement speed (units/sec) |
| MouseSensitivity | 0.002f | Mouse look sensitivity |
| Gravity | 15.0f | Downward acceleration |
| ZoomStep | 0.5f | Mouse wheel zoom increment |
| MinZoom | 1.5f | Closest camera distance |
| MaxZoom | 4.0f | Furthest camera distance |

**Scene Hierarchy (player.tscn):**
```
Player (CharacterBody3D)
├── ModelPivot (Node3D, Y=0.25)          - faces movement direction
│   └── Character (player.glb instance, scale=0.3)
├── CollisionShape3D (Y=0.35)            - SphereShape3D, radius=0.3
└── CameraYaw (Node3D, Y=0.5)            - mouse X: RotateY
    └── CameraPitch (Node3D)             - mouse Y: RotateX (clamped)
        └── Camera3D (Z=2, current=true) - perspective
```

**Player Model:**
- Source: art/player.glb (GLTF binary)
- Original bounds (3 primitives): X[-1.0..1.0], Y[-0.47..0.43], Z[-1.04..1.96]
- Applied scale: 0.3 -> effective size approx 0.61 wide x 0.27 tall x 0.90 deep
- ModelPivot Y offset: 0.25 - places model feet approx at floor level (Y=0)
- Collision sphere: radius 0.3, centred at Player.Y+0.35, bottom at Player.Y+0.05

**Camera System - Dual-Node Orbit:**

- CameraYaw (Node3D, Y=0.5): rotates around world Y axis via RotateY(-mouse_dx * sensitivity). Horizontal orbit.
- CameraPitch (Node3D, child of CameraYaw): rotates around local X axis via RotateX(-mouse_dy * sensitivity). Clamped to [-15 deg, +25 deg].
- Camera3D (child of CameraPitch, local Z=zoom): distance controlled by mouse wheel.

Why this works: CameraYaw rotates the entire pitch+camera assembly horizontally. CameraPitch tilts up/down. Camera at Z=+zoom always looks back toward player (origin of CameraPitch = cameraYaw origin = player pos + Y=0.5).

Camera height constraint: Pitch clamped to [-15 deg, +25 deg]. At max zoom (4.0) and max downward pitch (-15 deg): camera world Y = Player.Y + 0.5 - 4*sin(-15) = 0.5 + 1.04 = 1.54 (slightly above 1-unit wall). At nominal zoom (2.0): Y = 0.5 + 0.52 = 1.02 (at wall-top level). Camera cannot look OVER walls into adjacent corridors.

**Movement Logic (_PhysicsProcess):**

1. Gravity: if !IsOnFloor(), Velocity.Y -= Gravity * dt
2. Input: Input.GetVector("move_left", "move_right", "move_forward", "move_back") returns Vector2(-1..1, -1..1)
3. Camera-relative direction:
   - camForward = -CameraYaw.GlobalBasis.Z (world forward)
   - camRight = CameraYaw.GlobalBasis.X (world right)
   - moveDir = (camForward * -input.Y + camRight * input.X).Normalized()
   - Note: input.Y is negated because GetVector returns -1 for the "negative Y" action (move_forward, i.e. W key). So -input.Y is +1 when W is pressed.
4. Velocity: vel.X = moveDir.X * Speed; vel.Z = moveDir.Z * Speed
5. Model facing: ModelPivot.Basis = Basis.LookingAt(moveDir.WithY(0), Vector3.Up)
6. Physics: MoveAndSlide() handles collision with floor and walls
7. Chunk update: ChunkManager.UpdateChunks(new Vector2(GlobalPosition.X, GlobalPosition.Z))

**Start Position:**
```
Position = (PlayerStartCell.X * CellWorldSize + WorldOffsetX + CellWorldSize/2,
            1,   // falls onto ground
            PlayerStartCell.Y * CellWorldSize + WorldOffsetZ + CellWorldSize/2)
         = (1*2 + (-10000) + 1, 1, 1*2 + (-10000) + 1)
         = (-9997, 1, -9997)
```
The +CellWorldSize/2 centres the player within the cell (GridMap places cell centres at cell_index * cell_size).

**Input Map (project.godot):**

| Action | Keys (physical) | Keys (logical) | Gamepad |
|--------|-----------------|----------------|---------|
| move_left | A(65), LeftArrow(4194319) | - | Button 2 |
| move_right | D(68), RightArrow(4194321) | - | Button 1 |
| move_forward | W(87), UpArrow(4194377) | - | Button 3 |
| move_back | S(83), DownArrow(4194376) | - | Button 4 |
| jump | Space(32) | - | Button 0 |

Dead zone: 0.2. Mouse captured (Input.MouseMode = Captured).

### 5.6 Ground - Floor Collision

Scene node in main.tscn:
- Ground (StaticBody3D, Y=-0.5)
  - CollisionShape3D: BoxShape3D(256, 1, 256), identity transform
  - MeshInstance3D: BoxMesh(256, 1, 256), green-brown material

Ground collision spans Y=[-1.0, 0.0]. Top surface at Y=0. Provides flat floor across 256x256 playable area. Maze is 20000x20000. For outer areas, GridMap floor tile collision provides walking surface.

### 5.7 Lighting and Environment

**DirectionalLight3D:**
- Rotation X=-60 deg (tilted down), Y=90 deg (from side). World position (1, 15, -1).
- Energy: 0.8, shadows enabled.

**WorldEnvironment:**
- Background: Sky (mode=2)
- Sky: ProceduralSkyMaterial
- Ambient light: source=Sky (mode=2), energy=0.3
- Reflected light: source=Sky (mode=1)
- SDFGI: disabled

### 5.8 Mob - Enemy (Placeholder)

**File:** `src/Mob.cs`
**Type:** `CharacterBody3D`
**Scene:** `mob.tscn` (not instantiated in main scene)

- Collision: BoxShape3D(2, 1, 2)
- Model: art/mob.glb with separate body (blue) and eye (red emissive) materials
- Initialize(startPos, playerPos): faces player, sets forward velocity with random speed [10, 15]
- OnVisibilityNotifierScreenExited(): self-destructs via QueueFree()
- Not currently spawned - code exists but no spawner implemented.

### 5.9 Art Assets

| File | Type | Purpose |
|------|------|---------|
| art/player.glb | GLTF binary | Player model (sphere-based character) |
| art/player.blend | Blender source | Player model source |
| art/body.tres | SpatialMaterial | Player body - orange (#E85A00), roughness 0.5 |
| art/eye.tres | SpatialMaterial | Player eye - white (#DBDBDB), metallic, emissive |
| art/pupil.tres | SpatialMaterial | Player pupil - black, roughness 0.3, rim effect |
| art/mob.glb | GLTF binary | Enemy model |
| art/mob.blend | Blender source | Enemy model source |
| art/mob_body.tres | SpatialMaterial | Enemy body - blue (#0F447D), roughness 0.43 |
| art/mob_eye.tres | SpatialMaterial | Enemy eye - red (#C21D30), metallic, emissive |
| art/House In a Forest Loop.ogg | OGG audio | Background music (not yet integrated) |

All .blend import disabled (filesystem/import/blender/enabled=false).

## 6. Physics Configuration

- Engine: Jolt Physics
- Player collision: sphere radius 0.3, layer 1, mask 1
- Ground collision: static box 256x1x256, layer 1 (default)
- Wall collision: GridMap-generated BoxShape3D(2, 1, 2) per wall tile
- Floor collision: GridMap-generated BoxShape3D(2, 0.2, 2) per floor tile
- Movement: CharacterBody3D.MoveAndSlide() with built-in sliding collision

## 7. Coordinate Systems

**Maze Cell Coordinates:**
- Origin: (0, 0) at top-left corner of the maze
- X-axis: east (increasing column index)
- Z-axis: south (increasing row index)
- Range: [0, 9999] in both axes
- Cell (1, 0) = entrance; Cell (9998, 9999) = exit

**World Coordinates:**
- Origin: centre of the maze
- X-axis: east; Y-axis: up; Z-axis: south (Godot convention: -Z = forward)
- Maze cells map to world via:
  - worldX = cellX * CellWorldSize + WorldOffsetX + CellWorldSize/2
  - worldZ = cellZ * CellWorldSize + WorldOffsetZ + CellWorldSize/2
  - where WorldOffsetX = WorldOffsetZ = -10000, CellWorldSize = 2
- Maze extends from world X/Z approx [-10000, +9998]
- Floor surface Y = 0; walls occupy Y = [0, 1]

**Chunk Coordinates:**
- Chunk (0, 0) covers cells [0, 15]; Chunk (624, 624) covers cells [9984, 9999]
- Total: 625x625 = 390,625 possible chunks
- At any moment, 9 chunks are active (LoadDistance=1 -> 3x3 grid around player)

## 8. Key Design Decisions

1. **Procedural, stateless maze** - 10000x10000 = 100M cells cannot fit in memory (400MB for ints). Hash-based IsFloor() generates any cell in O(1) without storage.

2. **Odd-odd cells = guaranteed corridors** - ensures the entire maze is connected. Without this, the hash could create isolated rooms.

3. **70% wall removal** between adjacent corridors - balances openness (maze is navigable) with structure (dead ends and turns exist).

4. **CellWorldSize = 2** - corridors are 2 world-units wide, providing comfortable space for player model (0.6 wide) with clearance on both sides.

5. **Dual-node orbit camera** - avoids gimbal lock. Yaw-pitch decomposition means camera orbits cleanly regardless of orientation.

6. **Camera pitch clamped to [-15 deg, +25 deg]** - prevents player from looking over walls and seeing maze layout from above. Preserves first-person maze experience.

7. **AddChild before Setup** - _Ready() only fires after entering scene tree. AddChild(chunk) must precede chunk.Setup() so gridmap is not null.

8. **GridMap for maze rendering** - Godot's built-in GridMap efficiently batches same-mesh tiles, reducing draw calls. 16x16 cells per chunk = 256 tiles per GridMap.
