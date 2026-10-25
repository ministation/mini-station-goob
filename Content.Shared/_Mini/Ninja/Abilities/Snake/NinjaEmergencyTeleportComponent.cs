// SPDX-FileCopyrightText: 2026 Mini Station
// Мини-станция, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/ministation/mini-station-goob/master/LICENSE.TXT

using System.Numerics;
using Content.Shared.Actions;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;

namespace Content.Shared._Mini.Ninja.Abilities.Snake;

/// <summary>
/// Snake tier 3: emergency teleport. Lives on the suit. The teleport itself is server-authoritative,
/// because picking a random safe position can not be predicted.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class NinjaEmergencyTeleportComponent : Component
{
    /// <summary>
    /// X is the minimum distance, Y is the maximum distance of the teleport, in tiles.
    /// </summary>
    [DataField]
    public Vector2 TeleportRadius = new(10f, 15f);

    /// <summary>
    /// Sound played to everyone around when the ninja teleports.
    /// </summary>
    [DataField]
    public SoundSpecifier? TeleportSound = new SoundPathSpecifier("/Audio/Effects/teleport_arrival.ogg");

    /// <summary>
    /// Suit charge spent per teleport.
    /// </summary>
    [DataField]
    public float EnergyCost = 120f;
}

/// <summary>
/// Raised when the emergency teleport action is used.
/// </summary>
public sealed partial class NinjaEmergencyTeleportEvent : InstantActionEvent;
