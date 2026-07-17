// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Diagnostics.CodeAnalysis;
using Content.Shared._Mini.AntagUnlock;
using Content.Shared._Mini.JobUnlock;
using Content.Shared._Mini.RoleUnlock;
using Content.Shared.CCVar;
using Content.Shared.Humanoid;
using Content.Shared.Players;
using Content.Shared.Players.JobWhitelist;
using Content.Shared.Players.PlayTimeTracking;
using Content.Shared.Preferences;
using Content.Shared.Roles;
using Robust.Client;
using Robust.Client.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Client.Players.PlayTimeTracking;

public sealed class JobRequirementsManager : ISharedPlaytimeManager
{
    [Dependency] private readonly IBaseClient _client = default!;
    [Dependency] private readonly IClientNetManager _net = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IEntityManager _entManager = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;

    private readonly Dictionary<string, TimeSpan> _roles = new();
    private readonly List<ProtoId<JobPrototype>> _jobBans = new();
    private readonly List<ProtoId<AntagPrototype>> _antagBans = new();
    private readonly List<string> _jobWhitelists = new();
    private readonly HashSet<ProtoId<JobPrototype>> _jobUnlocks = new();
    private readonly HashSet<ProtoId<AntagPrototype>> _antagUnlocks = new();
    private int _donateSponsorLevel;

    private ISawmill _sawmill = default!;

    public event Action? Updated;

    public void Initialize()
    {
        _sawmill = Logger.GetSawmill("job_requirements");

        // Yeah the client manager handles role bans and playtime but the server ones are separate DEAL.
        _net.RegisterNetMessage<MsgRoleBans>(RxRoleBans);
        _net.RegisterNetMessage<MsgPlayTime>(RxPlayTime);
        _net.RegisterNetMessage<MsgJobWhitelist>(RxJobWhitelist);
        _net.RegisterNetMessage<MsgJobUnlocks>(RxJobUnlocks);
        _net.RegisterNetMessage<MsgAntagUnlocks>(RxAntagUnlocks);
        _net.RegisterNetMessage<MsgDonateSponsorLevel>(RxDonateSponsorLevel);

        _client.RunLevelChanged += ClientOnRunLevelChanged;
    }

    private void ClientOnRunLevelChanged(object? sender, RunLevelChangedEventArgs e)
    {
        if (e.NewLevel == ClientRunLevel.Initialize)
        {
            // Reset on disconnect, just in case.
            _roles.Clear();
            _jobWhitelists.Clear();
            _jobBans.Clear();
            _antagBans.Clear();
            _jobUnlocks.Clear();
            _antagUnlocks.Clear();
            _donateSponsorLevel = 0;
        }
    }

    private void RxAntagUnlocks(MsgAntagUnlocks message)
    {
        _antagUnlocks.Clear();
        _antagUnlocks.UnionWith(message.UnlockedAntags);
        Updated?.Invoke();
    }

    private void RxDonateSponsorLevel(MsgDonateSponsorLevel message)
    {
        _donateSponsorLevel = message.Level;
        Updated?.Invoke();
    }

    public bool HasDonateSponsorBypass() => _donateSponsorLevel > 0;

    public bool HasAntagUnlock(ProtoId<AntagPrototype> antagId) => _antagUnlocks.Contains(antagId);

    public bool TryGetAntagUnlockCost(ProtoId<AntagPrototype> antagId, HumanoidCharacterProfile? profile, out int cost)
    {
        cost = 0;

        var listings = _entManager.System<AntagUnlockListingSystem>();
        if (!listings.TryGetListing(antagId, out _))
            return false;

        listings.TryGetShopTokenCost(antagId, out var shopCost);
        return _entManager.System<RoleUnlockCostSystem>().TryGetAntagUnlockCost(
            antagId,
            _roles,
            profile,
            shopCost,
            out cost);
    }

    private void RxJobUnlocks(MsgJobUnlocks message)
    {
        _jobUnlocks.Clear();
        _jobUnlocks.UnionWith(message.UnlockedJobs);
        Updated?.Invoke();
    }

    public bool HasJobUnlock(ProtoId<JobPrototype> jobId) => _jobUnlocks.Contains(jobId);

    public bool TryGetJobUnlockCost(ProtoId<JobPrototype> jobId, HumanoidCharacterProfile? profile, out int cost)
    {
        cost = 0;

        if (!_entManager.System<JobUnlockListingSystem>().TryGetListing(jobId, out _))
            return false;

        return _entManager.System<RoleUnlockCostSystem>().TryGetJobUnlockCost(jobId, _roles, profile, out cost);
    }

    private void RxRoleBans(MsgRoleBans message)
    {
        _sawmill.Debug($"Received role ban info: {message.JobBans.Count} job ban entries and {message.AntagBans.Count} antag ban entries.");

        _jobBans.Clear();
        _jobBans.AddRange(message.JobBans);
        _antagBans.Clear();
        _antagBans.AddRange(message.AntagBans);
        Updated?.Invoke();
    }

    private void RxPlayTime(MsgPlayTime message)
    {
        _roles.Clear();

        // NOTE: do not assign _roles = message.Trackers due to implicit data sharing in integration tests.
        foreach (var (tracker, time) in message.Trackers)
        {
            _roles[tracker] = time;
        }

        /*var sawmill = Logger.GetSawmill("play_time");
        foreach (var (tracker, time) in _roles)
        {
            sawmill.Info($"{tracker}: {time}");
        }*/
        Updated?.Invoke();
    }

