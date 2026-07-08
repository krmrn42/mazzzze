using Godot;
using System.Collections.Generic;

public sealed class RavineKit : EnvironmentKit
{
    private const int RocksPerCell = 2;

    public RavineKit()
    {
        Prototypes = new Mesh[8];
        for (int i = 0; i < 8; i++)
        {
            Prototypes[i] = GD.Load<Mesh>(
                $"res://art/RockPack1/Models/meshes/Cliff_models_cliff{i + 1}_mesh.res");
        }
        RockMaterial = GD.Load<Material>(
            "res://art/RockPack1/Materials/Cliff_Material_Photoscan.tres");
        ComputeBaseScales();
    }

    public override List<RockPlacement> PlaceRocks(Vector3 cellCenterLocal, ulong seed)
    {
        var rng = Rng(seed);
        var list = new List<RockPlacement>(RocksPerCell);
        for (int i = 0; i < RocksPerCell; i++)
        {
            int proto = (int)(rng.Randi() % (uint)Prototypes.Length);
            float yaw = rng.RandfRange(0.0f, Mathf.Tau);
            float pitch = rng.RandfRange(-0.20f, 0.20f);
            float roll = rng.RandfRange(-0.20f, 0.20f);
            float s = BaseScales[proto] * rng.RandfRange(0.65f, 0.9f);
            var scale = new Vector3(s, s, s);
            var pos = cellCenterLocal + new Vector3(
                rng.RandfRange(-1.0f, 1.0f), 0.0f, rng.RandfRange(-1.0f, 1.0f));
            list.Add(new RockPlacement(proto, MakeTransform(pos, new Vector3(pitch, yaw, roll), scale)));
        }
        return list;
    }
}
