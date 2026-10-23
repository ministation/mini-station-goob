using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Content.Goobstation.Shared.Achievements;
using Content.Server.Database;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Goobstation.Server.Achievements;

public sealed partial class AchievementManager : IPostInjectInit
{
    [Dependency] private IServerDbManager _db = default!;
    [Dependency] private UserDbDataManager _userDb = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private ILogManager _log = default!;

    private ISawmill _sawmill = default!;

    private readonly Dictionary<NetUserId, Dictionary<string, AchievementEntry>> _players = new();
    private Dictionary<string, int> _unlockCounts = new();
    private int _playerCount;

    public event Action<ICommonSession, AchievementPrototype>? Unlocked;
    public event Action<ICommonSession>? PlayerLoaded;

    void IPostInjectInit.PostInject()
    {
        _sawmill = _log.GetSawmill("achievements");
        _userDb.AddOnLoadPlayer(LoadData);
        _userDb.AddOnFinishLoad(FinishLoad);
        _userDb.AddOnPlayerDisconnect(ClientDisconnected);
    }

    private async Task LoadData(ICommonSession session, CancellationToken cancel)
    {
        var rows = await _db.GetAchievementProgressAsync(session.UserId, cancel);
        cancel.ThrowIfCancellationRequested();

        var entries = new Dictionary<string, AchievementEntry>();
        foreach (var row in rows)
        {
            entries[row.AchievementId] = new AchievementEntry
            {
                Progress = row.Progress,
                UnlockedAt = row.UnlockedAt,
                CoinRewardPaid = row.CoinRewardPaid,
            };
        }

        _players[session.UserId] = entries;
    }

    private void FinishLoad(ICommonSession session)
    {
        PlayerLoaded?.Invoke(session);
    }

    private void ClientDisconnected(ICommonSession session)
    {
        if (!_players.Remove(session.UserId, out var entries))
            return;

        foreach (var (id, entry) in entries)
        {
            SaveEntry(session.UserId, id, entry);
        }
    }

    public bool IsLoaded(ICommonSession session)
    {
        return _players.ContainsKey(session.UserId);
    }

    public bool IsUnlocked(ICommonSession session, string id)
    {
        return _players.TryGetValue(session.UserId, out var entries)
               && entries.TryGetValue(id, out var entry)
               && entry.UnlockedAt != null;
    }

    public int CountUnlocked(ICommonSession session, ProtoId<AchievementCategoryPrototype>? category = null)
    {
        if (!_players.TryGetValue(session.UserId, out var entries))
            return 0;

        var count = 0;
        foreach (var (id, entry) in entries)
        {
            if (entry.UnlockedAt == null || !_proto.TryIndex<AchievementPrototype>(id, out var proto))
                continue;

            if (category != null && proto.Category != category)
                continue;

            count++;
        }

        return count;
    }

    public void AddProgress(ICommonSession session, AchievementPrototype proto, int amount)
    {
        if (amount <= 0 || !TryGetEntry(session, proto, out var entry) || entry.UnlockedAt != null)
            return;

        entry.Progress += amount;
        entry.PendingDelta += amount;

        if (entry.Progress >= proto.Condition.Count)
            Unlock(session, proto, entry);
    }

    public void SetDerivedProgress(ICommonSession session, AchievementPrototype proto, int value)
    {
        if (!TryGetEntry(session, proto, out var entry) || entry.UnlockedAt != null)
            return;

        entry.Progress = value;

        if (entry.Progress >= proto.Condition.Count)
            Unlock(session, proto, entry);
    }

    /// <summary>
    /// Unlocked achievements that have no recorded coin reward payout yet, paired with their unlock time.
    /// Rows unlocked before the coin reward mechanic reached the live server need a retroactive payment,
    /// rows unlocked after it were paid on unlock and only need the flag persisted.
    /// </summary>
    public List<PendingCoinReward> GetPendingCoinRewards(ICommonSession session)
    {
        var result = new List<PendingCoinReward>();
        if (!_players.TryGetValue(session.UserId, out var entries))
            return result;

        foreach (var (id, entry) in entries)
        {
            if (entry.UnlockedAt is not { } unlockedAt || entry.CoinRewardPaid)
                continue;

            if (_proto.TryIndex<AchievementPrototype>(id, out var proto))
                result.Add(new PendingCoinReward(proto, unlockedAt));
        }

        return result;
    }

    /// <summary>
    /// Records that the one-off coin reward for <paramref name="proto"/> has been credited, so it is never
    /// paid twice.
    /// </summary>
    public void MarkCoinRewardPaid(ICommonSession session, AchievementPrototype proto)
    {
        if (!_players.TryGetValue(session.UserId, out var entries)
            || !entries.TryGetValue(proto.ID, out var entry)
            || entry.CoinRewardPaid)
            return;

        entry.CoinRewardPaid = true;
        entry.PendingCoinRewardPaid = true;
        SaveEntry(session.UserId, proto.ID, entry);
    }

