// SPDX-FileCopyrightText: 2026 Mini Station
// Мини-станция, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/ministation/mini-station-goob/master/LICENSE.TXT

using Content.Shared.Actions;
using Content.Shared.Chemistry.Reagent;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Mini.Ninja.Abilities.Steel;

/// <summary>
/// Steel tier 2: the adrenaline surge of the ninja suit. The suit injects a dose of Velaxin into the
/// bloodstream of the wearer, which works like the syndicate adrenaline implant, but lasts longer and
/// hits the body with a bit of radiation.
/// The component lives on the suit, the charges are stored on the action itself.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class NinjaAdrenalComponent : Component
{
    /// <summary>
    /// Reagent injected into the bloodstream of the wearer.
    /// </summary>
    [DataField]
    public ProtoId<ReagentPrototype> VelaxinReagent = "Velaxin";

    /// <summary>
    /// Amount of Velaxin injected per use.
    /// </summary>
    [DataField]
    public float VelaxinAmount = 15f;

    /// <summary>
    /// Reagent that causes the radiation side effect.
    /// </summary>
    [DataField]
    public ProtoId<ReagentPrototype> RadiumReagent = "Radium";

    /// <summary>
    /// Amount of Radium injected per use.
    /// </summary>
    [DataField]
    public float RadiumAmount = 2.5f;
}

/// <summary>
/// Raised when the adrenaline surge action is used.
/// </summary>
public sealed partial class NinjaAdrenalEvent : InstantActionEvent;
