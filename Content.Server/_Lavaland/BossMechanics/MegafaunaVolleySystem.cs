// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

using Content.Shared._Lavaland.Megafauna.Components;
using Content.Shared._Lavaland.Megafauna.Events;
using Content.Shared._Lavaland.Megafauna.Systems;
using Content.Shared._Goobstation.Wizard.Projectiles;
using Content.Shared.Projectiles;
using Robust.Shared.Audio;
using Robust.Shared.Map;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._Lavaland.BossMechanics;

/// <summary>
/// Handles <see cref="MegafaunaVolleyActionEvent"/>: fires a series of projectile volleys,
/// either with random spread or with an explicit alternating angle pattern.
/// Re-aims every volley at the current target, so a moving player can still be tracked.
/// </summary>
public sealed class MegafaunaVolleySystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly MegafaunaBlinkSystem _blink = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MegafaunaVolleyActionEvent>(OnVolleyAction);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<MegafaunaVolleyComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (TerminatingOrDeleted(uid))
            {
                RemCompDeferred(uid, comp);
                continue;
            }

            if (now < comp.NextVolley)
                continue;

            if (comp.VolleyIndex >= comp.Volleys)
            {
                RemCompDeferred(uid, comp);
                continue;
            }

            // Keep tracking the target while the burst is firing.
            if (comp.Target is { } target && !TerminatingOrDeleted(target))
                comp.BaseAngle = (Transform(target).WorldPosition - _xform.GetWorldPosition(uid)).ToWorldAngle();

            FireVolley(uid, comp);
            comp.VolleyIndex++;
            comp.NextVolley = now + comp.Interval;
        }
    }

    private void OnVolleyAction(MegafaunaVolleyActionEvent args)
    {
        if (args.Handled || TerminatingOrDeleted(args.Performer))
            return;

        // One volley at a time: a boss in the middle of a long barrage (the colossus finale) must not
        // have its remaining waves cut off because another volley attack fired over it.
        if (TryComp<MegafaunaVolleyComponent>(args.Performer, out var active)
            && active.VolleyIndex < active.Volleys)
        {
            args.Handled = true;
            return;
        }

        args.Handled = true;

        var performer = args.Performer;
        var targetMap = args.Target.ToMap(EntityManager, _xform);
        var start = _timing.CurTime;

        var comp = EnsureComp<MegafaunaVolleyComponent>(performer);
        comp.Projectile = args.Projectile;
        comp.Speed = args.Speed;
        comp.RandomSpread = args.RandomSpread;
        comp.Homing = args.Homing;
        comp.HomingSpeed = args.HomingSpeed;
        comp.Patterns = args.Patterns;
        comp.CountPerAngle = Math.Max(1, args.CountPerAngle);
        comp.VolleyIndex = 0;
        comp.Volleys = Math.Max(1, args.Volleys);
        comp.Interval = TimeSpan.FromSeconds(Math.Max(0.01f, args.Interval));
        comp.BaseAngle = (targetMap.Position - _xform.GetWorldPosition(performer)).ToWorldAngle();
        comp.Target = TryComp<MegafaunaAiTargetingComponent>(performer, out var targeting)
            ? targeting.TargetEnt
            : null;

        if (args.BlinkDelay > 0f)
        {
            var delay = TimeSpan.FromSeconds(args.BlinkDelay);
            SoundSpecifier? sound = TryComp<MegafaunaBlinkComponent>(performer, out var blink)
                ? blink.Sound
                : null;
            _blink.Blink(performer, args.Target, delay, sound);
            start += delay;
        }

        comp.NextVolley = start;
    }

    private void FireVolley(EntityUid performer, MegafaunaVolleyComponent comp)
    {
        if (comp.Patterns.Count == 0)
        {
            FireRandomVolley(performer, comp);
            return;
        }

        var pattern = comp.Patterns[comp.VolleyIndex % comp.Patterns.Count];

        // An empty pattern is a wave of random bolts, the same as having no patterns at all.
        if (pattern.Count == 0)
        {
            FireRandomVolley(performer, comp);
            return;
        }

        foreach (var offset in pattern)
            FireProjectile(performer, comp, comp.BaseAngle + Angle.FromDegrees(offset));
    }

    private void FireRandomVolley(EntityUid performer, MegafaunaVolleyComponent comp)
    {
        for (var i = 0; i < comp.CountPerAngle; i++)
        {
            var angle = comp.BaseAngle + Angle.FromDegrees(_random.NextFloat(-comp.RandomSpread, comp.RandomSpread));
            FireProjectile(performer, comp, angle);
        }
    }

    private void FireProjectile(EntityUid shooter, MegafaunaVolleyComponent comp, Angle angle)
    {
        var projectile = Spawn(comp.Projectile, Transform(shooter).Coordinates);
        _xform.SetLocalRotation(projectile, angle);

        if (TryComp<ProjectileComponent>(projectile, out var projectileComp))
        {
            projectileComp.Shooter = shooter;
            projectileComp.Weapon = shooter;
        }

        _physics.SetLinearVelocity(projectile, angle.ToWorldVec() * comp.Speed);

        if (!comp.Homing)
            return;

        var homing = EnsureComp<HomingProjectileComponent>(projectile);
        homing.Target = comp.Target;
        homing.HomingSpeed = comp.HomingSpeed;
    }
}
