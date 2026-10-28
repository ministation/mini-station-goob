using Content.Server._Mini.CustomGhost;
using Content.Server.Players.PlayTimeTracking;
using Content.Shared.Players.PlayTimeTracking;
using Robust.Shared.Player;

namespace Content.Goobstation.Server.Achievements.Conditions;

/// <summary>
/// Условие «наиграй N часов после выхода хэллоуин-апдейта». Считает не общий playtime,
/// а трекер <see cref="HalloweenPlaytimeSystem"/>: только живое участие в раундах.
/// </summary>
public sealed partial class HalloweenPlaytimeCondition : AchievementCondition;

public sealed partial class HalloweenPlaytimeConditionSystem : AchievementConditionSystem<HalloweenPlaytimeCondition>
{
    [Dependency] private PlayTimeTrackingManager _playTime = default!;

    public override void Initialize()
    {
        base.Initialize();

        _playTime.SessionPlayTimeUpdated += CheckSession;
        Achievements.SessionLoaded += CheckSession;
    }

    public override void Shutdown()
    {
        base.Shutdown();

        _playTime.SessionPlayTimeUpdated -= CheckSession;
        Achievements.SessionLoaded -= CheckSession;
    }

    private void CheckSession(ICommonSession session)
    {
        if (!_playTime.TryGetTrackerTimes(session, out var times))
            return;

        var hours = (int) times.GetValueOrDefault(HalloweenPlaytimeSystem.HalloweenTracker).TotalHours;

        foreach (var (proto, _) in Entries)
            Achievements.SetDerivedProgress(session, proto, hours);
    }
}
