// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

﻿using System;
using System.Collections.Generic;
using Content.Shared.Damage;
using Content.Goobstation.Maths.FixedPoint;
using Robust.Shared.GameObjects;
using Robust.Shared.Serialization.Manager.Attributes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Server.RPSX.DarkForces.Ratvar.Righteous.Structures.Beacon;

[RegisterComponent]
public sealed partial class RatvarBeaconComponent : Component
{
    [DataField]
    public bool Enabled = true;

    [DataField]
    public DamageSpecifier HealingDamage = new()
    {
        DamageDict = new Dictionary<string, FixedPoint2>
        {
            {"Blunt", -3},
            {"Slash", -3},
            {"Piercing", -3},
            {"Heat", -3},
            {"Cold", -3},
            {"Shock", -3},
            {"Burn", -3}
        }
    };

    [DataField]
    public TimeSpan ConversionInterval = TimeSpan.FromSeconds(30);

    [DataField]
    public TimeSpan NextConversionTime;

    [DataField]
    public TimeSpan LastConversionTick;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))]
    public TimeSpan LastHealTick;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))]
    public TimeSpan LastPowerTick;

    [DataField]
    public int PowerPerTick = 2;

    [DataField]
    public int CurrentConversionRadius;

    [DataField]
    public int MaxConversionsPerTick = 9;

    [DataField]
    public Queue<Vector2i>? ConversionQueue;

    [DataField]
    public HashSet<Vector2i>? ConvertedTiles;
}
