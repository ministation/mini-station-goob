// SPDX-FileCopyrightText: 2026 Egorik1
// Мини-станция, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/ministation/mini-station-goob/master/LICENSE.TXT

using Content.Shared.Administration;
using Robust.Shared.Console;

namespace Content.Client._Mini.CoinShop;

[AnyCommand]
public sealed class CoinShopCommand : IConsoleCommand
{
    public string Command => "coinshop";
    public string Description => "Открывает магазин за монетки.";
    public string Help => "Usage: coinshop";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        var entMan = IoCManager.Resolve<IEntityManager>();
        entMan.System<CoinShopSystem>().OpenShop();
    }
}
