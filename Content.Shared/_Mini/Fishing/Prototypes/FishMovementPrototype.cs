// SPDX-License-Identifier: LicenseRef-MiniStation-1.0
// Mechanics reference: tgstation/tgstation (code/modules/fishing).

using Robust.Shared.Prototypes;

namespace Content.Shared._Mini.Fishing.Prototypes;

/// <summary>
/// Parametrised movement pattern of a fish inside the fishing minigame.
/// </summary>
[Prototype("fishMovement")]
public sealed partial class FishMovementPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    // Named movementType in YAML: a plain "type" key is taken by the prototype kind itself.
    [DataField("movementType")]
    public FishMovementType Type = FishMovementType.Slow;

    [DataField]
    public float ShortJumpChance = 2.25f;

    [DataField]
    public float LongJumpChance = 0.0625f;

    [DataField]
    public float ShortJumpVelocityLimit = 400f;

    [DataField]
    public float LongJumpVelocityLimit = 200f;

    [DataField]
    public float FishIdleVelocity;

    [DataField]
    public bool CanInterruptMove = true;

    /// <summary>
    /// Acceleration multiplier for <see cref="FishMovementType.Accelerando"/>.
    /// </summary>
    [DataField]
    public float AccelerationTimeCap = 10f;

    /// <summary>
    /// Extra idle velocity for <see cref="FishMovementType.Plunger"/> while plunging down.
    /// </summary>
    [DataField]
    public float PlungingSpeed = -22f;
}
