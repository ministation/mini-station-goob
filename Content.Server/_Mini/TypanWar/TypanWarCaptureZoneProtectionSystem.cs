// SPDX-FileCopyrightText: 2026 Egorik1
// Мини-станция, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/ministation/mini-station-goob/master/LICENSE.TXT

using Content.Shared.Construction.Components;
using Content.Shared.GameTicking;
using Content.Shared.Maps;
using Content.Shared.Tiles;
using Content.Shared._Mini.TypanWar;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Server._Mini.TypanWar;

/// <summary>
/// Protects capture zone floor tiles from explosions and blocks construction near active zones.
/// Walls, doors, and other entities remain destructible.
/// </summary>
public sealed class TypanWarCaptureZoneProtectionSystem : EntitySystem
{
    public const int IndestructibleTileMargin = 5;
    public const int BuildBlockedTileMargin = 3;

    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MapGridComponent, FloorTileAttemptEvent>(OnFloorTileAttempt);
        SubscribeLocalEvent<AnchorableComponent, AnchorAttemptEvent>(OnAnchorAttempt);
    }

    public void RefreshAllZoneProtection()
    {
        // Tile protection is evaluated on demand; no entity marking required.
    }

    private void OnFloorTileAttempt(EntityUid uid, MapGridComponent grid, ref FloorTileAttemptEvent args)
    {
        if (!TypanStationWarRuleSystem.IsWarActive)
            return;

        if (IsBuildBlocked(uid, args.GridIndices))
            args.Cancelled = true;
    }

    private void OnAnchorAttempt(Entity<AnchorableComponent> ent, ref AnchorAttemptEvent args)
    {
        if (!TypanStationWarRuleSystem.IsWarActive)
            return;

        if (!TryComp<TransformComponent>(ent, out var xform) || xform.GridUid is not { } gridUid)
            return;

        if (!TryComp<MapGridComponent>(gridUid, out var grid))
            return;

        var tile = _map.TileIndicesFor(gridUid, grid, xform.Coordinates);
        if (IsBuildBlocked(gridUid, tile))
            args.Cancel();
    }

    public bool IsIndestructible(EntityUid gridUid, Vector2i tile)
    {
        return IsWithinZoneMargin(gridUid, tile, IndestructibleTileMargin);
    }

    public bool IsBuildBlocked(EntityUid gridUid, Vector2i tile)
    {
        return IsWithinZoneMargin(gridUid, tile, BuildBlockedTileMargin);
    }

    private bool IsWithinZoneMargin(EntityUid gridUid, Vector2i tile, int margin)
    {
        var query = EntityQueryEnumerator<TypanWarCaptureZoneComponent, TransformComponent>();
        while (query.MoveNext(out _, out var zone, out var xform))
        {
            if (!zone.Active || xform.GridUid != gridUid)
                continue;

            if (!TryComp<MapGridComponent>(gridUid, out var grid))
                continue;

            var center = _map.TileIndicesFor(gridUid, grid, xform.Coordinates);
            var half = zone.ZoneHalfExtents + new Vector2i(margin, margin);
            var delta = tile - center;

            if (Math.Abs(delta.X) <= half.X && Math.Abs(delta.Y) <= half.Y)
                return true;
        }

        return false;
    }
}
