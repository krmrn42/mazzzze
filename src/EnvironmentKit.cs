using Godot;
using System.Collections.Generic;

public abstract class EnvironmentKit
{
    public Mesh[] Prototypes { get; protected set; }
    public Material RockMaterial { get; protected set; }
    public float[] BaseScales { get; private set; }

    protected float FootprintFraction = 1.2f;

    protected void LoadCliffPrototypes()
    {
        Prototypes = new Mesh[8];
        for (int i = 0; i < 8; i++)
        {
            Prototypes[i] = GD.Load<Mesh>(
                $"res://art/RockPack1/Models/meshes/Cliff_models_cliff{i + 1}_mesh.res");
        }
    }

    protected void ComputeBaseScales()
    {
        BaseScales = new float[Prototypes.Length];
        for (int i = 0; i < Prototypes.Length; i++)
        {
            var size = Prototypes[i].GetAabb().Size;
            float horiz = Mathf.Max(size.X, size.Z);
            BaseScales[i] = horiz > 0.001f
                ? MazeData.CellWorldSize * FootprintFraction / horiz
                : 1.0f;
        }
    }

    public abstract List<RockPlacement> PlaceRocks(Vector3 cellCenterLocal, ulong seed);

    protected List<RockPlacement> BuildStack(
        Vector3 cellCenterLocal, ulong seed,
        float jitter, float tiltMax, float scaleMin, float scaleMax, float overlap)
    {
        var rng = Rng(seed);
        var list = new List<RockPlacement>();
        float y = 0.0f;
        int guard = 0;
        while (y < MazeData.WallHeight && guard++ < 48)
        {
            int proto = (int)(rng.Randi() % (uint)Prototypes.Length);
            float s = BaseScales[proto] * rng.RandfRange(scaleMin, scaleMax);
            float rockH = Prototypes[proto].GetAabb().Size.Y * s;
            float yaw = rng.RandfRange(0.0f, Mathf.Tau);
            float pitch = tiltMax > 0.0f ? rng.RandfRange(-tiltMax, tiltMax) : 0.0f;
            float roll = tiltMax > 0.0f ? rng.RandfRange(-tiltMax, tiltMax) : 0.0f;
            var pos = cellCenterLocal + new Vector3(
                rng.RandfRange(-jitter, jitter), y, rng.RandfRange(-jitter, jitter));
            list.Add(new RockPlacement(proto,
                MakeTransform(pos, new Vector3(pitch, yaw, roll), new Vector3(s, s, s))));
            y += Mathf.Max(rockH * overlap, 1.0f);
        }
        return list;
    }

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
