// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Actions;
using Robust.Shared.Prototypes;

namespace Content.Shared._Lavaland.Megafauna.Events;

public enum MegafaunaShotPattern
{
    /// <summary>
    /// Fires <see cref="Count"/> projectiles in a cone towards the target.
    /// </summary>
    Cone,

    /// <summary>
    /// Fires <see cref="Count"/> projectiles in all directions.
    /// </summary>
    Ring,

    /// <summary>
    /// Fires single projectiles for <see cref="Duration"/> seconds,
    /// rotating the emitter by <see cref="SpinSpeed"/> degrees per second.
    /// </summary>
    Spiral,
}

/// <summary>
/// Fires a pattern of projectiles from the performer towards the target.
/// Handles both AI-performed actions (megafauna AI) and player-performed ones.
/// </summary>
public sealed partial class MegafaunaShotActionEvent : WorldTargetActionEvent
{
    [DataField(required: true)]
    public EntProtoId Projectile;

    [DataField]
    public int Count = 5;

    /// <summary>
    /// Total arc of the cone pattern, in degrees.
    /// </summary>
    [DataField]
    public float Spread = 60f;

    [DataField]
    public float Speed = 15f;

    [DataField]
    public MegafaunaShotPattern Pattern = MegafaunaShotPattern.Cone;

    /// <summary>
    /// Spiral pattern: total firing duration, in seconds.
    /// </summary>
    [DataField]
    public float Duration = 3f;

    /// <summary>
    /// Spiral pattern: delay between single bolts, in seconds.
    /// </summary>
    [DataField]
    public float Interval = 0.15f;

    /// <summary>
    /// Spiral pattern: emitter rotation speed, in degrees per second.
    /// </summary>
    [DataField]
    public float SpinSpeed = 150f;

    /// <summary>
    /// Spiral pattern: how many spirals to fire at once, rotating in opposite directions.
    /// </summary>
    [DataField]
    public int SpiralCount = 1;
}

/// <summary>
/// Component for bosses that are currently emitting a spiral pattern.
/// </summary>
[RegisterComponent]
public sealed partial class MegafaunaSpiralComponent : Component
{
    [DataField]
    public EntProtoId Projectile = default!;

    [DataField]
    public float Speed = 15f;

    [DataField]
    public float Interval = 0.15f;

    [DataField]
    public float SpinSpeed = 150f;

    public int SpiralCount = 1;
    public Angle Angle;
    public Angle CounterAngle;
    public TimeSpan NextShot;
    public TimeSpan EndTime;
}
