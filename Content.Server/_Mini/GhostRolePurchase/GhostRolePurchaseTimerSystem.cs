// SPDX-FileCopyrightText: 2026 Casha
// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

using Content.Server.GameTicking;
using Content.Server.GameTicking.Events;
using Content.Shared._Mini.GhostRolePurchase;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.Server._Mini.GhostRolePurchase;
public sealed class GhostRolePurchaseTimerSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RoundStartingEvent>(OnRoundStarting);
        SubscribeLocalEvent<GhostRolePurchasedEvent>(OnGhostRolePurchased);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var query = EntityQueryEnumerator<GhostRolePurchaseTimerComponent>();
        while (query.MoveNext(out var uid, out var timer))
        {
            if (!timer.IsBlocked)
                continue;

            if (timer.TimerEndTime == null)
                continue;

            if (_timing.CurTime >= timer.TimerEndTime.Value)
            {
                timer.IsBlocked = false;
                timer.TimerEndTime = null;
                Dirty(uid, timer);

                RaiseNetworkEvent(new GhostRolePurchaseTimerUpdateEvent(TimeSpan.Zero));
            }
        }
    }

    private void OnRoundStarting(RoundStartingEvent ev)
    {
        StartTimer();
    }

    private void OnGhostRolePurchased(GhostRolePurchasedEvent ev)
    {
        StartTimer();
    }

    public void StartTimer()
    {
        var timerEntity = GetOrCreateTimerEntity();
        if (timerEntity == null)
            return;

        var timer = Comp<GhostRolePurchaseTimerComponent>(timerEntity.Value);
        timer.TimerEndTime = _timing.CurTime + timer.BlockDuration;
        timer.IsBlocked = true;
        Dirty(timerEntity.Value, timer);

        RaiseNetworkEvent(new GhostRolePurchaseTimerUpdateEvent(timer.TimerEndTime.Value));
    }

    public bool IsTimerActive()
    {
        var timerEntity = GetOrCreateTimerEntity();
        if (timerEntity == null)
            return false;

        if (!TryComp<GhostRolePurchaseTimerComponent>(timerEntity.Value, out var timer))
            return false;

        return timer.IsBlocked && timer.TimerEndTime.HasValue && _timing.CurTime < timer.TimerEndTime.Value;
    }

    public TimeSpan GetRemainingTime()
    {
        var timerEntity = GetOrCreateTimerEntity();
        if (timerEntity == null)
            return TimeSpan.Zero;

        if (!TryComp<GhostRolePurchaseTimerComponent>(timerEntity.Value, out var timer))
            return TimeSpan.Zero;

        if (!timer.IsBlocked || !timer.TimerEndTime.HasValue)
            return TimeSpan.Zero;

        var remaining = timer.TimerEndTime.Value - _timing.CurTime;
        return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
    }

    private EntityUid? GetOrCreateTimerEntity()
    {
        var query = EntityQueryEnumerator<GhostRolePurchaseTimerComponent>();
        if (query.MoveNext(out var uid, out _))
            return uid;

        var timerEntity = EntityManager.SpawnEntity(null, MapCoordinates.Nullspace);
        EnsureComp<GhostRolePurchaseTimerComponent>(timerEntity);
        return timerEntity;
    }
}
