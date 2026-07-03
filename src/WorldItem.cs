using Godot;
using System.Collections.Generic;

// Предмет, лежащий в лабиринте (состояние InWorld) — REQ-0014 (US-14 / F-24, F-25).
//
// Отображает glb-модель типа предмета, уменьшенную до заданной высоты, стоящей на
// полу. Живёт под Main (не под чанком) → не выгружается при стриминге. Хранит свой
// Item, чтобы при подборе (REQ-0016) восстановить его в инвентаре, и ведёт статический
// реестр для сканирования подбора. Коллизии нет (задел).
public partial class WorldItem : Node3D
{
	private const float SpawnPopDuration = 0.2f; // F-25: длительность появления

	// Реестр всех предметов в мире — по нему InventoryHud сканирует подбор (REQ-0016 / F-29).
	public static readonly List<WorldItem> All = new();
	// Радиус «взвода»: предмет становится доступен для подбора только после того, как игрок
	// хотя бы раз оказался дальше него (защита от мгновенного повторного подбора). Задаётся
	// InventoryHud от PickupRange.
	public static float ArmingRadius = 1.5f;

	public Item Item { get; private set; }
	public bool Armed { get; private set; }

	private float _popT; // 0 → 1
	private Node3D _player;

	// item — что за предмет; targetHeight — желаемая высота модели в метрах.
	public void Setup(Item item, float targetHeight)
	{
		Item = item;
		var model = GD.Load<PackedScene>(item.ModelPath).Instantiate<Node3D>();
		AddChild(model);

		Aabb bounds = ComputeSceneAabb(model);
		float scale = bounds.Size.Y > 0.0001f ? targetHeight / bounds.Size.Y : 1.0f;
		model.Scale = Vector3.One * scale;
		// Низ модели ставим на y=0 этого узла (узел размещают на полу).
		model.Position = new Vector3(0, -bounds.Position.Y * scale, 0);
	}

	// Забрать предмет из мира (при подборе): убрать из реестра сразу, чтобы скан не взял дважды.
	public void Take()
	{
		All.Remove(this);
		QueueFree();
	}

	public override void _EnterTree() => All.Add(this);
	public override void _ExitTree() => All.Remove(this);

	public override void _Ready() => _player = GetNodeOrNull<Node3D>("/root/Main/Player");

	public override void _PhysicsProcess(double delta)
	{
		// «Взвод»: как только игрок оказался вне радиуса (по горизонтали) — предмет
		// можно поднимать (F-29). Планарная дистанция: вертикаль не влияет.
		if (!Armed && _player != null)
		{
			Vector3 p = _player.GlobalPosition;
			Vector3 s = GlobalPosition;
			if (new Vector2(p.X - s.X, p.Z - s.Z).Length() > ArmingRadius)
				Armed = true;
		}

		// Анимация появления (F-25): модель быстро «вырастает» до целевого масштаба.
		if (_popT < 1.0f)
		{
			_popT = Mathf.Min(1.0f, _popT + (float)delta / SpawnPopDuration);
			float s = Mathf.Lerp(0.2f, 1.0f, Mathf.Ease(_popT, -1.8f));
			Scale = Vector3.One * s;
		}
	}

	// Объединённый мировой AABB всех VisualInstance3D под root (root уже в дереве).
	public static Aabb ComputeSceneAabb(Node3D root)
	{
		Aabb bounds = new();
		bool has = false;
		foreach (Node n in root.FindChildren("*", "VisualInstance3D", true, false))
		{
			var vi = (VisualInstance3D)n;
			Aabb local = vi.GetAabb();
			Transform3D gt = vi.GlobalTransform;
			for (int i = 0; i < 8; i++)
			{
				Vector3 corner = gt * (local.Position + local.Size * new Vector3(
					i & 1, (i >> 1) & 1, (i >> 2) & 1));
				bounds = has ? bounds.Expand(corner) : new Aabb(corner, Vector3.Zero);
				has = true;
			}
		}
		return has ? bounds : new Aabb(Vector3.Zero, Vector3.One);
	}
}
