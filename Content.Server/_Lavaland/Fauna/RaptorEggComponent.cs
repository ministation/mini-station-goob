// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Prototypes;

namespace Content.Server._Lavaland.Fauna;

/// <summary>
/// A raptor egg incubating towards hatching. TG parity:
/// /obj/item/food/egg/raptor_egg (progress 100, ~1/s, x1.5 on lava,
/// never hatches while sitting on lava).
/// </summary>
[RegisterComponent]
public sealed partial class RaptorEggComponent : Component
{
    /// <summary>
    /// Baby raptor this egg hatches into. Set when a raptor lays the egg.
    /// </summary>
    [DataField]
    public EntProtoId? BabyProto;

    /// <summary>
    /// Seconds of incubation needed to hatch.
    /// </summary>
    [DataField]
    public float HatchSeconds = 100f;

    /// <summary>
    /// Progress multiplier while the egg sits on lava. TG: x1.5.
    /// </summary>
    [DataField]
    public float LavaMultiplier = 1.5f;

    [ViewVariables]
    public float Progress;
}
