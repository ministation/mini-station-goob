// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

using Robust.Shared.Prototypes;

namespace Content.Server._Lavaland.BossMechanics;

/// <summary>
/// Leaves burning ground along the path of this megafauna's dashes,
/// so the dash keeps threatening the ground it crossed instead of being a single hit.
/// </summary>
[RegisterComponent]
public sealed partial class MegafaunaFireTrailComponent : Component
{
    /// <summary>
    /// Entity that is spawned on every tile of the dash path.
    /// </summary>
    [DataField(required: true)]
    public EntProtoId Trail;

    /// <summary>
    /// Distance between two trail tiles, in tiles.
    /// </summary>
    [DataField]
    public float Step = 1f;
}
