// SPDX-License-Identifier: LicenseRef-MiniStation-1.0
// Mechanics reference: tgstation/tgstation (code/modules/fishing).

using Robust.Shared.Prototypes;
using Robust.Shared.GameStates;

namespace Content.Shared._Mini.Fishing.Components;

/// <summary>
/// The wall frame of a trophy mount. Using it on a wall places the <see cref="Mount"/> structure.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class FishMountItemComponent : Component
{
    [DataField]
    public EntProtoId Mount = "FishMount";
}
