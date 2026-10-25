// SPDX-FileCopyrightText: 2026 Mini Station
// Мини-станция, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/ministation/mini-station-goob/master/LICENSE.TXT

using Content.Shared.Actions;
using Robust.Shared.Prototypes;

namespace Content.Shared._Mini.Ninja.Abilities.Ghost;

/// <summary>
/// Ghost tier 2: the phase cloak. Toggles the stealth of the suit wearer and heats the suit up
/// while it is used. Living on the suit, the component also tracks the heat and forces the cloak
/// off on overheat.
/// </summary>
[RegisterComponent]
public sealed partial class NinjaGhostCloakComponent : Component
{
    /// <summary>
    /// Suit charge drained per second while the cloak is active.
    /// </summary>
    [DataField("drainRate")]
    public float DrainRate = 1f;

    /// <summary>
    /// Heat accumulated per second while the cloak is active.
    /// </summary>
    [DataField]
    public float HeatRate = 5f;

    /// <summary>
    /// Heat lost per second while the cloak is inactive.
    /// </summary>
    [DataField]
    public float CoolRate = 10f;

    /// <summary>
    /// Heat at which the cloak is forced off.
    /// </summary>
    [DataField]
    public float MaxHeat = 400f;

    /// <summary>
    /// Heat threshold for the first warning and for the spark effect.
    /// </summary>
    [DataField]
    public float EffectsThreshold = 200f;

    /// <summary>
    /// Heat threshold for the second warning.
    /// </summary>
    [DataField]
    public float DangerThreshold = 300f;

    /// <summary>
    /// Spark effect spawned on the wearer when the suit is overheated.
    /// </summary>
    [DataField]
    public EntProtoId EffectPrototype = "EffectSparks";

    /// <summary>
    /// Components (Stealth and StealthOnMove) copied to the wearer while the cloak is active.
    /// The stealth fields are write protected outside of the stealth system, so the values come from YAML.
    /// </summary>
    [DataField]
    public ComponentRegistry CloakComponents = new();

    /// <summary>
    /// Current heat, kept on the suit. Not networked: the client does not draw any heat visuals.
    /// </summary>
    [ViewVariables]
    public float Heat;
}

/// <summary>
/// Raised when the phase cloak action is used.
/// </summary>
public sealed partial class NinjaGhostCloakEvent : InstantActionEvent;