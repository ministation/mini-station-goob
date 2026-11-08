// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

using Robust.Shared.Prototypes;

namespace Content.Shared._Lavaland.Elites.Components;

/// <summary>
/// Marks a goliath spawned by the broodmother, used to count and cap her brood.
/// </summary>
[RegisterComponent]
public sealed partial class EliteChildComponent : Component
{
    public EntityUid? Parent;
}

/// <summary>
/// Bone turret placed by the legionnaire. Fires bolts at nearby hostiles for a limited time.
/// </summary>
[RegisterComponent]
public sealed partial class LegionTurretComponent : Component
{
    /// <summary>
    /// The mob that placed this turret, used to cap the number of turrets per legionnaire.
    /// </summary>
    public EntityUid? PlacedBy;

    [DataField]
    public EntProtoId Projectile = "LavalandHeraldBolt";

    [DataField]
    public float FireRate = 1.2f;

    [DataField]
    public float Range = 9f;

    public TimeSpan NextShot;
}

/// <summary>
/// Projectile that teleports its shooter to the impact point when it hits a mob.
/// </summary>
[RegisterComponent]
public sealed partial class TeleportShooterOnHitComponent : Component
{
}
