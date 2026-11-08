// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

using Robust.Shared.GameStates;

namespace Content.Shared._Lavaland.BossHud;

/// <summary>
/// Networked snapshot of a boss fight for the client boss HUD.
/// The server system keeps health updated on damage; the client only reads it.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class BossHudComponent : Component
{
    /// <summary>
    /// True while the fight is active and the HUD should be displayed.
    /// </summary>
    // Runtime snapshot only, not a prototype field, so saving a spawned HUD is a no-op.
    [AutoNetworkedField]
    public bool Active;

    // Runtime snapshot only, not a prototype field, so saving a spawned HUD is a no-op.
    [AutoNetworkedField]
    public float Health;

    // Runtime snapshot only, not a prototype field, so saving a spawned HUD is a no-op.
    [AutoNetworkedField]
    public float MaxHealth = 1f;

    /// <summary>
    /// Armor left on the boss, absorbed before health. Zero for bosses without armor.
    /// </summary>
    // Runtime snapshot only, not a prototype field, so saving a spawned HUD is a no-op.
    [AutoNetworkedField]
    public float Armor;

    // Runtime snapshot only, not a prototype field, so saving a spawned HUD is a no-op.
    [AutoNetworkedField]
    public float MaxArmor;

    /// <summary>
    /// True once the armor pool broke, a broken pool never regenerates.
    /// </summary>
    // Runtime snapshot only, not a prototype field, so saving a spawned HUD is a no-op.
    [AutoNetworkedField]
    public bool ArmorBroken;
}
