// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

using Content.Shared.Actions;
using Robust.Shared.Prototypes;

namespace Content.Shared._Lavaland.Megafauna.Events;

/// <summary>
/// Shape of the projectile patterns the wendigo fires around itself.
/// </summary>
public enum WendigoShockwavePattern
{
    /// <summary>
    /// Rings of projectiles, every other ring shifted by half a step.
    /// </summary>
    Alternating,

    /// <summary>
    /// Rotating fan of projectiles fired towards the target.
    /// </summary>
    Spiral,

    /// <summary>
    /// Ring of projectiles that rotates between volleys.
    /// </summary>
    Wave,
}

/// <summary>
/// Teleports the wendigo around its target, TG's teleport attack. When the boss is
/// enraged it also fires a shotgun spread at the target after landing.
/// </summary>
public sealed partial class WendigoTeleportActionEvent : WorldTargetActionEvent
{
    /// <summary>
    /// Distance from the target the wendigo teleports to.
    /// </summary>
    [DataField]
    public float Radius = 6f;

    /// <summary>
    /// If true, teleports to the position the boss was spawned at instead of around the target.
    /// </summary>
    [DataField]
    public bool ToOrigin;

    /// <summary>
    /// Projectile fired at the target after teleporting if the boss is enraged. Null fires nothing.
    /// </summary>
    [DataField]
    public EntProtoId? EnragedProjectile;

    [DataField]
    public int EnragedCount = 5;

    /// <summary>
    /// Full angle of the enraged spread, in degrees.
    /// </summary>
    [DataField]
    public float EnragedSpread = 40f;

    [DataField]
    public float EnragedSpeed = 3.5f;
}

/// <summary>
/// Fires one of the wendigo's shockwave patterns around the boss. The boss returns to
/// its arena center and screams first, like TG does for all three shockwave attacks.
/// </summary>
public sealed partial class WendigoShockwaveActionEvent : WorldTargetActionEvent
{
    [DataField(required: true)]
    public EntProtoId Projectile;

    [DataField]
    public WendigoShockwavePattern Pattern = WendigoShockwavePattern.Alternating;

    /// <summary>
    /// Projectiles fired per ring, tick or wave.
    /// </summary>
    [DataField]
    public int Count = 24;

    /// <summary>
    /// Amount of rings, ticks or waves.
    /// </summary>
    [DataField]
    public int Repeats = 8;

    /// <summary>
    /// Delay between two rings, ticks or waves, in seconds.
    /// </summary>
    [DataField]
    public float Interval = 0.6f;

    [DataField]
    public float Speed = 9.2f;

    /// <summary>
    /// Degrees the pattern advances per repeat. Spiral: base rotation per tick. Wave: rotation per wave.
    /// </summary>
    [DataField]
    public float Step = 5f;

    /// <summary>
    /// Extra degrees per repeat index, used by the enraged spiral.
    /// </summary>
    [DataField]
    public float Growth;
}
