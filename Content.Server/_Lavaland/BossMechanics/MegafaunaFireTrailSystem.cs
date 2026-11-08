// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

using Content.Shared._Lavaland.Megafauna.Events;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Server._Lavaland.BossMechanics;

/// <summary>
/// Spawns the fire trail of <see cref="MegafaunaFireTrailComponent"/> along every megafauna dash.
/// </summary>
public sealed class MegafaunaFireTrailSystem : EntitySystem
{
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MegafaunaFireTrailComponent, MegafaunaBlinkActionEvent>(OnBlinkAction);
    }

    private void OnBlinkAction(Entity<MegafaunaFireTrailComponent> ent, ref MegafaunaBlinkActionEvent args)
    {
        if (ent.Comp.Step <= 0f
            || !TryComp<TransformComponent>(ent, out var xform)
            || xform.GridUid is not { } gridUid
            || _xform.GetGrid(args.Target) != gridUid)
            return;

        var start = _xform.GetWorldPosition(xform);
        var end = _xform.ToMapCoordinates(args.Target).Position;
        var diff = end - start;
        var distance = diff.Length();
        if (distance < ent.Comp.Step)
            return;

        var grid = Comp<MapGridComponent>(gridUid);
        var dir = diff / distance;

        // Spawned immediately: the fire streak is what tells players where the dash is going,
        // the teleport itself happens after the blink delay.
        for (var covered = ent.Comp.Step; covered < distance; covered += ent.Comp.Step)
        {
            var pos = start + dir * covered;
            var tile = _map.TileIndicesFor(gridUid, grid, _xform.ToCoordinates(new MapCoordinates(pos, xform.MapID)));
            SpawnAtPosition(ent.Comp.Trail, _map.GridTileToLocal(gridUid, grid, tile));
        }
    }
}
