// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;

namespace Content.Server._Lavaland.Ruins;

/// <summary>
/// Builds the ashwalker den landmark: a scorched camp with the nest
/// and a first egg.
/// </summary>
public sealed class AshwalkerDenSystem : EntitySystem
{
    private const string StoneTileBurnt = "LavalandStoneTileBurnt";
    private const string Nest = "LavalandAshwalkerNest";
    private const string Egg = "LavalandAshwalkerEgg";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AshwalkerDenSpawnerComponent, MapInitEvent>(OnSpawnerMapInit);
    }

    private void OnSpawnerMapInit(Entity<AshwalkerDenSpawnerComponent> ent, ref MapInitEvent args)
    {
        // No self-delete: the prototype sweep requires every entity to stay deletable.
        var comp = ent.Comp;
        var baseCoords = Transform(ent).Coordinates;

        for (var x = -comp.CampRadius; x <= comp.CampRadius; x++)
        {
            for (var y = -comp.CampRadius; y <= comp.CampRadius; y++)
            {
                Spawn(StoneTileBurnt, baseCoords.Offset(new Vector2(x, y)));
            }
        }

        Spawn(Nest, baseCoords.Offset(new Vector2(0, comp.CampRadius - 1)));
        Spawn(Egg, baseCoords.Offset(new Vector2(-2, 0)));
    }
}

[RegisterComponent]
public sealed partial class AshwalkerDenSpawnerComponent : Component
{
    /// <summary>
    /// Half-size of the camp patch, in tiles.
    /// </summary>
    [DataField]
    public int CampRadius = 5;
}
