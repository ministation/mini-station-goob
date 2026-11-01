// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from tgstation/tgstation (code/modules/fishing, AGPL-3.0-or-later).

using Content.Shared._Mini.Fishing.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Shared._Mini.Fishing.Components;

/// <summary>
/// Which fish an artificial lure can catch. Every set field has to match; a fish that does not
/// match never bites the lure, like tg's <c>is_catchable_fish</c> returning false.
/// </summary>
[DataDefinition]
public sealed partial class FishingLureFilter
{
    /// <summary>
    /// Average length of the fish in centimetres, same thresholds as tg (tiny 30, small 50, normal 80).
    /// </summary>
    [DataField]
    public float MinSize;

    [DataField]
    public float MaxSize = float.MaxValue;

    /// <summary>
    /// Traits the fish needs all of, for example <see cref="FishTraitFlags.Predator"/>.
    /// </summary>
    [DataField]
    public FishTraitFlags RequiredTraits;

    /// <summary>
    /// Traits the fish needs at least one of, for example nocturnal or heavy for the LED lure.
    /// </summary>
    [DataField]
    public FishTraitFlags RequiredAnyTraits;

    /// <summary>
    /// Traits the fish must not have.
    /// </summary>
    [DataField]
    public FishTraitFlags ExcludedTraits;

    /// <summary>
    /// Fluid types the fish lives in. Null means any water. Air fish only match when amphibious,
    /// like tg checks before allowing an air fish to bite a lure.
    /// </summary>
    [DataField]
    public List<AquariumFluid>? Fluids;

    /// <summary>
    /// Catches the fish of the toilet and moisture trap sources and the disgustingly ugly ones.
    /// </summary>
    [DataField]
    public bool JunkFish;

    /// <summary>
    /// Weight multiplier of a matching fish. tg gives the lure's fish ten times the weight.
    /// </summary>
    [DataField]
    public float WeightMultiplier = 10f;
}
