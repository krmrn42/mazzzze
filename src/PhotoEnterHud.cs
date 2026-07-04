using Godot;

// Визуальное сопровождение фотографии (REQ-0017, US-17 / F-33, F-34).
//
// Пока фотография активирована, по центру экрана висит окно с **живым** видом
// запечатлённой точки: отдельная Camera3D стоит в CapturedWorldPos и смотрит вдоль
// запечатлённого yaw, рендеря тот же мир (если в этот момент там проходит монстр — он
// виден в окне). Живой вид обесцвечен (saturation=0) и тонируется в сепию → снимок
// «под старину». Окно обрамлено рамкой полароида (модель polaroid_photo.glb): модель
// рендерится анфас в отдельном SubViewport, а её лицевая грань-фотография (в модели —
// вшитое фото девушки) заменяется на живой сепия-вид. По мере входа (Progress 0→1)
// окно растёт из центра — «фото увеличивается». В момент переноса — сепия-вспышка.
public partial class PhotoEnterHud : Control
{
	[Export] public float FlashDuration = 0.6f;
	[Export] public float PreviewFov = 50.0f;
	[Export] public float WindowWidthMin = 0.22f;  // доля экрана в начале входа
	[Export] public float WindowWidthMax = 0.85f;  // доля экрана перед переносом

	private const string PolaroidModelPath = "res://art/polaroid_photo.glb";
	private static readonly Color SepiaTint  = new(1.00f, 0.84f, 0.58f); // множитель тона старой фотографии
	private static readonly Color FlashColor = new(0.55f, 0.42f, 0.24f); // сепия-вспышка переноса

	public float Progress { get; set; } // 0..1, ставится InventoryHud каждый физ.кадр
	private float _flashT;               // 1 → 0

	private bool _active;                // окно «сквозь фото» показывается

	private SubViewport _lensVp;         // живой вид запечатлённой точки (сепия)
	private Camera3D _lensCam;
	private SubViewport _frameVp;        // рамка-полароид (лицо = живой вид)
	private float _frameAspect = 0.86f;  // ширина/высота окна = пропорции полароида

	public override void _Ready()
	{
		MouseFilter = MouseFilterEnum.Ignore;

		// 1) Живой вид объектива. Обесцвечиваем рендер (saturation=0) — «монохром»,
		//    тёплый тон сепии накладывается модуляцией (см. живой материал полароида).
		_lensVp = new SubViewport
		{
			Size = new Vector2I(512, 600),
			RenderTargetUpdateMode = SubViewport.UpdateMode.Disabled,
		};
		AddChild(_lensVp);
		_lensCam = new Camera3D { Fov = PreviewFov };
		_lensCam.Environment = new Godot.Environment
		{
			AdjustmentEnabled = true,
			AdjustmentSaturation = 0.0f,
			AdjustmentContrast = 1.08f,
		};
		_lensVp.AddChild(_lensCam);

		// 2) Рамка-полароид: модель анфас, прозрачный фон, лицевая грань = живой сепия-вид.
		_frameVp = new SubViewport
		{
			Size = new Vector2I(560, 650),
			TransparentBg = true,
			OwnWorld3D = true,
			RenderTargetUpdateMode = SubViewport.UpdateMode.Disabled,
		};
		AddChild(_frameVp);
		BuildPolaroidFrame();
	}

