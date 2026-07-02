using Godot;

// Инвентарь (рюкзак) — HUD-виджет в правом нижнем углу (REQ-0011, US-11 / F-12..F-14).
//
// Два состояния отрисовки:
//   - compact (закрыт): иконка мешка + счётчик «N/12»
//   - expanded (открыт): сетка 3×4, пустые/заполненные ячейки, выбор ряд→ячейка
//
// Управление (F-14, F-27):
//   - двойное нажатие I  → открыть/закрыть
//   - при открытом: цифра 1–3 выбирает ряд (подсветка), затем 1–4 — ячейку (применение)
//   - Shift + цифра-колонки → выбросить ячейку (drop, REQ-0015)
//   - G → выбросить предмет под текущим курсором (drop, REQ-0015)
//
// Игра при открытии НЕ ставится на паузу, мышь остаётся захваченной, управление
// цифровое (ввод по клику в углу — отложен, т.к. курсор захвачен во время игры).
//
// Применение делегируется предмету (Item.Use, паттерн A / F-18). Выброс (REQ-0015)
// создаёт летящую звёздочку (DropProjectile) → предмет в мире (WorldItem, REQ-0014).
// Полная сущность предмета (REQ-0012: активация в руку F-18/B, бронь слота F-19a,
// подбор) и поведение конкретных предметов (REQ-0013) — вне этой задачи.
public partial class InventoryHud : Control
{
	[Export] public float DoublePressWindow = 0.4f; // окно двойного нажатия I, сек
	[Export] public float Margin = 16.0f;
	[Export] public float CellSize = 72.0f;
	[Export] public float CellGap = 8.0f;
	[Export] public float Padding = 12.0f;
	[Export] public float TitleBarHeight = 28.0f;
	[Export] public float FlashDuration = 0.35f;

	// Конфигурация выброса (REQ-0015 / REQ-0014).
	[Export] public float DropDistanceBodies = 3.0f;    // дальность в «корпусах» игрока
	[Export] public float PlayerBodyDiameter = 0.6f;    // эталон «корпуса»
	[Export] public float WorldItemSizeFraction = 0.125f; // 1/8 роста игрока
	[Export] public float PlayerHeight = 1.8f;          // эталон роста
	[Export] public float DropFlightDuration = 0.6f;    // время полёта звёздочки, сек
	[Export] public float DropArcHeight = 1.2f;         // высота параболы, м

	// Палитра в тон миникарте (тёплый пергамент).
	private static readonly Color PanelBg      = new(0.10f, 0.08f, 0.06f, 0.85f);
	private static readonly Color BorderColor  = new(0.18f, 0.12f, 0.07f);
	private static readonly Color EmptyOutline = new(0.42f, 0.36f, 0.28f, 0.7f);
	private static readonly Color FilledBg     = new(0.30f, 0.25f, 0.18f);
	private static readonly Color RowSelect    = new(0.85f, 0.68f, 0.28f);
	private static readonly Color CursorColor  = new(0.95f, 0.85f, 0.55f);
	private static readonly Color TextColor    = new(0.90f, 0.83f, 0.68f);
	private static readonly Color HintColor    = new(0.60f, 0.53f, 0.42f);
	private static readonly Color ConsumableDot= new(0.35f, 0.75f, 0.35f);
	private static readonly Color KeyDot       = new(0.90f, 0.72f, 0.25f);
	private static readonly Color FlashColor   = new(1.0f, 0.97f, 0.85f);

	private readonly Inventory _inv = new();

	private bool _open;
	private int _selectedRow = -1;    // -1 = ряд ещё не выбран
	private int _cursorSlot = -1;     // последняя выбранная ячейка (курсор для drop по G)
	private double _lastIPressSec = -1.0;

	private int _flashSlot = -1;      // ячейка, которую сейчас «вспыхиваем»
	private float _flashT;            // 1 → 0

	private SubViewport _iconViewport;
	private Player _player;

