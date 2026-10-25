// SPDX-FileCopyrightText: 2026 Mini Station
// Мини-станция, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/ministation/mini-station-goob/master/LICENSE.TXT

using Content.Shared.Actions;
using Robust.Shared.GameStates;

namespace Content.Shared._Mini.Ninja.Abilities.Steel;

/// <summary>
/// Steel tier 3: the electromagnetic pulse of the ninja suit. The pulse is centered on the suit and
/// does not hurt the ninja, but disables nearby technology. Lives on the suit.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class NinjaEmpAbilityComponent : Component
{
    /// <summary>
    /// Suit battery charge spent by the pulse.
    /// </summary>
    [DataField]
    public float Charge = 180f;

    /// <summary>
    /// Radius of the pulse, in tiles.
    /// </summary>
    [DataField]
    public float EmpRange = 6f;

    /// <summary>
    /// Energy of the pulse in joules, taken from every affected battery.
    /// </summary>
    [DataField]
    public float EmpEnergyConsumption = 100000f;

    /// <summary>
    /// How long the EMP effects last on the affected devices.
    /// </summary>
    [DataField]
    public TimeSpan EmpDuration = TimeSpan.FromSeconds(60);
}

/// <summary>
/// Raised when the EMP action is used.
/// </summary>
public sealed partial class NinjaEmpAbilityEvent : InstantActionEvent;