	// Загружает полароид, заменяет вшитую фотографию на живой сепия-вид и ставит
	// ортокамеру анфас (по нормали лицевой грани), подгоняя пропорции окна под модель.
	private void BuildPolaroidFrame()
	{
		var packed = GD.Load<PackedScene>(PolaroidModelPath);
		if (packed == null)
			return;
		var model = packed.Instantiate<Node3D>();
		_frameVp.AddChild(model);

		// Живой материал вместо вшитого фото: тёплый сепия-тон поверх обесцвеченного вида.
		var liveMat = new StandardMaterial3D
		{
			ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
			AlbedoTexture = _lensVp.GetTexture(),
			AlbedoColor = SepiaTint,
			CullMode = BaseMaterial3D.CullModeEnum.Disabled,
			TextureFilter = BaseMaterial3D.TextureFilterEnum.Linear,
		};

		Vector3 faceNormal = Vector3.Back;
		Vector3[] cardPts = null; int cardVerts = -1;
		Vector3[] photoPts = null;
		foreach (Node n in FindMeshes(model))
		{
			var mi = (MeshInstance3D)n;
			if (mi.Mesh == null || mi.Mesh.GetSurfaceCount() == 0)
				continue;
			Godot.Collections.Array arrays = mi.Mesh.SurfaceGetArrays(0);
			var uv = arrays[(int)Mesh.ArrayType.TexUV].As<Vector2[]>();
			var pos = arrays[(int)Mesh.ArrayType.Vertex].As<Vector3[]>();
			if (uv == null || uv.Length == 0 || pos == null)
				continue;
			Vector2 uvMean = Vector2.Zero, uvMin = uv[0], uvMax = uv[0];
			foreach (Vector2 t in uv) { uvMean += t; uvMin = uvMin.Min(t); uvMax = uvMax.Max(t); }
			uvMean /= uv.Length;

			// Самая крупная грань (по числу вершин) — тело карточки: по нему берём ориентацию.
			if (pos.Length > cardVerts)
			{
				cardVerts = pos.Length;
				cardPts = new Vector3[pos.Length];
				for (int i = 0; i < pos.Length; i++)
					cardPts[i] = mi.GlobalTransform * pos[i];
			}

			// Грань фотографии — та, чьи UV лежат в правой-верхней области атласа
			// (там в текстуре модели вшито фото девушки).
			if (uvMean.X > 0.5f && uvMean.Y < 0.5f)
			{
				// Плоская грань фото — самый чистый прямоугольник модели: по нему берём ориентацию.
				photoPts = new Vector3[pos.Length];
				for (int i = 0; i < pos.Length; i++) photoPts[i] = mi.GlobalTransform * pos[i];
				Vector2 span = uvMax - uvMin;
				if (span.X > 0.0001f && span.Y > 0.0001f)
				{
					// Ремап под-квадрата UV на полный кадр живого вида.
					liveMat.Uv1Scale = new Vector3(1.0f / span.X, 1.0f / span.Y, 1.0f);
					liveMat.Uv1Offset = new Vector3(-uvMin.X / span.X, -uvMin.Y / span.Y, 0.0f);
				}
				mi.SetSurfaceOverrideMaterial(0, liveMat);
				faceNormal = mi.GlobalTransform.Basis.Orthonormalized().Z;
				var normals = arrays[(int)Mesh.ArrayType.Normal].As<Vector3[]>();
				Vector3 nsum = Vector3.Zero;
				if (normals != null)
					foreach (Vector3 nn in normals) nsum += nn;
				if ((mi.GlobalTransform.Basis * nsum).Dot(faceNormal) < 0.0f)
					faceNormal = -faceNormal;
			}
		}

		// «Верх» карточки = истинное ребро прямоугольника грани фото (мин. охватывающий
		// прямоугольник) → рамка встаёт строго вертикально, без наклона от «шумной» PCA корпуса.
		Vector3 faceUp = UprightAxis(photoPts ?? cardPts, faceNormal);
		FrameFrontOn(cardPts, faceNormal, faceUp);

		var key = new DirectionalLight3D { LightEnergy = 1.4f };
		key.RotationDegrees = new Vector3(-40, -25, 0);
		_frameVp.AddChild(key);
	}

