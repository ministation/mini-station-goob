using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;

namespace Content.Shared._Mini.TypanWar;

[Prototype("typanWarSurplusLoot")]
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
}
