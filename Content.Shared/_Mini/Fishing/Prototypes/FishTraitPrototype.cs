// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from tgstation/tgstation (code/modules/fishing, AGPL-3.0-or-later).

using Content.Shared.Chemistry.Reagent;
using Robust.Shared.Prototypes;

namespace Content.Shared._Mini.Fishing.Prototypes;

/// <summary>
/// A behavioural trait of a fish. Traits may alter the minigame difficulty and how the fish behaves.
/// </summary>
[Prototype("fishTrait")]
public sealed partial class FishTraitPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>
    /// Display name shown by the fish catalog and the analyzer.
    /// </summary>
    [DataField]
    public LocId Name = default!;

    [DataField]
    public FishTraitFlags Flags = FishTraitFlags.None;

    [DataField]
    public float AddedDifficulty;

    /// <summary>
    /// Added to the idle velocity of the fish in the minigame. Demersal fish stay near the bottom.
    /// </summary>
    [DataField]
    public float IdleVelocityBonus;

    /// <summary>
    /// Multiplies the bait control speeds in the minigame. Slippery fish move the bait faster.
    /// </summary>
    [DataField]
    public float ControlSpeedMultiplier = 1f;

    [DataField]
    public LocId CatalogDescription = default!;

    /// <summary>
    /// Components added to a fish carrying this trait when it spawns, from tg apply_to_fish.
    /// </summary>
    [DataField]
    public ComponentRegistry? Components;

    /// <summary>
    /// Reagent injected when the stinger of a fish carrying this trait hits someone, from tg reagents_to_add.
    /// Only works together with the Stinger trait.
    /// </summary>
    [DataField]
    public ProtoId<ReagentPrototype>? Venom;

    /// <summary>
    /// Venom dose multiplier per unit of weight for a living fish, from tg add_venom.
    /// </summary>
    [DataField]
    public float VenomLiveMultiplier = 0.25f;

    /// <summary>
    /// Venom dose multiplier per unit of weight for a dead fish, from tg change_venom.
    /// </summary>
    [DataField]
    public float VenomDeadMultiplier = 0.1f;
}
