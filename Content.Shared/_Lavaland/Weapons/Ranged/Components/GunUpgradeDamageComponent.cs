// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

using Content.Shared.Damage;
using Robust.Shared.GameStates;

namespace Content.Shared._Lavaland.Weapons.Ranged.Components;

/// <summary>
/// An upgrade for increasing the damage of a gun's projectile.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class GunUpgradeDamageComponent : Component
{
    [DataField]
    public DamageSpecifier? BonusDamage;

    /// <summary>
    /// How much should we multiply the total projectile's damage.
    /// </summary>
    [DataField]
    public float Modifier = 1f;
}
