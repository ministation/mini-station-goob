// SPDX-FileCopyrightText: 2026 Egorik1
// Мини-станция, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/ministation/mini-station-goob/master/LICENSE.TXT

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
