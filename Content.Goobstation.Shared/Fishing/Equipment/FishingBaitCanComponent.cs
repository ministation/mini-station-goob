// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from tgstation/tgstation (code/modules/fishing, AGPL-3.0-or-later).

using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Goobstation.Shared.Fishing.Components;

/// <summary>
/// A can that hands out a limited amount of bait.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(raiseAfterAutoHandleState: true)]
public sealed partial class FishingBaitCanComponent : Component
{
    [DataField]
    public EntProtoId BaitPrototype = "BaitWorm";

    [DataField, AutoNetworkedField]
    public int MaxUses = 20;

    [DataField, AutoNetworkedField]
    public int UsesLeft = 20;

    /// <summary>
    /// Time between two bait retrievals, in seconds.
    /// </summary>
    [DataField]
    public float Cooldown = 5f;

    [ViewVariables]
    public TimeSpan NextUse;
}
