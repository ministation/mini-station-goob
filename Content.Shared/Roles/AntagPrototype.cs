// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Guidebook;
using Content.Shared.StatusIcon;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Roles;

/// <summary>
///     Describes information for a single antag.
/// </summary>
[Prototype]
public sealed partial class AntagPrototype : IPrototype
{
    // The name to group all antagonists under. Equivalent to DepartmentPrototype IDs.
    public static readonly string GroupName = "Antagonist";

    // The colour to group all antagonists using. Equivalent to DepartmentPrototype Color fields.
    public static readonly Color GroupColor = Color.Red;

    [ViewVariables]
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>
    ///     The name of this antag as displayed to players.
    /// </summary>
    [DataField("name")]
    public string Name { get; private set; } = "";

    /// <summary>
    ///     The antag's objective, shown in a tooltip in the antag preference menu or as a ghost role description.
    /// </summary>
    [DataField("objective", required: true)]
    public string Objective { get; private set; } = "";

    /// <summary>
    ///     Whether or not the antag role is one of the bad guys.
    /// </summary>
    [DataField("antagonist")]
    public bool Antagonist { get; private set; }

    /// <summary>
    ///     Whether or not the player can set the antag role in antag preferences.
    /// </summary>
    [DataField("setPreference")]
    public bool SetPreference { get; private set; }

    /// <summary>
    ///     Requirements that must be met to opt in to this antag role.
    /// </summary>
    [DataField, Access(typeof(SharedRoleSystem), Other = AccessPermissions.None)]
    public HashSet<JobRequirement>? Requirements;

    /// <summary>
    /// Optional list of guides associated with this antag. If the guides are opened, the first entry in this list
    /// will be used to select the currently selected guidebook.
    /// </summary>
    [DataField("guides")]
    public List<ProtoId<GuideEntryPrototype>>? Guides;

    /// <summary>
    ///     Localized antag name for UI. Falls back to the raw prototype value when no Fluent entry exists.
    /// </summary>
    public string LocalizedName => string.IsNullOrEmpty(Name)
        ? string.Empty
        : Loc.TryGetString(Name, out var name) ? name : Name;

    /// <summary>
    ///     Localized antag objective for UI. Falls back to the raw prototype value when no Fluent entry exists.
    /// </summary>
    public string LocalizedObjective => Loc.TryGetString(Objective, out var objective) ? objective : Objective;

    /// <summary>
    /// Lobby preference menu icon, same format as <see cref="JobPrototype.Icon"/>.
    /// </summary>
    [DataField]
    public ProtoId<JobIconPrototype> Icon { get; private set; } = "JobIconUnknown";
}