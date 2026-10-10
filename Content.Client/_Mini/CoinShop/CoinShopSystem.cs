// SPDX-FileCopyrightText: 2026 Egorik1
// Мини-станция, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/ministation/mini-station-goob/master/LICENSE.TXT

using Content.Shared._Mini.CoinShop;
using Robust.Client.UserInterface;

namespace Content.Client._Mini.CoinShop;

public sealed class CoinShopSystem : EntitySystem
{
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

    public void RequestLootbox() =>
        RaiseNetworkEvent(new CoinShopLootboxRequestEvent());

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
