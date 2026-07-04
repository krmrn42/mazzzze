using Godot;

// Wukong — первый конкретный монстр (REQ-0020, US-20 / F-44..F-46).
//
// Волк, контактный режим доставки урона. Наследует шаблон `Monster` (REQ-0019) и фиксирует
// свои параметры по умолчанию (F-39): модель `black_myth_wukong.glb`, дальность/FOV зрения,
// скорости патруля/погони, порог прекращения преследования = 1 сегмент (≈1 чанк). Форма
// нейтрального цикла — патруль сегмента лабиринта (реализована в базе через сегмент + BFS).
public partial class Wukong : Monster
{
	public Wukong()
	{
		TypeId = "wukong";
		Size = MonsterSize.Large;
		Delivery = ThreatDelivery.Contact;
		ModelPath = "res://art/black_myth_wukong.glb";
		TargetLength = 2.8f;          // крупный низкий волк, вписан в коридор (модель длинная/низкая)

		VisionRange = 18.0f;          // ~5 клеток — в пределах прямой видимости коридора
		VisionFovDeg = 100.0f;

		PatrolSpeed = 2.0f;           // ниже скорости преследования
		ChaseSpeed = 4.0f;            // ниже скорости игрока (5.0) — можно оторваться

		ContactDamage = 10.0f;
		ChaseDropDistance = 57.6f;    // 1 сегмент ≈ 1 чанк (16×16 клеток, F-46)
		ContactInterval = 0.7f;

		StunDuration = 2.5f;
		DistractReachRadius = 1.5f;
		SegmentCells = 16;            // сегмент патруля = 1 чанк (F-45)

		ModelYawOffsetDeg = 180.0f;   // калибруется по forward модели
	}
}
