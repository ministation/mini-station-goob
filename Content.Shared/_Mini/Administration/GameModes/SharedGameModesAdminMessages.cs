// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Serialization;

namespace Content.Shared._Mini.Administration.GameModes;

[Serializable, NetSerializable]
public sealed class RequestGameModesAdminStateMessage : EntityEventArgs;

[Serializable, NetSerializable]
public sealed class GameModesAdminStateResponse : EntityEventArgs
{
    public string? CurrentPresetId { get; }
    public string? CurrentPresetName { get; }
    public bool IsLobby { get; }
    public bool VotePresetEnabled { get; }
    public List<GameModePresetInfo> Presets { get; }

    public GameModesAdminStateResponse(
        string? currentPresetId,
        string? currentPresetName,
        bool isLobby,
        bool votePresetEnabled,
        List<GameModePresetInfo> presets)
    {
        CurrentPresetId = currentPresetId;
        CurrentPresetName = currentPresetName;
        IsLobby = isLobby;
        VotePresetEnabled = votePresetEnabled;
        Presets = presets;
    }
}

[Serializable, NetSerializable]
public sealed class GameModePresetInfo
{
    public string Id { get; }
    public string Name { get; }
    public string Description { get; }
    public bool ShowInVote { get; }
    public int? MinPlayers { get; }
    public int? MaxPlayers { get; }

    public GameModePresetInfo(
        string id,
        string name,
        string description,
        bool showInVote,
        int? minPlayers,
        int? maxPlayers)
    {
        Id = id;
        Name = name;
        Description = description;
        ShowInVote = showInVote;
        MinPlayers = minPlayers;
        MaxPlayers = maxPlayers;
    }
}

[Serializable, NetSerializable]
public sealed class GameModesForcePresetMessage : EntityEventArgs
{
    public string PresetId { get; }

    public GameModesForcePresetMessage(string presetId)
    {
        PresetId = presetId;
    }
}

[Serializable, NetSerializable]
public sealed class GameModesSetPresetMessage : EntityEventArgs
{
    public string PresetId { get; }

    public GameModesSetPresetMessage(string presetId)
    {
        PresetId = presetId;
    }
}

[Serializable, NetSerializable]
public sealed class GameModesStartPresetVoteMessage : EntityEventArgs;

[Serializable, NetSerializable]
public sealed class GameModesSetVoteEnabledMessage : EntityEventArgs
{
    public bool Enabled { get; }

    public GameModesSetVoteEnabledMessage(bool enabled)
    {
        Enabled = enabled;
    }
}

[Serializable, NetSerializable]
public sealed class GameModesAdminActionResult : EntityEventArgs
{
    public bool Success { get; }
    public string Message { get; }

    public GameModesAdminActionResult(bool success, string message)
    {
        Success = success;
        Message = message;
    }
}
