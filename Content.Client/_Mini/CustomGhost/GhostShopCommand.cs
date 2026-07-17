using Content.Shared.Administration;
using Robust.Shared.Console;

namespace Content.Client._Mini.CustomGhost;

[AnyCommand]
public sealed class GhostShopCommand : IConsoleCommand
{
    public string Command => "ghostshop";
    public string Description => "Opens the ghost theme shop.";
    public string Help => "Usage: ghostshop";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        var entMan = IoCManager.Resolve<IEntityManager>();
        entMan.System<GhostShopSystem>().OpenShop();
    }
}
