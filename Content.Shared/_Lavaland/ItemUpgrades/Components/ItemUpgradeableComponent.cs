// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

using Robust.Shared.GameStates;

namespace Content.Shared._Lavaland.ItemUpgrades.Components;

/// <summary>
/// Allows the entity to support item slot upgrades.
/// </summary>
[RegisterComponent, NetworkedComponent, Access(typeof(ItemUpgradesSystem))]
public sealed partial class ItemUpgradeableComponent : Component;
