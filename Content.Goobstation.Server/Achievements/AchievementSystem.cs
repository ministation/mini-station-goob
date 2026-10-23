using System.Diagnostics.CodeAnalysis;
using Content.Goobstation.Common.CCVar;
using Content.Goobstation.Shared.Achievements;
using Content.Server._Mini.AntagTokens;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Events;
using Content.Server.Maps;
using Content.Server.Popups;
using Content.Shared.EntityConditions;
using Content.Shared.GameTicking;
using Content.Shared.Ghost;
using Content.Shared.Mind;
using Content.Shared.Projectiles;
using Content.Shared.Roles;
using Content.Shared.Roles.Components;
using Robust.Server.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Goobstation.Server.Achievements;

public sealed partial class AchievementSystem : EntitySystem
{
    [Dependency] private AchievementManager _manager = default!;
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IGameMapManager _gameMap = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private GameTicker _ticker = default!;
    [Dependency] private SharedMindSystem _mind = default!;
    [Dependency] private SharedRoleSystem _roles = default!;
    [Dependency] private SharedEntityConditionsSystem _conditions = default!;
    [Dependency] private PopupSystem _popup = default!;
    [Dependency] private AntagTokenSystem _antagTokens = default!;

    private bool _enabled;
    private bool _roundExcluded;
    private TimeSpan _saveInterval;
    private TimeSpan _nextSave;

