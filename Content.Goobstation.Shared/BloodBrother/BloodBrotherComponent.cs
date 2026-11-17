// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Antag;
using Content.Shared.StatusIcon;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Goobstation.Shared.BloodBrother;

/// <summary>
/// Marks a mob as a member of a blood brother conspiracy.
/// Every brother starts alone with their own team and their own flash, and recruits
/// partners one by one by flashing them directly with a handheld flash.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class BloodBrotherComponent : Component, IAntagStatusIconComponent
{
    [DataField]
    public ProtoId<FactionIconPrototype> StatusIcon { get; set; } = "BloodBrotherFaction";

    [DataField]
    public bool IconVisibleToGhost { get; set; } = true;

    /// <summary>
    /// Identifier of the conspiracy this brother belongs to.
    /// Unique across every blood brother rule in the round.
    /// </summary>
    [DataField, AutoNetworkedField]
    public long TeamId;

    /// <summary>
    /// How many more people this particular brother is able to recruit.
    /// Recruiting a partner spends the charge, not a shared team pool.
    /// </summary>
    [DataField, AutoNetworkedField]
    public int ConversionsLeft;

    /// <summary>
    /// The game rule that owns this brother. Used by the server to resolve the team.
    /// </summary>
    [ViewVariables]
    public EntityUid Rule;
}
