using Content.Shared._Mini.CustomGhost;
using Content.Client.Resources;
using Robust.Client.Console;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations;
using Robust.Shared.Utility;

namespace Content.Client._Mini.CustomGhost;

public sealed class GhostShopSystem : EntitySystem
{
    [Dependency] private readonly IClientConsoleHost _console = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IResourceCache _resourceCache = default!;
    [Dependency] private readonly IUserInterfaceManager _ui = default!;
    [Dependency] private readonly SpriteSystem _sprite = default!;

    private GhostShopWindow? _window;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<GhostShopStateEvent>(OnShopState);
    }

    public void OpenShop()
    {
        RaiseNetworkEvent(new GhostShopOpenRequestEvent());
        _window?.Close();
            _window = new GhostShopWindow(this);
        _window.OpenCentered();
    }

    public void RequestBuy(string themeId)
    {
        RaiseNetworkEvent(new GhostShopBuyRequestEvent(themeId));
    }

    public void RequestSelect(string? themeId)
    {
        if (themeId == "GhostThemeDefault")
            themeId = null;
        RaiseNetworkEvent(new GhostShopSelectRequestEvent(themeId));
    }

    public void RequestBuyColor(string colorId)
    {
        RaiseNetworkEvent(new GhostShopBuyColorRequestEvent(colorId));
    }

    public void RequestSelectColor(string colorId)
    {
        RaiseNetworkEvent(new GhostShopSelectColorRequestEvent(colorId));
    }

    public Texture? GetIconTexture(string rsiPath, string state)
    {
        // Theme folders are ordinary RSI directories (meta.json), so take one frame of the requested
        // state. Loading the png straight from the folder draws the whole sheet: every direction and
        // animation frame at once, which looked like all the states were shown on one card.
        var path = new ResPath(rsiPath);
        if (_resourceCache.TryGetResource<RSIResource>(SpriteSpecifierSerializer.TextureRoot / path, out var rsi)
            && rsi.RSI.TryGetState(state, out _))
        {
            return _sprite.Frame0(new SpriteSpecifier.Rsi(path, state));
        }

        // Folders without RSI metadata: fall back to a bare png, preferring the single frame one.
        foreach (var suffix in new[] { "/icon.png", "/animated.png", "/ghost.png" })
        {
            if (_resourceCache.TryGetResource<TextureResource>(new ResPath(ToTexturePath(rsiPath) + suffix), out var res))
                return res.Texture;
        }

        return null;
    }

    public static string ToTexturePath(string rsiPath)
    {
        var rooted = rsiPath.StartsWith('/') ? rsiPath : "/" + rsiPath;
        return rooted.StartsWith("/Textures/") ? rooted : "/Textures" + rooted;
    }

    private void OnShopState(GhostShopStateEvent ev)
    {
        if (_window == null || !_window.IsOpen)
        {
            _window?.Close();
        _window = new GhostShopWindow(this);
            _window.OpenCentered();
        }

        _window.UpdateState(ev);
    }
}
