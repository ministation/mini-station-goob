// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from tgstation/tgstation (code/modules/fishing, AGPL-3.0-or-later).

using Robust.Shared.Prototypes;
using Robust.Shared.GameStates;

namespace Content.Goobstation.Shared.Fishing.Components;

/// <summary>
/// The wall frame of a trophy mount. Using it on a wall places the <see cref="Mount"/> structure.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class FishMountItemComponent : Component
{
    [DataField]
    public EntProtoId Mount = "FishMount";
}
