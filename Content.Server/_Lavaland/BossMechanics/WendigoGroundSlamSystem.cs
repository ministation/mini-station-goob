// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._Lavaland.Megafauna.Events;
using Content.Shared.Damage.Systems;
using Content.Shared.Mobs.Components;
using Content.Shared.Throwing;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._Lavaland.BossMechanics;

/// <summary>
/// Runs <see cref="WendigoGroundSlamComponent"/>: one damage and throw ring per step,
/// expanding outwards from where the slam started.
/// </summary>
public sealed class WendigoGroundSlamSystem : EntitySystem
{
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;
    [Dependency] private readonly ThrowingSystem _throwing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<WendigoGroundSlamComponent, SpawnedByActionEvent>(OnSpawned);
        SubscribeLocalEvent<WendigoGroundSlamComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<WendigoGroundSlamComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnSpawned(Entity<WendigoGroundSlamComponent> ent, ref SpawnedByActionEvent args)
    {
        ent.Comp.User = args.User;

        if (ent.Comp.AnchorUser && !Transform(args.User).Anchored)
            _xform.AnchorEntity(args.User);
    }

    private void OnStartup(Entity<WendigoGroundSlamComponent> ent, ref ComponentStartup args)
    {
        ent.Comp.NextRing = _timing.CurTime;
    }

    private void OnShutdown(Entity<WendigoGroundSlamComponent> ent, ref ComponentShutdown args)
    {
        if (!ent.Comp.AnchorUser || ent.Comp.User is not { } user)
            return;

        if (TerminatingOrDeleted(user) || !TryComp<TransformComponent>(user, out var xform) || !xform.Anchored)
            return;

        _xform.Unanchor(user);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<WendigoGroundSlamComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var slam, out var xform))
        {
            if (now < slam.NextRing)
                continue;

            if (slam.Ring > slam.Range)
            {
                QueueDel(uid);
                continue;
            }

            DoRing(uid, slam, xform);
            slam.Ring++;
            slam.NextRing += TimeSpan.FromSeconds(slam.Delay);
        }
    }

    private void DoRing(EntityUid uid, WendigoGroundSlamComponent slam, TransformComponent xform)
    {
        var origin = _xform.GetWorldPosition(xform);
        var coords = xform.Coordinates;
        var range = slam.Ring + 0.5f;

        _audio.PlayPvs(slam.Sound, coords);

        foreach (var target in _lookup.GetEntitiesInRange(coords, range))
        {
            if (target == uid
                || target == slam.User
                || slam.Hit.Contains(target)
                || !HasComp<MobStateComponent>(target)
                || HasComp<ThrownItemComponent>(target))
                continue;

            slam.Hit.Add(target);

            var targetPos = _xform.GetWorldPosition(target);
            if ((targetPos - origin).Length() > range)
                continue;

            _damageable.TryChangeDamage(target, slam.Damage, origin: slam.User);

            if (slam.ThrowSpeed <= 0f)
                continue;

            var direction = targetPos - origin;
            if (direction.LengthSquared() < 0.0001f)
                direction = _random.NextVector2();

            var scatter = Angle.FromDegrees(_random.NextFloat(-slam.ThrowSpread * 0.5f, slam.ThrowSpread * 0.5f));
            direction = scatter.RotateVec(direction.Normalized());

            _throwing.TryThrow(target, direction, slam.ThrowSpeed, user: slam.User, pushbackRatio: 0f, doSpin: false, playSound: false);
        }
    }
}
