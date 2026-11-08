// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

using Content.Shared.Damage;
using Content.Goobstation.Maths.FixedPoint;

namespace Content.Server._Lavaland.Bubblegum;

/// <summary>
/// Blood cloud left behind by Bubblegum while it is enraged.
/// Damages and knocks back anything that steps into it.
/// </summary>
[RegisterComponent]
public sealed partial class BubblegumEnrageCloudComponent : Component
{
    [DataField]
    public DamageSpecifier Damage = new()
    {
        DamageDict = new()
        {
            { "Blunt", FixedPoint2.New(15) },
        },
    };

    [DataField]
    public float KnockbackDistance = 2.5f;

    [DataField]
    public float KnockbackSpeed = 6f;
}
