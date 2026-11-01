// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;

namespace Content.Server._Lavaland.Ruins;

/// <summary>
/// Builds the elite tumor landmark: a scorched crater with the tumor.
/// </summary>
public sealed class EliteTumorDenSystem : EntitySystem
{
    private const string StoneTileBurnt = "LavalandStoneTileBurnt";
    private const string Tumor = "LavalandEliteTumor";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<EliteTumorSpawnerComponent, MapInitEvent>(OnSpawnerMapInit);
    }

    private void OnSpawnerMapInit(Entity<EliteTumorSpawnerComponent> ent, ref MapInitEvent args)
    {
        // No self-delete: the prototype sweep requires every entity to stay deletable.
        var comp = ent.Comp;
        var baseCoords = Transform(ent).Coordinates;

        for (var x = -comp.CraterRadius; x <= comp.CraterRadius; x++)
        {
            for (var y = -comp.CraterRadius; y <= comp.CraterRadius; y++)
            {
                Spawn(StoneTileBurnt, baseCoords.Offset(new Vector2(x, y)));
            }
        }

        Spawn(Tumor, baseCoords);
    }
}

[RegisterComponent]
public sealed partial class EliteTumorSpawnerComponent : Component
{
    /// <summary>
    /// Half-size of the crater patch, in tiles.
    /// </summary>
    [DataField]
    public int CraterRadius = 4;
}
