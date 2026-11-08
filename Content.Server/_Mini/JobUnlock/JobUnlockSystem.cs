// SPDX-FileCopyrightText: 2026 Casha
// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

using Content.Server._Mini.AntagTokens;
using Content.Server.GameTicking.Events;
using Content.Server.Players.PlayTimeTracking;
using Content.Server.Popups;
using Content.Server.Preferences.Managers;
using Content.Shared._Mini.JobUnlock;
using Content.Shared._Mini.MiniCCVars;
using Content.Shared._Mini.RoleUnlock;
using Content.Shared.Players.PlayTimeTracking;
using Content.Shared.Preferences;
using Content.Shared.Roles;
using Robust.Server.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server._Mini.JobUnlock;

public sealed class JobUnlockSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly AntagTokenSystem _antagTokens = default!;
    [Dependency] private readonly JobUnlockListingSystem _listings = default!;
    [Dependency] private readonly RoleUnlockCostSystem _costs = default!;
    [Dependency] private readonly PlayTimeTrackingManager _tracking = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IServerNetManager _net = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly IServerPreferencesManager _preferences = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;

    public override void Initialize()
    {
        base.Initialize();

        _net.RegisterNetMessage<MsgJobUnlocks>();
        SubscribeNetworkEvent<JobUnlockPurchaseRequestEvent>(OnPurchaseRequest);
        SubscribeLocalEvent<GetDisallowedJobsEvent>(OnGetDisallowedJobs);
    }

    private void OnPurchaseRequest(JobUnlockPurchaseRequestEvent msg, EntitySessionEventArgs args)
    {
        if (args.SenderSession is not { } session)
            return;

        if (!_cfg.GetCVar(MiniCCVars.RoleUnlockEarlyPurchaseEnabled))
        {
            ShowPopup(session, Loc.GetString("job-unlock-error-unavailable"));
            return;
        }

        if (!_listings.TryGetListing(msg.JobId, out _))
        {
            ShowPopup(session, Loc.GetString("job-unlock-error-unavailable"));
            return;
        }

        if (_antagTokens.HasJobUnlock(session.UserId, msg.JobId))
        {
            ShowPopup(session, Loc.GetString("job-unlock-error-already-unlocked"));
            return;
        }

        if (!_tracking.TryGetTrackerTimes(session, out var playTimes))
            playTimes = new Dictionary<string, TimeSpan>();

        var profile = (HumanoidCharacterProfile?) _preferences.GetPreferences(session.UserId).SelectedCharacter;
        if (!_costs.TryGetJobUnlockCost(msg.JobId, playTimes, profile, out var cost))
        {
            ShowPopup(session, Loc.GetString("job-unlock-error-unavailable"));
            return;
        }

        if (!_antagTokens.TryUnlockJob(session.UserId, msg.JobId, cost, out var error))
        {
            ShowPopup(session, error ?? Loc.GetString("job-unlock-error-failed"));
            return;
        }

        ShowPopup(session, Loc.GetString("job-unlock-popup-success",
            ("job", Loc.GetString(_prototypes.Index(msg.JobId).Name))));
        SendJobUnlocks(session);
    }

    public void SendJobUnlocks(ICommonSession session)
    {
        var msg = new MsgJobUnlocks
        {
            UnlockedJobs = new HashSet<ProtoId<JobPrototype>>(_antagTokens.GetJobUnlocks(session.UserId)),
        };

        _net.ServerSendMessage(msg, session.Channel);
    }

    public void SendJobUnlocksIfOnline(NetUserId userId)
    {
        if (_playerManager.TryGetSessionById(userId, out var session))
            SendJobUnlocks(session);
    }

    private void OnGetDisallowedJobs(ref GetDisallowedJobsEvent ev)
    {
        foreach (var jobId in _antagTokens.GetJobUnlocks(ev.Player.UserId))
            ev.Jobs.Remove(jobId);
    }

    private void ShowPopup(ICommonSession session, string message)
    {
        if (session.AttachedEntity is not { } entity)
            return;

        _popup.PopupEntity(message, entity, entity);
    }
}