	public override void _Ready()
	{
		MouseFilter = MouseFilterEnum.Ignore; // не перехватываем клики у игры
		_player = GetNodeOrNull<Player>("/root/Main/Player");

		// Засев: один предмет — «Старинный фотоаппарат» (используем только glb-модель
		// из REQ-0013; сама механика фотоаппарата не реализуется).
		var camera = new Item("vintage_camera", "Старинный фотоаппарат", ItemCategory.Key,
			"res://art/old_kodak_camera.glb");
		camera.Icon = BuildIcon(camera.ModelPath);
		_inv.PutAt(0, camera);
		GD.Print($"[Inventory] Seeded {_inv.Count}/{Inventory.Capacity} items");
	}

	// Рендерит glb-модель в текстуру для иконки слота.
	private Texture2D BuildIcon(string modelPath)
	{
		_iconViewport = new SubViewport
		{
			Size = new Vector2I(192, 192),
			TransparentBg = true,
			OwnWorld3D = true,
			RenderTargetUpdateMode = SubViewport.UpdateMode.Always,
		};
		AddChild(_iconViewport);

		var model = GD.Load<PackedScene>(modelPath).Instantiate<Node3D>();
		_iconViewport.AddChild(model);

		var key = new DirectionalLight3D { LightEnergy = 1.6f };
		key.RotationDegrees = new Vector3(-45, -35, 0);
		_iconViewport.AddChild(key);
		var fill = new OmniLight3D { LightEnergy = 1.2f, OmniRange = 100 };
		fill.Position = new Vector3(-2, 1, 2);
		_iconViewport.AddChild(fill);

		var cam = new Camera3D();
		_iconViewport.AddChild(cam);
		FrameCamera(cam, model);

		return _iconViewport.GetTexture();
	}

	// Ставит камеру на 3/4-вид так, чтобы модель целиком поместилась в кадр
	// (модель произвольного масштаба/ориентации — считаем её общий AABB).
	private static void FrameCamera(Camera3D cam, Node3D model)
	{
		Aabb bounds = WorldItem.ComputeSceneAabb(model);
		Vector3 center = bounds.Position + bounds.Size * 0.5f;
		float radius = Mathf.Max(bounds.Size.Length() * 0.5f, 0.01f);
		Vector3 dir = new Vector3(0.7f, 0.55f, 1.0f).Normalized();
		cam.Position = center + dir * radius * 1.8f;
		cam.LookAt(center, Vector3.Up);
	}

	public override void _Input(InputEvent @event)
	{
		// Двойное нажатие I — открыть/закрыть (F-14).
		if (@event.IsActionPressed("inventory_toggle"))
		{
			double now = Time.GetTicksMsec() / 1000.0;
			if (_lastIPressSec >= 0 && now - _lastIPressSec <= DoublePressWindow)
			{
				Toggle();
				_lastIPressSec = -1.0; // сброс, чтобы тройное нажатие не переключало снова
			}
			else
			{
				_lastIPressSec = now;
			}
			GetViewport().SetInputAsHandled();
			return;
		}

		if (!_open || @event is not InputEventKey key || !key.Pressed || key.Echo)
			return;

		// G — выбросить предмет под текущим курсором (REQ-0015 / F-27).
		if (key.PhysicalKeycode == Key.G)
		{
			if (_cursorSlot >= 0)
				DropSlot(_cursorSlot);
			GetViewport().SetInputAsHandled();
			return;
		}

		// Физический код устойчив к раскладке и к Shift (иначе верхний ряд даёт !@#$).
		int digit = DigitOf(key.PhysicalKeycode);
		if (digit < 0)
			return;

		HandleDigit(digit, key.ShiftPressed);
		GetViewport().SetInputAsHandled();
	}

	// Возвращает 1–4 для цифровых клавиш (верхний ряд и цифровой блок), иначе -1.
	private static int DigitOf(Key kc)
	{
		switch (kc)
		{
			case Key.Key1: case Key.Kp1: return 1;
			case Key.Key2: case Key.Kp2: return 2;
			case Key.Key3: case Key.Kp3: return 3;
			case Key.Key4: case Key.Kp4: return 4;
			default: return -1;
		}
	}

