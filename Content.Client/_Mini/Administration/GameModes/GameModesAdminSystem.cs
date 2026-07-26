// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._Mini.Administration.GameModes;

namespace Content.Client._Mini.Administration.GameModes;

public sealed class GameModesAdminSystem : EntitySystem
{
    public GameModesAdminStateResponse? LastState { get; private set; }
    public event Action? OnStateUpdated;
    public event Action<GameModesAdminActionResult>? OnActionResult;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<GameModesAdminStateResponse>(OnState);
        SubscribeNetworkEvent<GameModesAdminActionResult>(OnResult);
    }

    private void OnState(GameModesAdminStateResponse msg, EntitySessionEventArgs args)
    {
        LastState = msg;
        OnStateUpdated?.Invoke();
    }

    private void OnResult(GameModesAdminActionResult msg, EntitySessionEventArgs args)
    {
        OnActionResult?.Invoke(msg);
    }

    public void RequestState() => RaiseNetworkEvent(new RequestGameModesAdminStateMessage());

    public void ForcePreset(string presetId) => RaiseNetworkEvent(new GameModesForcePresetMessage(presetId));

    public void SetPreset(string presetId) => RaiseNetworkEvent(new GameModesSetPresetMessage(presetId));

    public void StartPresetVote() => RaiseNetworkEvent(new GameModesStartPresetVoteMessage());

    public void SetVoteEnabled(bool enabled) => RaiseNetworkEvent(new GameModesSetVoteEnabledMessage(enabled));
}
