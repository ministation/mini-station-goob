// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

using Robust.Shared.Audio;

namespace Content.Server._Lavaland.BossMechanics;

/// <summary>
/// Enrages a boss once its health drops below <see cref="HealthFraction"/>:
/// it moves and attacks faster, and a sound announces the change.
/// </summary>
[RegisterComponent]
public sealed partial class BossEnrageComponent : Component
{
    /// <summary>
    /// Remaining health fraction at which the enrage triggers.
    /// </summary>
    [DataField]
    public float HealthFraction = 0.35f;

    /// <summary>
    /// Multiplier applied to walk and sprint speed.
    /// </summary>
    [DataField]
    public float SpeedMultiplier = 1.35f;

    /// <summary>
    /// Multiplier applied to melee attack rate.
    /// </summary>
    [DataField]
    public float AttackRateMultiplier = 1.3f;

    /// <summary>
    /// If set, replaces the megafauna AI action delay with this value on enrage.
    /// </summary>
    [DataField]
    public float? ActionDelay;

    /// <summary>
    /// Sound played when the enrage starts, so the speed up is telegraphed.
    /// </summary>
    [DataField]
    public SoundSpecifier? Sound;

    [ViewVariables]
    public bool Enraged;
}
