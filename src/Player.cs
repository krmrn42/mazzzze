using Godot;
using System;

public partial class Player : CharacterBody3D
{
	[Export] public float Speed = 5.0f;
	[Export] public float MouseSensitivity = 0.002f;
	[Export] public float ZoomStep = 1.0f;
	[Export] public float MinZoom = 6.0f;
	[Export] public float MaxZoom = 14.0f;
	[Export] public float Gravity = 15.0f;

	// Имена анимаций из AnimationLibrary_Godot_Standard.glb и время кроссфейда между ними.
	// Клип движения — "Jog_Fwd": при Speed=5 (бег по лабиринту) бег выглядит естественнее
	// шага; ходьбу можно вернуть, поставив WalkAnim="Walk" и WalkAnimRefSpeed≈1.4.
	[Export] public string IdleAnim = "Idle";
	[Export] public string WalkAnim = "Jog_Fwd";
	[Export] public float AnimBlend = 0.2f;
	// Скорость игрока (м/с), при которой клип движения проигрывается в «родном» темпе.
	// Клипы in-place (без root motion), поэтому темп масштабируем под фактическую скорость
	// перемещения: speed_scale = горизонтальная_скорость / WalkAnimRefSpeed — так ноги
	// не «скользят» по полу. Подбирается на глаз; для "Jog_Fwd" ≈ 4.0.
	[Export] public float WalkAnimRefSpeed = 4.0f;

	// Камера высоко над игроком и чуть сзади, направлена вниз под углом.
	// Угол крутой (ближе к виду сверху): так камера остаётся в открытом «колодце»
	// над клеткой игрока и не утыкается в боковые стены узкого коридора.
	[Export] public float DefaultPitchDeg = -60.0f;  // наклон вниз по умолчанию
	[Export] public float MinPitchDeg = -85.0f;      // почти строго вниз
	[Export] public float MaxPitchDeg = -25.0f;      // более пологий угол: видно высоту стен и небо
	[Export] public float CameraMargin = 0.4f;       // отступ камеры от стены при столкновении

	private Camera3D _camera;
	private Node3D _cameraYaw;
	private Node3D _cameraPitch;
	private ChunkManager _chunkManager;
	private Node3D _modelPivot;
	private AnimationPlayer _anim;
	private float _zoomLevel;

	// Planar (XZ) world directions, for the mini-map. Facing = where the player model
	// looks (last movement direction); CamForward = the camera's horizontal heading.
	public Vector2 PlanarFacing
	{
		get { var f = -_modelPivot.GlobalBasis.Z; return new Vector2(f.X, f.Z); }
	}
	public Vector2 PlanarCamForward
	{
		get { var f = -_cameraYaw.GlobalBasis.Z; return new Vector2(f.X, f.Z); }
	}

	public override void _Ready()
	{
		Input.MouseMode = Input.MouseModeEnum.Captured;
		// Без этого на Linux/X11 при зажатых клавишах движения авто-повтор клавиатуры
		// «затапливает» очередь ввода, и события движения мыши копятся/теряются — камера
		// перестаёт вращаться мышью во время ходьбы. Отключаем аккумуляцию ввода, чтобы
		// каждое InputEventMouseMotion доставлялось сразу, и обзор работал на ходу.
		Input.UseAccumulatedInput = false;

		_camera = GetNode<Camera3D>("CameraYaw/CameraPitch/Camera3D");
		_cameraYaw = GetNode<Node3D>("CameraYaw");
		_cameraPitch = GetNode<Node3D>("CameraYaw/CameraPitch");
		_modelPivot = GetNode<Node3D>("ModelPivot");
		_chunkManager = GetNode<ChunkManager>("/root/Main/ChunkManager");

		// Плеер анимаций живёт внутри инстанса glb-персонажа.
		_anim = GetNodeOrNull<AnimationPlayer>("ModelPivot/Character/AnimationPlayer");
		PlayAnim(IdleAnim);

		_zoomLevel = _camera.Position.Z;

		// Камера по умолчанию смотрит вниз на игрока сверху-сзади
		_cameraPitch.Rotation = new Vector3(Mathf.DegToRad(DefaultPitchDeg), 0, 0);

		if (MazeData.Instance != null)
		{
			var m = MazeData.Instance;
			var start = m.PlayerStartCell;
			float cs = MazeData.CellWorldSize;
			// X/Z: центр клетки-коридора. GridMap центрирует ячейки по X/Z (cell_center_x/z = true),
			// поэтому формула с +cs/2 совпадает с центром плитки пола.
			// Y: пол сверху на Y≈0.1, низ коллизии-сферы на Position.Y+0.05 → ставим чуть выше пола,
			// чтобы игрок мягко встал НА пол (не над и не под ним).
			Position = new Vector3(
				start.X * cs + m.WorldOffsetX + cs / 2,
				0.3f,
				start.Y * cs + m.WorldOffsetZ + cs / 2
			);
			GD.Print($"[Player] Start cell=({start.X},{start.Y}) world=({Position.X:F1}, {Position.Y:F1}, {Position.Z:F1})");
		}
	}

