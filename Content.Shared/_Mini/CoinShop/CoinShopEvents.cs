// SPDX-FileCopyrightText: 2026 Egorik1
// Мини-станция, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/ministation/mini-station-goob/master/LICENSE.TXT

using Robust.Shared.Serialization;

namespace Content.Shared._Mini.CoinShop;

[Serializable, NetSerializable]
public sealed class CoinShopOpenRequestEvent : EntityEventArgs
{
}

[Serializable, NetSerializable]
public sealed class CoinShopBuyCosmeticRequestEvent(string cosmeticId) : EntityEventArgs
{
    public string CosmeticId { get; } = cosmeticId;
}

[Serializable, NetSerializable]
public sealed class CoinShopBuyOocColorRequestEvent(string colorId) : EntityEventArgs
{
    public string ColorId { get; } = colorId;
}

/// <summary>Pick an owned cosmetic to carry every round (null = carry nothing).</summary>
[Serializable, NetSerializable]
public sealed class CoinShopSelectCosmeticRequestEvent(string? cosmeticId) : EntityEventArgs
{
    public string? CosmeticId { get; } = cosmeticId;
}

[Serializable, NetSerializable]
public sealed class CoinShopLootboxRequestEvent : EntityEventArgs
{
}

[Serializable, NetSerializable]
public sealed class CoinShopStateEvent(
    int balance,
    List<CoinShopItemEntry> cosmetics,
    List<CoinShopColorEntry> colors,
    string? selectedCosmetic,
    List<CoinShopGhostEntry> ghosts,
    string? selectedGhost) : EntityEventArgs
{
    public int Balance { get; } = balance;
    public List<CoinShopItemEntry> Cosmetics { get; } = cosmetics;
    public List<CoinShopColorEntry> Colors { get; } = colors;
    public string? SelectedCosmetic { get; } = selectedCosmetic;
    public List<CoinShopGhostEntry> Ghosts { get; } = ghosts;
    public string? SelectedGhost { get; } = selectedGhost;
}

/// <summary>A ghost theme from the CustomGhost catalog, purchasable with coins.</summary>
[Serializable, NetSerializable]
public sealed class CoinShopGhostEntry(
    string id,
    string name,
    string description,
    int price,
    bool owned,
    bool selected,
    string iconRsiPath)
{
    public string Id { get; } = id;
    public string Name { get; } = name;
    public string Description { get; } = description;
    public int Price { get; } = price;
    public bool Owned { get; } = owned;
    public bool Selected { get; } = selected;
    public string IconRsiPath { get; } = iconRsiPath;
}

[Serializable, NetSerializable]
public sealed class CoinShopBuyGhostRequestEvent(string themeId) : EntityEventArgs
{
    public string ThemeId { get; } = themeId;
}

[Serializable, NetSerializable]
public sealed class CoinShopSelectGhostRequestEvent(string? themeId) : EntityEventArgs
{
    public string? ThemeId { get; } = themeId;
}

[Serializable, NetSerializable]
public sealed class CoinShopItemEntry(
    string id,
    string name,
    int price,
    CoinRarity rarity,
    bool owned)
{
    public string Id { get; } = id;
    public string Name { get; } = name;
    public int Price { get; } = price;
    public CoinRarity Rarity { get; } = rarity;
    public bool Owned { get; } = owned;
}

[Serializable, NetSerializable]
public sealed class CoinShopColorEntry(
    string id,
    string colorHex,
    int price,
    bool owned,
    bool active)
{
    public string Id { get; } = id;
    public string ColorHex { get; } = colorHex;
    public int Price { get; } = price;
    public bool Owned { get; } = owned;
    public bool Active { get; } = active;
}

/// <summary>Lootbox roll outcome for the client popup.</summary>
[Serializable, NetSerializable]
public sealed class CoinShopRollResultEvent(
    string itemName,
    CoinRarity rarity,
    int refund,
    bool duplicate) : EntityEventArgs
{
    public string ItemName { get; } = itemName;
    public CoinRarity Rarity { get; } = rarity;
    public int Refund { get; } = refund;
    public bool Duplicate { get; } = duplicate;
}
