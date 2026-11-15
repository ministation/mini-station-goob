// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.Goobstation.Server.BloodBrother;

/// <summary>
/// Server-side data of a single blood brother conspiracy.
/// Teams do not have their own entity; they live inside <see cref="BloodBrotherRuleComponent"/>.
/// A team is created for every brother the rule spawns and grows only through flash conversions.
/// </summary>
public sealed class BloodBrotherTeam
{
    /// <summary>
    /// Identifier of the team. Unique across every blood brother rule in the round,
    /// so two conspiracies can never mistake each other for their own.
    /// </summary>
    public long Id;

    /// <summary>
    /// Minds of all brothers in this team, including converted ones.
    /// The first mind is the founder whose objectives everyone shares.
    /// </summary>
    public List<EntityUid> Minds = new();

    /// <summary>
    /// Objectives shared by every member of the team.
    /// </summary>
    public List<EntityUid> Objectives = new();
}
