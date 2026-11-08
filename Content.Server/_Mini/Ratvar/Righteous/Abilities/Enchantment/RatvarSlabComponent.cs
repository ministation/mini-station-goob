// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

﻿using System.Collections.Generic;
using Content.Shared.Damage;
using Content.Goobstation.Maths.FixedPoint;
using Robust.Shared.GameObjects;
using Robust.Shared.Serialization.Manager.Attributes;

namespace Content.Server.RPSX.DarkForces.Ratvar.Righteous.Abilities.Enchantment;

[RegisterComponent]
public sealed partial class RatvarSlabComponent : Component
{
    [DataField]
    public DamageSpecifier HealingHumanoidDamage = new()
    {
        DamageDict = new Dictionary<string, FixedPoint2>
        {
            {"Blunt", -30},
            {"Burn", -30}
        }
    };


    [DataField]
    public DamageSpecifier HealingMarauderDamage = new()
    {
        DamageDict = new Dictionary<string, FixedPoint2>
        {
            {"Blunt", 50}
        }
    };

    [DataField]
    public bool HidingStructure;
}