	// Первое нажатие (1–3) выбирает ряд; второе (1–4) — колонку. Без Shift — применить,
	// с Shift — выбросить (REQ-0015 / F-27). Ячейка запоминается как курсор для drop по G.
	private void HandleDigit(int digit, bool shift)
	{
		if (_selectedRow < 0)
		{
			if (digit <= Inventory.Rows) // ряды только 1–3
			{
				_selectedRow = digit - 1;
				QueueRedraw();
			}
			return;
		}

		int col = digit - 1; // 0–3
		int slot = _selectedRow * Inventory.Cols + col;
		_cursorSlot = slot; // курсор остаётся на выбранной ячейке

		if (shift)
			DropSlot(slot);
		else
			ApplySlot(slot);

		_selectedRow = -1; // возвращаемся к выбору ряда
		QueueRedraw();
	}

	private void ApplySlot(int slot)
	{
		Item item = _inv.Get(slot);
		if (item == null) // пустая ячейка — ничего не происходит (F-14)
			return;

		_flashSlot = slot; // анимированная подсветка применения (F-13)
		_flashT = 1.0f;

		if (item.Use()) // расходник — уничтожается, слот освобождается
			_inv.RemoveAt(slot);
	}

	// Выброс (REQ-0015 / F-26): убрать из слота и запустить летящую звёздочку в мир.
	private void DropSlot(int slot)
	{
		Item item = _inv.RemoveAt(slot);
		if (item == null) // пустая ячейка — ничего не происходит
			return;

		SpawnDrop(item);
	}

	private void SpawnDrop(Item item)
	{
		if (_player == null)
			return;

		// Направление — куда смотрит игрок; при вырождении берём направление камеры.
		Vector2 facing2 = _player.PlanarFacing;
		if (facing2.LengthSquared() < 0.0001f) facing2 = _player.PlanarCamForward;
		if (facing2.LengthSquared() < 0.0001f) facing2 = new Vector2(0, -1);
		facing2 = facing2.Normalized();
		Vector3 dir = new Vector3(facing2.X, 0, facing2.Y);

		float distance = DropDistanceBodies * PlayerBodyDiameter;

		// Кламп к стене: луч на уровне корпуса, чтобы не улететь за стену.
		Vector3 origin = _player.GlobalPosition + Vector3.Up * 0.5f;
		var query = PhysicsRayQueryParameters3D.Create(origin, origin + dir * distance);
		query.CollisionMask = 1;
		query.Exclude = new Godot.Collections.Array<Rid> { _player.GetRid() };
		var hit = _player.GetWorld3D().DirectSpaceState.IntersectRay(query);
		if (hit.Count > 0)
			distance = Mathf.Max(0.3f, origin.DistanceTo((Vector3)hit["position"]) - 0.4f);

		Vector3 start = _player.GlobalPosition + Vector3.Up * 1.0f;
		Vector3 land = new Vector3(
			_player.GlobalPosition.X + dir.X * distance,
			_player.GlobalPosition.Y - 0.2f, // на уровне пола
			_player.GlobalPosition.Z + dir.Z * distance);

		float targetHeight = WorldItemSizeFraction * PlayerHeight;

		var projectile = new DropProjectile();
		_player.GetParent().AddChild(projectile);
		projectile.Setup(start, land, DropArcHeight, DropFlightDuration, item.ModelPath, targetHeight);
		GD.Print($"[Inventory] Drop '{item.TypeId}' → world at ({land.X:F1}, {land.Z:F1})");
	}

	private void Toggle()
	{
		_open = !_open;
		_selectedRow = -1;
		QueueRedraw();
	}

	public override void _Process(double delta)
	{
		if (_flashT > 0.0f)
		{
			_flashT = Mathf.Max(0.0f, _flashT - (float)delta / FlashDuration);
			if (_flashT <= 0.0f)
				_flashSlot = -1;
			QueueRedraw();
		}

		// Держим виджет в правом нижнем углу и подгоняем размер под состояние.
		Vector2 content = _open ? ExpandedSize() : CompactSize();
		Vector2 screen = GetViewport().GetVisibleRect().Size;
		Size = content;
		Position = new Vector2(screen.X - content.X - Margin, screen.Y - content.Y - Margin);
	}

	private Vector2 CompactSize() => new(150, 54);

