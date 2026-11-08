// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._Lavaland.Fauna;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.Server._Lavaland.Fauna;

/// <summary>
/// Hungry trough feeders eat ore out of nearby food troughs.
/// TG parity: gutlunches and raptors eat straight from food troughs.
/// </summary>
public sealed class TroughFeederSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly FoodTroughSystem _trough = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    private TimeSpan _nextUpdate = TimeSpan.Zero;

    public override void Initialize()
    {
        base.Initialize();

        _nextUpdate = _timing.CurTime + TimeSpan.FromSeconds(1);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        if (now < _nextUpdate)
            return;
        _nextUpdate = now + TimeSpan.FromSeconds(1);

        // Snapshot troughs first: there are only ever a handful of them.
        var troughs = new List<(Entity<FoodTroughComponent> Ent, MapCoordinates Coords)>();
        var troughQuery = EntityQueryEnumerator<FoodTroughComponent, TransformComponent>();
        while (troughQuery.MoveNext(out var trough, out var comp, out var troughXform))
        {
            if (comp.OreCount <= 0)
                continue;
            troughs.Add(((trough, comp), _transform.GetMapCoordinates(trough, troughXform)));
        }

        if (troughs.Count == 0)
            return;

        var feeders = EntityQueryEnumerator<TroughFeederComponent, TransformComponent>();
        while (feeders.MoveNext(out var uid, out var feeder, out var xform))
        {
            if (now < feeder.FedUntil)
                continue;
            if (TerminatingOrDeleted(uid))
                continue;

            var feederCoords = _transform.GetMapCoordinates(uid, xform);
            foreach (var (trough, coords) in troughs)
            {
                if (coords.MapId != feederCoords.MapId)
                    continue;

                var distance = (coords.Position - feederCoords.Position).Length();
                if (distance > feeder.FeedingRadius)
                    continue;

                if (!_trough.TryTakeOre(trough))
                    continue;

                feeder.FedUntil = now + feeder.FedDuration;
                break;
            }
        }
    }
}