	// Ортокамера строго анфас к карточке; размеры/центр берутся по её реальным вершинам
	// (а не по осевому AABB), поэтому карточка заполняет окно без лишних полей.
	private void FrameFrontOn(Vector3[] cardPts, Vector3 normal, Vector3 cardUp)
	{
		Vector3 n = normal.LengthSquared() > 0.0001f ? normal.Normalized() : Vector3.Back;
		Vector3 up = cardUp.LengthSquared() > 0.0001f ? cardUp.Normalized() : Vector3.Up;
		if (Mathf.Abs(up.Dot(n)) > 0.99f) up = Vector3.Up; // страховка от вырождения
		Vector3 right = up.Cross(n).Normalized();

		float rMin = float.MaxValue, rMax = float.MinValue;
		float uMin = float.MaxValue, uMax = float.MinValue;
		foreach (Vector3 p in cardPts)
		{
			float rr = p.Dot(right), uu = p.Dot(up);
			rMin = Mathf.Min(rMin, rr); rMax = Mathf.Max(rMax, rr);
			uMin = Mathf.Min(uMin, uu); uMax = Mathf.Max(uMax, uu);
		}
		float w = rMax - rMin, h = uMax - uMin;
		_frameAspect = h > 0.001f ? w / h : 0.86f;
		_frameVp.Size = new Vector2I(Mathf.RoundToInt(650 * _frameAspect), 650);

		// Центр карточки в её плоскости; глубину камеры берём с запасом по нормали.
		Vector3 nAxisMid = Vector3.Zero;
		foreach (Vector3 p in cardPts) nAxisMid += p;
		nAxisMid /= cardPts.Length;
		Vector3 center = right * ((rMin + rMax) * 0.5f) + up * ((uMin + uMax) * 0.5f) + n * nAxisMid.Dot(n);

		float depth = Mathf.Max(w, h) * 2.0f;
		var cam = new Camera3D
		{
			Projection = Camera3D.ProjectionType.Orthogonal,
			Size = h * 1.04f,
			Near = 0.01f,
			Far = depth * 4.0f,
		};
		_frameVp.AddChild(cam);
		cam.GlobalPosition = center + n * depth;
		cam.LookAt(center, up);
	}

	// Истинное «вертикальное» ребро плоского прямоугольника (грани фото): ищем угол
	// минимального охватывающего прямоугольника (rotating-calipers по свипу угла) — его рёбра
	// совпадают с реальными сторонами карточки, поэтому рамка встаёт ровно, без наклона.
	// Знак/ось выбираем ближе к PCA-«верху» контента, чтобы фото не перевернулось.
	private static Vector3 UprightAxis(Vector3[] pts, Vector3 n)
	{
		if (pts == null || pts.Length < 3)
			return Vector3.Up;
		n = n.Normalized();
		Vector3 t1 = (Mathf.Abs(n.Y) < 0.9f ? n.Cross(Vector3.Up) : n.Cross(Vector3.Right)).Normalized();
		Vector3 t2 = n.Cross(t1).Normalized();

		float bestArea = float.MaxValue, bestTheta = 0.0f;
		for (int i = 0; i < 180; i++) // 0..90° с шагом 0.5° (прямоугольник симметричен mod 90°)
		{
			float th = Mathf.DegToRad(i * 0.5f);
			float c = Mathf.Cos(th), s = Mathf.Sin(th);
			float uMin = float.MaxValue, uMax = float.MinValue, vMin = float.MaxValue, vMax = float.MinValue;
			foreach (Vector3 p in pts)
			{
				float a = p.Dot(t1), b = p.Dot(t2);
				float u = a * c + b * s, v = -a * s + b * c;
				uMin = Mathf.Min(uMin, u); uMax = Mathf.Max(uMax, u);
				vMin = Mathf.Min(vMin, v); vMax = Mathf.Max(vMax, v);
			}
			float area = (uMax - uMin) * (vMax - vMin);
			if (area < bestArea) { bestArea = area; bestTheta = th; }
		}

		float cc = Mathf.Cos(bestTheta), ss = Mathf.Sin(bestTheta);
		Vector3 axisU = (t1 * cc + t2 * ss).Normalized();
		Vector3 axisV = (t1 * -ss + t2 * cc).Normalized();
		Vector3 refUp = InPlaneMajorAxis(pts, n); // грубое направление «верха» контента

		Vector3 best = axisU; float bestDot = -1.0f;
		foreach (Vector3 cand in new[] { axisU, -axisU, axisV, -axisV })
		{
			float d = cand.Dot(refUp);
			if (d > bestDot) { bestDot = d; best = cand; }
		}
		return best;
	}

