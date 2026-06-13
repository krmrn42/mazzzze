using Godot;

// Mini-map HUD widget (US-10, F-09, F-10, F-11).
//
// A procedurally drawn, top-left overlay over the streaming maze:
//   - near zone (NearRadius cells around the player): per-cell floor/wall detail
//   - explored-but-far cells: a flat "schematic" silhouette, no per-cell detail
//   - everything else: fog of war (hidden), tracked by MinimapState
//   - player position + facing arrow; entrance/exit arches once visited
//   - rotates with the camera by default ("forward = up"); Tab toggles north-up
//   - Ctrl+wheel zooms (clamped so you can neither reach a single cell nor see it all)
//
// TODO (F-09, next version): replace this functional styling with the full hand-drawn
// parchment look — procedural parchment texture background, soft "burnt" fog edges,
// hatched walls, and decorative arch icons. The behaviour below is the agreed first pass.
public partial class Minimap : Control
{
	[Export] public float ScreenWidthFraction = 0.18f; // F-09: ~15–20% of screen width
	[Export] public float Margin = 16.0f;
	[Export] public int NearRadius = 7;                // 15×15 fully-detailed near zone
	[Export] public int MinCellsRadius = 5;            // most zoomed-IN (can't reach one cell)
	[Export] public int MaxCellsRadius = 28;           // most zoomed-OUT (can't see whole maze)
	[Export] public float ZoomStep = 1.0f;

	// Parchment-toned functional palette.
	private static readonly Color FogColor        = new(0.12f, 0.10f, 0.07f);
	private static readonly Color ExploredFarColor= new(0.55f, 0.47f, 0.34f);
	private static readonly Color FloorColor      = new(0.82f, 0.73f, 0.55f);
	private static readonly Color WallColor       = new(0.28f, 0.22f, 0.16f);
	private static readonly Color BorderColor     = new(0.18f, 0.12f, 0.07f);
	private static readonly Color PlayerColor     = new(0.80f, 0.20f, 0.12f);
	private static readonly Color GateColor       = new(0.20f, 0.45f, 0.85f);

	private readonly MinimapState _state = new();
	private Player _player;

	private float _cellsRadius = 11.0f; // current zoom (cells from centre to edge)
	private bool _northUp = false;      // F-11: default = rotate with camera
	private Vector2I _playerCell = new(int.MinValue, int.MinValue);

	public override void _Ready()
	{
		ClipContents = true;
		MouseFilter = MouseFilterEnum.Ignore; // never steal clicks from the game
		_player = GetNodeOrNull<Player>("/root/Main/Player");
	}

	public override void _Input(InputEvent @event)
	{
		if (@event.IsActionPressed("minimap_toggle"))
		{
			_northUp = !_northUp;
			QueueRedraw();
			GetViewport().SetInputAsHandled();
			return;
		}

		// F-11: Ctrl + wheel zooms the mini-map (plain wheel still zooms the camera).
		if (@event is InputEventMouseButton mb && mb.Pressed && mb.CtrlPressed)
		{
			if (mb.ButtonIndex == MouseButton.WheelUp)
				_cellsRadius = Mathf.Max(MinCellsRadius, _cellsRadius - ZoomStep);
			else if (mb.ButtonIndex == MouseButton.WheelDown)
				_cellsRadius = Mathf.Min(MaxCellsRadius, _cellsRadius + ZoomStep);
			else
				return;
			QueueRedraw();
			GetViewport().SetInputAsHandled();
		}
	}

	// Cell-visit detection runs on the fixed physics tick: between two ticks the player
	// moves far less than one cell, so no entered cell is ever skipped (F-10).
	public override void _PhysicsProcess(double delta)
	{
		if (_player == null || MazeData.Instance == null)
			return;

		Vector2I cell = WorldToCell(_player.GlobalPosition);
		if (cell == _playerCell)
			return;

		_playerCell = cell;
		_state.Visit(cell);
		// Entrance/exit are remembered permanently once stepped on (F-10).
		if (cell == MazeData.EntranceCell) _state.RevealPermanently(MazeData.EntranceCell);
		if (cell == MazeData.ExitCell)     _state.RevealPermanently(MazeData.ExitCell);
	}

