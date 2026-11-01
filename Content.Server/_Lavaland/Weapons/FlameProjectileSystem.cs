// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.Atmos.EntitySystems;
using Content.Shared._Lavaland.Weapons.Flame;
using Content.Shared.Atmos.Components;
using Content.Shared.Projectiles;

namespace Content.Server._Lavaland.Weapons;

/// <summary>
/// Adds fire stacks to flammable targets hit by flame projectiles.
/// </summary>
public sealed class FlameProjectileSystem : EntitySystem
{
    [Dependency] private readonly FlammableSystem _flammable = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FlameProjectileComponent, ProjectileHitEvent>(OnFlameHit);
    }

    private void OnFlameHit(Entity<FlameProjectileComponent> ent, ref ProjectileHitEvent args)
    {
        if (HasComp<FlammableComponent>(args.Target))
            _flammable.AdjustFireStacks(args.Target, ent.Comp.FireStacks, ignite: true);
    }
}
