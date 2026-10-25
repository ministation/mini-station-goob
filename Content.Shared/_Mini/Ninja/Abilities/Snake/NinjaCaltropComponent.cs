// SPDX-FileCopyrightText: 2026 Mini Station
// Мини-станция, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/ministation/mini-station-goob/master/LICENSE.TXT

using Content.Shared.Actions;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Mini.Ninja.Abilities.Snake;

/// <summary>
/// Snake tier 4: energy caltrops. One use places a single permanent trap behind the ninja,
/// and the third trap of the same batch puts the ability on cooldown.
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
    /// Suit charge spent per placed trap.
    /// </summary>
    [DataField]
    public float EnergyCost = 50f;

    /// <summary>
    /// How far from the ninja the trap is placed, in tiles.
    /// </summary>
    [DataField]
    public float SpawnDistance = 1.5f;

    /// <summary>
    /// Maximum number of active traps on one station.
    /// </summary>
    [DataField]
    public int MaxTraps = 3;

    /// <summary>
    /// Cooldown of the ability after the third trap of the same batch. The batch is reset when it ends.
    /// </summary>
    [DataField]
    public TimeSpan RechargeDelay = TimeSpan.FromSeconds(50);

    /// <summary>
    /// Traps placed since the last recharge. Server only.
    /// </summary>
    [ViewVariables]
    public int PlacedInBatch;

    /// <summary>
    /// When the current batch is reset. Server only.
    /// </summary>
    [ViewVariables]
    public TimeSpan RechargeEnds;
}

/// <summary>
/// Marker of a placed energy caltrop. The trap is single use: it burns out right after it triggers.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class NinjaCaltropTrapComponent : Component
{
}

/// <summary>
/// Raised when the energy caltrop action is used.
/// </summary>
public sealed partial class NinjaCaltropAbilityEvent : InstantActionEvent;
