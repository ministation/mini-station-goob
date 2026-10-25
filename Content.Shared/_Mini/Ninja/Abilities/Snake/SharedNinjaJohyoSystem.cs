// SPDX-FileCopyrightText: 2026 Mini Station
// Мини-станция, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/ministation/mini-station-goob/master/LICENSE.TXT

using System.Numerics;
using Content.Shared.Physics;
using Content.Shared.Projectiles;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Network;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;

namespace Content.Shared._Mini.Ninja.Abilities.Snake;

/// <summary>
/// Shared logic of the chain kunai: copy the settings of the weapon onto the projectile,
/// attach the chain visual after the kunai embeds into something and pull the impaled target.
/// </summary>
public abstract partial class SharedNinjaJohyoSystem : EntitySystem
{
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly INetManager _net = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<NinjaJohyoComponent, GunShotEvent>(OnShot);
        SubscribeLocalEvent<NinjaJohyoProjectileComponent, ProjectileEmbedEvent>(OnEmbed);
    }

    private void OnShot(Entity<NinjaJohyoComponent> ent, ref GunShotEvent args)
    {
        foreach (var (uid, _) in args.Ammo)
        {
            if (uid == null || !TryComp<NinjaJohyoProjectileComponent>(uid, out var projectile))
                continue;

            projectile.Shooter = args.User;
            projectile.PullAcceleration = ent.Comp.PullAcceleration;
            projectile.MaxPullSpeed = ent.Comp.MaxPullSpeed;
            projectile.ArrivalDistance = ent.Comp.ArrivalDistance;
            projectile.MaxDistance = ent.Comp.MaxDistance;
            Dirty(uid.Value, projectile);
        }

        SayPhrase(args.User, ent.Comp);
    }

    private void OnEmbed(Entity<NinjaJohyoProjectileComponent> ent, ref ProjectileEmbedEvent args)
    {
        if (ent.Comp.Shooter == null || !HasComp<PhysicsComponent>(args.Embedded))
        {
            QueueDel(ent);
            return;
        }

        if (args.Weapon == null || !TryComp<NinjaJohyoComponent>(args.Weapon, out var johyo))
        {
            QueueDel(ent);
            return;
        }

        ent.Comp.Target = args.Embedded;
        ent.Comp.Pulling = true;
        Dirty(ent);

        var visuals = EnsureComp<JointVisualsComponent>(ent);
        visuals.Sprite = johyo.ChainSprite;
        visuals.Target = ent.Comp.Shooter;
        Dirty(ent, visuals);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        // The pull is authoritative on the server, the client only sees the replicated projectile.
        if (!_net.IsServer)
            return;

        var query = EntityQueryEnumerator<NinjaJohyoProjectileComponent>();
        while (query.MoveNext(out var uid, out var projectile))
        {
            if (projectile.Shooter is not { } shooter)
                continue;

            if (!Exists(shooter))
            {
                QueueDel(uid);
                continue;
            }

            var kunaiPos = _transform.GetWorldPosition(uid);
            var shooterPos = _transform.GetWorldPosition(shooter);
            var diff = shooterPos - kunaiPos;
            var distance = diff.Length();

            if (distance >= projectile.MaxDistance)
            {
                QueueDel(uid);
                continue;
            }

            if (!projectile.Pulling || projectile.Target is not { } target)
                continue;

            if (!Exists(target))
            {
                QueueDel(uid);
                continue;
            }

            if (!TryComp<PhysicsComponent>(target, out var physics))
            {
                QueueDel(uid);
                continue;
            }

            if (physics.BodyType == BodyType.Static)
            {
                QueueDel(uid);
                continue;
            }

            if (distance <= projectile.ArrivalDistance)
            {
                QueueDel(uid);
                continue;
            }

            var direction = Vector2.Normalize(diff);
            var currentSpeed = Vector2.Dot(physics.LinearVelocity, direction);

            if (currentSpeed >= projectile.MaxPullSpeed)
                continue;

            var impulse = direction * projectile.PullAcceleration * frameTime * physics.Mass;
            _physics.ApplyLinearImpulse(target, impulse, body: physics);
            _physics.WakeBody(target, body: physics);
        }
    }

    /// <summary>
    /// Called when the ninja fires the chain kunai, used by the server to speak the shot phrases.
    /// </summary>
    protected virtual void SayPhrase(EntityUid user, NinjaJohyoComponent comp)
    {
    }
}
