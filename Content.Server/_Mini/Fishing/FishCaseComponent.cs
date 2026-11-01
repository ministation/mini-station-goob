// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from tgstation/tgstation (code/modules/fishing, AGPL-3.0-or-later).

using Content.Shared._Mini.Fishing;
using Content.Shared._Mini.Fishing.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Server._Mini.Fishing;

/// <summary>
/// A stasis fish case that comes with one random fish inside, like tg fish cases.
/// </summary>
[RegisterComponent]
public sealed partial class FishCaseComponent : Component
{
    /// <summary>
    /// Water type the random fish must live in. Null means any water fish.
    /// </summary>
    [DataField]
    public AquariumFluid? Fluid;

    /// <summary>
    /// Explicit candidates, repeated entries weigh more.
    /// </summary>
    [DataField]
    public List<ProtoId<FishPrototype>> Fish = new();

    /// <summary>
    /// The fish inside is already dead, like on the black market case.
    /// </summary>
    [DataField]
    public bool Dead;
}
