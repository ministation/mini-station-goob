// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using Content.Shared._Lavaland.Fauna;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.Server._Lavaland.Fauna;

/// <summary>
/// Trough breeding for raptors: two fed adult raptors near a food trough lay
/// an egg. TG parity: breed component (3 minute timer, MAX_RAPTOR_POP 64,
/// baby = raptor_egg).
/// </summary>
public sealed class RaptorBreedingSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    private TimeSpan _nextUpdate = TimeSpan.Zero;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        if (now < _nextUpdate)
            return;
        _nextUpdate = now + TimeSpan.FromSeconds(5);

        var troughQuery = EntityQueryEnumerator<FoodTroughComponent, TransformComponent>();
        while (troughQuery.MoveNext(out var trough, out var comp, out var xform))
        {
            if (now < comp.NextRaptorBreed)
                continue;

            var coords = _transform.GetMapCoordinates(trough, xform);

            EntityUid? first = null;
            EntityUid? second = null;
            EntProtoId? babyProto = null;

            var raptors = EntityQueryEnumerator<RaptorComponent, TransformComponent>();
            while (raptors.MoveNext(out var uid, out var raptor, out var raptorXform))
            {
                if (TerminatingOrDeleted(uid) || !raptor.CanBreed)
                    continue;

                var mobCoords = _transform.GetMapCoordinates(uid, raptorXform);
                if (mobCoords.MapId != coords.MapId)
                    continue;
                if ((mobCoords.Position - coords.Position).Length() > comp.BreedingRadius)
                    continue;

                // TG: breeding parents must have eaten.
                if (!TryComp<TroughFeederComponent>(uid, out var feeder) || now >= feeder.FedUntil)
                    continue;

                first ??= uid;
                second ??= uid;
                babyProto ??= raptor.BabyProto;

                if (second != null)
                    break;
            }

            if (first == null || second == null)
                continue;

            // Population cap across the whole raptor family.
            var pop = 0;
            var popQuery = EntityQueryEnumerator<RaptorComponent>();
            while (popQuery.MoveNext(out _, out _))
                pop++;
            if (pop >= comp.MaxRaptorPop)
                continue;

            comp.NextRaptorBreed = now + comp.RaptorBreedCooldown;

            var egg = Spawn("LavalandRaptorEgg", Transform(trough).Coordinates.Offset(new Vector2(1, 0)));
            if (babyProto != null && TryComp<RaptorEggComponent>(egg, out var eggComp))
                eggComp.BabyProto = babyProto.Value;
        }
    }
}
