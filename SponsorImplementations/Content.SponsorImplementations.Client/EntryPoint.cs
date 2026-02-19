using Content.Corvax.Interfaces.Shared;
using Content.SponsorImplementations.Shared.NetMessages;
using Robust.Shared.ContentPack;
using Robust.Shared.IoC;
using Robust.Shared.Log;
using Robust.Shared.Network;

namespace Content.SponsorImplementations.Client;

public sealed class EntryPoint : GameClient
{
    public override void PreInit()
    {
        Logger.GetSawmill("SponsorImplementations").Info("Starting up");
        DependencyRegistration.Register(Dependencies);
        IoCManager.BuildGraph();
    }

    public override void PostInit()
    {
        IoCManager.Resolve<ISharedSponsorsManager>().Initialize();
        IoCManager.Resolve<INetManager>().Connected += OnConnected;
    }

    private void OnConnected(object? sender, NetChannelArgs e)
    {
        IoCManager.Resolve<INetManager>().ClientSendMessage(new SponsorInfoRequiredMessage());
    }
}
