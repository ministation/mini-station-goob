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

    public GhostShopStateEvent(int balance, List<GhostThemeEntry> themes)
    {
        Balance = balance;
        Themes = themes;
        Colors = new List<GhostColorEntry>();
    }

    public GhostShopStateEvent(int balance, List<GhostThemeEntry> themes, List<GhostColorEntry> colors)
    {
        Balance = balance;
        Themes = themes;
        Colors = colors;
    }
}

[Serializable, NetSerializable]
public sealed class GhostColorEntry(
    string id,
    string colorHex,
    int price,
    bool active)
{
    public string Id { get; } = id;
    public string ColorHex { get; } = colorHex;
    public int Price { get; } = price;
    public bool Active { get; } = active;
}

[Serializable, NetSerializable]
public sealed class GhostShopBuyColorRequestEvent(string colorId) : EntityEventArgs
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
    public bool Owned { get; }
    public bool Selected { get; }
    public string IconRsiPath { get; }
    public string IconRsiState { get; }

    public GhostThemeEntry(string id, string name, string description, int price, bool owned, bool selected, string iconRsiPath, string iconRsiState)
    {
        Id = id;
        Name = name;
        Description = description;
        Price = price;
        Owned = owned;
        Selected = selected;
        IconRsiPath = iconRsiPath;
        IconRsiState = iconRsiState;
    }
}
