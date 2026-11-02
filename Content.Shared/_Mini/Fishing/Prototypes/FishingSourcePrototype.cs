// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from tgstation/tgstation (code/modules/fishing, AGPL-3.0-or-later).

using Content.Shared._Mini.Research.Prototypes;
using Content.Shared.EntityTable.EntitySelectors;
using Content.Shared.Maps;
using Robust.Shared.Prototypes;

namespace Content.Shared._Mini.Fishing.Prototypes;

/// <summary>
/// A place where fish can be caught: a water turf, a chasm, a toilet, an aquarium, a rift and so on.
/// Sources are referenced by <see cref="Components.FishingSourceComponent"/> on entities and by
/// <see cref="Tiles"/> for plain tiles.
/// </summary>
[Prototype("fishingSource")]
public sealed partial class FishingSourcePrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>
    /// Weighted loot table. Use a <c>NoneSelector</c> entry for a dud roll.
    /// </summary>
    [DataField(required: true)]
    public EntityTableSelector Table = default!;

    [DataField]
    public LocId? CatalogDescription;

    /// <summary>
    /// Short name shown in the fish-porter radial menu. Falls back to
    /// <see cref="CatalogDescription"/> when it is not set.
    /// </summary>
    [DataField]
    public LocId? RadialName;

    /// <summary>
    /// Can be picked as a destination of the fish-porter, like tg portal sources.
    /// </summary>
    [DataField]
    public bool PortalDestination;

    /// <summary>
    /// Needs an emag on the fish-porter to show up, like the syndicate dimension in tg.
    /// </summary>
    [DataField]
    public bool RequiresEmag;

    /// <summary>
    /// Research experiment that has to be completed before the fish-porter offers this dimension,
    /// like the fish scanning experiments in tg.
    /// </summary>
    [DataField]
    public ProtoId<ResearchExperimentPrototype>? RequiredExperiment;

    /// <summary>
    /// Base difficulty of the minigame at this source.
    /// </summary>
    [DataField]
    public float Difficulty = 15f;

    [DataField]
    public float WaitTimeMin = 3f;

    [DataField]
    public float WaitTimeMax = 25f;

    /// <summary>
    /// Background state from the fishing HUD RSI.
    /// </summary>
    [DataField]
    public string Background = "background_default";

    /// <summary>
    /// Tiles that automatically host this source.
    /// </summary>
    [DataField]
    public List<ProtoId<ContentTileDefinition>> Tiles = new();

    [DataField]
    public FishingSourceFlags Flags = FishingSourceFlags.None;

    /// <summary>
    /// If set, the loot is built from the entity the spot is attached to instead of the table.
    /// </summary>
    [DataField]
    public FishingDynamicSource Dynamic = FishingDynamicSource.None;

    /// <summary>
    /// Limited amount of each catch for this source. Missing keys are unlimited.
    /// </summary>
    [DataField]
    public Dictionary<EntProtoId, int> FishCounts = new();

    /// <summary>
    /// Seconds it takes to regenerate one limited catch, per catch prototype.
    /// </summary>
    [DataField]
    public Dictionary<EntProtoId, float> FishCountRegen = new();

    /// <summary>
    /// Icon state from the fishing radial RSI, shown by the fish-porter machine.
    /// </summary>
    [DataField]
    public string RadialState = "default";
}