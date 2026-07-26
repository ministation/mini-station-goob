// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using Content.Server.Administration.Managers;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Presets;
using Content.Server.Voting.Managers;
using Content.Shared._Mini.Administration.GameModes;
using Content.Shared.Administration;
using Content.Shared.CCVar;
using Content.Shared.Voting;
using Robust.Shared.Configuration;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server._Mini.Administration.GameModes;

public sealed class GameModesAdminSystem : EntitySystem
{
    [Dependency] private readonly IAdminManager _admin = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly IVoteManager _votes = default!;
    [Dependency] private readonly GameTicker _ticker = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<RequestGameModesAdminStateMessage>(OnRequestState);
        SubscribeNetworkEvent<GameModesForcePresetMessage>(OnForcePreset);
        SubscribeNetworkEvent<GameModesSetPresetMessage>(OnSetPreset);
        SubscribeNetworkEvent<GameModesStartPresetVoteMessage>(OnStartVote);
        SubscribeNetworkEvent<GameModesSetVoteEnabledMessage>(OnSetVoteEnabled);
    }

    private bool TryAuthorize(ICommonSession session, AdminFlags flags, out string error)
    {
        error = string.Empty;
        if (!_admin.IsAdmin(session) || !_admin.HasAdminFlag(session, flags))
        {
            error = Loc.GetString("game-modes-admin-denied");
            return false;
        }

        return true;
    }

    private void OnRequestState(RequestGameModesAdminStateMessage msg, EntitySessionEventArgs args)
    {
        if (!TryAuthorize(args.SenderSession, AdminFlags.Admin, out var error))
        {
            RaiseNetworkEvent(new GameModesAdminActionResult(false, error), args.SenderSession.Channel);
            return;
        }

        SendState(args.SenderSession);
    }

    private void SendState(ICommonSession session)
    {
        var presets = _prototypes.EnumeratePrototypes<GamePresetPrototype>()
            .OrderBy(p => Loc.GetString(p.ModeTitle))
            .Select(p => new GameModePresetInfo(
                p.ID,
                Loc.GetString(p.ModeTitle),
                Loc.GetString(p.Description),
                p.ShowInVote,
                p.MinPlayers,
                p.MaxPlayers))
            .ToList();

        var current = _ticker.Preset;
        var response = new GameModesAdminStateResponse(
            current?.ID,
            current != null ? Loc.GetString(current.ModeTitle) : null,
            _ticker.RunLevel == GameRunLevel.PreRoundLobby,
            _cfg.GetCVar(CCVars.VotePresetEnabled),
            presets);

        RaiseNetworkEvent(response, session.Channel);
    }

    private void OnForcePreset(GameModesForcePresetMessage msg, EntitySessionEventArgs args)
    {
        if (!TryAuthorize(args.SenderSession, AdminFlags.Round, out var error))
        {
            RaiseNetworkEvent(new GameModesAdminActionResult(false, error), args.SenderSession.Channel);
            return;
        }

        if (_ticker.RunLevel != GameRunLevel.PreRoundLobby)
        {
            RaiseNetworkEvent(new GameModesAdminActionResult(false, Loc.GetString("game-modes-admin-lobby-only")),
                args.SenderSession.Channel);
            return;
        }

        if (!_ticker.TryFindGamePreset(msg.PresetId, out var preset))
        {
            RaiseNetworkEvent(new GameModesAdminActionResult(false, Loc.GetString("game-modes-admin-preset-missing")),
                args.SenderSession.Channel);
            return;
        }

        _ticker.SetGamePreset(preset, true);
        _ticker.UpdateInfoText();
        RaiseNetworkEvent(new GameModesAdminActionResult(true, Loc.GetString("game-modes-admin-force-ok", ("preset", preset.ID))),
            args.SenderSession.Channel);
        SendState(args.SenderSession);
    }

    private void OnSetPreset(GameModesSetPresetMessage msg, EntitySessionEventArgs args)
    {
        if (!TryAuthorize(args.SenderSession, AdminFlags.Round, out var error))
        {
            RaiseNetworkEvent(new GameModesAdminActionResult(false, error), args.SenderSession.Channel);
            return;
        }

        if (!_ticker.TryFindGamePreset(msg.PresetId, out var preset))
        {
            RaiseNetworkEvent(new GameModesAdminActionResult(false, Loc.GetString("game-modes-admin-preset-missing")),
                args.SenderSession.Channel);
            return;
        }

        _ticker.SetGamePreset(preset);
        _ticker.UpdateInfoText();
        RaiseNetworkEvent(new GameModesAdminActionResult(true, Loc.GetString("game-modes-admin-set-ok", ("preset", preset.ID))),
            args.SenderSession.Channel);
        SendState(args.SenderSession);
    }

    private void OnStartVote(GameModesStartPresetVoteMessage msg, EntitySessionEventArgs args)
    {
        if (!TryAuthorize(args.SenderSession, AdminFlags.Round, out var error))
        {
            RaiseNetworkEvent(new GameModesAdminActionResult(false, error), args.SenderSession.Channel);
            return;
        }

        _votes.CreateStandardVote(args.SenderSession, StandardVoteType.Preset);
        RaiseNetworkEvent(new GameModesAdminActionResult(true, Loc.GetString("game-modes-admin-vote-started")),
            args.SenderSession.Channel);
        SendState(args.SenderSession);
    }

    private void OnSetVoteEnabled(GameModesSetVoteEnabledMessage msg, EntitySessionEventArgs args)
    {
        if (!TryAuthorize(args.SenderSession, AdminFlags.Round, out var error))
        {
            RaiseNetworkEvent(new GameModesAdminActionResult(false, error), args.SenderSession.Channel);
            return;
        }

        _cfg.SetCVar(CCVars.VotePresetEnabled, msg.Enabled);
        RaiseNetworkEvent(new GameModesAdminActionResult(true, Loc.GetString("game-modes-admin-vote-toggle-ok")),
            args.SenderSession.Channel);
        SendState(args.SenderSession);
    }
}
