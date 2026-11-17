// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

using System.Numerics;
using Content.Shared._Lavaland.Damage.Components;
using Content.Shared._Lavaland.Megafauna.Components;
using Content.Shared._Lavaland.Megafauna.Events;
using Content.Shared.Coordinates.Helpers;
using Content.Shared.Damage.Components;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.Server._Lavaland.BossMechanics;

/// <summary>
/// Handles <see cref="MegafaunaSwoopComponent"/>: keeps the boss flying to the blink target for the
/// blink delay, makes it untouchable on the way and drops the slam spawner on the landing tile.
/// The teleport, the dash sound and the fire trail along the dash stay in <see cref="MegafaunaBlinkSystem"/>.
/// </summary>
public sealed class MegafaunaSwoopSystem : EntitySystem
{
    [Dependency] private readonly SharedTransformSystem _xform = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MegafaunaSwoopComponent, MegafaunaBlinkActionEvent>(OnSwoopAction);
        SubscribeLocalEvent<MegafaunaSwoopComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnSwoopAction(Entity<MegafaunaSwoopComponent> ent, ref MegafaunaBlinkActionEvent args)
    {
        // Handled is left alone on purpose: the blink handler of the same event still has to
        // schedule the landing teleport and the fire trail.
        if (ent.Comp.LandAt != null
            || !args.Target.IsValid(EntityManager)
            || !TryComp<MegafaunaBlinkComponent>(ent, out var blink))
            return;

        var flight = (float) blink.Delay.TotalSeconds;
        if (flight <= 0f)
            return;

        // TG keeps a godmode trait on the drake for the whole swoop. EnsureComp, not EnableGodmode:
        // the latter rejuvenates the boss to full health on every swoop.
        ent.Comp.OwnsGodmode = !EnsureComp<GodmodeComponent>(ent.Owner, out _);
        ent.Comp.Origin = _xform.GetMapCoordinates(ent.Owner);
        ent.Comp.Target = args.Target;
        ent.Comp.FlightSeconds = flight;
        ent.Comp.LandAt = _timing.CurTime + blink.Delay;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<MegafaunaSwoopComponent>();
        while (query.MoveNext(out var uid, out var swoop))
        {
            if (swoop.LandAt is not { } landAt)
                continue;

            if (now >= landAt)
            {
                Land((uid, swoop));
                continue;
            }

            var target = _xform.ToMapCoordinates(swoop.Target);
            if (swoop.FlightSeconds <= 0f || target.MapId != swoop.Origin.MapId)
                continue;

            var left = (float) (landAt - now).TotalSeconds;
            var progress = Math.Clamp(1f - left / swoop.FlightSeconds, 0f, 1f);
            _xform.SetMapCoordinates(uid, new MapCoordinates(
                Vector2.Lerp(swoop.Origin.Position, target.Position, progress), swoop.Origin.MapId));
        }
    }

    private void Land(Entity<MegafaunaSwoopComponent> ent)
    {
        var coords = ent.Comp.Target.IsValid(EntityManager)
            ? ent.Comp.Target.SnapToGrid(EntityManager)
            : Transform(ent.Owner).Coordinates;

        _xform.SetCoordinates(ent.Owner, coords);

        if (ent.Comp.OwnsGodmode)
            RemComp<GodmodeComponent>(ent.Owner);

        // TG slams every mob around the drake except the drake itself, while our damage tiles hit
        // everything standing on them, so the landing boss gets a short damage square immunity.
        EnsureComp<DamageSquareImmunityComponent>(ent.Owner).ImmunityEndTime =
            _timing.CurTime + TimeSpan.FromSeconds(0.5f);

        SpawnAtPosition(ent.Comp.Slam, coords);

        ent.Comp.LandAt = null;
        ent.Comp.OwnsGodmode = false;
    }

    private void OnShutdown(Entity<MegafaunaSwoopComponent> ent, ref ComponentShutdown args)
    {
        // A terminating boss loses the component anyway, only a live one would keep the godmode.
        if (ent.Comp.OwnsGodmode && !TerminatingOrDeleted(ent.Owner))
            RemComp<GodmodeComponent>(ent.Owner);
    }
}
