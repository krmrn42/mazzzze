using Godot;

// Анимация выброса: яркая «звёздочка» летит по параболе от игрока к точке приземления
// и превращается в предмет в мире (REQ-0015 / F-28 → REQ-0014).
//
// Свечение сцены (WorldEnvironment.glow) заставляет emissive-сферу «цвести», а лёгкая
// пульсация размера читается как мерцание звезды. По завершении полёта создаётся WorldItem.
public partial class DropProjectile : Node3D
{
	private Vector3 _start;
	private Vector3 _land;
	private float _arc;
	private float _duration;
	private string _modelPath;
	private float _targetHeight;

	private float _t;
	private MeshInstance3D _star;

	public void Setup(Vector3 start, Vector3 land, float arcHeight, float duration,
		string modelPath, float targetHeight)
	{
		_start = start;
		_land = land;
		_arc = arcHeight;
		_duration = Mathf.Max(0.05f, duration);
		_modelPath = modelPath;
		_targetHeight = targetHeight;

		GlobalPosition = start;

		var mat = new StandardMaterial3D
		{
			AlbedoColor = new Color(1.0f, 0.98f, 0.85f),
			EmissionEnabled = true,
			Emission = new Color(1.0f, 0.92f, 0.65f),
			EmissionEnergyMultiplier = 9.0f,
		};
		_star = new MeshInstance3D
		{
			Mesh = new SphereMesh { Radius = 0.06f, Height = 0.12f, RadialSegments = 12, Rings = 6 },
			MaterialOverride = mat,
			CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
		};
		AddChild(_star);

		AddChild(new OmniLight3D
		{
			LightColor = new Color(1.0f, 0.9f, 0.65f),
			LightEnergy = 2.5f,
			OmniRange = 3.0f,
		});
	}

	public override void _PhysicsProcess(double delta)
	{
		_t += (float)delta / _duration;

		if (_t >= 1.0f)
		{
			Land();
			return;
		}

		Vector3 p = _start.Lerp(_land, _t);
		p.Y += _arc * 4.0f * _t * (1.0f - _t); // парабола: пик в t=0.5
		GlobalPosition = p;

		// Мерцание: пульсация размера звёздочки.
		float twinkle = 1.0f + 0.35f * Mathf.Sin(_t * 40.0f);
		_star.Scale = Vector3.One * twinkle;
	}

	private void Land()
	{
		var world = new WorldItem();
		GetParent().AddChild(world);
		world.GlobalPosition = _land;
		world.Setup(_modelPath, _targetHeight);
		QueueFree();
	}
}
