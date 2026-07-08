using Godot;
using System.Collections.Generic;

public sealed class SlotCanyonKit : EnvironmentKit
{
    public SlotCanyonKit()
    {
        LoadCliffPrototypes();
        RockMaterial = GD.Load<Material>(
            "res://art/RockPack1/Materials/Cliff_Material_Red_Sand.tres");
        ComputeBaseScales();
    }

    public override List<RockPlacement> PlaceRocks(Vector3 cellCenterLocal, ulong seed)
    {
        return BuildStack(cellCenterLocal, seed,
            jitter: 0.35f, tiltMax: 0.0f, scaleMin: 0.9f, scaleMax: 1.2f, overlap: 0.6f);
    }
}
