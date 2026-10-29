// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from tgstation/tgstation (code/modules/fishing, AGPL-3.0-or-later).

using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Goobstation.Shared.Fishing.Components;

/// <summary>
/// A wall mount that keeps a trophy fish on display. The fish lives in the <see cref="Container"/>,
/// so it is never on the map and does not dry out or die while mounted.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class FishMountComponent : Component
{
    /// <summary>
    /// Name of the container that holds the mounted fish.
    /// </summary>
    [DataField]
    public string Container = "fishMount";

    /// <summary>
    /// Wall frame item spawned when the mount is taken down with a screwdriver.
    /// </summary>
    [DataField]
    public EntProtoId Frame = "FishMountItem";
}
