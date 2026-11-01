// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using Content.Shared._Lavaland.Megafauna.Events;
using Content.Shared.Projectiles;
using Robust.Shared.Map;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._Lavaland.Megafauna;

/// <summary>
/// Fires projectile patterns (cone, ring, spiral) for megafauna AI actions.
/// Works for both AI-performed and player-performed actions.
/// </summary>
public sealed class MegafaunaShotSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MegafaunaShotActionEvent>(OnShotAction);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<MegafaunaSpiralEmitterComponent>();
        while (query.MoveNext(out var uid, out var spiral))
        {
            if (TerminatingOrDeleted(spiral.Boss) || now >= spiral.EndTime)
            {
                QueueDel(uid);
                continue;
            }

            // The emitter follows its boss around.
            _xform.SetCoordinates(uid, Transform(spiral.Boss).Coordinates);

            if (now < spiral.NextShot)
                continue;

            spiral.NextShot = now + TimeSpan.FromSeconds(spiral.Interval);
            FireProjectile(spiral.Boss, spiral.Projectile, spiral.Angle, spiral.Speed);
            spiral.Angle += Angle.FromDegrees(spiral.SpinSpeed * spiral.Interval);
        }
    }

    private void OnShotAction(MegafaunaShotActionEvent args)
    {
        if (args.Handled || TerminatingOrDeleted(args.Performer))
            return;
        args.Handled = true;

        var shooter = args.Performer;
        var targetMap = args.Target.ToMap(EntityManager, _xform);
        var baseAngle = (targetMap.Position - _xform.GetWorldPosition(shooter)).ToWorldAngle();

        switch (args.Pattern)
        {
            case MegafaunaShotPattern.Cone:
            {
                for (var i = 0; i < args.Count; i++)
                {
                    var t = args.Count == 1 ? 0.5f : i / (float) (args.Count - 1);
                    var angle = baseAngle + Angle.FromDegrees((t - 0.5f) * args.Spread);
                    FireProjectile(shooter, args.Projectile, angle, args.Speed);
                }
                break;
            }
            case MegafaunaShotPattern.Ring:
            {
                for (var i = 0; i < args.Count; i++)
                {
                    var angle = Angle.FromDegrees(i * 360f / args.Count);
                    FireProjectile(shooter, args.Projectile, angle, args.Speed);
                }
                break;
            }
            case MegafaunaShotPattern.Spiral:
            {
                for (var i = 0; i < args.SpiralCount; i++)
                {
                    var emitter = Spawn(null, Transform(shooter).Coordinates);
                    var comp = EnsureComp<MegafaunaSpiralEmitterComponent>(emitter);
                    comp.Projectile = args.Projectile;
                    comp.Speed = args.Speed;
                    comp.Interval = args.Interval;
                    comp.SpinSpeed = args.SpinSpeed;
                    comp.Boss = shooter;
                    // Counter-spiral: emitters spin in opposite directions.
                    comp.Angle = baseAngle + Angle.FromDegrees(i * 180f);
                    comp.SpinSpeed = i % 2 == 0 ? args.SpinSpeed : -args.SpinSpeed;
                    comp.NextShot = _timing.CurTime;
                    comp.EndTime = _timing.CurTime + TimeSpan.FromSeconds(args.Duration);
                }
                break;
            }
        }
    }

    private void FireProjectile(EntityUid shooter, EntProtoId proto, Angle angle, float speed)
    {
        var projectile = Spawn(proto, Transform(shooter).Coordinates);
        _xform.SetLocalRotation(projectile, angle);

        // Don't shoot the shooter.
        if (TryComp<ProjectileComponent>(projectile, out var projectileComp))
        {
            projectileComp.Shooter = shooter;
            projectileComp.Weapon = shooter;
        }

        _physics.SetLinearVelocity(projectile, angle.ToWorldVec() * speed);
    }
}
