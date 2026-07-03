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
	private Item _item;
	private float _targetHeight;

	private float _t;
	private MeshInstance3D _star;

	public void Setup(Vector3 start, Vector3 land, float arcHeight, float duration,
		Item item, float targetHeight)
	{
		_start = start;
		_land = land;
		_arc = arcHeight;
		_duration = Mathf.Max(0.05f, duration);
		_item = item;
		_targetHeight = targetHeight;

		GlobalPosition = start;
		_star = ItemStar.Attach(this);
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

		_star.Scale = Vector3.One * ItemStar.Twinkle(_t); // мерцание
	}

	private void Land()
	{
		var world = new WorldItem();
		GetParent().AddChild(world);
		world.GlobalPosition = _land;
		world.Setup(_item, _targetHeight);
		QueueFree();
	}
}
