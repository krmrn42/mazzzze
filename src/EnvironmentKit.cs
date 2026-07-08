using Godot;
using System.Collections.Generic;

public abstract class EnvironmentKit
{
    public Mesh[] Prototypes { get; protected set; }
    public Material RockMaterial { get; protected set; }
    public float[] BaseScales { get; private set; }

    protected void ComputeBaseScales()
    {
        BaseScales = new float[Prototypes.Length];
        for (int i = 0; i < Prototypes.Length; i++)
        {
            float h = Prototypes[i].GetAabb().Size.Y;
            BaseScales[i] = h > 0.001f ? MazeData.WallHeight / h : 1.0f;
        }
    }

    public abstract List<RockPlacement> PlaceRocks(Vector3 cellCenterLocal, ulong seed);

    protected static RandomNumberGenerator Rng(ulong seed)
    {
        var rng = new RandomNumberGenerator();
        rng.Seed = seed;
        return rng;
    }

    protected static Transform3D MakeTransform(Vector3 pos, Vector3 eulerRad, Vector3 scale)
    {
        var basis = Basis.FromEuler(eulerRad).Scaled(scale);
        return new Transform3D(basis, pos);
    }
}
