// SPDX-FileCopyrightText: 2026 Mini Station
// Мини-станция, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/ministation/mini-station-goob/master/LICENSE.TXT

using Content.Shared.Actions;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Mini.Ninja.Abilities.Snake;

/// <summary>
/// Snake tier 4: energy caltrops. Lives on the suit and scatters a row of traps behind the ninja.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class NinjaCaltropComponent : Component
{
    /// <summary>
    /// Prototype of the trap that is spawned.
    /// </summary>
    [DataField]
    public EntProtoId CaltropProto = "NinjaCaltrop";

    /// <summary>
    /// Suit charge spent per placement.
    /// </summary>
    [DataField]
    public float EnergyCost = 100f;

    /// <summary>
    /// How far behind the ninja the trap line is placed, in tiles.
    /// </summary>
    [DataField]
    public float SpawnDistance = 1.5f;

    /// <summary>
    /// Lateral step between the traps of the line, in tiles.
    /// </summary>
    [DataField]
    public int SideStep = 1;

    /// <summary>
    /// How many traps are placed on each side of the center one. 1 means three traps in total.
    /// </summary>
    [DataField]
    public int SideCount = 1;
}

/// <summary>
/// Raised when the energy caltrop action is used.
/// </summary>
public sealed partial class NinjaCaltropAbilityEvent : InstantActionEvent;