	// Главная ось разброса точек в плоскости с нормалью n (2D-PCA) — направление «высоты»
	// карточки, чтобы убрать крен независимо от того, как повёрнута модель в мире.
	private static Vector3 InPlaneMajorAxis(Vector3[] pts, Vector3 n)
	{
		if (pts == null || pts.Length < 3)
			return Vector3.Up;
		n = n.Normalized();
		Vector3 t1 = (Mathf.Abs(n.Y) < 0.9f ? n.Cross(Vector3.Up) : n.Cross(Vector3.Right)).Normalized();
		Vector3 t2 = n.Cross(t1).Normalized();
		float mu = 0, mv = 0;
		foreach (Vector3 p in pts) { mu += p.Dot(t1); mv += p.Dot(t2); }
		mu /= pts.Length; mv /= pts.Length;
		float suu = 0, svv = 0, suv = 0;
		foreach (Vector3 p in pts)
		{
			float a = p.Dot(t1) - mu, b = p.Dot(t2) - mv;
			suu += a * a; svv += b * b; suv += a * b;
		}
		float theta = 0.5f * Mathf.Atan2(2 * suv, suu - svv);
		Vector3 axisA = t1 * Mathf.Cos(theta) + t2 * Mathf.Sin(theta);
		Vector3 axisB = t1 * -Mathf.Sin(theta) + t2 * Mathf.Cos(theta);
		float c = Mathf.Cos(theta), s = Mathf.Sin(theta);
		float varA = suu * c * c + svv * s * s + 2 * suv * c * s;
		float varB = suu * s * s + svv * c * c - 2 * suv * c * s;
		return (varA >= varB ? axisA : axisB).Normalized(); // ось наибольшего разброса = высота
	}

	private static System.Collections.Generic.List<Node> FindMeshes(Node root)
	{
		var list = new System.Collections.Generic.List<Node>();
		if (root is MeshInstance3D)
			list.Add(root);
		foreach (Node c in root.GetChildren())
			list.AddRange(FindMeshes(c));
		return list;
	}

	// Открыть живое окно вида запечатлённой точки (при активации фотографии).
	public void BeginPreview(Player player, PhotoItem photo)
	{
		_lensVp.World3D = player.GetWorld3D();
		_lensCam.Position = new Vector3(photo.CapturedWorldPos.X, 1.5f, photo.CapturedWorldPos.Y);
		_lensCam.Rotation = new Vector3(0, Mathf.DegToRad(photo.CapturedYawDeg), 0);
		_lensVp.RenderTargetUpdateMode = SubViewport.UpdateMode.Always;
		_frameVp.RenderTargetUpdateMode = SubViewport.UpdateMode.Always;
		_active = true;
	}

	// Закрыть окно (деактивация / выброс / расход фотографии).
	public void EndPreview()
	{
		_active = false;
		_lensVp.RenderTargetUpdateMode = SubViewport.UpdateMode.Disabled;
		_frameVp.RenderTargetUpdateMode = SubViewport.UpdateMode.Disabled;
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

	// Растущее окно-полароид по центру экрана: увеличивается по мере входа (p: мелко → крупно).
	private void DrawPreviewWindow(float p)
	{
		float frac = Mathf.Lerp(WindowWidthMin, WindowWidthMax, p);
		float w = Size.X * frac;
		float h = _frameAspect > 0.001f ? w / _frameAspect : w * 1.16f;

		Vector2 tl = new Vector2(Size.X * 0.5f, Size.Y * 0.5f) - new Vector2(w, h) * 0.5f;
		DrawTextureRect(_frameVp.GetTexture(), new Rect2(tl, new Vector2(w, h)), false);
	}
}
