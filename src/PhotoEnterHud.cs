using Godot;

// Визуальное сопровождение фотографии (REQ-0017, US-17 / F-33, F-34).
//
// Пока фотография активирована, по центру экрана висит **простое окно** с живым видом
// запечатлённой точки: отдельная Camera3D стоит в CapturedWorldPos и смотрит вдоль
// запечатлённого yaw, рендеря тот же мир (если в этот момент там проходит монстр — он
// виден в окне). Живой вид обесцвечен (saturation=0) и тонируется сепией → снимок «под
// старину». Окно оформлено простой рамкой (дерево + латунный кант). По мере входа
// (Progress 0→1) окно растёт из центра — «фото увеличивается». В момент переноса — сепия-вспышка.
public partial class PhotoEnterHud : Control
{
	[Export] public float FlashDuration = 0.6f;
	[Export] public float PreviewFov = 50.0f;
	[Export] public float WindowWidthMin = 0.22f;  // доля экрана в начале входа
	[Export] public float WindowWidthMax = 0.85f;  // доля экрана перед переносом

	private static readonly Color FrameWood  = new(0.12f, 0.08f, 0.04f);
	private static readonly Color FrameBrass = new(0.72f, 0.58f, 0.28f);
	private static readonly Color Sepia      = new(0.45f, 0.32f, 0.16f, 0.16f);
	private static readonly Color FlashColor = new(0.55f, 0.42f, 0.24f); // сепия-вспышка переноса

	public float Progress { get; set; } // 0..1, ставится InventoryHud каждый физ.кадр
	private float _flashT;               // 1 → 0

	private bool _active;                // окно «сквозь фото» показывается

	private SubViewport _vp;             // живой вид запечатлённой точки (обесцвечен)
	private Camera3D _cam;

	public override void _Ready()
	{
		MouseFilter = MouseFilterEnum.Ignore;

		// Живой вид объектива. Обесцвечиваем рендер (saturation=0) — «монохром»,
		// тёплый тон сепии добавляется наложением при отрисовке окна.
		_vp = new SubViewport
		{
			Size = new Vector2I(720, 540),
			RenderTargetUpdateMode = SubViewport.UpdateMode.Disabled,
		};
		AddChild(_vp);
		_cam = new Camera3D { Fov = PreviewFov };
		_cam.Environment = new Godot.Environment
		{
			AdjustmentEnabled = true,
			AdjustmentSaturation = 0.0f,
			AdjustmentContrast = 1.08f,
		};
		_vp.AddChild(_cam);
	}

	// Открыть живое окно вида запечатлённой точки (при активации фотографии).
	public void BeginPreview(Player player, PhotoItem photo)
	{
		_vp.World3D = player.GetWorld3D();
		_cam.Position = new Vector3(photo.CapturedWorldPos.X, 1.5f, photo.CapturedWorldPos.Y);
		_cam.Rotation = new Vector3(0, Mathf.DegToRad(photo.CapturedYawDeg), 0);
		_vp.RenderTargetUpdateMode = SubViewport.UpdateMode.Always;
		_active = true;
	}

	// Закрыть окно (деактивация / выброс / расход фотографии).
	public void EndPreview()
	{
		_active = false;
		_vp.RenderTargetUpdateMode = SubViewport.UpdateMode.Disabled;
	}

	// Вызвать в момент срабатывания переноса.
	public void Flash() => _flashT = 1.0f;

	public override void _Process(double delta)
	{
		Position = Vector2.Zero;
		Size = GetViewport().GetVisibleRect().Size;

		if (_flashT > 0.0f)
			_flashT = Mathf.Max(0.0f, _flashT - (float)delta / FlashDuration);

		QueueRedraw();
	}

	public override void _Draw()
	{
		if (_active)
			DrawPreviewWindow(Mathf.Clamp(Progress, 0.0f, 1.0f));

		if (_flashT > 0.0f)
			DrawRect(new Rect2(Vector2.Zero, Size), new Color(FlashColor, _flashT));
	}

	// Растущее простое окно по центру экрана: увеличивается по мере входа (p: мелко → крупно).
	private void DrawPreviewWindow(float p)
	{
		float frac = Mathf.Lerp(WindowWidthMin, WindowWidthMax, p);
		float w = Size.X * frac;
		float h = w * 0.75f;

		Vector2 tl = new Vector2(Size.X * 0.5f, Size.Y * 0.5f) - new Vector2(w, h) * 0.5f;
		var vf = new Rect2(tl, new Vector2(w, h));

		DrawRect(vf.Grow(14), FrameWood);             // деревянная рамка
		DrawTextureRect(_vp.GetTexture(), vf, false); // живой обесцвеченный вид
		DrawRect(vf, Sepia);                          // сепия-тон
		DrawRect(vf, FrameBrass, false, 4.0f);        // латунный кант
	}
}
