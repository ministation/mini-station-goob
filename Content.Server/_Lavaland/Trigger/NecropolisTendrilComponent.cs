// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

using Robust.Shared.Prototypes;

namespace Content.Server._Lavaland.Trigger;

/// <summary>
///     TG parity for the necropolis tendril: it lashes back at melee attackers
///     and infects the rock around it with necropolis walls when it dies.
/// </summary>
[RegisterComponent]
public sealed partial class NecropolisTendrilComponent : Component
{
    /// <summary>
    ///     Action performed by the tendril when it is hit in melee. Its cooldown gates the retaliation.
    /// </summary>
    [DataField]
    public EntProtoId RetaliateAction = "ActionTendrilLash";

    /// <summary>
    ///     Wall spawned in place of the rock around the tendril when it is destroyed.
    /// </summary>
    [DataField]
    public EntProtoId InfectionWall = "WallNecropolisIndestructible";

    /// <summary>
    ///     Radius in tiles of the infected ring around the tendril.
    /// </summary>
    [DataField]
    public int InfectionRadius = 2;
}
