// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Projectiles;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._Lavaland.BossMechanics;

/// <summary>
/// Makes a projectile split into a ring of other projectiles after <see cref="Delay"/> seconds.
/// Port of TG's frost orb, which bursts into ice blasts instead of just flying into a wall.
/// </summary>
[RegisterComponent]
public sealed partial class ShrapnelProjectileComponent : Component
{
    [DataField(required: true)]
    public EntProtoId Spawn;

    [DataField]
    public int Count = 6;

    /// <summary>
    /// Max random radial offset of every shard, in degrees.
    /// </summary>
    [DataField]
    public float Spread = 60f;

    [DataField]
    public float Speed = 10.5f;

    [DataField]
    public float Delay = 2f;
}

public sealed class ShrapnelProjectileSystem : EntitySystem
{
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ShrapnelProjectileComponent, MapInitEvent>(OnMapInit);
    }

    private void OnMapInit(Entity<ShrapnelProjectileComponent> ent, ref MapInitEvent args)
    {
        // Capture plain values, the projectile can be deleted by a hit before the timer runs.
        var uid = ent.Owner;
        var spawn = ent.Comp.Spawn;
        var count = ent.Comp.Count;
        var spread = ent.Comp.Spread;
        var speed = ent.Comp.Speed;

        Timer.Spawn(TimeSpan.FromSeconds(ent.Comp.Delay), () => Explode(uid, spawn, count, spread, speed));
    }

    private void Explode(EntityUid uid, EntProtoId spawn, int count, float spread, float speed)
    {
        if (TerminatingOrDeleted(uid) || count <= 0)
            return;

        var coords = Transform(uid).Coordinates;
        var baseAngle = _xform.GetWorldRotation(uid);
        EntityUid? shooter = TryComp<ProjectileComponent>(uid, out var projectile)
            ? projectile.Shooter
            : null;

        for (var i = 0; i < count; i++)
        {
            var angle = baseAngle
                        + Angle.FromDegrees(i * 360f / count)
                        + Angle.FromDegrees(_random.NextFloat(-spread, spread));

            var shard = Spawn(spawn, coords);
            _xform.SetLocalRotation(shard, angle);

            if (TryComp<ProjectileComponent>(shard, out var shardProjectile))
            {
                shardProjectile.Shooter = shooter;
                shardProjectile.Weapon = shooter;
            }

            _physics.SetLinearVelocity(shard, angle.ToWorldVec() * speed);
        }

        QueueDel(uid);
    }
}
