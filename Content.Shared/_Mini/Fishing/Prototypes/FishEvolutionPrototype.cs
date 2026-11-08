// SPDX-License-Identifier: LicenseRef-MiniStation-1.0
// Mechanics reference: tgstation/tgstation (code/modules/fishing).

using Robust.Shared.Prototypes;

namespace Content.Shared._Mini.Fishing.Prototypes;

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

    /// <summary>
    /// When true this evolution happens through aquarium growth instead of breeding.
    /// </summary>
    [DataField]
    public bool Growth;

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