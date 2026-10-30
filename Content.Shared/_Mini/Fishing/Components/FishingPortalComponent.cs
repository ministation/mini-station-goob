// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from tgstation/tgstation (code/modules/fishing, AGPL-3.0-or-later).

using Content.Shared._Mini.Fishing.Prototypes;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Mini.Fishing.Components;

/// <summary>
/// The fish-porter 3000: a machine that lets the player fish in any known source.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(raiseAfterAutoHandleState: true)]
public sealed partial class FishingPortalComponent : Component
{
    [DataField, AutoNetworkedField]
    public ProtoId<FishingSourcePrototype>? ActiveSource;

    /// <summary>
    /// Sprite layer that is shown while the portal is connected to a source.
    /// </summary>
    [DataField]
    public string ActiveLayer = "active";
}