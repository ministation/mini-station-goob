// SPDX-FileCopyrightText: 2026 Casha
// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

using Content.Server._Mini.AntagTokens;
using Content.Server.Players.PlayTimeTracking;
using Content.Server.Popups;
using Content.Server.Preferences.Managers;
using Content.Shared._Mini.AntagUnlock;
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

namespace Content.Server._Mini.AntagUnlock;

public sealed class AntagUnlockSystem : EntitySystem
{
    [Dependency] private readonly AntagTokenSystem _antagTokens = default!;
    [Dependency] private readonly AntagUnlockListingSystem _listings = default!;
    [Dependency] private readonly RoleUnlockCostSystem _costs = default!;
    [Dependency] private readonly PlayTimeTrackingManager _tracking = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IServerNetManager _net = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly IServerPreferencesManager _preferences = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;

    public override void Initialize()
    {
        base.Initialize();

        _net.RegisterNetMessage<MsgAntagUnlocks>();
        _net.RegisterNetMessage<MsgDonateSponsorLevel>();
        SubscribeNetworkEvent<AntagUnlockPurchaseRequestEvent>(OnPurchaseRequest);
    }

    private void OnPurchaseRequest(AntagUnlockPurchaseRequestEvent msg, EntitySessionEventArgs args)
    {
        if (args.SenderSession is not { } session)
            return;

        if (!_cfg.GetCVar(MiniCCVars.RoleUnlockEarlyPurchaseEnabled))
        {
            ShowPopup(session, Loc.GetString("antag-unlock-error-unavailable"));
            return;
        }

        if (!_prototypes.TryIndex<AntagPrototype>(msg.AntagId, out var antag))
        {
            ShowPopup(session, Loc.GetString("antag-unlock-error-unavailable"));
            return;
        }

        if (!_listings.TryGetListing(antag.ID, out _))
        {
            ShowPopup(session, Loc.GetString("antag-unlock-error-unavailable"));
            return;
        }

        if (_antagTokens.HasAntagUnlock(session.UserId, antag.ID))
        {
            ShowPopup(session, Loc.GetString("antag-unlock-error-already-unlocked"));
            return;
        }

        if (!_tracking.TryGetTrackerTimes(session, out var playTimes))
            playTimes = new Dictionary<string, TimeSpan>();

        var profile = (HumanoidCharacterProfile?) _preferences.GetPreferences(session.UserId).SelectedCharacter;
        _listings.TryGetShopTokenCost(antag.ID, out var shopCost);
        if (!_costs.TryGetAntagUnlockCost(antag.ID, playTimes, profile, shopCost, out var cost))
        {
            ShowPopup(session, Loc.GetString("antag-unlock-error-unavailable"));
            return;
        }

        if (!_antagTokens.TryUnlockAntag(session.UserId, antag.ID, cost, out var error))
        {
            ShowPopup(session, error ?? Loc.GetString("antag-unlock-error-failed"));
            return;
        }

        ShowPopup(session, Loc.GetString("antag-unlock-popup-success",
            ("antag", Loc.GetString(antag.Name))));
        SendAntagUnlocks(session);
    }

    public void SendAntagUnlocks(ICommonSession session)
    {
        var msg = new MsgAntagUnlocks
        {
            UnlockedAntags = new HashSet<ProtoId<AntagPrototype>>(_antagTokens.GetAntagUnlocks(session.UserId)),
        };

        _net.ServerSendMessage(msg, session.Channel);
    }

    public void SendAntagUnlocksIfOnline(NetUserId userId)
    {
        if (_playerManager.TryGetSessionById(userId, out var session))
            SendAntagUnlocks(session);
    }

    public void SendDonateSponsorLevel(ICommonSession session)
    {
        var msg = new MsgDonateSponsorLevel
        {
            Level = _antagTokens.GetEffectiveSponsorLevel(session.UserId),
        };

        _net.ServerSendMessage(msg, session.Channel);
    }

    public void SendDonateSponsorLevelIfOnline(NetUserId userId)
    {
        if (_playerManager.TryGetSessionById(userId, out var session))
            SendDonateSponsorLevel(session);
    }

    public void SendRoleBypassState(ICommonSession session)
    {
        SendDonateSponsorLevel(session);
        SendAntagUnlocks(session);
    }

    public void SendRoleBypassStateIfOnline(NetUserId userId)
    {
        if (_playerManager.TryGetSessionById(userId, out var session))
            SendRoleBypassState(session);
    }

    private void ShowPopup(ICommonSession session, string message)
    {
        if (session.AttachedEntity is not { } entity)
            return;

        _popup.PopupEntity(message, entity, entity);
    }
}
