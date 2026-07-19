// SPDX-FileCopyrightText: 2026 Egorik1
// Мини-станция, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/ministation/mini-station-goob/master/LICENSE.TXT

using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;

namespace Content.Shared._Mini.TypanWar;

[Prototype]
public sealed partial class TypanWarSurplusLootPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField]
    public List<TypanWarSurplusLootEntry> Entries = new();
}

[DataDefinition]
public sealed partial class TypanWarSurplusLootEntry
{
    [DataField(required: true)]
    public EntProtoId Item = default!;

    /// <summary>Independent roll chance per crate open (0–1).</summary>
    [DataField(required: true)]
    public float Probability;

    /// <summary>
    /// When set, the roll chance lerps from <see cref="Probability"/> at war start
    /// to this value at war end (used for rare late-round rewards).
    /// </summary>
    [DataField]
    public float? LateRoundProbability;

    /// <summary>Minimum copies spawned when this entry succeeds (inclusive).</summary>
    [DataField]
    public int CountMin = 1;

    /// <summary>Maximum copies spawned when this entry succeeds (inclusive).</summary>
    [DataField]
    public int CountMax = 1;
}
