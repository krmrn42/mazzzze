using Godot;

// Анимация подбора (REQ-0016 / F-30): та же «звёздочка», что при выбросе, но летит по
// параболе от предмета к игроку (зеркально выбросу «игрок → земля»), сжимаясь к концу.
// По прибытии зовёт InventoryHud.OnPickupArrived — предмет уже лежит в инвентаре.
public partial class PickupProjectile : Node3D
{
	private Vector3 _start;
	private Node3D _player;
	private InventoryHud _hud;
	private int _slot;
	private float _arc;
	private float _duration;

	private float _t;
	private MeshInstance3D _star;

	public void Setup(Vector3 start, Node3D player, InventoryHud hud, int slot,
		float arcHeight, float duration)
	{
		_start = start;
		_player = player;
		_hud = hud;
		_slot = slot;
		_arc = arcHeight;
		_duration = Mathf.Max(0.05f, duration);

		GlobalPosition = start;
		_star = ItemStar.Attach(this);
	}

	public override void _PhysicsProcess(double delta)
	{
		_t += (float)delta / _duration;

		if (_t >= 1.0f)
		{
			_hud?.OnPickupArrived(_slot);
			QueueFree();
			return;
		}

		// Цель — грудь игрока, пересчитывается каждый кадр (игрок может двигаться).
		Vector3 target = _player != null ? _player.GlobalPosition + Vector3.Up * 1.0f : _start;
		Vector3 p = _start.Lerp(target, _t);
		p.Y += _arc * 4.0f * _t * (1.0f - _t); // парабола: пик в t=0.5
		GlobalPosition = p;

		// Сжатие «втягивания» в игрока + мерцание.
		float shrink = Mathf.Lerp(1.0f, 0.15f, _t);
		_star.Scale = Vector3.One * shrink * ItemStar.Twinkle(_t);
	}
}
