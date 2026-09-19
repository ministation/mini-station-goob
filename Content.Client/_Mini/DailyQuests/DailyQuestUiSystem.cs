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

    private readonly List<DailyQuestEntry> _quests = new();
    private float _interpSeconds;
    private float _timerRefreshAccumulator;
    private DateTime _lastStateUtc = DateTime.UtcNow;
    private DateTime _lastUpdateUtc = DateTime.UtcNow;
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

        // Client entity systems are re-run for every predicted tick, so frameTime does
        // not accumulate in real seconds. Derive everything from the wall-clock instead.
        var nowUtc = DateTime.UtcNow;
        var realDt = (float)MaxZero(nowUtc - _lastUpdateUtc).TotalSeconds;
        _lastUpdateUtc = nowUtc;

        // Only extrapolate quest time while the server actually tracks the player.
        if (_hasActiveTimeQuest)
        {
            if (_isTracking)
                _interpSeconds = (float)MaxZero(nowUtc - _lastStateUtc).TotalSeconds;
            else
                _interpSeconds = 0f;
        }

        _timerRefreshAccumulator += realDt;
        if (_timerRefreshAccumulator < TimerRefreshInterval)
            return;

        _timerRefreshAccumulator = 0f;
        QuestsUpdated?.Invoke(_quests, _interpSeconds);
    }

    private static TimeSpan MaxZero(TimeSpan span)
    {
        return span < TimeSpan.Zero ? TimeSpan.Zero : span;
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

        _lastStateUtc = DateTime.UtcNow;
        _lastUpdateUtc = DateTime.UtcNow;
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
