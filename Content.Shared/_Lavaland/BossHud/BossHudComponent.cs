// SPDX-License-Identifier: AGPL-3.0-or-later

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
    [DataField, AutoNetworkedField]
    public bool Active;

    [DataField, AutoNetworkedField]
    public float Health;

    [DataField, AutoNetworkedField]
    public float MaxHealth = 1f;

    /// <summary>
    /// Armor left on the boss, absorbed before health. Zero for bosses without armor.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float Armor;

    [DataField, AutoNetworkedField]
    public float MaxArmor;

    /// <summary>
    /// True once the armor pool broke, a broken pool never regenerates.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool ArmorBroken;
}
