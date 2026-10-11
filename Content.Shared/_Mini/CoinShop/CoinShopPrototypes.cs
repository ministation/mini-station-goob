// SPDX-FileCopyrightText: 2026 Egorik1
// Мини-станция, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/ministation/mini-station-goob/master/LICENSE.TXT

using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using Robust.Shared.Serialization;

namespace Content.Shared._Mini.CoinShop;

[Prototype("coinCosmetic")]
public sealed partial class CoinCosmeticPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>Shop display name (raw string, following the ghost shop convention).</summary>
    [DataField(required: true)]
    public string Name = default!;

    /// <summary>Clothing entity granted on spawn (equipped to <see cref="Slot"/>, dropped at feet as fallback).</summary>
    [DataField(required: true)]
    public EntProtoId Item = default!;

    /// <summary>RSI shown in the shop card.</summary>
    [DataField(required: true)]
    public ResPath Icon = default!;

    /// <summary>Equipment slot to try first (e.g. "neck", "head").</summary>
    [DataField(required: true)]
    public string Slot = default!;

    [DataField]
    public int Price = 15;

    [DataField]
    public int Order;

    [DataField]
    public CoinRarity Rarity = CoinRarity.Common;
}

[Prototype("coinOocColor")]
public sealed partial class CoinOocColorPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>6-digit hex markup color (no alpha — breaks OOC/AHelp markup).</summary>
    [DataField(required: true)]
    public string Color = default!;

    [DataField]
    public int Price = 30;

    [DataField]
    public int Order;
}

public enum CoinRarity : byte
{
    Common = 0,
    Rare = 1,
    Epic = 2,
    Legendary = 3,
}
