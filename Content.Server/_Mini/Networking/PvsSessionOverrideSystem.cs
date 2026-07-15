// SPDX-FileCopyrightText: 2025 Mini Station
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using Content.Server._Mini.TypanWar;
using Content.Server.Station.Components;
using Content.Server.Station.Systems;
using Content.Shared._Goobstation.Silo;
using Content.Shared._Mini.TypanWar;
using Content.Shared.GameTicking;
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
/// <remarks>
/// Do NOT put station/drop-shuttle grids or player bodies into session overrides.
/// Those trees are huge, share the PVS enter budget with normal chunks, and abort
/// mid-recursion when the budget is exceeded — causing inventory LeavePvs and
/// nearby players to appear invisible on other clients.
/// </remarks>
public sealed class PvsSessionOverrideSystem : EntitySystem
{
    private const float RefreshDebounceSeconds = 0.25f;

    [Dependency] private readonly PvsOverrideSystem _pvs = default!;
    [Dependency] private readonly IPlayerManager _player = default!;

    private readonly Dictionary<ICommonSession, EntityUid?> _pendingRefresh = new();
    private float _pendingRefreshAccumulator;

    public override void Initialize()
    {
        SubscribeLocalEvent<PlayerAttachedEvent>(OnPlayerAttached);
        SubscribeLocalEvent<PlayerDetachedEvent>(OnPlayerDetached);
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawnComplete);
        SubscribeLocalEvent<ActorComponent, EntParentChangedMessage>(OnActorParentChanged);
        SubscribeLocalEvent<ActorComponent, GridUidChangedEvent>(OnActorGridChanged);
        SubscribeLocalEvent<TypanWarLayoutReadyEvent>(OnWarLayoutReady);
        SubscribeLocalEvent<StationGridAddedEvent>(OnStationGridAdded);
        SubscribeLocalEvent<TypanWarFactionComponent, ComponentStartup>(OnFactionStartup);
        SubscribeLocalEvent<TypanWarDropShuttleComponent, ComponentStartup>(OnDropShuttleStartup);
        _player.PlayerStatusChanged += OnPlayerStatusChanged;
    }

    public override void Shutdown()
    {
        _player.PlayerStatusChanged -= OnPlayerStatusChanged;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_pendingRefresh.Count == 0)
            return;

        _pendingRefreshAccumulator += frameTime;
        if (_pendingRefreshAccumulator < RefreshDebounceSeconds)
            return;

        _pendingRefreshAccumulator = 0f;
        FlushPendingRefreshes();
    }

    private void QueueRefresh(ICommonSession session, EntityUid? player = null)
    {
        if (session == null)
            return;

        _pendingRefresh[session] = player ?? session.AttachedEntity;
    }

    private void FlushPendingRefreshes()
    {
        if (_pendingRefresh.Count == 0)
            return;

        var pending = _pendingRefresh.ToArray();
        _pendingRefresh.Clear();

        foreach (var (session, player) in pending)
            RefreshPlayer(session, player);
    }

    private void OnPlayerStatusChanged(object? sender, SessionStatusEventArgs e)
    {
        if (e.NewStatus == SessionStatus.InGame)
            RefreshPlayer(e.Session, e.Session.AttachedEntity);
    }

    private void OnPlayerSpawnComplete(PlayerSpawnCompleteEvent args)
    {
        RefreshPlayer(args.Player, args.Mob);
    }

    private void OnPlayerAttached(PlayerAttachedEvent args)
    {
        RefreshPlayer(args.Player, args.Entity);
    }

    private void OnPlayerDetached(PlayerDetachedEvent args)
    {
        // Clear recursive overrides from the body being left (ghost/SSD) before AttachedEntity is null.
        if (!Deleted(args.Entity))
            _pvs.RemoveSessionOverride(args.Entity, args.Player);

        RefreshPlayer(args.Player);
    }

    private void OnActorParentChanged(EntityUid uid, ActorComponent component, EntParentChangedMessage args)
    {
        if (component.PlayerSession == null)
            return;

        // Parent thrashing during war (FTL/arrivals reparent) must be coalesced.
        QueueRefresh(component.PlayerSession, uid);
    }

    private void OnActorGridChanged(EntityUid uid, ActorComponent component, ref GridUidChangedEvent args)
    {
        if (component.PlayerSession == null)
            return;

        QueueRefresh(component.PlayerSession, uid);
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

    private void OnDropShuttleStartup(EntityUid uid, TypanWarDropShuttleComponent component, ComponentStartup args)
    {
        // Clear any leftover grid override from older builds; never re-add grids.
        foreach (var session in _player.Sessions)
        {
            if (session.Status == SessionStatus.InGame)
                _pvs.RemoveSessionOverride(uid, session);
        }
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
        ClearBodySessionOverride(session, player);
        RefreshPointManagerOverrides(session, player);
        RefreshSiloOverrides(session, player);
    }

    public void RefreshStationOverrides(ICommonSession session, EntityUid? player = null)
    {
        if (session.Status != SessionStatus.InGame)
            return;

        player ??= session.AttachedEntity;
        EntityUid? station = null;

        if (player != null && TryComp(player, out TransformComponent? xform))
        {
            if (xform.GridUid != null && TryComp(xform.GridUid, out StationMemberComponent? member))
                station = member.Station;
        }

        // Station *data* entities are tiny; safe to force for UI/station state.
        var query = EntityQueryEnumerator<StationDataComponent>();
        while (query.MoveNext(out var uid, out _))
        {
            if (uid == station)
                _pvs.AddSessionOverride(uid, session);
            else
                _pvs.RemoveSessionOverride(uid, session);
        }

        // Never force-replicate grids (stations or drop shuttles). Full transform trees
        // exhaust PVS enter budget → inventory Detached + players invisible to others.
        var gridQuery = EntityQueryEnumerator<StationMemberComponent>();
        while (gridQuery.MoveNext(out var gridUid, out _))
            _pvs.RemoveSessionOverride(gridUid, session);
    }

    /// <summary>
    /// Clears legacy war self-body session overrides that burned PVS enter budget.
    /// Inventory and nearby players use normal chunk PVS instead.
    /// </summary>
    private void ClearBodySessionOverride(ICommonSession session, EntityUid? player)
    {
        player ??= session.AttachedEntity;
        if (player != null && !Deleted(player.Value))
            _pvs.RemoveSessionOverride(player.Value, session);
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

            if (attached != null && TryComp(attached, out TransformComponent? xform))
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

            if (attached != null && TryComp(attached, out TransformComponent? xform))
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
