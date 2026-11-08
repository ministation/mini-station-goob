// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

using Content.Shared.Damage;
using Robust.Shared.GameStates;

namespace Content.Shared._Lavaland.Weapons.Ranged.Components;

/// <summary>
/// An upgrade for increasing the damage of a gun's projectile.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class GunUpgradeVampirismComponent : Component
{
    [DataField]
    public DamageSpecifier DamageOnHit = new();
}

[RegisterComponent, NetworkedComponent]
public sealed partial class ProjectileVampirismComponent : Component
{
    [DataField]
    public DamageSpecifier DamageOnHit = new();
}
