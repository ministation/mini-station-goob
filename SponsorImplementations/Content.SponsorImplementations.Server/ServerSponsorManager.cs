using System.Diagnostics.CodeAnalysis;
using Content.Corvax.Interfaces.Shared;
using Content.SponsorImplementations.Shared;
using Content.SponsorImplementations.Shared.NetMessages;
using Robust.Server.Player;
using Robust.Shared.IoC;
using Robust.Shared.Log;
using Robust.Shared.Maths;
using Robust.Shared.Network;

namespace Content.SponsorImplementations.Server;

internal sealed class ServerSponsorManager : ISharedSponsorsManager, ISponsorRecordProvider, IDisposable
{
    [Dependency] private readonly INetManager _netManager = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;

    private readonly HashSet<ISponsorDataProvider> _sponsorDataProviders = new();

    public void Initialize()
    {
        IoCManager.InjectDependencies(this);
        _netManager.RegisterNetMessage<SponsorInfoMessage>();
        _netManager.RegisterNetMessage<SponsorInfoRequiredMessage>(OnRequiredMessage);
    }

    public List<string> GetClientPrototypes()
    {
        throw new NotImplementedException();
    }

    public bool TryGetServerPrototypes(NetUserId userId, [NotNullWhen(true)] out List<string>? prototypes)
    {
        if (!TryGetInfo(userId, out var sponsorData))
        {
            prototypes = null;
            return false;
        }

        prototypes = sponsorData.Prototypes;
        return true;
    }

    public bool TryGetServerOocColor(NetUserId userId, [NotNullWhen(true)] out Color? color)
    {
        if (!TryGetInfo(userId, out var sponsorData))
        {
            color = null;
            return false;
        }

        color = sponsorData.Color;
        return color != null;
    }

    public int GetServerExtraCharSlots(NetUserId userId)
    {
        return !TryGetInfo(userId, out var sponsorData) ? 0 : sponsorData.ExtraCharSlots;
    }

    public bool HaveServerPriorityJoin(NetUserId userId)
    {
        return TryGetInfo(userId, out var sponsorData) && sponsorData.ServerPriorityJoin;
    }

    public bool TryGetTierNameForPrototype(string prototypeId, [NotNullWhen(true)] out string? tierName)
    {
        // Mini sponsor sync has prototype lists, but no tier-name mapping yet.
        tierName = null;
        return false;
    }

    public void RegisterSponsorDataProvider(ISponsorDataProvider sponsorDataProvider)
    {
        _sponsorDataProviders.Add(sponsorDataProvider);
    }

    public T GetSponsorDataProvider<T>() where T : ISponsorDataProvider
    {
        foreach (var provider in _sponsorDataProviders)
        {
            if (provider is T t)
                return t;
        }

        return default!;
    }

    public void SendSponsorInfo(INetChannel channel,ISponsorData? sponsorData)
    {
        _netManager.ServerSendMessage(new SponsorInfoMessage()
        {
            Prototypes = sponsorData?.Prototypes ?? [],
        },
            channel);
    }

    public void SendSponsorDataToClient(NetUserId id)
    {
        if(!_playerManager.TryGetSessionById(id, out var session))
            return;

        SendSponsorInfo(session.Channel, TryGetInfo(id, out var sponsorData) ? sponsorData : null);
    }

    public void Dispose()
    {
        foreach (var provider in _sponsorDataProviders)
        {
            if (provider is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }
    }

    private void OnRequiredMessage(SponsorInfoRequiredMessage message)
    {
        TryGetInfo(message.MsgChannel.UserId, out var data);
        SendSponsorInfo(message.MsgChannel, data);
    }

    private bool TryGetInfo(NetUserId netUserId, [NotNullWhen(true)] out ISponsorData? data)
    {
        data = null;

        foreach (var provider in _sponsorDataProviders)
        {
            data = provider.GetSponsorInfo(netUserId);
            return data != null;
        }

        return false;
    }
}
