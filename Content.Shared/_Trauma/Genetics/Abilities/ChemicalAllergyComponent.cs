// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Chemistry.Reagent;
using Robust.Shared.Prototypes;

namespace Content.Shared._Trauma.Genetics.Abilities;

/// <summary>
/// Mutation that makes the mutated mob allergic to most medicine and drugs in its bloodstream.
/// While an offending reagent is being metabolized the mob periodically suffers
/// tg-like symptoms (toxin damage, stutter, jitter, dizziness, blood loss, ...).
/// </summary>
/// <remarks>
/// Behaviour lives in <c>Content.Server._Trauma.Genetics.Abilities.ChemicalAllergySystem</c>.
/// </remarks>
[RegisterComponent, NetworkedComponent]
public sealed partial class ChemicalAllergyComponent : Component
{
    /// <summary>
    /// Reagent <see cref="ReagentPrototype.Group">groups</see> that trigger the allergy.
    /// </summary>
    [DataField]
    public List<string> ReagentGroups = new()
    {
        "Medicine",
        "Narcotics",
    };

    /// <summary>
    /// Reagents ignored even if they belong to one of <see cref="ReagentGroups"/>.
    /// Mirrors tg's exclusion of mannitol and mutadone. Mutadone does not exist in this
    /// codebase, so only Mannitol is listed by default.
    /// </summary>
    [DataField]
    public List<ProtoId<ReagentPrototype>> ExcludedReagents = new()
    {
        "Mannitol",
    };

    /// <summary>
    /// Minimum amount of an offending reagent in the bloodstream for the allergy to trigger.
    /// </summary>
    [DataField]
    public float MinimumQuantity = 1f;

    /// <summary>
    /// How often the allergy rolls for symptoms while an offending reagent is present.
    /// tg uses <c>SPT_PROB</c> values, which are chances per second.
    /// </summary>
    [DataField]
    public TimeSpan UpdateInterval = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Chance per second to actually experience symptoms while an offending reagent is present.
    /// </summary>
    [DataField]
    public float SymptomChance = 0.8f;

    /// <summary>
    /// Server-only time until the next symptom roll.
    /// </summary>
    [ViewVariables]
    public TimeSpan NextUpdate;
}
