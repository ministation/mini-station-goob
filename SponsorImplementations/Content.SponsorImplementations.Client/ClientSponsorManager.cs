using System.Diagnostics.CodeAnalysis;
using Content.Corvax.Interfaces.Shared;
using Content.SponsorImplementations.Shared.NetMessages;
using Robust.Client.UserInterface;
using Robust.Shared.IoC;
using Robust.Shared.Log;
using Robust.Shared.Maths;
using Robust.Shared.Network;

namespace Content.SponsorImplementations.Client;

internal sealed class ClientSponsorManager : ISharedSponsorsManager, ISponsorUpdateInvoker
{
    [Dependency] private readonly INetManager _netManager = default!;

    public Action<List<string>>? OnSponsorInfoUpdated { get; set; }

    private List<string> _clientPrototypes = new();
    private ISawmill _logger = default!;

    public void Initialize()
    {
        IoCManager.InjectDependencies(this);
        _logger = Logger.GetSawmill("ClientSponsorManager");

        _netManager.RegisterNetMessage<SponsorInfoRequiredMessage>();
        _netManager.RegisterNetMessage<SponsorInfoMessage>(OnReceiveSponsorInfo);
    }

    private void OnReceiveSponsorInfo(SponsorInfoMessage message)
    {
        _logger.Info($"Received Sponsor Info. Count: {message.Prototypes.Count}");
        _clientPrototypes = message.Prototypes;

        OnSponsorInfoUpdated?.Invoke(message.Prototypes);
    }

    public List<string> GetClientPrototypes()
    {
        return _clientPrototypes;
    }

    public bool TryGetServerPrototypes(NetUserId userId, [NotNullWhen(true)] out List<string>? prototypes)
    {
        prototypes = new List<string>(); //mini
            return false; //mini
    }

    public bool TryGetServerOocColor(NetUserId userId, [NotNullWhen(true)] out Color? color)
    {
        throw new NotImplementedException();
    }

    public int GetServerExtraCharSlots(NetUserId userId)
    {
        throw new NotImplementedException();
    }

    public bool HaveServerPriorityJoin(NetUserId userId)
    {
        throw new NotImplementedException();
    }

    public bool TryGetTierNameForPrototype(string prototypeId, [NotNullWhen(true)] out string? tierName)
    {
        tierName = null;
        return false;
    }
}

public interface ISponsorUpdateInvoker
{
    public Action<List<string>>? OnSponsorInfoUpdated {get; set;}
}
