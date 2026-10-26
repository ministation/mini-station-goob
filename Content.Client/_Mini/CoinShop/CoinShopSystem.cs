// SPDX-FileCopyrightText: 2026 Egorik1
// Мини-станция, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/ministation/mini-station-goob/master/LICENSE.TXT

using Content.Shared._Mini.CoinShop;
using Content.Shared._Mini.CustomGhost;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Shared.Serialization.TypeSerializers.Implementations;
using Robust.Shared.Utility;

namespace Content.Client._Mini.CoinShop;

public sealed class CoinShopSystem : EntitySystem
{
    [Dependency] private readonly IResourceCache _resourceCache = default!;
    [Dependency] private readonly SpriteSystem _sprite = default!;
    [Dependency] private readonly IUserInterfaceManager _ui = default!;

    private CoinShopWindow? _window;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<CoinShopStateEvent>(OnShopState);
        SubscribeNetworkEvent<CoinShopRollResultEvent>(OnRollResult);
    }

    public void OpenShop()
    {
        RaiseNetworkEvent(new CoinShopOpenRequestEvent());
        OpenWindow();
    }

    public void RequestBuyCosmetic(string cosmeticId) =>
        RaiseNetworkEvent(new CoinShopBuyCosmeticRequestEvent(cosmeticId));

    public void RequestSelectCosmetic(string? cosmeticId) =>
        RaiseNetworkEvent(new CoinShopSelectCosmeticRequestEvent(cosmeticId));

    public void RequestBuyOocColor(string colorId) =>
        RaiseNetworkEvent(new CoinShopBuyOocColorRequestEvent(colorId));

    public void RequestSelectOocColor(string colorId) =>
        RaiseNetworkEvent(new CoinShopSelectOocColorRequestEvent(colorId));

    public void RequestLootbox() =>
        RaiseNetworkEvent(new CoinShopLootboxRequestEvent());

    public void RequestBuyGhost(string themeId) =>
        RaiseNetworkEvent(new CoinShopBuyGhostRequestEvent(themeId));

    public void RequestSelectGhost(string? themeId) =>
        RaiseNetworkEvent(new CoinShopSelectGhostRequestEvent(themeId));

    public Texture? GetGhostIcon(string rsiPath)
    {
        // Theme folders are ordinary RSI directories: take one frame instead of the raw png, which
        // would draw the whole sheet (all directions and animation frames) on the shop card.
        var path = new ResPath(rsiPath);
        if (_resourceCache.TryGetResource<RSIResource>(SpriteSpecifierSerializer.TextureRoot / path, out var rsi)
            && rsi.RSI.TryGetState("animated", out _))
        {
            return _sprite.Frame0(new SpriteSpecifier.Rsi(path, "animated"));
        }

        foreach (var suffix in new[] { "/icon.png", "/animated.png", "/ghost.png" })
        {
            if (_resourceCache.TryGetResource<TextureResource>(
                    new ResPath(CustomGhost.GhostShopSystem.ToTexturePath(rsiPath) + suffix), out var res))
                return res.Texture;
        }

        return null;
    }

    public Texture? GetItemIcon(string rsiPath)
    {
        try
        {
            return _sprite.Frame0(new SpriteSpecifier.Rsi(new ResPath(rsiPath), "icon"));
        }
        catch
        {
            return null;
        }
    }

    private void OpenWindow()
    {
        if (_window is { IsOpen: true })
            return;

        _window?.Close();
        _window = new CoinShopWindow(this);
        _window.OpenCentered();
    }

    private void OnShopState(CoinShopStateEvent ev)
    {
        OpenWindow();
        _window!.UpdateState(ev);
    }

    private void OnRollResult(CoinShopRollResultEvent ev)
    {
        _window?.ShowRollResult(ev);
    }
}
