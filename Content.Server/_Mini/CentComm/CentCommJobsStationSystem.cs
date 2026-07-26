// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.Shuttles.Components;
using Content.Server.Spawners.Components;
using Content.Server.Station.Events;
using Content.Server.Station.Systems;
using Content.Shared.Maps;
using Content.Shared.Station.Components;
using Content.Shared.Warps;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;

namespace Content.Server._Mini.CentComm;

/// <summary>
/// CentComm is normally loaded as a raw grid via <see cref="StationCentcommComponent"/>,
/// which skips gameMap station setup. This creates a real CentComm station entity so
/// StationJobs (ПЦК / стажер ЦК) and job spawn points on the CentComm grid work.
/// Uses the same TTStationHandleJob pattern as Typan so multi-station AssignJobs
/// does not bury these slots and overflow onto Typan as Passenger.
/// </summary>
public sealed class CentCommJobsStationSystem : EntitySystem
{
    private const string CentCommGameMapId = "CentComm";
    private const string CentCommStationId = "centcomm";

    private static readonly EntProtoId OfficialSpawnPoint = "SpawnPointCentralCommandOfficial";
    private static readonly EntProtoId AssistantSpawnPoint = "SpawnPointCentralCommandAssistant";
    private static readonly EntProtoId LateJoinSpawnPoint = "SpawnPointLateJoinCentComm";

    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly StationSystem _station = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<StationCentcommComponent, StationPostInitEvent>(OnStationPostInit);
    }

    private void OnStationPostInit(Entity<StationCentcommComponent> ent, ref StationPostInitEvent args)
    {
        if (ent.Comp.Entity is not { } centcommGrid)
            return;

        // Shared CentComm across multiple stations — only set up once.
        if (HasComp<StationMemberComponent>(centcommGrid))
        {
            EnsureCentCommSpawnPoints(centcommGrid);
            return;
        }

        if (!_prototypes.TryIndex<GameMapPrototype>(CentCommGameMapId, out var gameMap) ||
            !gameMap.Stations.TryGetValue(CentCommStationId, out var stationConfig))
        {
            Log.Error($"CentComm gameMap '{CentCommGameMapId}' / station '{CentCommStationId}' missing; cannot register CentComm jobs.");
            return;
        }

        var name = Loc.GetString("map-name-centcomm");
        var station = _station.InitializeNewStation(stationConfig, new[] { centcommGrid }, name);
        EnsureCentCommSpawnPoints(centcommGrid);
        Log.Info($"Registered CentComm station {ToPrettyString(station)} on grid {ToPrettyString(centcommGrid)} for jobs spawning.");
    }

    /// <summary>
    /// Map markers can be missing or fail to load; ensure ПЦК / стажер spawners exist on the CentComm grid
    /// next to the CentCom warp (never grid origin — that is empty space).
    /// </summary>
    private void EnsureCentCommSpawnPoints(EntityUid centcommGrid)
    {
        var hasOfficial = false;
        var hasAssistant = false;
        var hasLateJoin = false;

        var query = EntityQueryEnumerator<SpawnPointComponent, TransformComponent>();
        while (query.MoveNext(out _, out var spawn, out var xform))
        {
            if (xform.GridUid != centcommGrid && xform.ParentUid != centcommGrid)
                continue;

            if (spawn.Job == "CentralCommandOfficial")
                hasOfficial = true;
            else if (spawn.Job == "CentralCommandAssistant")
                hasAssistant = true;
            else if (spawn.SpawnType == SpawnPointType.LateJoin && spawn.Job == null)
                hasLateJoin = true;
        }

        if (hasOfficial && hasAssistant && hasLateJoin)
            return;

        if (!TryGetCentCommInteriorCoords(centcommGrid, out var origin))
        {
            Log.Error($"CentComm grid {ToPrettyString(centcommGrid)} has no warp/interior anchor; cannot ensure spawn points.");
            return;
        }

        Log.Warning(
            $"CentComm spawn markers incomplete (official={hasOfficial}, assistant={hasAssistant}, latejoin={hasLateJoin}); spawning missing markers at {origin}.");

        if (!hasOfficial)
            SpawnAt(OfficialSpawnPoint, origin, offset: (-1f, 0f));
        if (!hasAssistant)
            SpawnAt(AssistantSpawnPoint, origin, offset: (0f, 1f));
        if (!hasLateJoin)
            SpawnAt(LateJoinSpawnPoint, origin, offset: (1f, 0f));
    }

    private bool TryGetCentCommInteriorCoords(EntityUid centcommGrid, out EntityCoordinates coords)
    {
        var warps = EntityQueryEnumerator<WarpPointComponent, TransformComponent>();
        while (warps.MoveNext(out _, out var warp, out var xform))
        {
            if (xform.GridUid != centcommGrid && xform.ParentUid != centcommGrid)
                continue;

            if (warp.Location != null &&
                warp.Location.Contains("CentCom", StringComparison.OrdinalIgnoreCase))
            {
                coords = xform.Coordinates;
                return true;
            }
        }

        warps = EntityQueryEnumerator<WarpPointComponent, TransformComponent>();
        while (warps.MoveNext(out _, out _, out var xform))
        {
            if (xform.GridUid != centcommGrid && xform.ParentUid != centcommGrid)
                continue;

            coords = xform.Coordinates;
            return true;
        }

        if (TryComp(centcommGrid, out MapGridComponent? grid))
        {
            coords = new EntityCoordinates(centcommGrid, grid.LocalAABB.Center);
            return true;
        }

        coords = default;
        return false;
    }

    private void SpawnAt(EntProtoId prototype, EntityCoordinates origin, (float X, float Y) offset)
    {
        var coords = origin.Offset(new System.Numerics.Vector2(offset.X, offset.Y));
        var uid = Spawn(prototype, coords);
        _transform.SetCoordinates(uid, coords);
    }
}
