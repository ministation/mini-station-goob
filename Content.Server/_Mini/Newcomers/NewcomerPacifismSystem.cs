// SPDX-FileCopyrightText: 2026 Space Station 14 Contributors
//
// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

// SPDX-FileCopyrightText: 2026 Casha
// SPDX-License-Identifier: LicenseRef-MiniStation-1.0
using Content.Server.Players.PlayTimeTracking;
using Content.Shared.CombatMode.Pacification;
using Content.Shared.GameTicking;
using Content.Shared.Players.PlayTimeTracking;
using Robust.Server.Player;
using Robust.Shared.Player;

namespace Content.Server._Mini.Newcomers;

/// <summary>
/// Keeps new players pacified until they have actually played for two hours.
/// Overall playtime only ticks while the player is alive in a round, so lobby and AFK time do not count.
/// </summary>
public sealed class NewcomerPacifismSystem : EntitySystem
{
    private static readonly TimeSpan NewcomerPlaytime = TimeSpan.FromHours(2);
    private const float CheckInterval = 15f;

    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly PlayTimeTrackingManager _playTime = default!;

    private float _checkAccumulator;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawnComplete);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        _checkAccumulator += frameTime;
        if (_checkAccumulator < CheckInterval)
            return;

        _checkAccumulator = 0f;

        foreach (var session in _playerManager.Sessions)
        {
            if (session.AttachedEntity is not { } mob || !HasComp<PacifiedComponent>(mob))
                continue;

            if (GetPlaytime(session) >= NewcomerPlaytime)
                RemComp<PacifiedComponent>(mob);
        }
    }

    private void OnPlayerSpawnComplete(PlayerSpawnCompleteEvent ev)
    {
        if (GetPlaytime(ev.Player) >= NewcomerPlaytime)
            return;

        EnsureComp<PacifiedComponent>(ev.Mob);
    }

    private TimeSpan GetPlaytime(ICommonSession session)
    {
        if (!_playTime.TryGetTrackerTimes(session, out _))
            return TimeSpan.Zero;

        // Flush first: stored trackers do not include the current unflushed segment.
        _playTime.FlushTracker(session);
        return _playTime.GetPlayTimeForTracker(session, PlayTimeTrackingShared.TrackerOverall);
    }
}
