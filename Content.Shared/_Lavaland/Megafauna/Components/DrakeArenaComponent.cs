// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Goobstation.Maths.FixedPoint;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;

namespace Content.Shared._Lavaland.Megafauna.Components;

/// <summary>
///     Ash drake's lava arena (TG lava_swoop enraged swoop): the target is encased in
///     a ring of fire walls, waves of lava flood the interior with safe tiles scattered
///     around each player inside. Escaping the arena enrages the drake: it heals, gets
///     fast for a while and answers with a mass fire burst.
/// </summary>
[RegisterComponent]
public sealed partial class DrakeArenaComponent : Component
{
    #region Configuration

    /// <summary>
    ///     Temporary fire wall spawned on the arena ring.
    /// </summary>
    [DataField]
    public EntProtoId WallId = "WallDrakeArenaTemporary";

    /// <summary>
    ///     Burning ground spawned on the interior tiles each wave.
    /// </summary>
    [DataField]
    public EntProtoId PoolId = "LavalandLavaPool";

    /// <summary>
    ///     Mass fire burst answered to arena escapees after the punish delay.
    /// </summary>
    [DataField]
    public EntProtoId PunishBurstId = "LavalandDrakeMassFireBurst";

    /// <summary>
    ///     Chebyshev radius of the wall ring around the arena center.
    /// </summary>
    [DataField]
    public int WallRing = 3;

    /// <summary>
    ///     Chebyshev radius of the flooded interior.
    /// </summary>
    [DataField]
    public int InteriorRange = 2;

    /// <summary>
    ///     Number of lava waves inside the arena.
    /// </summary>
    [DataField]
    public int Waves = 3;

    /// <summary>
    ///     Delay between waves.
    /// </summary>
    [DataField]
    public float WaveDelay = 2.4f;

    /// <summary>
    ///     Delay before the first wave, so players can read the arena.
    /// </summary>
    [DataField]
    public float StartDelay = 1.0f;

    /// <summary>
    ///     Health restored to the drake for escaping the arena (TG: 250).
    /// </summary>
    [DataField]
    public FixedPoint2 PunishHeal = 250;

    /// <summary>
    ///     Walk/sprint speed multiplier while the drake is enraged about the escape.
    /// </summary>
    [DataField]
    public float PunishSpeedMultiplier = 2f;

    /// <summary>
    ///     How long the speed boost lasts, after which the mass fire burst lands.
    /// </summary>
    [DataField]
    public float PunishDuration = 5f;

    #endregion

    #region Runtime state

    public bool Active;

    public EntityCoordinates Center;

    /// <summary>
    ///     Interior tiles picked as the safe spots of the current wave.
    /// </summary>
    public HashSet<Vector2i> SafeTiles = new();

    public int WavesLeft;

    public TimeSpan NextWaveAt;

    /// <summary>
    ///     Player-controlled victims that were seen inside the arena.
    /// </summary>
    public List<EntityUid> Victims = new();

    /// <summary>
    ///     Escape punishment state: the drake is fast until SpeedRestoreAt, then answers
    ///     with a mass fire burst at the escapee's position.
    /// </summary>
    public bool PunishPending;

    public TimeSpan PunishAt;

    public TimeSpan SpeedRestoreAt;

    public EntityUid? PunishTarget;

    /// <summary>
    ///     Walk/sprint speeds captured before the punishment boost.
    /// </summary>
    public float BaseWalkSpeed;

    public float BaseSprintSpeed;

    #endregion
}
