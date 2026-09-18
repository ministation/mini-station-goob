// SPDX-FileCopyrightText: 2026 Casha
// Мини-станция/Freaky-station, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/ministation/mini-station-goob/master/LICENSE.TXT
using System;
using System.Collections.Generic;
using Content.Shared._Mini.DailyQuests;
using Content.Shared._Mini.DailyRewards;

namespace Content.Client._Mini.DailyQuests;

/// <summary>
/// Caches daily quest state from server updates and broadcasts UI refreshes.
/// </summary>
public sealed class DailyQuestUiSystem : EntitySystem
{
    private const float TimerRefreshInterval = 0.1f;
    private const float MaxTimeInterp = 5f;

    private readonly List<DailyQuestEntry> _quests = new();
    private float _interpSeconds;
    private float _timerRefreshAccumulator;
    private bool _hasActiveTimeQuest;
    private bool _hasClaimedQuestTimer;
    private bool _isTracking;

    public event Action<IReadOnlyList<DailyQuestEntry>, float>? QuestsUpdated;

    public IReadOnlyList<DailyQuestEntry> Quests => _quests;

    public float TimeInterpSeconds => _interpSeconds;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<DailyRewardStateEvent>(OnRewardState);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if ((!_hasActiveTimeQuest && !_hasClaimedQuestTimer) || _quests.Count == 0)
            return;

        // Only extrapolate quest time while the server actively tracks the player,
        // and cap it at one refresh interval so the display never drifts past the next authoritative snapshot.
        if (_hasActiveTimeQuest)
        {
            if (_isTracking)
                _interpSeconds = Math.Min(_interpSeconds + frameTime, MaxTimeInterp);
            else
                _interpSeconds = 0f;
        }
        _timerRefreshAccumulator += frameTime;
        if (_timerRefreshAccumulator < TimerRefreshInterval)
            return;

        _timerRefreshAccumulator = 0f;
        QuestsUpdated?.Invoke(_quests, _interpSeconds);
    }

    private void OnRewardState(DailyRewardStateEvent ev)
    {
        _isTracking = ev.State.IsTrackingActiveTime;
        UpdateQuests(ev.State.DailyQuests);
    }

    public void UpdateQuests(IReadOnlyList<DailyQuestEntry>? quests)
    {
        _quests.Clear();
        if (quests != null)
            _quests.AddRange(quests);

        _interpSeconds = 0;
        _hasActiveTimeQuest = false;
        _hasClaimedQuestTimer = false;
        foreach (var quest in _quests)
        {
            if (quest.IsTimeBased && !quest.IsCompleted && !quest.IsClaimed)
                _hasActiveTimeQuest = true;

            if ((quest.IsCompleted || quest.IsClaimed)
                && quest.NextQuestResetUtc is { } resetUtc
                && resetUtc > DateTime.UtcNow)
            {
                _hasClaimedQuestTimer = true;
            }
        }

        QuestsUpdated?.Invoke(_quests, 0f);
    }
}
