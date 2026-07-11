using Robust.Shared.Prototypes;

namespace Content.Shared._Mini.TypanWar;

/// <summary>
/// Fills a crate using independent probability rolls from a <see cref="TypanWarSurplusLootPrototype"/> table.
/// </summary>
[RegisterComponent]
public sealed partial class TypanWarSurplusBundleComponent : Component
{
    [DataField(required: true)]
    public ProtoId<TypanWarSurplusLootPrototype> LootTable;

    /// <summary>Spawned when every roll fails (very unlikely with high-probability medkits).</summary>
    [DataField]
    public EntProtoId FallbackItem = "MedkitFilled";
}