	private Vector2 ExpandedSize()
	{
		float w = Padding * 2 + Inventory.Cols * CellSize + (Inventory.Cols - 1) * CellGap;
		float h = TitleBarHeight + Padding * 2 + Inventory.Rows * CellSize + (Inventory.Rows - 1) * CellGap;
		return new Vector2(w, h);
	}

	public override void _Draw()
	{
		if (_open)
			DrawExpanded();
		else
			DrawCompact();
	}

	private void DrawCompact()
	{
		var rect = new Rect2(Vector2.Zero, Size);
		DrawRect(rect, PanelBg);
		DrawRect(rect, BorderColor, false, 2.0f);

		// Простой значок мешка: округлое тело + горловина.
		var bag = new Rect2(12, 16, 26, 28);
		DrawRect(new Rect2(bag.Position + new Vector2(4, -6), new Vector2(18, 10)), FilledBg);
		DrawRect(bag, FilledBg);
		DrawRect(bag, BorderColor, false, 2.0f);

		Font font = GetThemeDefaultFont();
		int fs = 20;
		string text = $"{_inv.Count}/{Inventory.Capacity}";
		DrawString(font, new Vector2(52, 36), text, HorizontalAlignment.Left, -1, fs, TextColor);
	}

	private void DrawExpanded()
	{
		var rect = new Rect2(Vector2.Zero, Size);
		DrawRect(rect, PanelBg);
		DrawRect(rect, BorderColor, false, 2.0f);

		Font font = GetThemeDefaultFont();
		DrawString(font, new Vector2(Padding, 20), $"Рюкзак  {_inv.Count}/{Inventory.Capacity}",
			HorizontalAlignment.Left, -1, 16, TextColor);
		DrawString(font, new Vector2(Padding, 20), "Shift/G — выбросить",
			HorizontalAlignment.Right, Size.X - Padding * 2, 12, HintColor);

		// Подсветка выбранного ряда — рамка вокруг всей полосы ряда.
		if (_selectedRow >= 0)
		{
			float y = CellTop(_selectedRow);
			float bandW = Inventory.Cols * CellSize + (Inventory.Cols - 1) * CellGap;
			DrawRect(new Rect2(Padding - 4, y - 4, bandW + 8, CellSize + 8), RowSelect, false, 3.0f);
		}

		for (int row = 0; row < Inventory.Rows; row++)
		for (int col = 0; col < Inventory.Cols; col++)
			DrawSlot(row, col, font);
	}

	private float CellTop(int row) =>
		TitleBarHeight + Padding + row * (CellSize + CellGap);

	private float CellLeft(int col) =>
		Padding + col * (CellSize + CellGap);

	private void DrawSlot(int row, int col, Font font)
	{
		int slot = row * Inventory.Cols + col;
		var cell = new Rect2(CellLeft(col), CellTop(row), CellSize, CellSize);
		Item item = _inv.Get(slot);

		if (item == null)
		{
			// Пустая ячейка: приглушённый контур без содержимого (F-13).
			DrawRect(cell, EmptyOutline, false, 2.0f);
		}
		else
		{
			DrawRect(cell, FilledBg);
			DrawRect(cell, BorderColor, false, 2.0f);

			if (item.Icon != null)
			{
				var pad = new Vector2(6, 6);
				DrawTextureRect(item.Icon, new Rect2(cell.Position + pad, cell.Size - pad * 2), false);
			}

			// Индикатор типа: цветная точка в углу (расходник ↔ ключ/квест, F-13).
			Color dot = item.Category == ItemCategory.Consumable ? ConsumableDot : KeyDot;
			DrawCircle(cell.Position + new Vector2(CellSize - 10, 10), 5.0f, dot);
		}

		// Порядковый номер колонки для навигации цифрами.
		DrawString(font, cell.Position + new Vector2(5, CellSize - 6),
			(col + 1).ToString(), HorizontalAlignment.Left, -1, 12, EmptyOutline);

		// Курсор — последняя выбранная ячейка (цель для выброса по G).
		if (slot == _cursorSlot)
			DrawRect(cell.Grow(-1), CursorColor, false, 2.0f);

		// Вспышка применения.
		if (slot == _flashSlot && _flashT > 0.0f)
			DrawRect(cell, new Color(FlashColor, _flashT * 0.6f));
	}
}
