using Godot;

// Общий визуал «звёздочки» для анимаций предмета — выброс (REQ-0015) и подбор (REQ-0016).
// Яркая emissive-сфера + точечный свет; свечение сцены (WorldEnvironment.glow) даёт эффект
// звезды, лёгкая пульсация размера читается как мерцание.
public static class ItemStar
{
	// Создаёт звезду как ребёнка parent и возвращает меш (для пульсации/сжатия вызывающим).
	public static MeshInstance3D Attach(Node3D parent)
	{
		var mat = new StandardMaterial3D
		{
			AlbedoColor = new Color(1.0f, 0.98f, 0.85f),
			EmissionEnabled = true,
			Emission = new Color(1.0f, 0.92f, 0.65f),
			EmissionEnergyMultiplier = 9.0f,
		};
		var star = new MeshInstance3D
		{
			Mesh = new SphereMesh { Radius = 0.06f, Height = 0.12f, RadialSegments = 12, Rings = 6 },
			MaterialOverride = mat,
			CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
		};
		parent.AddChild(star);

		parent.AddChild(new OmniLight3D
		{
			LightColor = new Color(1.0f, 0.9f, 0.65f),
			LightEnergy = 2.5f,
			OmniRange = 3.0f,
		});
		return star;
	}

	public static float Twinkle(float t) => 1.0f + 0.35f * Mathf.Sin(t * 40.0f);
}
