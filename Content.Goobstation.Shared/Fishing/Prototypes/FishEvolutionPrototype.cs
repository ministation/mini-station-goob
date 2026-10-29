// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from tgstation/tgstation (code/modules/fishing, AGPL-3.0-or-later).

using Robust.Shared.Prototypes;

namespace Content.Goobstation.Shared.Fishing.Prototypes;

/// <summary>
/// A possible offspring mutation of a fish inside an aquarium.
/// </summary>
[Prototype("fishEvolution")]
public sealed partial class FishEvolutionPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>
    /// Optional display name, shown in the catalog. Falls back to the evolved fish name.
    /// </summary>
    [DataField]
    public LocId? Name;

    [DataField(required: true)]
    public ProtoId<FishPrototype> EvolvesTo = default!;

    [DataField]
    public float Probability;

    [DataField]
    public float TemperatureMin;

    [DataField]
    public float TemperatureMax = float.PositiveInfinity;

    [DataField]
    public List<ProtoId<FishTraitPrototype>> NewTraits = new();

    [DataField]
    public List<ProtoId<FishTraitPrototype>> RemovedTraits = new();

    [DataField]
    public LocId? ConditionsNote;

    [DataField]
    public bool ShowResultOnWiki = true;
}