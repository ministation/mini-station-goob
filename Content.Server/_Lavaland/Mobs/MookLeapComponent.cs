// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.Server._Lavaland.Mobs;

/// <summary>
/// Mook leap: a wind-up followed by throwing itself at the victim and a rapid
/// melee combo. TG parity: mook_leap ability (cooldown 7s, wind-up 2s,
/// throw_at range 7, combo of 4 melee hits at 0.4s intervals).
/// </summary>
[RegisterComponent]
public sealed partial class MookLeapComponent : Component
{
    /// <summary>
    /// Maximum range of a leap.
    /// </summary>
    [DataField]
    public float LeapRange = 7f;

    /// <summary>
    /// Cooldown between leaps. TG: 7s.
    /// </summary>
    [DataField]
    public TimeSpan Cooldown = TimeSpan.FromSeconds(7);

    /// <summary>
    /// Wind-up before the leap. TG: 2s.
    /// </summary>
    [DataField]
    public TimeSpan Windup = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Throw speed of the leap.
    /// </summary>
    [DataField]
    public float ThrowSpeed = 6f;

    /// <summary>
    /// Melee hits landed after a successful leap. TG: a combo of 4.
    /// </summary>
    [DataField]
    public int ComboHits = 4;

    /// <summary>
    /// Seconds between combo hits. TG: 0.4s.
    /// </summary>
    [DataField]
    public float ComboInterval = 0.4f;

    [ViewVariables]
    public TimeSpan NextLeap;

    /// <summary>
    /// When the wind-up finishes and the throw happens. TimeSpan.Zero while idle.
    /// </summary>
    [ViewVariables]
    public TimeSpan WindupUntil;

    /// <summary>
    /// The victim the current leap is aimed at.
    /// </summary>
    [ViewVariables]
    public EntityUid? LeapTarget;

    /// <summary>
    /// Whether a leap is in the air (waiting for impact).
    /// </summary>
    [ViewVariables]
    public bool Leaping;
}
