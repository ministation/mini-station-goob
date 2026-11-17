// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

using Content.Shared.Actions;
using Robust.Shared.Prototypes;

namespace Content.Shared._Lavaland.Megafauna.Events;

/// <summary>
/// Fires <see cref="Volleys"/> volleys of projectiles towards the target, one volley every
/// <see cref="Interval"/> seconds. Used by the demonic frost miner, whose attacks are bursts of
/// shots with random spread or with an explicit alternating angle pattern, which the fixed
/// <see cref="MegafaunaShotActionEvent"/> patterns cannot express.
/// </summary>
public sealed partial class MegafaunaVolleyActionEvent : WorldTargetActionEvent
{
    [DataField(required: true)]
    public EntProtoId Projectile;

    [DataField]
    public float Speed = 10f;

    [DataField]
    public int Volleys = 1;

    /// <summary>
    /// Seconds between volleys.
    /// </summary>
    [DataField]
    public float Interval = 0.1f;

    /// <summary>
    /// Projectiles fired per angle on every volley. A volley without a pattern (an empty entry in
    /// <see cref="Patterns"/>, or the whole field left out) fires this many bolts in random directions
    /// instead, which is how a wave of random bolts is made.
    /// </summary>
    [DataField]
    public int CountPerAngle = 1;

    /// <summary>
    /// Max random radial offset of each projectile, in degrees. Ignored when <see cref="Patterns"/> is set.
    /// A value of 180 fires every projectile in a random direction.
    /// </summary>
    [DataField]
    public float RandomSpread;

    /// <summary>
    /// Explicit angle offsets from the target direction, in degrees, one pattern per volley.
    /// Patterns are cycled when there are fewer of them than <see cref="Volleys"/>.
    /// </summary>
    [DataField]
    public List<List<float>> Patterns = new();

    /// <summary>
    /// If positive, blink to the target first and start firing after this many seconds.
    /// </summary>
    [DataField]
    public float BlinkDelay;

    /// <summary>
    /// Projectiles turn towards the target instead of flying straight (TG homing_turn_speed).
    /// </summary>
    [DataField]
    public bool Homing;

    /// <summary>
    /// Turn rate of homing projectiles, in degrees per second.
    /// </summary>
    [DataField]
    public float HomingSpeed = 60f;

    /// <summary>
    /// Scripted barrage: when set, these steps are played in order instead of
    /// <see cref="Volleys"/>, <see cref="Interval"/> and <see cref="Patterns"/>.
    /// </summary>
    [DataField]
    public List<MegafaunaVolleyStep> Steps = new();

    /// <summary>
    /// Line the performer says in game chat when the barrage starts (TG colossus "Perish.").
    /// </summary>
    [DataField]
    public string? Announce;

    /// <summary>
    /// Freeze the performer's megafauna AI while the barrage is firing and restore it afterwards,
    /// so a long scripted barrage is not mixed with the regular attack schedule (TG stops the AI).
    /// </summary>
    [DataField]
    public bool PauseAi;
}

/// <summary>
/// One wave of a scripted <see cref="MegafaunaVolleyActionEvent"/> barrage.
/// </summary>
[DataDefinition]
public sealed partial class MegafaunaVolleyStep
{
    /// <summary>
    /// Pause before this wave, in seconds. TG colossus sleeps finale_counter + 1 deciseconds between waves.
    /// </summary>
    [DataField]
    public float Interval = 0.1f;

    /// <summary>
    /// Angle offsets from the target direction, in degrees. Empty means the wave has no aimed bolts.
    /// </summary>
    [DataField]
    public List<float> Pattern = new();

    /// <summary>
    /// Bolts fired in random directions, spread by <see cref="RandomSpread"/>.
    /// </summary>
    [DataField]
    public int RandomCount;

    [DataField]
    public float RandomSpread = 180f;

    /// <summary>
    /// Bolts fired at random tiles within <see cref="ScatterRadius"/> tiles of the performer.
    /// </summary>
    [DataField]
    public int ScatterCount;

    [DataField]
    public int ScatterRadius = 12;
}

/// <summary>
/// State of the boss that is currently firing a <see cref="MegafaunaVolleyActionEvent"/>.
/// </summary>
[RegisterComponent]
public sealed partial class MegafaunaVolleyComponent : Component
{
    public EntProtoId Projectile = default!;
    public float Speed;
    public float RandomSpread;
    public bool Homing;
    public float HomingSpeed;
    public List<List<float>> Patterns = new();
    public int CountPerAngle = 1;

    /// <summary>
    /// Entity to keep aiming at, so the volley follows a moving target.
    /// </summary>
    public EntityUid? Target;
    public Angle BaseAngle;
    public int VolleyIndex;
    public int Volleys;
    public TimeSpan Interval;
    public TimeSpan NextVolley;
    public List<MegafaunaVolleyStep> Steps = new();
    public int StepIndex;
    public bool PauseAi;
    public bool AiWasActive;
}
