// SPDX-FileCopyrightText: 2026 Mini Station
// Мини-станция, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/ministation/mini-station-goob/master/LICENSE.TXT

using Content.Shared.Actions;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Mini.Ninja.Abilities.Ghost;

/// <summary>
/// Ghost tier 1: the smoke screen of the ninja suit. The ninja can deploy a cloud of smoke
/// to escape, or arm the automatic mode that uses a weaker cloud on a few other abilities.
/// The component lives on the suit.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class NinjaGhostSmokeComponent : Component
{
    /// <summary>
    /// Entity prototype of the smoke cloud.
    /// </summary>
    [DataField]
    public EntProtoId SmokePrototype = "Smoke";

    /// <summary>
    /// How long the regular cloud lasts.
    /// </summary>
    [DataField]
    public TimeSpan Duration = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Spread strength of the regular cloud.
    /// </summary>
    [DataField]
    public int SpreadAmount = 10;

    /// <summary>
    /// How long the weakened cloud of the automatic mode lasts.
    /// </summary>
    [DataField]
    public TimeSpan AutoModeDuration = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Spread strength of the weakened cloud of the automatic mode.
    /// </summary>
    [DataField]
    public int AutoModeSpreadAmount = 5;

    /// <summary>
    /// Whether the automatic mode is armed. Stored on the suit and synced to the client for the action icon state.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool AutoMode;

    [DataField]
    public SoundSpecifier? SmokeSound = new SoundPathSpecifier("/Audio/Effects/smoke.ogg");

    /// <summary>
    /// Suit charge spent by a regular cloud.
    /// </summary>
    [DataField("energyCost")]
    public float EnergyCost = 50f;

    /// <summary>
    /// Suit charge spent by the weakened cloud of the automatic mode. Auto mode fails silently when there is not enough charge.
    /// </summary>
    [DataField("energyCostAutoMode")]
    public float EnergyCostAutoMode = 25f;
}

/// <summary>
/// Raised when the smoke screen action is used.
/// </summary>
public sealed partial class NinjaGhostSmokeEvent : InstantActionEvent;

/// <summary>
/// Raised when the automatic smoke mode is toggled.
/// </summary>
public sealed partial class NinjaGhostSmokeAutoToggleEvent : InstantActionEvent;
