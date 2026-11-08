// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

namespace Content.Shared._Lavaland.Megafauna.Components;

/// <summary>
/// Lets a megafauna boss sidestep incoming projectiles. The hit is cancelled,
/// the boss teleports a short distance away and a blink effect is left behind.
/// </summary>
[RegisterComponent]
public sealed partial class MegafaunaDodgeComponent : Component
{
    [DataField]
    public bool Enabled = true;

    /// <summary>
    /// Chance to dodge a projectile, from 0 to 1.
    /// </summary>
    [DataField]
    public float DodgeChance = 0.5f;

    /// <summary>
    /// Minimum time between two dodges.
    /// </summary>
    [DataField]
    public TimeSpan Cooldown = TimeSpan.FromSeconds(2.5f);

    /// <summary>
    /// How far the boss blinks away from its current position, in tiles.
    /// </summary>
    [DataField]
    public float Distance = 2.5f;

    /// <summary>
    /// When the boss may dodge again.
    /// </summary>
    public TimeSpan NextDodge;
}
