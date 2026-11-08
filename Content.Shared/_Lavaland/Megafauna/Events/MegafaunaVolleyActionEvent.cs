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
    public List<List<float>> Patterns = new();

    /// <summary>
    /// Entity to keep aiming at, so the volley follows a moving target.
    /// </summary>
    public EntityUid? Target;
    public Angle BaseAngle;
    public int VolleyIndex;
    public int Volleys;
    public TimeSpan Interval;
    public TimeSpan NextVolley;
}
