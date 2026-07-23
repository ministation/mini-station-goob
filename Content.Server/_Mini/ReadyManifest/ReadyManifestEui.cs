using System.Linq;
using Content.Server.EUI;
using Content.Shared._Mini.ReadyManifest;

namespace Content.Server._Mini.ReadyManifest;

public sealed class ReadyManifestEui : BaseEui
{
    private readonly ReadyManifestSystem _readyManifestSystem;
    public readonly EntityUid? Owner;

    public ReadyManifestEui(EntityUid? owner, ReadyManifestSystem readyManifestSystem)
    {
        Owner = owner;
        _readyManifestSystem = readyManifestSystem;
    }

    public override ReadyManifestEuiState GetNewState()
    {
        var manifest = _readyManifestSystem.GetReadyManifest();
        var antags = _readyManifestSystem.GetAntagQueue();
        return new ReadyManifestEuiState(
            manifest.ToDictionary(kv => kv.Key, kv => kv.Value.ToList()),
            antags);
    }

    public override void Closed()
    {
        base.Closed();
        _readyManifestSystem.CloseEui(Player, Owner);
    }
}
