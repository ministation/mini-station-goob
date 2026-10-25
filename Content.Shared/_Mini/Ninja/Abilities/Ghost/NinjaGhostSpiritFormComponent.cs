// SPDX-FileCopyrightText: 2026 Mini Station
// Мини-станция, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/ministation/mini-station-goob/master/LICENSE.TXT

using Content.Shared.Actions;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;

namespace Content.Shared._Mini.Ninja.Abilities.Ghost;

/// <summary>
/// Ghost tier 5: the spirit form. The wearer becomes a ghost that can walk through walls.
/// The component lives on the suit.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(raiseAfterAutoHandleState: true)]
public sealed partial class NinjaGhostSpiritFormComponent : Component
{
    /// <summary>
    /// Whether the form is active. Single source of truth for both sides.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool Active;

    /// <summary>
    /// Fraction of the cell capacity paid on activation and drained once per second.
    /// </summary>
    [DataField]
    public float EnergyDrainPercent = 0.24f;

    /// <summary>
    /// Tint the client applies to the wearer sprite while the form is active.
    /// </summary>
    [DataField]
    public Color SpiritFormColor = Color.FromHex("#00d400");

    /// <summary>
    /// Alpha the client applies together with the tint so the wearer looks translucent.
    /// </summary>
    [DataField]
    public float SpiritFormAlpha = 0.6f;

    [DataField]
    public SoundSpecifier? ActivateSound;

    [DataField]
    public SoundSpecifier? DeactivateSound;
}

/// <summary>
/// Raised when the spirit form action is used.
/// </summary>
public sealed partial class NinjaGhostSpiritFormEvent : InstantActionEvent;
