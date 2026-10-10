// SPDX-FileCopyrightText: 2026 Egorik1
// Мини-станция, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/ministation/mini-station-goob/master/LICENSE.TXT

using System.Numerics;
using Content.Server.Station.Systems;
using Content.Shared._Mini.TypanWar;
using Content.Shared.Lock;
using Content.Shared.Tag;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Physics.Components;
using Robust.Shared.Prototypes;

namespace Content.Server._Mini.TypanWar;

/// <summary>
/// Unlocks war armory lockers tagged with <see cref="TypanWarArmoryTag"/> when combat begins.
/// </summary>
public sealed class TypanWarArmorySystem : EntitySystem
{
    public static readonly ProtoId<TagPrototype> TypanWarArmoryTag = "TypanWarArmory";

    private static readonly Vector2i[] LockerTileOffsets =
    [
        new(0, -1),
        new(0, 1),
        new(1, 0),
        new(-1, 0),
    ];

    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly LockSystem _lock = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly StationSystem _station = default!;
    [Dependency] private readonly TagSystem _tag = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    private readonly HashSet<EntityUid> _tileEnts = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<TypanWarStartedEvent>(OnWarStarted);
    }

    private void OnWarStarted(TypanWarStartedEvent ev)
    {
        SpawnWarArmories();
        UnlockArmoriesOnStation(ev.NtStation);
        UnlockArmoriesOnStation(ev.TypanStation);
    }

    /// <summary>
    /// Maps carry no mapper-marked war armories, so a stocked locker is spawned next to each drop shuttle console.
    /// </summary>
    private void SpawnWarArmories()
    {
        var query = EntityQueryEnumerator<TypanWarDropShuttleConsoleComponent, TransformComponent>();
        while (query.MoveNext(out _, out var console, out var xform))
        {
            if (xform.GridUid is not { } grid || !TryComp<MapGridComponent>(grid, out var mapGrid))
                continue;

            var proto = console.Side == TypanWarSide.Nanotrasen
                ? "TypanWarArmoryLockerNt"
                : "TypanWarArmoryLockerTypan";

            Spawn(proto, FindFreeTile(grid, mapGrid, xform));
        }
    }

    /// <summary>
    /// First floor tile near the console without anchored static bodies (walls, machines);
    /// the console's own tile as a last resort.
    /// </summary>
    private EntityCoordinates FindFreeTile(EntityUid grid, MapGridComponent mapGrid, TransformComponent consoleXform)
    {
        var centerTile = _transform.GetGridOrMapTilePosition(consoleXform.Owner, consoleXform);

        foreach (var offset in LockerTileOffsets)
        {
            var tile = centerTile + offset;
            if (!_map.TryGetTileRef(grid, mapGrid, tile, out var tileRef) || tileRef.Tile.IsEmpty)
                continue;

            var aabb = _lookup.GetLocalBounds(tile, mapGrid.TileSize);
            _tileEnts.Clear();
            _lookup.GetLocalEntitiesIntersecting(grid, aabb, _tileEnts);

            var blocked = false;
            foreach (var ent in _tileEnts)
            {
                if (!TryComp<PhysicsComponent>(ent, out var physics) ||
                    !physics.CanCollide ||
                    physics.BodyType != Robust.Shared.Physics.BodyType.Static)
                {
                    continue;
                }

                blocked = true;
                break;
            }

            if (!blocked)
                return _map.GridTileToLocal(grid, mapGrid, tile);
        }

        return consoleXform.Coordinates;
    }

    private void UnlockArmoriesOnStation(EntityUid station)
    {
        var query = EntityQueryEnumerator<LockComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var lockComp, out _))
        {
            if (!lockComp.Locked || !_tag.HasTag(uid, TypanWarArmoryTag))
                continue;

            if (_station.GetOwningStation(uid) != station)
                continue;

            _lock.Unlock(uid, null, lockComp);
        }
    }
}
