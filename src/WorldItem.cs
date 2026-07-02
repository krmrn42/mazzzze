using Godot;

// Предмет, лежащий в лабиринте (состояние InWorld) — REQ-0014 (US-14 / F-24, F-25).
//
// Отображает glb-модель типа предмета, уменьшенную до заданной высоты, стоящей на
// полу в точке размещения. Живёт под Main (не под чанком), поэтому не выгружается
// при стриминге. Анимация появления (F-25): модель быстро «вырастает» до целевого
// масштаба. Коллизия/подбор (InWorld → InInventory) — вне REQ-0014 (задел).
public partial class WorldItem : Node3D
{
	private const float SpawnPopDuration = 0.2f; // F-25: длительность появления

	private float _popT; // 0 → 1

	// modelPath — .glb предмета; targetHeight — желаемая высота модели в метрах.
	public void Setup(string modelPath, float targetHeight)
	{
		var model = GD.Load<PackedScene>(modelPath).Instantiate<Node3D>();
		AddChild(model);

		Aabb bounds = ComputeSceneAabb(model);
		float scale = bounds.Size.Y > 0.0001f ? targetHeight / bounds.Size.Y : 1.0f;
		model.Scale = Vector3.One * scale;
		// Низ модели ставим на y=0 этого узла (узел размещают на полу).
		model.Position = new Vector3(0, -bounds.Position.Y * scale, 0);
	}

	public override void _PhysicsProcess(double delta)
	{
		if (_popT >= 1.0f)
			return;

		_popT = Mathf.Min(1.0f, _popT + (float)delta / SpawnPopDuration);
		// Небольшой «перелёт» за 1.0 для живого появления.
		float s = Mathf.Lerp(0.2f, 1.0f, Mathf.Ease(_popT, -1.8f));
		Scale = Vector3.One * s;
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
