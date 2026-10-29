// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from tgstation/tgstation (code/modules/fishing, AGPL-3.0-or-later).

using Content.Shared.EntityTable.EntitySelectors;
using Content.Shared.Maps;
using Robust.Shared.Prototypes;

namespace Content.Goobstation.Shared.Fishing.Prototypes;

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
}
