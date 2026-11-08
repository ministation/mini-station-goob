// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

﻿using Content.Shared.EntityEffects;

namespace Content.Server._Lavaland.Weapons.Melee.Components;

/// <summary>
/// Applies a list of entity effects on a target when hit with a melee attack.
/// </summary>
[RegisterComponent]
public sealed partial class WeaponUpgradeEffectsComponent : Component
{
    [DataField]
    public List<EntityEffect> Effects = new();
}
