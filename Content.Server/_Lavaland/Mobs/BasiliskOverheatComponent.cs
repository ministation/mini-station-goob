// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

using Robust.Shared.Prototypes;

namespace Content.Server._Lavaland.Mobs;

/// <summary>
/// TG parity: a basilisk that touches something hot overheats for a while, heals to full, speeds up,
/// switches to a hot projectile and shows its alert sprite.
/// </summary>
[RegisterComponent]
public sealed partial class BasiliskOverheatComponent : Component
{
    /// <summary>
    /// Projectile the basilisk fires while overheated.
    /// </summary>
    [DataField]
    public EntProtoId HotProjectile = "BasiliskOverheatBolt";

    /// <summary>
    /// How long the overheat lasts after a single heat source.
    /// </summary>
    [DataField]
    public TimeSpan Duration = TimeSpan.FromMinutes(3);

    /// <summary>
    /// Speed boost while overheated. TG removes almost all of the basilisk's slowness, but its speed scale
    /// does not map 1:1 to SS14, so this is a modest boost instead.
    /// </summary>
    [DataField]
    public float SpeedMultiplier = 1.5f;

    /// <summary>
    /// When the overheat ends. Zero means the basilisk is not overheated.
    /// </summary>
    [ViewVariables]
    public TimeSpan EndsAt;

    /// <summary>
    /// When the next standing-in-lava check happens.
    /// </summary>
    [ViewVariables]
    public TimeSpan NextLavaCheck;

    [ViewVariables]
    public EntProtoId? OriginalProjectile;

    [ViewVariables]
    public float OriginalWalkSpeed;

    [ViewVariables]
    public float OriginalSprintSpeed;

    [ViewVariables]
    public float OriginalAcceleration;
}
