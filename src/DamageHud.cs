using Godot;

// Обратная связь по урону игроку (REQ-0019 / F-42): короткая красная вспышка-виньетка при
// контакте монстра с игроком. Полноценной системы здоровья пока нет — это лишь индикатор
// того, что попадание случилось (монстр «сообщает» о нём). Создаётся `MonsterSpawner`.
public partial class DamageHud : Control
{
	[Export] public float FadeDuration = 0.5f;

	private float _t; // 1 → 0

	public override void _Ready() => MouseFilter = MouseFilterEnum.Ignore;

	public void Flash() => _t = 1.0f;

	public override void _Process(double delta)
	{
		Position = Vector2.Zero;
		Size = GetViewport().GetVisibleRect().Size;
		if (_t > 0.0f)
		{
			_t = Mathf.Max(0.0f, _t - (float)delta / FadeDuration);
			QueueRedraw();
		}
	}

	public override void _Draw()
	{
		if (_t <= 0.0f)
			return;
		// Лёгкая заливка + усиление к краям экрана (виньетка «получил урон»).
		DrawRect(new Rect2(Vector2.Zero, Size), new Color(0.75f, 0.05f, 0.05f, _t * 0.22f));
		float band = Mathf.Min(Size.X, Size.Y) * 0.14f;
		var edge = new Color(0.85f, 0.04f, 0.04f, _t * 0.5f);
		DrawRect(new Rect2(0, 0, Size.X, band), edge);
		DrawRect(new Rect2(0, Size.Y - band, Size.X, band), edge);
		DrawRect(new Rect2(0, 0, band, Size.Y), edge);
		DrawRect(new Rect2(Size.X - band, 0, band, Size.Y), edge);
	}
}