    public event Action<ICommonSession>? SessionLoaded;
    public event Action<ICommonSession, AchievementPrototype>? AchievementUnlocked;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<RequestAchievementsEvent>(OnRequest);
        SubscribeLocalEvent<RoundStartingEvent>(OnRoundStarting);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);

        Subs.CVar(_cfg, GoobCVars.AchievementsEnabled, value => _enabled = value, true);
        Subs.CVar(_cfg, GoobCVars.AchievementsSaveInterval, value => _saveInterval = TimeSpan.FromSeconds(value), true);

        _manager.Unlocked += OnUnlocked;
        _manager.PlayerLoaded += OnPlayerLoaded;
        _ = _manager.RefreshStats();
    }

    public override void Shutdown()
    {
        base.Shutdown();

        _manager.Unlocked -= OnUnlocked;
        _manager.PlayerLoaded -= OnPlayerLoaded;
        _manager.SaveAll();
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_timing.CurTime < _nextSave)
            return;

        _nextSave = _timing.CurTime + _saveInterval;
        _manager.SaveAll();
    }

    private void OnRequest(RequestAchievementsEvent ev, EntitySessionEventArgs args)
    {
        RaiseNetworkEvent(new AchievementsStateEvent(_manager.BuildState(args.SenderSession)), args.SenderSession);
    }

    private void OnRoundStarting(RoundStartingEvent ev)
    {
        var presets = _cfg.GetCVar(GoobCVars.AchievementsExcludedPresets).Split(',', StringSplitOptions.TrimEntries);
        var maps = _cfg.GetCVar(GoobCVars.AchievementsExcludedMaps).Split(',', StringSplitOptions.TrimEntries);

        _roundExcluded = (_ticker.CurrentPreset ?? _ticker.Preset) is { } preset && Array.IndexOf(presets, preset.ID) >= 0
                         || _gameMap.GetSelectedMap() is { } map && Array.IndexOf(maps, map.ID) >= 0;

        _ = _manager.RefreshStats();
    }

    private void OnRoundRestart(RoundRestartCleanupEvent ev)
    {
        _manager.SaveAll();
    }

    private void OnUnlocked(ICommonSession session, AchievementPrototype proto)
    {
        RaiseNetworkEvent(new AchievementUnlockedEvent(_manager.BuildData(session, proto)), session);

        // The reward goes into the same coin wallet as weekly quests. AddBalance also applies the
        // monthly coin cap, so the granted amount can be lower than the achievement's reward.
        var reward = proto.GetCoinReward();
        if (reward > 0
            && _antagTokens.AddBalance(session.UserId, reward, out var granted, out var note)
            && granted > 0)
        {
            var message = Loc.GetString("achievement-coin-reward-popup",
                ("amount", granted),
                ("achievement", Loc.GetString(proto.Name)));

            if (!string.IsNullOrWhiteSpace(note))
                message = $"{message} {note}";

            if (session.AttachedEntity is { Valid: true } uid)
                _popup.PopupEntity(message, uid, uid);
        }

        AchievementUnlocked?.Invoke(session, proto);
    }

    private void OnPlayerLoaded(ICommonSession session)
    {
        SessionLoaded?.Invoke(session);
        BackfillCoinRewards(session);
    }

    /// <summary>
    /// Moment the live server switched from a build that had achievements but no coin rewards to the
    /// build that pays them out (2026-10-21 17:18:26 MSK, the update to bb539936a9).
    /// Achievements unlocked on the earlier build never received coins, so they are paid retroactively.
    /// </summary>
    private static readonly DateTime CoinRewardBackfillCutoff = new(2026, 10, 21, 14, 18, 26, DateTimeKind.Utc);

    /// <summary>
    /// Pays the coin rewards that were missed because an achievement was unlocked before the reward
    /// mechanic was deployed, then records the payout so it never happens twice. Unlocks created after
    /// the cutoff were already paid on unlock, only their missing flag is persisted here.
    /// </summary>
    private void BackfillCoinRewards(ICommonSession session)
    {
        if (!_enabled)
            return;

        var pending = _manager.GetPendingCoinRewards(session);
        if (pending.Count == 0)
            return;

        var total = 0;
        foreach (var (proto, unlockedAt) in pending)
        {
            // Unlocks after the cutoff were already paid when they unlocked, so only the flag is persisted.
            if (unlockedAt >= CoinRewardBackfillCutoff)
            {
                _manager.MarkCoinRewardPaid(session, proto);
                continue;
            }

            var reward = proto.GetCoinReward();
            if (reward <= 0)
            {
                _manager.MarkCoinRewardPaid(session, proto);
                continue;
            }

            // Never pay an achievement partially: a partial payout could not be recorded per achievement and
            // would be paid a second time on the next login. Wait until the whole reward fits under the
            // monthly cap; the achievement stays pending and is retried on a later load.
            if (_antagTokens.GetRemainingMonthlyReward(session.UserId) < reward)
                continue;

            if (!_antagTokens.AddBalance(session.UserId, reward, out var granted, out _)
                || granted != reward)
                continue;

            total += granted;
            _manager.MarkCoinRewardPaid(session, proto);
        }

        if (total <= 0)
            return;

        var message = Loc.GetString("achievement-coin-reward-backfill", ("amount", total));

        if (session.AttachedEntity is { Valid: true } uid)
            _popup.PopupEntity(message, uid, uid);
    }

    public EntityUid ResolveAttacker(EntityUid origin)
    {
        return TryComp<ProjectileComponent>(origin, out var projectile) && projectile.Shooter is { } shooter
            ? shooter
            : origin;
    }

    public bool TryGetSession(EntityUid player, [NotNullWhen(true)] out ICommonSession? session)
    {
        session = null;
        return _mind.TryGetMind(player, out _, out var mind)
               && _player.TryGetSessionById(mind.UserId, out session);
    }

    public bool CanProgress(EntityUid player, [NotNullWhen(true)] out ICommonSession? session)
    {
        session = null;

        if (!_enabled
            || _roundExcluded
            || HasComp<GhostComponent>(player)
            || !_mind.TryGetMind(player, out var mindId, out var mind)
            || !_player.TryGetSessionById(mind.UserId, out session))
            return false;

        return _roles.MindHasRole<JobRoleComponent>(mindId)
               || _roles.MindHasRole<GhostRoleMarkerRoleComponent>(mindId)
               || _roles.MindIsAntagonist(mindId);
    }

    public bool CanProgress(ICommonSession session)
    {
        return _enabled && _manager.IsLoaded(session);
    }

    public void AddProgress(EntityUid player, AchievementPrototype proto, int amount = 1)
    {
        if (!CanProgress(player, out var session)
            || proto.Condition.Requirements is { } requirements && !_conditions.TryConditions(player, requirements))
            return;

        _manager.AddProgress(session, proto, amount);
    }

    public void AddProgress(ICommonSession session, AchievementPrototype proto, int amount = 1)
    {
        if (!CanProgress(session) || _roundExcluded)
            return;

        _manager.AddProgress(session, proto, amount);
    }

    public void SetDerivedProgress(ICommonSession session, AchievementPrototype proto, int value)
    {
        if (!CanProgress(session))
            return;

        _manager.SetDerivedProgress(session, proto, value);
    }

    public bool IsUnlocked(ICommonSession session, AchievementPrototype proto)
    {
        return _manager.IsUnlocked(session, proto.ID);
    }

    public int CountUnlocked(ICommonSession session, ProtoId<AchievementCategoryPrototype>? category = null)
    {
        return _manager.CountUnlocked(session, category);
    }
}
