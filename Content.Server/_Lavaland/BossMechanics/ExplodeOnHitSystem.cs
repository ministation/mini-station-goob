// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

using Content.Server.Explosion.EntitySystems;
using Content.Shared.Mobs.Components;
using Content.Shared.Projectiles;

namespace Content.Server._Lavaland.BossMechanics;

/// <summary>
/// Detonates the projectile's <see cref="Content.Shared.Explosion.Components.ExplosiveComponent"/>
/// when it hits a non-mob target, mirroring TG's "isturf(target) || isobj(target)" check.
/// </summary>
public sealed class ExplodeOnHitSystem : EntitySystem
{
    [Dependency] private readonly ExplosionSystem _explosion = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ExplodeOnHitComponent, ProjectileHitEvent>(OnHit);
    }

    private void OnHit(Entity<ExplodeOnHitComponent> ent, ref ProjectileHitEvent args)
    {
        // TG only explodes on turf or object impacts, a directly hit mob keeps the projectile damage only.
        if (HasComp<MobStateComponent>(args.Target))
            return;

        // TG EX_ACT hits the atom that was struck, so the epicenter goes on the target tile and not
        // on the tile the projectile stopped in. The shot is deleted on hit, so the move is not seen.
        _xform.SetCoordinates(ent.Owner, Transform(args.Target).Coordinates);

        // The projectile stays alive for its normal hit handling. TriggerExplosive ignores
        // repeat calls once ExplosiveComponent.Exploded is set, so a penetrating projectile
        // cannot detonate twice.
        _explosion.TriggerExplosive(ent.Owner, delete: false);
    }
}
