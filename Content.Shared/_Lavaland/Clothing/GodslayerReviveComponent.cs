// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

using Content.Goobstation.Maths.FixedPoint;

namespace Content.Shared._Lavaland.Clothing;

/// <summary>
/// Godslayer armour and helm: a charge that pulls the wearer back from crit or death
/// by healing a large amount of damage. Recharges on its own cooldown.
/// </summary>
[RegisterComponent]
public sealed partial class GodslayerReviveComponent : Component
{
    /// <summary>
    /// Total damage healed when the effect triggers.
    /// </summary>
    [DataField]
    public FixedPoint2 HealAmount = 500;

    /// <summary>
    /// Time before the effect can trigger again.
    /// </summary>
    [DataField]
    public TimeSpan Cooldown = TimeSpan.FromMinutes(10);

    /// <summary>
    /// When the effect can trigger again.
    /// </summary>
    public TimeSpan CooldownEnd;
}
