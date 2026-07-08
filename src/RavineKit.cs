using Godot;
using System.Collections.Generic;

public sealed class RavineKit : EnvironmentKit
{
    public RavineKit()
    {
        LoadCliffPrototypes();
        RockMaterial = GD.Load<Material>(
            "res://art/RockPack1/Materials/Cliff_Material_Photoscan.tres");
        ComputeBaseScales();
    }

    public override List<RockPlacement> PlaceRocks(Vector3 cellCenterLocal, ulong seed)
    {
        return BuildStack(cellCenterLocal, seed,
            jitter: 0.5f, tiltMax: 0.10f, scaleMin: 0.8f, scaleMax: 1.05f, overlap: 0.65f);
    }
}
