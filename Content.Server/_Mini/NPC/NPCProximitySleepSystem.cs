// SPDX-FileCopyrightText: 2025 Mini Station
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using Content.Server.NPC.Components;
using Content.Server.NPC.HTN;
using Content.Server.NPC.Systems;
using Content.Server.GameTicking.Events;
using Content.Shared._Mini.MiniCCVars;
using Content.Shared.Mind.Components;
using Robust.Server.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server._Mini.NPC;

/// <summary>
/// Puts NPCs to sleep when no players are nearby to reduce server tick and replication load.
/// </summary>
public sealed class NPCProximitySleepSystem : EntitySystem
{
    [Dependency] private readonly NPCSystem _npc = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPlayerManager _players = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;

    private bool _enabled = true;
    private float _distanceSq = 20f * 20f;
    private TimeSpan _nextCheck = TimeSpan.Zero;

    private const float CheckIntervalSeconds = 1f;

    public override void Initialize()
    {
        base.Initialize();

        Subs.CVar(_cfg, MiniCCVars.NPCDisableWithoutPlayers, v => _enabled = v, true);
        Subs.CVar(_cfg, MiniCCVars.NPCDisableDistance, v => _distanceSq = v * v, true);

        SubscribeLocalEvent<RoundStartingEvent>(OnRoundStarting);
        SubscribeLocalEvent<PlayerAttachedEvent>(OnPlayerAttached);
    }

    private void OnRoundStarting(RoundStartingEvent ev)
    {
        _nextCheck = TimeSpan.Zero;
    }

    private void OnPlayerAttached(PlayerAttachedEvent ev)
    {
        if (!_enabled)
            return;

        _nextCheck = TimeSpan.Zero;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (!_enabled || !_npc.Enabled)
            return;

        if (_timing.CurTime < _nextCheck)
            return;

        _nextCheck = _timing.CurTime + TimeSpan.FromSeconds(CheckIntervalSeconds);

        var playersByMap = new Dictionary<MapId, List<Vector2>>();
        foreach (var session in _players.Sessions)
        {
            if (session.AttachedEntity is not { Valid: true } player)
                continue;

            if (!TryComp(player, out TransformComponent? xform))
                continue;

            var map = xform.MapID;
            if (!playersByMap.TryGetValue(map, out var positions))
                playersByMap[map] = positions = new List<Vector2>();
            positions.Add(_xform.GetWorldPosition(xform));
        }

        var query = EntityQueryEnumerator<HTNComponent, TransformComponent, MetaDataComponent>();
        while (query.MoveNext(out var uid, out var htn, out var xform, out var meta))
        {
            if (meta.EntityLifeStage < EntityLifeStage.MapInitialized)
                continue;

            if (meta.EntityLifeStage >= EntityLifeStage.Terminating)
                continue;

            if (HasComp<ActorComponent>(uid))
                continue;

            if (TryComp<MindContainerComponent>(uid, out var mind) && mind.HasMind)
                continue;

            var nearPlayer = false;
            if (playersByMap.TryGetValue(xform.MapID, out var mapPlayers))
            {
                var npcPos = _xform.GetWorldPosition(xform);
                foreach (var pos in mapPlayers)
                {
                    if ((pos - npcPos).LengthSquared() <= _distanceSq)
                    {
                        nearPlayer = true;
                        break;
                    }
                }
            }

            if (nearPlayer)
            {
                if (!_npc.IsAwake(uid, htn))
                    _npc.WakeNPC(uid, htn);
            }
            else if (_npc.IsAwake(uid, htn))
            {
                _npc.SleepNPC(uid, htn);
            }
        }
    }
}
