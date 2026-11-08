// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

using Content.Shared.Damage.Systems;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;

namespace Content.Server._Lavaland.Mobs;

/// <summary>
/// TG parity for <see cref="DeathExplosionComponent"/>: one burst of damage to living entities in radius on death.
/// </summary>
public sealed class DeathExplosionSystem : EntitySystem
{
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DeathExplosionComponent, MobStateChangedEvent>(OnMobStateChanged);
    }

    private void OnMobStateChanged(Entity<DeathExplosionComponent> ent, ref MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead)
            return;

        var coords = _xform.GetMapCoordinates(ent);
        foreach (var target in _lookup.GetEntitiesInRange(coords, ent.Comp.Radius))
        {
            if (target == ent.Owner || !_mobState.IsAlive(target))
                continue;

            _damageable.TryChangeDamage(target, ent.Comp.Damage, origin: ent.Owner);
        }
    }
}