    private void RxJobWhitelist(MsgJobWhitelist message)
    {
        _jobWhitelists.Clear();
        _jobWhitelists.AddRange(message.Whitelist);
        Updated?.Invoke();
    }

    /// <summary>
    /// Check a list of job- and antag prototypes against the current player, for requirements and bans.
    /// </summary>
    /// <returns>
    /// False if any of the prototypes are banned or have unmet requirements.
    /// </returns>>
    public bool IsAllowed(
        List<ProtoId<JobPrototype>>? jobs,
        List<ProtoId<AntagPrototype>>? antags,
        HumanoidCharacterProfile? profile,
        [NotNullWhen(false)] out FormattedMessage? reason)
    {
        reason = null;

        if (antags is not null)
        {
            foreach (var proto in antags)
            {
                if (!IsAllowed(_prototypes.Index(proto), profile, out reason))
                    return false;
            }
        }

        if (jobs is not null)
        {
            foreach (var proto in jobs)
            {
                if (!IsAllowed(_prototypes.Index(proto), profile, out reason))
                    return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Check the job prototype against the current player, for requirements and bans
    /// </summary>
    public bool IsAllowed(
        JobPrototype job,
        HumanoidCharacterProfile? profile,
        [NotNullWhen(false)] out FormattedMessage? reason)
    {
        // Check the player's bans
        if (_jobBans.Contains(job.ID))
        {
            reason = FormattedMessage.FromUnformatted(Loc.GetString("role-ban"));
            return false;
        }

        // Check whitelist requirements
        if (!CheckWhitelist(job, out reason))
            return false;

        if (HasDonateSponsorBypass())
            return true;

        if (_jobUnlocks.Contains(job.ID))
            return true;

        // Check other role requirements
        var reqs = _entManager.System<SharedRoleSystem>().GetRoleRequirements(job);
        if (!CheckRoleRequirements(reqs, profile, out reason))
            return false;

        return true;
    }

    /// <summary>
    /// Check the antag prototype against the current player, for requirements and bans
    /// </summary>
    public bool IsAllowed(
        AntagPrototype antag,
        HumanoidCharacterProfile? profile,
        [NotNullWhen(false)] out FormattedMessage? reason)
    {
        // Check the player's bans
        if (_antagBans.Contains(antag.ID))
        {
            reason = FormattedMessage.FromUnformatted(Loc.GetString("role-ban"));
            return false;
        }

        // Check whitelist requirements
        if (!CheckWhitelist(antag, out reason))
            return false;

        if (HasDonateSponsorBypass())
            return true;

        if (_antagUnlocks.Contains(antag.ID))
            return true;

        // Check other role requirements
        var reqs = _entManager.System<SharedRoleSystem>().GetRoleRequirements(antag);
        if (!CheckRoleRequirements(reqs, profile, out reason))
            return false;

        return true;
    }

    // This must be private so code paths can't accidentally skip requirement overrides. Call this through IsAllowed()
    private bool CheckRoleRequirements(HashSet<JobRequirement>? requirements, HumanoidCharacterProfile? profile, [NotNullWhen(false)] out FormattedMessage? reason)
    {
        reason = null;

        if (requirements == null || !_cfg.GetCVar(CCVars.GameRoleTimers))
            return true;

        var reasons = new List<string>();
        foreach (var requirement in requirements)
        {
            if (requirement.Check(_entManager, _prototypes, profile, _roles, out var jobReason))
                continue;

            reasons.Add(jobReason.ToMarkup());
        }

        reason = reasons.Count == 0 ? null : FormattedMessage.FromMarkupOrThrow(string.Join('\n', reasons));
        return reason == null;
    }

    public bool CheckWhitelist(JobPrototype job, [NotNullWhen(false)] out FormattedMessage? reason)
    {
        reason = default;
        if (!_cfg.GetCVar(CCVars.GameRoleWhitelist))
            return true;

        if (job.Whitelisted && !_jobWhitelists.Contains(job.ID))
        {
            reason = FormattedMessage.FromUnformatted(Loc.GetString("role-not-whitelisted"));
            return false;
        }

        return true;
    }

    public bool CheckWhitelist(AntagPrototype antag, [NotNullWhen(false)] out FormattedMessage? reason)
    {
        reason = default;

        // TODO: Implement antag whitelisting.

        return true;
    }

    public TimeSpan FetchOverallPlaytime()
    {
        return _roles.TryGetValue("Overall", out var overallPlaytime) ? overallPlaytime : TimeSpan.Zero;
    }

    public IEnumerable<KeyValuePair<string, TimeSpan>> FetchPlaytimeByRoles()
    {
        var jobsToMap = _prototypes.EnumeratePrototypes<JobPrototype>();

        foreach (var job in jobsToMap)
        {
            if (_roles.TryGetValue(job.PlayTimeTracker, out var locJobName))
            {
                yield return new KeyValuePair<string, TimeSpan>(job.Name, locJobName);
            }
        }
    }

    public IReadOnlyDictionary<string, TimeSpan> GetPlayTimes(ICommonSession session)
    {
        if (session != _playerManager.LocalSession)
        {
            return new Dictionary<string, TimeSpan>();
        }

        return _roles;
    }
}