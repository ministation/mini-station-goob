// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from tgstation/tgstation (code/modules/fishing, AGPL-3.0-or-later).

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

    [DataField]
    public LocId CatalogDescription = default!;
}