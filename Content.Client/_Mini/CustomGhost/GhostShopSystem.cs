using Content.Shared._Mini.CustomGhost;
using Content.Client.Resources;
using Robust.Client.Console;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Shared.Prototypes;
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

    public Texture? GetIconTexture(string rsiPath, string state)
    {
        // Most theme folders ship a bare png without rsi.json — try textures FIRST so the
        // RSI loader never logs "Failed to load RSI" for them.
        foreach (var suffix in new[] { "/icon.png", "/animated.png", "/ghost.png" })
        {
            foreach (var prefix in new[] { "", "/Textures/" })
            {
                try
                {
                    return _resourceCache.GetTexture(new ResPath(prefix + rsiPath + suffix));
                }
                catch
                {
                    // try next
                }
            }
        }

        try
        {
            return _sprite.Frame0(new SpriteSpecifier.Rsi(new ResPath(rsiPath), state));
        }
        catch
        {
            return null;
        }
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
