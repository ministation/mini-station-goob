// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

using Robust.Shared.GameStates;

namespace Content.Shared._Lavaland.Weapons.Ranged.Components;

/// <summary>
///     Component to indicate a valid bayonet for weapon attachment
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class GunUpgradeBayonetComponent : Component;
