using Godot;
using System.Collections.Generic;

// Fog-of-war / exploration memory for the mini-map (US-10, F-10).
//
// Stores the last `BufferCapacity` cells the player has ENTERED in a FIFO buffer.
// Each buffered visit reveals a small (2*RevealRadius+1)² neighbourhood around the
// cell so the corridor and its adjacent walls become visible. Reveal is tracked with
// reference counts, so when the oldest visit is evicted the fog only closes back over
// cells no longer covered by any remaining visit — the trail fades from the tail.
//
// Entrance/exit cells, once visited, are revealed PERMANENTLY (kept outside the FIFO,
// never forgotten). State is purely in-memory and is recreated on each game launch.
public sealed class MinimapState
{
	public const int BufferCapacity = 1000;   // F-10: last 1000 visited cells
	public const int RevealRadius = 1;        // 3×3 neighbourhood per visit (tight fog)

	private readonly Queue<Vector2I> _visited = new();
	private readonly Dictionary<Vector2I, int> _revealCount = new();
	private readonly HashSet<Vector2I> _permanent = new();

	// Records that the player entered `cell`. Caller must only invoke this when the
	// player actually moves into a NEW cell (a re-entry after leaving counts again).
	public void Visit(Vector2I cell)
	{
		_visited.Enqueue(cell);
		AddReveal(cell);

		if (_visited.Count > BufferCapacity)
			RemoveReveal(_visited.Dequeue());
	}

	// Reveal a cell forever (entrance/exit once the player has been there).
	public void RevealPermanently(Vector2I cell) => _permanent.Add(cell);

	public bool IsRevealed(Vector2I cell)
		=> _permanent.Contains(cell) || _revealCount.ContainsKey(cell);

	// True only for cells the player has actually stepped on AND that were marked
	// permanent (entrance/exit) — used to gate the entrance/exit markers, which must
	// not appear merely because the cell fell inside a neighbour's reveal radius.
	public bool IsPermanentlyRevealed(Vector2I cell) => _permanent.Contains(cell);

	private void AddReveal(Vector2I center)
	{
		for (int dx = -RevealRadius; dx <= RevealRadius; dx++)
		for (int dz = -RevealRadius; dz <= RevealRadius; dz++)
		{
			var c = new Vector2I(center.X + dx, center.Y + dz);
			_revealCount.TryGetValue(c, out int n);
			_revealCount[c] = n + 1;
		}
	}

	private void RemoveReveal(Vector2I center)
	{
		for (int dx = -RevealRadius; dx <= RevealRadius; dx++)
		for (int dz = -RevealRadius; dz <= RevealRadius; dz++)
		{
			var c = new Vector2I(center.X + dx, center.Y + dz);
			if (_revealCount.TryGetValue(c, out int n))
			{
				if (n <= 1) _revealCount.Remove(c);
				else _revealCount[c] = n - 1;
			}
		}
	}
}
