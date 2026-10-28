using Content.Server.Afk;
using Content.Server.Players.PlayTimeTracking;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Players.PlayTimeTracking;
using Robust.Shared.Player;

namespace Content.Server._Mini.CustomGhost;

/// <summary>
/// Хэллоуинское наигранное время - часы, набранные после выхода хэллоуин-апдейта,
/// а не за всё время игры. Реализовано отдельным трекером <see cref="HalloweenTracker"/>:
/// у всех игроков он стартует с нуля при выкате апдейта и тикает только пока игрок
/// жив и участвует в раунде - лобби, призраки и AFK не считаются. Пишется в ту же
/// таблицу playtime тем же механизмом автосохранения, что и остальные трекеры.
/// </summary>
public sealed class HalloweenPlaytimeSystem : EntitySystem
{
    public const string HalloweenTracker = "Halloween";

    [Dependency] private readonly PlayTimeTrackingManager _playTime = default!;
    [Dependency] private readonly IAfkManager _afk = default!;

    public override void Initialize()
    {
        base.Initialize();

        _playTime.CalcTrackers += CalcTrackers;
    }

    public override void Shutdown()
    {
        base.Shutdown();

        _playTime.CalcTrackers -= CalcTrackers;
    }

    // Те же условия активности, что у трекера Overall, но без админской поблажки:
    // админ в лобби или призраком хэллоуинское время не набирает.
    private void CalcTrackers(ICommonSession player, HashSet<string> trackers)
    {
        if (_afk.IsAfk(player))
            return;

        var attached = player.AttachedEntity;
        if (!TryComp<MobStateComponent>(attached, out var state)
            || state.CurrentState is not (MobState.Alive or MobState.Critical))
        {
            return;
        }

        trackers.Add(HalloweenTracker);
    }

    /// <summary>Наигранное время в раундах с момента хэллоуин-апдейта.</summary>
    public TimeSpan GetHalloweenPlaytime(ICommonSession session)
    {
        return _playTime.TryGetTrackerTime(session, HalloweenTracker, out var time)
            ? time ?? TimeSpan.Zero
            : TimeSpan.Zero;
    }
}
