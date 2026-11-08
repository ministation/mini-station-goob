// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

using Robust.Shared.Map.Components;

namespace Content.Server._Lavaland.Megafauna;

public sealed class HierophantClubDrillSystem : EntitySystem
{
    [Dependency] private readonly SharedMapSystem _map = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HierophantClubDrillComponent, MapInitEvent>(OnMapInit);
    }

    private void OnMapInit(Entity<HierophantClubDrillComponent> ent, ref MapInitEvent args)
    {
        var xform = Transform(ent);
        if (xform.GridUid is not { } gridUid || !TryComp<MapGridComponent>(gridUid, out var grid))
            return;

        var tile = _map.TileIndicesFor(gridUid, grid, xform.Coordinates);
        var rocks = new List<EntityUid>();
        foreach (var uid in _map.GetAnchoredEntities(gridUid, grid, tile))
        {
            if (IsRock(uid))
                rocks.Add(uid);
        }

        foreach (var rock in rocks)
        {
            QueueDel(rock);
        }
    }

    private bool IsRock(EntityUid uid)
    {
        var id = MetaData(uid).EntityPrototype?.ID;
        // shortcut: rock is matched by prototype id, switch to a tag if rock ids stop using the WallRock prefix
        return id != null && id.StartsWith("WallRock", StringComparison.Ordinal);
    }
}
