// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from tgstation/tgstation (code/modules/fishing, AGPL-3.0-or-later).

using Content.Shared._Mini.Fishing.Components;
using Content.Shared._Mini.Fishing.Prototypes;
using Content.Shared._Mini.Fishing.Systems;
using Robust.Shared.Prototypes;

namespace Content.Shared._Mini.Fishing.Traits;

/// <summary>
/// Trait lookups and the numbers behind the fish trait effects. The effects themselves run server side.
/// </summary>
public sealed partial class SharedFishTraitSystem : EntitySystem
{
    /// <summary>
    /// tg FISH_GRIND_RESULTS_WEIGHT_DIVISOR, the weight a venom dose is based on.
    /// </summary>
    public const float VenomWeightDivisor = 40f;

    /// <summary>
    /// tg SHADOW_SPECIES_LIGHT_THRESHOLD, brighter than this counts as bright light.
    /// </summary>
    public const float BrightLightThreshold = 0.2f;

    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private FishSystem _fish = default!;

    /// <summary>
    /// Whether any trait of this fish instance carries any of the given flags.
    /// </summary>
    public bool HasTrait(FishComponent fish, FishTraitFlags flags)
    {
        return GetTrait(fish, flags) != null;
    }

    /// <summary>
    /// First trait of this fish instance that carries any of the given flags.
    /// </summary>
    public FishTraitPrototype? GetTrait(FishComponent fish, FishTraitFlags flags)
    {
        foreach (var id in _fish.GetTraits(fish))
        {
            if (_proto.TryIndex(id, out FishTraitPrototype? trait) && (trait.Flags & flags) != 0)
                return trait;
        }

        return null;
    }

    /// <summary>
    /// Venom dose a stinger delivers per hit, scaled by weight like tg add_venom.
    /// </summary>
    public static float GetVenomDose(FishTraitPrototype trait, float weight, bool alive)
    {
        var mult = alive ? trait.VenomLiveMultiplier : trait.VenomDeadMultiplier;
        return MathF.Max(MathF.Round(weight / VenomWeightDivisor * mult * 10f) / 10f, mult);
    }
}
