using Content.Shared.Eui;
using Content.Shared.Roles;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Mini.ReadyManifest;

[Serializable, NetSerializable]
public sealed class RequestReadyManifestMessage : EntityEventArgs;

[Serializable, NetSerializable]
public sealed class ReadyManifestAntagEntry
{
    public string RoleId { get; }
    public string RoleName { get; }
    public int Cost { get; }
    public int QueuePosition { get; }

    public ReadyManifestAntagEntry(string roleId, string roleName, int cost, int queuePosition)
    {
        RoleId = roleId;
        RoleName = roleName;
        Cost = cost;
        QueuePosition = queuePosition;
    }
}

[Serializable, NetSerializable]
public sealed class ReadyManifestEuiState : EuiStateBase
{
    public Dictionary<ProtoId<JobPrototype>, List<string>> JobCharacters { get; }
    public List<ReadyManifestAntagEntry> AntagQueue { get; }

    public ReadyManifestEuiState(
        Dictionary<ProtoId<JobPrototype>, List<string>> jobCharacters,
        List<ReadyManifestAntagEntry> antagQueue)
    {
        JobCharacters = jobCharacters;
        AntagQueue = antagQueue;
    }
}

/// <summary>
/// Raised when lobby antag-token deposits change so ready-manifest EUIs can refresh.
/// </summary>
public sealed class AntagTokenQueueChangedEvent : EntityEventArgs;
