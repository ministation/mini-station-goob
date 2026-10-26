using Robust.Shared.Serialization;

namespace Content.Shared._Mini.CustomGhost;

[Serializable, NetSerializable]
public sealed class GhostShopOpenRequestEvent : EntityEventArgs
{
}

[Serializable, NetSerializable]
public sealed class GhostShopBuyRequestEvent : EntityEventArgs
{
    public string ThemeId { get; }

    public GhostShopBuyRequestEvent(string themeId)
    {
        ThemeId = themeId;
    }
}

[Serializable, NetSerializable]
public sealed class GhostShopSelectRequestEvent : EntityEventArgs
{
    public string? ThemeId { get; }

    public GhostShopSelectRequestEvent(string? themeId)
    {
        ThemeId = themeId;
    }
}

[Serializable, NetSerializable]
public sealed class GhostShopStateEvent : EntityEventArgs
{
    public int Balance { get; }
    public List<GhostThemeEntry> Themes { get; }
    public List<GhostColorEntry> Colors { get; }

    /// <summary>Непустой текст — ошибка последнего действия, показывается в окне магазина.</summary>
    public string? Error { get; }

    public GhostShopStateEvent(int balance, List<GhostThemeEntry> themes)
    {
        Balance = balance;
        Themes = themes;
        Colors = new List<GhostColorEntry>();
    }

    public GhostShopStateEvent(int balance, List<GhostThemeEntry> themes, List<GhostColorEntry> colors, string? error = null)
    {
        Balance = balance;
        Themes = themes;
        Colors = colors;
        Error = error;
    }
}

[Serializable, NetSerializable]
public sealed class GhostColorEntry(
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

[Serializable, NetSerializable]
public sealed class GhostShopBuyColorRequestEvent(string colorId) : EntityEventArgs
{
    public string ColorId { get; } = colorId;
}

/// <summary>Switch to an owned OOC color for free.</summary>
[Serializable, NetSerializable]
public sealed class GhostShopSelectColorRequestEvent(string colorId) : EntityEventArgs
{
    public string ColorId { get; } = colorId;
}

[Serializable, NetSerializable]
public sealed class GhostThemeEntry
{
    public string Id { get; }
    public string Name { get; }
    public string Description { get; }
    public int Price { get; }
    /// <summary>Больше нуля — покупка за наигранные часы, а не за монеты.</summary>
    public int PlaytimeHours { get; }
    public bool Owned { get; }
    public bool Selected { get; }
    public string IconRsiPath { get; }
    public string IconRsiState { get; }

    public GhostThemeEntry(string id, string name, string description, int price, int playtimeHours, bool owned, bool selected, string iconRsiPath, string iconRsiState)
    {
        Id = id;
        Name = name;
        Description = description;
        Price = price;
        PlaytimeHours = playtimeHours;
        Owned = owned;
        Selected = selected;
        IconRsiPath = iconRsiPath;
        IconRsiState = iconRsiState;
    }
}
