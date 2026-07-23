// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using Content.Goobstation.Common.CCVar; //Goobstation - Crawling
using Content.Goobstation.Common.Projectiles;
using Content.Shared._DV.Abilities;
using Content.Shared.Projectiles;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Standing;
using Robust.Shared.Map;
using Robust.Shared.Physics.Events;
using Robust.Shared.Containers;
using Robust.Shared.Physics.Components;
using Robust.Shared.Configuration; //Goobstation - Crawling

namespace Content.Shared.Damage.Components;

public sealed class RequireProjectileTargetSystem : EntitySystem
{
    [Dependency] private readonly SharedContainerSystem _container = default!;

    // Goobstation
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;

    private float _crawlHitzoneSize; //Goobstation

    public float CrawlHitzoneSize => _crawlHitzoneSize;

    public override void Initialize()
    {
        SubscribeLocalEvent<RequireProjectileTargetComponent, PreventCollideEvent>(PreventCollide);
        SubscribeLocalEvent<RequireProjectileTargetComponent, StoodEvent>(StandingBulletHit);
        SubscribeLocalEvent<RequireProjectileTargetComponent, DownedEvent>(LayingBulletPass);
        _cfg.OnValueChanged(GoobCVars.CrawlHitzoneSize, value => _crawlHitzoneSize = value, true); //Goobstation - Crawling
    }

    /// <summary>
    /// Finds a prone / require-target entity near the aim point so aimed shots still hit them.
    /// </summary>
    public EntityUid? TryGetAimedProneTarget(EntityCoordinates aimCoordinates, float? range = null)
    {
        var mapCoords = _transform.ToMapCoordinates(aimCoordinates);
        if (mapCoords.MapId == MapId.Nullspace)
            return null;

        var hitzone = range ?? _crawlHitzoneSize;
        if (hitzone <= 0f)
            return null;

        EntityUid? best = null;
        var bestDist = hitzone;

        foreach (var uid in _lookup.GetEntitiesInRange(mapCoords, hitzone))
        {
            if (!TryComp<RequireProjectileTargetComponent>(uid, out var require) || !require.Active)
                continue;

            var dist = (_transform.GetMapCoordinates(uid).Position - mapCoords.Position).Length();
            if (dist > bestDist)
                continue;

            bestDist = dist;
            best = uid;
        }

        return best;
    }

    public bool IsWithinCrawlHitzone(EntityUid entity, Vector2 aimMapPosition)
    {
        return (_transform.GetMapCoordinates(entity).Position - aimMapPosition).Length() <= _crawlHitzoneSize;
    }

    private void PreventCollide(Entity<RequireProjectileTargetComponent> ent, ref PreventCollideEvent args)
    {
        if (args.Cancelled)
            return;

        if (!ent.Comp.Active)
            return;

        var other = args.OtherEntity;
        // Goob edit start
        if (TryComp(other, out TargetedProjectileComponent? targeted))
        {
            if (targeted.Target == null || targeted.Target == ent)
                return;

            var ev = new ShouldTargetedProjectileCollideEvent(targeted.Target.Value);
            RaiseLocalEvent(ent, ev);
            if (ev.Handled)
                return;
        }

        if (TryComp(other, out ProjectileComponent? projectile))
        {
            // Goob edit end

            // Prevents shooting out of while inside of crates
            var shooter = projectile.Shooter;
            if (!shooter.HasValue)
                return;

            // Goobstation - Crawling
            if (TryComp<CrawlUnderObjectsComponent>(shooter, out var crawl) && crawl.Enabled)
                return;

            if (TryComp(ent, out PhysicsComponent? physics) && physics.LinearVelocity.Length() > 2.5f) // Goobstation
                return;

            // ProjectileGrenades delete the entity that's shooting the projectile,
            // so it's impossible to check if the entity is in a container
            if (TerminatingOrDeleted(shooter.Value))
                return;

            if (IsWithinCrawlHitzone(ent, projectile.TargetCoordinates)) //Goobstation
                return;

            if (!_container.IsEntityOrParentInContainer(shooter.Value))
                args.Cancelled = true;
        }
    }

    private void SetActive(Entity<RequireProjectileTargetComponent> ent, bool value)
    {
        if (ent.Comp.Active == value)
            return;

        ent.Comp.Active = value;
        Dirty(ent);
    }

    private void StandingBulletHit(Entity<RequireProjectileTargetComponent> ent, ref StoodEvent args)
    {
        SetActive(ent, false);
    }

    private void LayingBulletPass(Entity<RequireProjectileTargetComponent> ent, ref DownedEvent args)
    {
        SetActive(ent, true);
    }
}