    private bool TryGetEntry(ICommonSession session, AchievementPrototype proto, out AchievementEntry entry)
    {
        entry = default!;
        if (!_players.TryGetValue(session.UserId, out var entries))
            return false;

        if (!entries.TryGetValue(proto.ID, out var found))
        {
            found = new AchievementEntry();
            entries[proto.ID] = found;
        }

        entry = found;
        return true;
    }

    private void Unlock(ICommonSession session, AchievementPrototype proto, AchievementEntry entry)
    {
        entry.UnlockedAt = DateTime.UtcNow;
        entry.PendingUnlock = true;

        _unlockCounts[proto.ID] = _unlockCounts.GetValueOrDefault(proto.ID) + 1;
        _sawmill.Info($"{session.Name} unlocked achievement {proto.ID}");

        // Persist the unlock before the reward listeners run, so an exception in a listener cannot swallow
        // the achievement itself.
        SaveEntry(session.UserId, proto.ID, entry);

        Unlocked?.Invoke(session, proto);

        // The reward listener credits the coins. Record that the one-off reward is handled so the
        // retroactive backfill never pays the same achievement a second time.
        entry.CoinRewardPaid = true;
        entry.PendingCoinRewardPaid = true;
        SaveEntry(session.UserId, proto.ID, entry);
    }

    public void SaveAll()
    {
        foreach (var (user, entries) in _players)
        {
            foreach (var (id, entry) in entries)
            {
                SaveEntry(user, id, entry);
            }
        }
    }

    private void SaveEntry(NetUserId user, string id, AchievementEntry entry)
    {
        var delta = entry.PendingDelta;
        var unlockedAt = entry.PendingUnlock ? entry.UnlockedAt : null;
        var coinRewardPaid = entry.PendingCoinRewardPaid;
        if (delta == 0 && unlockedAt == null && !coinRewardPaid)
            return;

        entry.PendingDelta = 0;
        entry.PendingUnlock = false;
        entry.PendingCoinRewardPaid = false;
        _ = SaveAsync(user, id, delta, unlockedAt, coinRewardPaid);
    }

    private async Task SaveAsync(NetUserId user, string id, int delta, DateTime? unlockedAt, bool coinRewardPaid)
    {
        try
        {
            await _db.AddAchievementProgressAsync(user, id, delta, unlockedAt, coinRewardPaid);
        }
        catch (Exception e)
        {
            _sawmill.Error($"Failed to save achievement {id} for {user}: {e}");
        }
    }

    public async Task RefreshStats()
    {
        try
        {
            var (players, unlocks) = await _db.GetAchievementStatsAsync();
            _playerCount = players;
            _unlockCounts = unlocks;
        }
        catch (Exception e)
        {
            _sawmill.Error($"Failed to load achievement stats: {e}");
        }
    }

    public float GetRarity(string id)
    {
        if (_playerCount <= 0)
            return 0f;

        return Math.Clamp(_unlockCounts.GetValueOrDefault(id) / (float) _playerCount, 0f, 1f);
    }

    public AchievementData BuildData(ICommonSession session, AchievementPrototype proto)
    {
        AchievementEntry? entry = null;
        if (_players.TryGetValue(session.UserId, out var entries))
            entries.TryGetValue(proto.ID, out entry);

        var unlocked = entry?.UnlockedAt != null;
        var revealed = unlocked || !proto.Hidden;

        return new AchievementData(
            proto.ID,
            revealed ? proto.Name.Id : null,
            revealed ? proto.Description.Id : null,
            revealed ? proto.Icon : null,
            proto.Category,
            Math.Min(entry?.Progress ?? 0, proto.Condition.Count),
            proto.Condition.Count,
            entry?.UnlockedAt,
            GetRarity(proto.ID),
            proto.GetRarity(),
            proto.GetCoinReward());
    }

    public List<AchievementData> BuildState(ICommonSession session)
    {
        return _proto.EnumeratePrototypes<AchievementPrototype>()
            .Select(proto => BuildData(session, proto))
            .ToList();
    }

    /// <summary>An unlocked achievement whose coin reward has not been recorded as paid yet.</summary>
    public readonly record struct PendingCoinReward(AchievementPrototype Proto, DateTime UnlockedAt);

    private sealed partial class AchievementEntry
    {
        public int Progress;
        public DateTime? UnlockedAt;
        public bool CoinRewardPaid;
        public int PendingDelta;
        public bool PendingUnlock;
        public bool PendingCoinRewardPaid;
    }
}
