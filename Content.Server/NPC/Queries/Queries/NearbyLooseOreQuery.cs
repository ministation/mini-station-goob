// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Tag;
using Robust.Shared.Prototypes;

namespace Content.Server.NPC.Queries.Queries;

/// <summary>
/// Returns loose stacks of ore lying on the floor, so a mob can walk over and eat them.
/// </summary>
public sealed partial class NearbyLooseOreQuery : UtilityQuery
{
    /// <summary>
    /// Tag an entity needs to be considered ore.
    /// </summary>
    [DataField]
    public ProtoId<TagPrototype> Tag = "Ore";
}