	public override void _Input(InputEvent @event)
	{
		// Ctrl+Q — выход из игры (в редакторе Godot вернёт в студию).
		if (@event is InputEventKey key && key.Pressed && !key.Echo
			&& key.Keycode == Key.Q && key.CtrlPressed)
		{
			GetTree().Quit();
			return;
		}

		if (@event is InputEventMouseMotion mouseMotion && Input.MouseMode == Input.MouseModeEnum.Captured)
		{
			_cameraYaw.RotateY(-mouseMotion.Relative.X * MouseSensitivity);
			_cameraPitch.RotateX(-mouseMotion.Relative.Y * MouseSensitivity);
			// Камера всегда смотрит вниз под углом; верхний предел (MaxPitchDeg) держит её
			// ниже верхнего края стен — заглянуть поверх лабиринта нельзя.
			_cameraPitch.Rotation = new Vector3(
				Mathf.Clamp(_cameraPitch.Rotation.X, Mathf.DegToRad(MinPitchDeg), Mathf.DegToRad(MaxPitchDeg)),
				0, 0
			);
		}

		// Ctrl+wheel is reserved for mini-map zoom (handled by Minimap); plain wheel zooms the camera.
		if (@event is InputEventMouseButton mb && !mb.CtrlPressed)
		{
			if (mb.ButtonIndex == MouseButton.WheelUp)
			{
				_zoomLevel = Mathf.Max(MinZoom, _zoomLevel - ZoomStep);
				_camera.Position = new Vector3(_camera.Position.X, _camera.Position.Y, _zoomLevel);
			}
			if (mb.ButtonIndex == MouseButton.WheelDown)
			{
				_zoomLevel = Mathf.Min(MaxZoom, _zoomLevel + ZoomStep);
				_camera.Position = new Vector3(_camera.Position.X, _camera.Position.Y, _zoomLevel);
			}
		}
	}

	public override void _PhysicsProcess(double delta)
	{
		float dt = (float)delta;

		Vector3 vel = Velocity;
		if (!IsOnFloor())
			vel.Y -= Gravity * dt;

		Vector2 inputDir = Input.GetVector("move_left", "move_right", "move_forward", "move_back");
		Vector3 camForward = -_cameraYaw.GlobalBasis.Z;
		Vector3 camRight = _cameraYaw.GlobalBasis.X;
		Vector3 moveDir = (camForward * -inputDir.Y + camRight * inputDir.X);
		moveDir.Y = 0;
		moveDir = moveDir.Normalized();

		bool moving = moveDir != Vector3.Zero;
		if (moving)
		{
			vel.X = moveDir.X * Speed;
			vel.Z = moveDir.Z * Speed;
			_modelPivot.Basis = Basis.LookingAt(moveDir, Vector3.Up);
		}
		else
		{
			vel.X = 0;
			vel.Z = 0;
		}

		// Анимация состояния: шаг при движении, ожидание на месте.
		PlayAnim(moving ? WalkAnim : IdleAnim);
		// Темп анимации движения подгоняем под фактическую скорость перемещения по полу,
		// чтобы ноги/руки не отставали от тела (клипы in-place). На месте — обычный темп.
		if (_anim != null)
		{
			float planarSpeed = new Vector2(vel.X, vel.Z).Length();
			_anim.SpeedScale = moving && WalkAnimRefSpeed > 0.0f
				? Mathf.Clamp(planarSpeed / WalkAnimRefSpeed, 0.1f, 4.0f)
				: 1.0f;
		}

		Velocity = vel;
		MoveAndSlide();

		_chunkManager?.UpdateChunks(new Vector2(GlobalPosition.X, GlobalPosition.Z));

		UpdateCameraCollision(dt);
	}

	// Запускает анимацию с кроссфейдом, если она ещё не играет (без перезапуска каждый кадр).
	private void PlayAnim(string name)
	{
		if (_anim == null || string.IsNullOrEmpty(name) || _anim.CurrentAnimation == name)
			return;
		_anim.Play(name, AnimBlend);
	}

	// «Пружинная рука»: камера не должна проникать в стены узкого коридора.
	// Пускаем луч от точки опоры (над игроком) к желаемой позиции камеры; если он
	// упирается в стену — придвигаем камеру к точке удара, иначе плавно отдаляем.
	private void UpdateCameraCollision(float dt)
	{
		Vector3 pivot = _cameraPitch.GlobalPosition;
		Vector3 dir = _cameraPitch.GlobalBasis.Z.Normalized();   // +Z в кадре наклона — куда смещена камера
		Vector3 desired = pivot + dir * _zoomLevel;

		var query = PhysicsRayQueryParameters3D.Create(pivot, desired);
		query.CollisionMask = 1;
		query.Exclude = new Godot.Collections.Array<Rid> { GetRid() };
		var hit = GetWorld3D().DirectSpaceState.IntersectRay(query);

		float target = _zoomLevel;
		if (hit.Count > 0)
			target = Mathf.Clamp(pivot.DistanceTo((Vector3)hit["position"]) - CameraMargin, 0.6f, _zoomLevel);

		float current = _camera.Position.Z;
		// Придвигаемся мгновенно (чтобы не показать стену), отдаляемся плавно.
		float z = target < current ? target : Mathf.MoveToward(current, target, 12.0f * dt);
		_camera.Position = new Vector3(_camera.Position.X, _camera.Position.Y, z);
	}
}
