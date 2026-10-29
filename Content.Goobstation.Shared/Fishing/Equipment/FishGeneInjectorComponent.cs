// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from tgstation/tgstation (code/modules/fishing, AGPL-3.0-or-later).

using Content.Goobstation.Shared.Fishing.Prototypes;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Goobstation.Shared.Fishing.Components;

/// <summary>
/// Single use injector that carries one fish trait.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(raiseAfterAutoHandleState: true)]
public sealed partial class FishGeneInjectorComponent : Component
{
    /// <summary>
    /// Trait stored in the injector, null for an injector that was never filled.
    /// </summary>
    [DataField, AutoNetworkedField]
    public ProtoId<FishTraitPrototype>? Trait;
}
