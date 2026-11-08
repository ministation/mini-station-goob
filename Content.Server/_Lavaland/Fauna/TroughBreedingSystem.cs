// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using Content.Shared._Lavaland.Fauna;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.Server._Lavaland.Fauna;

/// <summary>
/// Trough breeding: a fed milk gubbuck and a fed partner near a food trough
/// produce a grub. TG parity: breed component (3 minute timer, pop cap,
/// parents must have eaten).
/// </summary>
public sealed class TroughBreedingSystem : EntitySystem
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
            if (comp.BreedingOffspring == null || now < comp.NextBreed)
                continue;

            var coords = _transform.GetMapCoordinates(trough, xform);

            EntityUid? mother = null;
            EntityUid? partner = null;

            var gutlunches = EntityQueryEnumerator<GutlunchComponent, TransformComponent>();
            while (gutlunches.MoveNext(out var uid, out var gutlunch, out var gutlunchXform))
            {
                if (TerminatingOrDeleted(uid))
                    continue;

                var mobCoords = _transform.GetMapCoordinates(uid, gutlunchXform);
                if (mobCoords.MapId != coords.MapId)
                    continue;
                if ((mobCoords.Position - coords.Position).Length() > comp.BreedingRadius)
                    continue;

                // TG: breeding parents must have eaten. Our feeders are fed
                // right after eating ore out of the trough.
                if (!TryComp<TroughFeederComponent>(uid, out var feeder) || now >= feeder.FedUntil)
                    continue;

                if (gutlunch.IsMother)
                    mother ??= uid;
                else
                    partner ??= uid;

                if (mother != null && partner != null)
                    break;
            }

            if (mother == null || partner == null)
                continue;

            // Population cap across the whole gutlunch family.
            var pop = 0;
            var popQuery = EntityQueryEnumerator<GutlunchComponent>();
            while (popQuery.MoveNext(out _, out _))
                pop++;
            if (pop >= comp.MaxOffspringPop)
                continue;

            comp.NextBreed = now + comp.BreedCooldown;
            Spawn(comp.BreedingOffspring.Value, Transform(trough).Coordinates.Offset(new Vector2(1, 0)));
        }
    }
}
