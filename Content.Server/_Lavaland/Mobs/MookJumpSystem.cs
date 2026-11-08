// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections.Generic;
using System.Numerics;
using Content.Server._Lavaland.Ruins;
using Robust.Shared.Map;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._Lavaland.Mobs;

/// <summary>
/// Village mooks that wander too far hop back to the village. TG parity:
/// mook_jump. The village center is the persistent MookVillageSpawner entity
/// left behind by MookVillageSystem (our stand-in for the TG landmark).
/// </summary>
public sealed class MookJumpSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    private TimeSpan _nextUpdate = TimeSpan.Zero;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        if (now < _nextUpdate)
            return;
        _nextUpdate = now + TimeSpan.FromSeconds(3);

        // Snapshot the village centers: there are only a few per map.
        var villages = new List<(EntityUid Uid, MapCoordinates Coords)>();
        var villageQuery = EntityQueryEnumerator<MookVillageSpawnerComponent, TransformComponent>();
        while (villageQuery.MoveNext(out var village, out _, out var villageXform))
        {
            villages.Add((village, _transform.GetMapCoordinates(village, villageXform)));
        }

        if (villages.Count == 0)
            return;

        var jumpers = EntityQueryEnumerator<MookJumpComponent, TransformComponent>();
        while (jumpers.MoveNext(out var uid, out var jump, out var xform))
        {
            if (now < jump.NextJump)
                continue;
            jump.NextJump = now + jump.Cooldown;

            if (TerminatingOrDeleted(uid))
                continue;

            var coords = _transform.GetMapCoordinates(uid, xform);

            foreach (var (_, villageCoords) in villages)
            {
                if (villageCoords.MapId != coords.MapId)
                    continue;

                var distance = (villageCoords.Position - coords.Position).Length();
                if (distance <= jump.VillageDistance)
                    continue;

                // TG precision 3: the mook reappears somewhere around the village.
                var offset = new Vector2(
                    _random.NextFloat(-2f, 2f),
                    _random.NextFloat(-2f, 2f));
                _transform.SetMapCoordinates(uid, villageCoords.Offset(offset));
                break;
            }
        }
    }
}
