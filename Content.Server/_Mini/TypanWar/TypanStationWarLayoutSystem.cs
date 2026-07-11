using System.Linq;
using System.Numerics;
using Content.Server.Cargo.Components;
using Content.Server.Shuttles.Systems;
using Content.Server.Station.Systems;
using Content.Shared._Mini.TypanWar;
using Content.Shared.Station.Components;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Server._Mini.TypanWar;

/// <summary>
/// Merges NT and Typan station grids onto one map with a fixed tile offset and repositions trade posts.
/// </summary>
public sealed class TypanStationWarLayoutSystem : EntitySystem
{
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly ShuttleSystem _shuttle = default!;
    [Dependency] private readonly StationSystem _station = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<TypanWarStartedEvent>(OnWarStarted);
    }

    private void OnWarStarted(TypanWarStartedEvent ev)
    {
        if (!TryComp<TypanStationWarRuleComponent>(ev.Rule, out var rule) || rule.LayoutApplied)
            return;

        if (!TryComp<StationDataComponent>(ev.NtStation, out var ntData) ||
            !TryComp<StationDataComponent>(ev.TypanStation, out var typanData))
        {
            Log.Warning("Typan station war layout: missing station data.");
            return;
        }

        var ntGrid = _station.GetLargestGrid((ev.NtStation, ntData));
        var typanGrid = _station.GetLargestGrid((ev.TypanStation, typanData));

        if (ntGrid == null || typanGrid == null)
        {
            Log.Warning("Typan station war layout: could not resolve largest station grids.");
            return;
        }

        if (!TryComp<TransformComponent>(ntGrid, out var ntXform) || ntXform.MapID == MapId.Nullspace)
        {
            Log.Warning("Typan station war layout: NT grid has no map.");
            return;
        }

        var ntMap = ntXform.MapID;
        var separation = new Vector2(rule.StationSeparationTiles, 0f);
        var ntCenter = _transform.GetWorldPosition(ntGrid.Value);
        var typanCenter = _transform.GetWorldPosition(typanGrid.Value);
        var offset = ntCenter + separation - typanCenter;

        foreach (var grid in typanData.Grids.ToList())
        {
            if (!TryComp<TransformComponent>(grid, out var gridXform))
                continue;

            var worldPos = _transform.GetWorldPosition(gridXform);
            _transform.SetMapCoordinates(grid, new MapCoordinates(worldPos + offset, ntMap));
        }

        RepositionTradeGrids(ev.NtStation, ntData, ntGrid.Value);
        RepositionTradeGrids(ev.TypanStation, typanData, typanGrid.Value);

        rule.LayoutApplied = true;
        Log.Info($"Typan station war layout: merged Typan grids onto map {ntMap} with {rule.StationSeparationTiles} tile offset.");

        RaiseLocalEvent(new TypanWarLayoutReadyEvent(ev.Rule, ev.NtStation, ev.TypanStation));
    }

    private void RepositionTradeGrids(EntityUid station, StationDataComponent stationData, EntityUid anchorGrid)
    {
        foreach (var grid in stationData.Grids)
        {
            if (grid == anchorGrid || !HasComp<TradeStationComponent>(grid))
                continue;

            if (!_shuttle.TryFTLProximity(grid, anchorGrid))
                Log.Warning($"Typan station war layout: failed to reposition trade grid {grid} near station {station}.");
        }
    }
}
