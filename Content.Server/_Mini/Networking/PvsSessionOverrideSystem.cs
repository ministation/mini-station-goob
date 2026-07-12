// SPDX-FileCopyrightText: 2025 Mini Station
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server._Mini.TypanWar;
using Content.Server.Station.Components;
using Content.Server.Station.Systems;
using Content.Shared._Goobstation.Silo;
using Content.Shared._Mini.TypanWar;
using Content.Shared.Points;
using Content.Shared.Station.Components;
using Robust.Server.GameStates;
using Robust.Server.Player;
using Robust.Shared.Enums;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Player;

namespace Content.Server._Mini.Networking;

/// <summary>
/// Centralizes session-scoped PVS overrides. Component-scoped events like
/// <see cref="ComponentStartup"/> only allow a single global subscription per component type.
/// </summary>
public sealed class PvsSessionOverrideSystem : EntitySystem
{
    [Dependency] private readonly PvsOverrideSystem _pvs = default!;
    [Dependency] private readonly IPlayerManager _player = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<PlayerAttachedEvent>(OnPlayerAttached);
        SubscribeLocalEvent<PlayerDetachedEvent>(OnPlayerDetached);
        SubscribeLocalEvent<ActorComponent, EntParentChangedMessage>(OnActorParentChanged);
        SubscribeLocalEvent<ActorComponent, GridUidChangedEvent>(OnActorGridChanged);
        SubscribeLocalEvent<TypanWarLayoutReadyEvent>(OnWarLayoutReady);
        SubscribeLocalEvent<StationGridAddedEvent>(OnStationGridAdded);
        SubscribeLocalEvent<TypanWarFactionComponent, ComponentStartup>(OnFactionStartup);
        _player.PlayerStatusChanged += OnPlayerStatusChanged;
    }

    public override void Shutdown()
    {
        _player.PlayerStatusChanged -= OnPlayerStatusChanged;
    }

    private void OnPlayerStatusChanged(object? sender, SessionStatusEventArgs e)
    {
        if (e.NewStatus == SessionStatus.InGame)
            RefreshPlayer(e.Session);
    }

    private void OnPlayerAttached(PlayerAttachedEvent args)
    {
        RefreshPlayer(args.Player, args.Entity);
    }

    private void OnPlayerDetached(PlayerDetachedEvent args)
    {
        RefreshPlayer(args.Player);
    }

    private void OnActorParentChanged(EntityUid uid, ActorComponent component, EntParentChangedMessage args)
    {
        if (component.PlayerSession == null)
            return;

        RefreshPlayer(component.PlayerSession, uid);
    }

    private void OnActorGridChanged(EntityUid uid, ActorComponent component, ref GridUidChangedEvent args)
    {
        if (component.PlayerSession == null)
            return;

        RefreshPlayer(component.PlayerSession, uid);
    }

    private void OnWarLayoutReady(TypanWarLayoutReadyEvent ev)
    {
        RefreshAllPlayers();
    }

    private void OnStationGridAdded(StationGridAddedEvent ev)
    {
        if (!TypanStationWarRuleSystem.IsWarActive)
            return;

        RefreshAllPlayers();
    }

    private void OnFactionStartup(EntityUid uid, TypanWarFactionComponent component, ComponentStartup args)
    {
        if (!TypanStationWarRuleSystem.IsWarActive)
            return;

        if (!TryComp<ActorComponent>(uid, out var actor) || actor.PlayerSession == null)
            return;

        RefreshPlayer(actor.PlayerSession, uid);
    }

    public void RefreshAllPlayers()
    {
        foreach (var session in _player.Sessions)
        {
            RefreshPlayer(session);
        }
    }

    public void RefreshPlayer(ICommonSession session, EntityUid? player = null)
    {
        if (session == null)
            return;

        RefreshStationOverrides(session, player);
        RefreshWarPlayerOverride(session, player);
        RefreshPointManagerOverrides(session, player);
        RefreshSiloOverrides(session, player);
    }

    public void RefreshStationOverrides(ICommonSession session, EntityUid? player = null)
    {
        if (session.Status != SessionStatus.InGame)
            return;

        player ??= session.AttachedEntity;
        EntityUid? station = null;
        MapId? playerMap = null;

        if (player != null && TryComp<TransformComponent>(player, out var xform))
        {
            playerMap = xform.MapID;

            if (xform.GridUid != null && TryComp<StationMemberComponent>(xform.GridUid, out var member))
                station = member.Station;
        }

        var warStations = TypanStationWarRuleSystem.IsWarActive && playerMap != null
            ? GetWarStationsOnMap(playerMap.Value)
            : null;

        var query = EntityQueryEnumerator<StationDataComponent>();
        while (query.MoveNext(out var uid, out _))
        {
            if (warStations != null && warStations.Contains(uid))
                _pvs.AddSessionOverride(uid, session);
            else if (uid == station)
                _pvs.AddSessionOverride(uid, session);
            else
                _pvs.RemoveSessionOverride(uid, session);
        }

        // Station entities are not parents of their grids — replicate each grid explicitly.
        var gridQuery = EntityQueryEnumerator<StationMemberComponent>();
        while (gridQuery.MoveNext(out var gridUid, out var member))
        {
            if (warStations != null && warStations.Contains(member.Station))
                _pvs.AddSessionOverride(gridUid, session);
            else if (member.Station == station)
                _pvs.AddSessionOverride(gridUid, session);
            else
                _pvs.RemoveSessionOverride(gridUid, session);
        }
    }

    /// <summary>
    /// Keeps the player's own inventory replicated when crossing grid boundaries during station war.
    /// </summary>
    private void RefreshWarPlayerOverride(ICommonSession session, EntityUid? player)
    {
        if (session.Status != SessionStatus.InGame)
            return;

        player ??= session.AttachedEntity;

        if (player == null)
            return;

        if (TypanStationWarRuleSystem.IsWarActive && HasComp<TypanWarFactionComponent>(player.Value))
            _pvs.AddSessionOverride(player.Value, session);
        else
            _pvs.RemoveSessionOverride(player.Value, session);
    }

    private HashSet<EntityUid> GetWarStationsOnMap(MapId mapId)
    {
        var stations = new HashSet<EntityUid>();
        var query = EntityQueryEnumerator<TypanStationWarRuleComponent>();
        while (query.MoveNext(out var ruleUid, out var rule))
        {
            if (rule.Phase != TypanWarPhase.Active)
                continue;

            if (rule.NtStation is { } nt && StationOnMap(nt, mapId))
                stations.Add(nt);

            if (rule.TypanStation is { } typan && StationOnMap(typan, mapId))
                stations.Add(typan);
        }

        return stations;
    }

    private bool StationOnMap(EntityUid station, MapId mapId)
    {
        if (!TryComp<StationDataComponent>(station, out var data) || data.Grids.Count == 0)
            return false;

        foreach (var grid in data.Grids)
        {
            if (TryComp(grid, out TransformComponent? xform) && xform.MapID == mapId)
                return true;
        }

        return false;
    }

    public void RefreshPointManagerOverrides(ICommonSession? session = null, EntityUid? player = null)
    {
        var sessions = session != null ? new[] { session } : _player.Sessions;

        foreach (var targetSession in sessions)
        {
            if (targetSession.Status != SessionStatus.InGame)
                continue;

            var attached = player ?? targetSession.AttachedEntity;
            MapId? playerMap = null;

            if (attached != null && TryComp<TransformComponent>(attached, out var xform))
                playerMap = xform.MapID;

            var query = EntityQueryEnumerator<PointManagerComponent, TransformComponent>();
            while (query.MoveNext(out var uid, out _, out var managerXform))
            {
                if (playerMap.HasValue && managerXform.MapID == playerMap.Value)
                    _pvs.AddSessionOverride(uid, targetSession);
                else
                    _pvs.RemoveSessionOverride(uid, targetSession);
            }
        }
    }

    public void RefreshSiloOverrides(ICommonSession? session = null, EntityUid? player = null)
    {
        var sessions = session != null ? new[] { session } : _player.Sessions;

        foreach (var targetSession in sessions)
        {
            if (targetSession.Status != SessionStatus.InGame)
                continue;

            var attached = player ?? targetSession.AttachedEntity;
            EntityUid? playerGrid = null;

            if (attached != null && TryComp<TransformComponent>(attached, out var xform))
                playerGrid = xform.GridUid;

            var query = EntityQueryEnumerator<SiloComponent, TransformComponent>();
            while (query.MoveNext(out var siloUid, out _, out var siloXform))
            {
                if (playerGrid != null && siloXform.GridUid == playerGrid)
                    _pvs.AddSessionOverride(siloUid, targetSession);
                else
                    _pvs.RemoveSessionOverride(siloUid, targetSession);
            }
        }
    }
}
