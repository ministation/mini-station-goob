// SPDX-FileCopyrightText: 2026 Mini Station
// Мини-станция/Freaky-station, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/ministation/mini-station-goob/master/LICENSE.TXT

using System;

using Content.Shared._Mini.Fishing;
using Content.Shared.GameTicking;
using Robust.Shared.Timing;

namespace Content.Client._Mini.Fishing;

/// <summary>
/// Caches the fishing mode HUD state from server broadcasts and the final table
/// for the round end manifest.
/// </summary>
public sealed class FishingModeUiSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;

    public bool Active { get; private set; }
    public FishingModeLeader[] Leaders { get; private set; } = Array.Empty<FishingModeLeader>();
    public FishingModeLeader[] RoundEndLeaders { get; private set; } = Array.Empty<FishingModeLeader>();

    public event Action? LeadersUpdated;
    public event Action? RoundEndUpdated;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<FishingModeStatusEvent>(OnStatus);
        SubscribeNetworkEvent<FishingModeRoundEndEvent>(OnRoundEnd);
        SubscribeNetworkEvent<RoundRestartCleanupEvent>(OnRoundRestart);
    }

    public void RequestStatus()
    {
        if (!_timing.IsFirstTimePredicted)
            return;

        RaiseNetworkEvent(new FishingModeStatusRequestEvent());
    }

    private void OnStatus(FishingModeStatusEvent ev)
    {
        Active = ev.Active;
        Leaders = ev.Leaders;
        LeadersUpdated?.Invoke();
    }

    private void OnRoundEnd(FishingModeRoundEndEvent ev)
    {
        RoundEndLeaders = ev.Leaders;
        RoundEndUpdated?.Invoke();
    }

    private void OnRoundRestart(RoundRestartCleanupEvent ev)
    {
        Active = false;
        Leaders = Array.Empty<FishingModeLeader>();
        RoundEndLeaders = Array.Empty<FishingModeLeader>();
        LeadersUpdated?.Invoke();
    }
}
