// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using Robust.Shared.Prototypes;

namespace Content.Server._Lavaland.BossMechanics;

/// <summary>
/// TG parity: when the wendigo dies for real, it leaves a one-way red portal at the arena
/// spawn point so the victors can leave without climbing over the crowd.
/// </summary>
[RegisterComponent]
public sealed partial class WendigoExitPortalComponent : Component
{
    /// <summary>
    /// The portal entity spawned at the arena origin.
    /// </summary>
    [DataField]
    public EntProtoId PortalProto = "LavalandWendigoExitPortal";

    /// <summary>
    /// The invisible link target placed outside the arena patch (arena radius is 7).
    /// </summary>
    [DataField]
    public EntProtoId ExitProto = "LavalandWendigoExitPoint";

    /// <summary>
    /// Offset from the arena origin for the exit point.
    /// </summary>
    [DataField]
    public Vector2 ExitOffset = new(0f, -10f);
}
