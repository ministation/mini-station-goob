// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Goobstation.Maths.FixedPoint;
using Content.Shared._Lavaland.Mobs;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Mobs.Components;
using Content.Shared.Weapons.Melee.Events;

namespace Content.Server._Lavaland.Mobs;

/// <summary>
/// Heals the owner for a fraction of melee damage dealt to living targets.
/// </summary>
public sealed class VampiricMeleeSystem : EntitySystem
{
    [Dependency] private readonly DamageableSystem _damageable = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<VampiricMeleeComponent, MeleeHitEvent>(OnMeleeHit);
    }

    private void OnMeleeHit(Entity<VampiricMeleeComponent> ent, ref MeleeHitEvent args)
    {
        if (args.HitEntities.Count == 0)
            return;

        var comp = ent.Comp;
        var healed = FixedPoint2.Zero;
        foreach (var target in args.HitEntities)
        {
            // Healing only from living targets.
            if (!HasComp<MobThresholdsComponent>(target))
                continue;

            foreach (var (_, value) in args.BaseDamage.DamageDict)
                healed += value;
        }

        if (healed <= FixedPoint2.Zero)
            return;

        var heal = new DamageSpecifier();
        heal.DamageDict["Blunt"] = -healed * comp.HealFraction;
        _damageable.TryChangeDamage(ent.Owner, heal, true);
    }
}
