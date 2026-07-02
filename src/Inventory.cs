using Godot;

// Модель рюкзака: 12 слотов, 3 ряда × 4 колонки (REQ-0011 / F-12).
// Чистая модель данных без UI — виджет InventoryHud рисует её содержимое.
public partial class Inventory : RefCounted
{
	public const int Rows = 3;
	public const int Cols = 4;
	public const int Capacity = Rows * Cols; // 12

	private readonly Item[] _slots = new Item[Capacity];

	public int Count { get; private set; }
	public bool IsFull => Count >= Capacity;

	public Item Get(int index) =>
		(index >= 0 && index < Capacity) ? _slots[index] : null;

	public Item Get(int row, int col) => Get(row * Cols + col);

	// Кладёт предмет в первый свободный слот. Возвращает индекс слота или -1,
	// если рюкзак полон (F-12: при заполнении новые предметы не подбираются).
	public int TryAdd(Item item)
	{
		if (item == null || IsFull)
			return -1;

		for (int i = 0; i < Capacity; i++)
		{
			if (_slots[i] == null)
			{
				_slots[i] = item;
				Count++;
				return i;
			}
		}
		return -1;
	}

	// Кладёт предмет в конкретный слот (для засева). Возвращает false, если занят.
	public bool PutAt(int index, Item item)
	{
		if (index < 0 || index >= Capacity || _slots[index] != null || item == null)
			return false;
		_slots[index] = item;
		Count++;
		return true;
	}

	public Item RemoveAt(int index)
	{
		if (index < 0 || index >= Capacity || _slots[index] == null)
			return null;
		Item removed = _slots[index];
		_slots[index] = null;
		Count--;
		return removed;
	}
}
