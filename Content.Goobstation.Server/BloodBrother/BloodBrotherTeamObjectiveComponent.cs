// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.Goobstation.Server.BloodBrother;

/// <summary>
/// Links an objective to the blood brother team that owns it.
/// Shared objectives need to know their team in order to compute progress.
/// </summary>
[RegisterComponent]
public sealed partial class BloodBrotherTeamObjectiveComponent : Component
{
    [ViewVariables]
    public BloodBrotherTeam? Team;
}
