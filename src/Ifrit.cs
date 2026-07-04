using Godot;

// Ifrit — первый конкретный монстр (REQ-0020, US-20 / F-44..F-46).
//
// Ифрит — крупный огненный демон-гуманоид, контактный режим доставки урона. Наследует шаблон
// `Monster` (REQ-0019) и фиксирует свои параметры по умолчанию (F-39): модель `ifrit.glb` (с
// полным набором анимаций), дальность/FOV зрения, скорости патруля/погони, порог прекращения
// преследования = 1 сегмент (≈1 чанк). Форма нейтрального цикла — патруль сегмента лабиринта.
public partial class Ifrit : Monster
{
	public Ifrit()
	{
		TypeId = "ifrit";
		Size = MonsterSize.Large;
		Delivery = ThreatDelivery.Contact;
		ModelPath = "res://art/ifrit.glb";
		ScaleByLength = false;        // гуманоид — масштаб по высоте
		TargetHeight = 2.4f;          // крупная фигура, выше игрока (калибруется)

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

		ModelYawOffsetDeg = 180.0f;   // forward гуманоида (калибруется по модели)

		// Полный набор анимаций в ассете: покой, бег, атака, реакция на попадание.
		IdleAnim = "Monster_YiFuLiTe_Idle";
		MoveAnim = "Monster_YiFuLiTe_Run";
		AttackAnim = "Monster_YiFuLiTe_Attack";
		StunAnim = "Monster_YiFuLiTe_BeHit";
		MoveAnimBaseScale = 1.0f;
	}
}
