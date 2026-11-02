// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Goobstation.Maths.FixedPoint;
using Content.Shared._Lavaland.Weapons.Lava;
using Content.Shared.Damage.Systems;

namespace Content.Server._Lavaland.Weapons;

/// <summary>
/// Nullifies heat damage for mobs with permanent lava immunity.
/// </summary>
public sealed class LavaImmunitySystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<LavaImmunityComponent, DamageModifyEvent>(OnDamageModify);
    }

    private void OnDamageModify(Entity<LavaImmunityComponent> ent, ref DamageModifyEvent args)
    {
        if (args.Damage.DamageDict.TryGetValue("Heat", out var heat) && heat > FixedPoint2.Zero)
            args.Damage.DamageDict["Heat"] = FixedPoint2.Zero;
    }
}
