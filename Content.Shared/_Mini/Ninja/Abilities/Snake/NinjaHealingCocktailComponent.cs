// SPDX-FileCopyrightText: 2026 Mini Station
// Мини-станция, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/ministation/mini-station-goob/master/LICENSE.TXT

using Content.Shared.Actions;
using Content.Goobstation.Maths.FixedPoint;
using Content.Shared.Chemistry.Reagent;
using Robust.Shared.GameStates;
using Robust.Shared.Localization;
using Robust.Shared.Prototypes;

namespace Content.Shared._Mini.Ninja.Abilities.Snake;

/// <summary>
/// Snake tier 2: the healing cocktail. Lives on the suit and injects the chiyurizine reagent into the wearer.
/// The action itself keeps its charges and the cooldown, the component only holds the payload.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class NinjaHealingCocktailComponent : Component
{
    /// <summary>
    /// Reagent injected into the bloodstream of the wearer.
    /// </summary>
    [DataField]
    public ProtoId<ReagentPrototype> Reagent = "Chiyurizine";

    /// <summary>
    /// Amount of the reagent injected per use.
    /// </summary>
    [DataField]
    public FixedPoint2 Quantity = 20;

    /// <summary>
    /// Popup shown to the wearer after the injection.
    /// </summary>
    [DataField]
    public LocId? Popup = "ninja-inject-healing-cocktail";
}

/// <summary>
/// Raised when the healing cocktail action is used.
/// </summary>
public sealed partial class NinjaHealingCocktailEvent : InstantActionEvent;
