using System.Diagnostics.CodeAnalysis;
using Robust.Shared.Serialization;

namespace Content.Shared.Genetics.Systems;

public sealed partial class DnaClientSystem : EntitySystem
{
    [Dependency] private DnaServerSystem _dnaServer = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DnaClientComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<DnaClientComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnInit(Entity<DnaClientComponent> ent, ref ComponentInit args)
    {
        // Drop map/runtime leftovers pointing at entities that no longer exist.
        if (ent.Comp.Server is { } stale && !Exists(stale))
        {
            ent.Comp.Server = null;
            ent.Comp.ConnectedToServer = false;
            Dirty(ent);
        }

        if (ent.Comp.ConnectedToServer && ent.Comp.Server is { } existing
            && TryComp<DnaServerComponent>(existing, out _))
            return;

        foreach (var server in _dnaServer.GetServers())
        {
            _dnaServer.RegisterClient((server, server.Comp), (ent, ent.Comp));
            break; // one server is enough; avoid multi-register churn
        }
    }

    private void OnShutdown(Entity<DnaClientComponent> ent, ref ComponentShutdown args)
    {
        _dnaServer.UnregisterClient((ent, ent.Comp));
    }

    public bool TryGetBufferData(Entity<DnaClientComponent?> client, int bufferIndex, [NotNullWhen(true)] out EnzymeInfo? data)
    {
        data = null;

        if (!TryGetServer(client, out var server))
            return false;

        return _dnaServer.TryGetBufferData((server.Value.Owner, server.Value.Comp), bufferIndex, out data);
    }

    public bool TryAddToBuffer(Entity<DnaClientComponent?> client, int bufferIndex, EnzymeInfo data)
    {
        if (!TryGetServer(client, out var server))
            return false;

        return _dnaServer.AddToBuffer((server.Value.Owner, server.Value.Comp), bufferIndex, data);
    }

    public bool TryAddToBufferDisk(Entity<DnaClientComponent?> client, int bufferIndex, EnzymeInfo data)
    {
        if (!TryGetServer(client, out var server))
            return false;

        return _dnaServer.AddToBufferDisk((server.Value.Owner, server.Value.Comp), bufferIndex, data);
    }

    public bool TryClearBuffer(Entity<DnaClientComponent?> client, int bufferIndex)
    {
        if (!TryGetServer(client, out var server))
            return false;

        return _dnaServer.ClearBuffer((server.Value.Owner, server.Value.Comp), bufferIndex);
    }

    public bool TryRenameBuffer(Entity<DnaClientComponent?> client, int bufferIndex, string name)
    {
        if (!TryGetServer(client, out var server))
            return false;

        return _dnaServer.RenameBuffer((server.Value.Owner, server.Value.Comp), bufferIndex, name);
    }

    public bool TryGetServer(Entity<DnaClientComponent?> client, [NotNullWhen(true)] out Entity<DnaServerComponent>? serverEnt)
    {
        serverEnt = null;

        if (!Resolve(client, ref client.Comp))
            return false;

        if (!client.Comp.ConnectedToServer || client.Comp.Server is not { } serverUid)
            return false;

        if (!TryComp<DnaServerComponent>(serverUid, out var serverComponent))
        {
            // Stale link (server deleted without a clean shutdown path).
            client.Comp.Server = null;
            client.Comp.ConnectedToServer = false;
            Dirty(client.Owner, client.Comp);
            return false;
        }

        serverEnt = (serverUid, serverComponent);
        return true;
    }
}
