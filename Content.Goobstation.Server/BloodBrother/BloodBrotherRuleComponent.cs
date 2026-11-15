// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Random;
using Robust.Shared.Prototypes;

namespace Content.Goobstation.Server.BloodBrother;

/// <summary>
/// Game rule for the blood brothers team antagonist.
/// Every selected player starts as a lone conspirator with their own team and their own flash.
/// A brother recruits a partner by flashing them, and the conspiracy can grow up to
/// <see cref="MaxTeamSize"/> members.
/// </summary>
[RegisterComponent]
public sealed partial class BloodBrotherRuleComponent : Component
{
    /// <summary>
    /// How many people a freshly spawned brother can recruit with a flash.
    /// </summary>
    [DataField]
    public int StartingConversions = 1;

    /// <summary>
    /// Chance that a recruited brother receives their own flash and can recruit one more person.
    /// </summary>
    [DataField]
    public float PartnerFlashChance = 0.05f;

    /// <summary>
    /// Maximum number of brothers in a single conspiracy, the founder included.
    /// </summary>
    [DataField]
    public int MaxTeamSize = 3;

    /// <summary>
    /// Weighted objective group used to pick the shared objectives.
    /// </summary>
    [DataField]
    public ProtoId<WeightedRandomPrototype> ObjectiveGroup = "BloodBrotherObjectiveGroups";

    /// <summary>
    /// Maximum difficulty of a randomly picked objective.
    /// </summary>
    [DataField]
    public float MaxObjectiveDifficulty = 4f;

    /// <summary>
    /// How many random objectives a team gets on top of the convert objective.
    /// </summary>
    [DataField]
    public int ObjectiveAmount = 2;

    /// <summary>
    /// Chance that the team gets a hijack objective instead of an escape one.
    /// </summary>
    [DataField]
    public float HijackChance = 0.15f;

    /// <summary>
    /// Next local team number, used to build unique team identifiers.
    /// </summary>
    [ViewVariables]
    public int NextTeamId = 1;

    /// <summary>
    /// All conspiracies created by this rule.
    /// </summary>
    [ViewVariables]
    public List<BloodBrotherTeam> Teams = new();

    /// <summary>
    /// Fast lookup of a team by the mind of one of its members.
    /// </summary>
    [ViewVariables]
    public Dictionary<EntityUid, BloodBrotherTeam> TeamByMind = new();
}
