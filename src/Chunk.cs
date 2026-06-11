using Godot;
using System;
using System.Collections;

public partial class Chunk : Node3D
{
	[Export]
	public int ChunkSize = 16;

	[Export]
	public MeshLibrary MeshLibrary;

	private GridMap gridmap;
	private Vector2 chunkCoord;

	public override void _Ready()
	{
		gridmap = GetNode<GridMap>("GridMap");
		gridmap.MeshLibrary = MeshLibrary;
	}
	public void Setup(Vector2 coord, int[,] chunkData)
	{
		chunkCoord = coord;
		// If _Ready hasn't fired yet, get the GridMap now
		gridmap ??= GetNode<GridMap>("GridMap");
		gridmap.Clear();
		var tileId =-1;
		for (int x=0; x<ChunkSize; x++)
		{
			for (int z=0; z<ChunkSize; z++)
			{
				var cellType = chunkData[x,z];
				tileId = cellType switch
				{
					0 => 0,
					1 => 1,
					_ => -1,
				};
				gridmap.SetCellItem(new Vector3I(x,0,z), tileId);
			}
		}
	}
}
