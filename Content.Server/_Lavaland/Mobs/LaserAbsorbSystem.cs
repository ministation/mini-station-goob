// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

using Content.Goobstation.Maths.FixedPoint;
using Content.Shared._Lavaland.Mobs;
using Content.Shared.Damage.Systems;

namespace Content.Server._Lavaland.Mobs;

/// <summary>
/// Converts laser damage into cold damage for laser-absorbing mobs.
/// </summary>
public sealed class LaserAbsorbSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<LaserAbsorbComponent, DamageModifyEvent>(OnDamageModify);
    }

    private void OnDamageModify(Entity<LaserAbsorbComponent> ent, ref DamageModifyEvent args)
    {
        if (!args.Damage.DamageDict.TryGetValue("Laser", out var laser) || laser <= FixedPoint2.Zero)
            return;

        var converted = laser * ent.Comp.Rate;
        args.Damage.DamageDict["Laser"] = laser - converted;
        args.Damage.DamageDict["Cold"] = args.Damage.DamageDict.TryGetValue("Cold", out var cold)
            ? cold + converted
            : converted;
    }
}
