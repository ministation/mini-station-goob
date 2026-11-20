// SPDX-FileCopyrightText: 2026 Space Station 14 Contributors
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.AlertLevel;
using Content.Server.RoundEnd;
using Content.Shared._Mini.StationsControl;
using Content.Shared.Station;
using Robust.Server.GameObjects;

namespace Content.Server._Mini.StationsControl;

public sealed partial class StationsControlConsoleSystem : EntitySystem
{
    [Dependency] private AlertLevelSystem _alertLevel = default!;
    [Dependency] private RoundEndSystem _roundEnd = default!;
    [Dependency] private SharedStationSystem _station = default!;
    [Dependency] private UserInterfaceSystem _uiSystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RoundEndSystemChangedEvent>(_ => OnRoundEndChanged());
        SubscribeLocalEvent<StationsControlConsoleComponent, BoundUIOpenedEvent>(OnUiOpened);

        Subs.BuiEvents<StationsControlConsoleComponent>(StationsControlConsoleUiKey.Key, subs =>
        {
            subs.Event<StationsControlConsoleSetAlertLevelMessage>(OnSetCode);
            subs.Event<StationsControlConsoleCallEvacMessage>(OnCallEvac);
            subs.Event<StationsControlConsoleRecallEvacMessage>(OnRecallEvac);
        });
    }

    private void OnUiOpened(EntityUid uid, StationsControlConsoleComponent comp, BoundUIOpenedEvent args)
    {
        SendState(uid, comp);
    }

    private void SendState(EntityUid uid, StationsControlConsoleComponent comp)
    {
        var stationsList = new List<string>();
        var stationsNetIds = new List<NetEntity>();
        var stationAlertLevels = new List<List<string>>();
        var currentAlertLevels = new List<string>();

        foreach (var station in _station.GetStations())
        {
            stationsList.Add(MetaData(station).EntityName);
            stationsNetIds.Add(GetNetEntity(station));

            var levels = new List<string>();
            var current = string.Empty;

            if (TryComp<AlertLevelComponent>(station, out var alert) && alert.AlertLevels != null)
            {
                current = alert.CurrentLevel;

                foreach (var level in alert.AlertLevels.Levels.Keys)
                {
                    if (comp.AllowAnyAlertLevel || IsAllowedLevel(comp, level))
                        levels.Add(level);
                }
            }

            stationAlertLevels.Add(levels);
            currentAlertLevels.Add(current);
        }

        _uiSystem.SetUiState(uid, StationsControlConsoleUiKey.Key, new StationsControlConsoleState(
            stationsList,
            stationsNetIds,
            stationAlertLevels,
            currentAlertLevels,
            _roundEnd.ShuttleTimeLeft));
    }

    private static bool IsAllowedLevel(StationsControlConsoleComponent comp, string level)
    {
        foreach (var allowed in comp.AllowedAlertLevels)
        {
            if (string.Equals(allowed, level, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private void OnSetCode(EntityUid uid, StationsControlConsoleComponent comp, StationsControlConsoleSetAlertLevelMessage args)
    {
        if (!comp.AllowAnyAlertLevel && !IsAllowedLevel(comp, args.AlertLevel))
            return;

        var stationUid = GetEntity(args.Station);
        _alertLevel.SetLevel(stationUid, args.AlertLevel, true, true, true);
        SendState(uid, comp);
    }

    private void OnCallEvac(EntityUid uid, StationsControlConsoleComponent comp, StationsControlConsoleCallEvacMessage args)
    {
        _roundEnd.RequestRoundEnd(
            args.CountdownTime,
            requester: uid,
            checkCooldown: false
        );
    }

    private void OnRecallEvac(EntityUid uid, StationsControlConsoleComponent comp, StationsControlConsoleRecallEvacMessage args)
    {
        _roundEnd.CancelRoundEndCountdown(
            requester: args.Actor,
            forceRecall: true
        );
    }

    private void OnRoundEndChanged()
    {
        var query = AllEntityQuery<StationsControlConsoleComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            SendState(uid, comp);
        }
    }
}
