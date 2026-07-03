using Godot;

// Фотография — предмет-портал (REQ-0017, US-17 / F-31, F-32).
//
// Наследует базовую сущность Item (паттерн B, одноразовый). Создаётся фотоаппаратом
// (REQ-0013) в момент срабатывания и хранит однократно захваченные, неизменяемые
// данные: точку стояния игрока (XZ) и направление основной камеры (yaw, pitch).
// «Использование» — вход движением вперёд (F-33): игрок телепортируется в
// CapturedWorldPos с восстановлением CapturedYawDeg/CapturedPitchDeg.
public partial class PhotoItem : Item
{
	public Vector2 CapturedWorldPos { get; } // мировые X, Z точки съёмки (высота — на пол при переносе)
	public float CapturedYawDeg { get; }     // CameraYaw.Rotation.Y в момент съёмки
	public float CapturedPitchDeg { get; }   // CameraPitch.Rotation.X в момент съёмки (штатный наклон)

	public PhotoItem(Vector2 capturedWorldPos, float capturedYawDeg, float capturedPitchDeg)
		: base("photo", "Фотография", ItemCategory.Key, "", ItemUsage.ActivatedB)
	{
		CapturedWorldPos = capturedWorldPos;
		CapturedYawDeg = capturedYawDeg;
		CapturedPitchDeg = capturedPitchDeg;
	}

	// Процедурный плейсхолдер-полароид (F-34): кремовая карточка со светлым полем и
	// сепия-«кадром» на лицевой стороне. Ассет TBD. Стоит вертикально, чтобы высота
	// (доминирующее измерение) корректно масштабировалась WorldItem под целевой размер.
	public override Node3D BuildModel()
	{
		var root = new Node3D();

		var card = new MeshInstance3D
		{
			Mesh = new BoxMesh { Size = new Vector3(0.7f, 0.85f, 0.04f) },
			MaterialOverride = new StandardMaterial3D
			{
				AlbedoColor = new Color(0.92f, 0.88f, 0.78f),
				Roughness = 0.9f,
			},
		};
		root.AddChild(card);

		var frame = new MeshInstance3D
		{
			Mesh = new BoxMesh { Size = new Vector3(0.56f, 0.54f, 0.02f) },
			Position = new Vector3(0, 0.08f, 0.02f), // сдвиг вверх — широкое нижнее поле полароида
			MaterialOverride = new StandardMaterial3D
			{
				AlbedoColor = new Color(0.36f, 0.26f, 0.16f),
				Roughness = 0.8f,
			},
		};
		root.AddChild(frame);

		return root;
	}
}