	public override void _Process(double delta)
	{
		if (_player == null)
			return;

		// Keep the widget square and sized relative to the screen (handles resizes).
		float side = Mathf.Round(GetViewport().GetVisibleRect().Size.X * ScreenWidthFraction);
		Position = new Vector2(Margin, Margin);
		Size = new Vector2(side, side);

		QueueRedraw(); // camera-relative rotation changes every frame
	}

	private static Vector2I WorldToCell(Vector3 world)
	{
		var m = MazeData.Instance;
		float cs = MazeData.CellWorldSize;
		return new Vector2I(
			Mathf.FloorToInt((world.X - m.WorldOffsetX) / cs),
			Mathf.FloorToInt((world.Z - m.WorldOffsetZ) / cs));
	}

	public override void _Draw()
	{
		if (_player == null || MazeData.Instance == null)
			return;

		Vector2 center = Size * 0.5f;
		float radius = Mathf.Min(Size.X, Size.Y) * 0.5f - 4.0f;
		float cellPix = radius / _cellsRadius;
		float radiusSq = radius * radius;

		// Fog background (everything not yet explored reads as dark parchment).
		DrawCircle(center, radius, FogColor);

		// Rotate the map so "forward" is up (camera heading, or world-north when toggled).
		Vector2 fwd = _northUp ? new Vector2(0, -1) : _player.PlanarCamForward;
		if (fwd.LengthSquared() < 0.0001f) fwd = new Vector2(0, -1);
		float phi = -Mathf.Pi / 2.0f - Mathf.Atan2(fwd.Y, fwd.X);
		DrawSetTransform(center, phi, Vector2.One);

		int reach = Mathf.CeilToInt(_cellsRadius) + 1;
		float pad = 0.6f; // tiny overlap hides seams between rotated cell quads
		for (int dx = -reach; dx <= reach; dx++)
		for (int dz = -reach; dz <= reach; dz++)
		{
			float px = dx * cellPix;
			float pz = dz * cellPix;
			if (px * px + pz * pz > radiusSq) continue; // circular clip (distance is rotation-invariant)

			var cell = new Vector2I(_playerCell.X + dx, _playerCell.Y + dz);
			if (!_state.IsRevealed(cell)) continue;     // fog of war

			Color c;
			if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dz)) <= NearRadius)
				c = MazeData.IsFloor(cell.X, cell.Y) ? FloorColor : WallColor; // detailed
			else
				c = ExploredFarColor;                                          // schematic

			DrawRect(new Rect2(px - cellPix / 2 - pad, pz - cellPix / 2 - pad,
							   cellPix + pad * 2, cellPix + pad * 2), c);
		}

		DrawGate(MazeData.EntranceCell, cellPix, radiusSq);
		DrawGate(MazeData.ExitCell, cellPix, radiusSq);
		DrawPlayerArrow(cellPix);

		// Frame ring on top, un-rotated.
		DrawSetTransform(Vector2.Zero, 0, Vector2.One);
		DrawArc(center, radius, 0, Mathf.Tau, 96, BorderColor, 3.0f, true);
	}

	// Entrance/exit arch marker, drawn only after the gate cell has been visited.
	private void DrawGate(Vector2I gate, float cellPix, float radiusSq)
	{
		if (!_state.IsPermanentlyRevealed(gate)) return; // only after the player has entered it
		float px = (gate.X - _playerCell.X) * cellPix;
		float pz = (gate.Y - _playerCell.Y) * cellPix;
		if (px * px + pz * pz > radiusSq) return;

		float r = Mathf.Max(cellPix * 0.42f, 3.0f);
		DrawArc(new Vector2(px, pz), r, Mathf.Pi, Mathf.Tau, 16, GateColor, Mathf.Max(cellPix * 0.18f, 1.5f), true);
		DrawRect(new Rect2(px - r, pz - 1, r * 2, 2), GateColor);
	}

	private void DrawPlayerArrow(float cellPix)
	{
		Vector2 face = _player.PlanarFacing;
		if (face.LengthSquared() < 0.0001f) face = new Vector2(0, -1);
		face = face.Normalized();
		Vector2 perp = new(-face.Y, face.X);

		float s = Mathf.Max(cellPix * 0.85f, 6.0f);
		Vector2 tip   = face * s;
		Vector2 left  = -face * s * 0.55f + perp * s * 0.6f;
		Vector2 right = -face * s * 0.55f - perp * s * 0.6f;
		DrawColoredPolygon(new[] { tip, left, right }, PlayerColor);
	}
}
