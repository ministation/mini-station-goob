// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

using Robust.Shared.GameStates;

namespace Content.Shared._Lavaland.Weapons.Ranged.Components;

/// <summary>
/// An upgrade for increasing the speed of a gun's projectile.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class GunUpgradeSpeedComponent : Component
{
    [DataField]
    public float Coefficient = 1;
}
