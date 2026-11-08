// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Lavaland.Weapons.Ranged.Components;

/// <summary>
/// Adds components when inserted and removes them when ejected from a weapon.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class GunUpgradeComponentsComponent : Component
{
    [DataField]
    public ComponentRegistry Components = new();
}
