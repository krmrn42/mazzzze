using Godot;
using System;

public partial class Player : CharacterBody3D
{
	[Export] public float Speed = 5.0f;
	[Export] public float MouseSensitivity = 0.002f;
	[Export] public float ZoomStep = 0.5f;
	[Export] public float MinZoom = 1.5f;
	[Export] public float MaxZoom = 4.0f;
	[Export] public float Gravity = 15.0f;

	private Camera3D _camera;
	private Node3D _cameraYaw;
	private Node3D _cameraPitch;
	private ChunkManager _chunkManager;
	private Node3D _modelPivot;
	private float _zoomLevel;

	public override void _Ready()
	{
		Input.MouseMode = Input.MouseModeEnum.Captured;

		_camera = GetNode<Camera3D>("CameraYaw/CameraPitch/Camera3D");
		_cameraYaw = GetNode<Node3D>("CameraYaw");
		_cameraPitch = GetNode<Node3D>("CameraYaw/CameraPitch");
		_modelPivot = GetNode<Node3D>("ModelPivot");
		_chunkManager = GetNode<ChunkManager>("/root/Main/ChunkManager");

		_zoomLevel = _camera.Position.Z;

		if (MazeData.Instance != null)
		{
			var m = MazeData.Instance;
			var start = m.PlayerStartCell;
			float cs = MazeData.CellWorldSize;
			Position = new Vector3(
				start.X * cs + m.WorldOffsetX + cs / 2,
				1,
				start.Y * cs + m.WorldOffsetZ + cs / 2
			);
			GD.Print($"[Player] Start cell=({start.X},{start.Y}) world=({Position.X:F0}, {Position.Z:F0})");
		}
	}

	public override void _Input(InputEvent @event)
	{
		if (@event is InputEventMouseMotion mouseMotion && Input.MouseMode == Input.MouseModeEnum.Captured)
		{
			_cameraYaw.RotateY(-mouseMotion.Relative.X * MouseSensitivity);
			_cameraPitch.RotateX(-mouseMotion.Relative.Y * MouseSensitivity);
			// Жёсткий зажим: камера не поднимается выше уровня стен (Y < 1.0)
			_cameraPitch.Rotation = new Vector3(
				Mathf.Clamp(_cameraPitch.Rotation.X, Mathf.DegToRad(-15), Mathf.DegToRad(25)),
				0, 0
			);
		}

		if (@event is InputEventMouseButton mb)
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

		if (moveDir != Vector3.Zero)
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

		Velocity = vel;
		MoveAndSlide();

		_chunkManager?.UpdateChunks(new Vector2(GlobalPosition.X, GlobalPosition.Z));
	}
}
