// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

﻿using Robust.Shared.GameStates;

namespace Content.Shared._Lavaland.Weapons.Melee.Components;

/// <summary>
/// Improves attack rate of a melee weapon.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class WeaponUpgradeSpeedComponent : Component
{
    [DataField]
    public float? BonusAttackRate;

    [DataField]
    public float? AttackRateMultiplier;
}
