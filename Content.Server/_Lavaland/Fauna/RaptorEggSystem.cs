// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Maps;
using Robust.Shared.Map;

namespace Content.Server._Lavaland.Fauna;

/// <summary>
/// Incubates raptor eggs. TG parity: raptor_egg.dm — progress accumulates at
/// ~1/s, 1.5x on lava turf, and the egg never hatches while sitting on lava.
/// </summary>
public sealed class RaptorEggSystem : EntitySystem
{
    [Dependency] private readonly TurfSystem _turf = default!;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<RaptorEggComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var egg, out var xform))
        {
            var onLava = false;
            if (_turf.TryGetTileRef(xform.Coordinates, out var tileRef) &&
                _turf.GetContentTileDefinition(tileRef.Value).ID == "FloorLava")
                onLava = true;

            egg.Progress += frameTime * (onLava ? egg.LavaMultiplier : 1f);
            if (egg.Progress < egg.HatchSeconds)
                continue;

            // TG: an egg on lava keeps warming up but never bursts while it sits there.
            if (onLava)
                continue;

            if (egg.BabyProto == null || TerminatingOrDeleted(uid))
                continue;

            Spawn(egg.BabyProto, xform.Coordinates);
            QueueDel(uid);
        }
    }
}
