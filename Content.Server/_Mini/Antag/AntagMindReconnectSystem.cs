// SPDX-FileCopyrightText: 2026 Mini Station
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.Antag.Components;
using Content.Server.Mind;
using Content.Server.Roles;
using Content.Shared.Mind;
using Robust.Server.GameStates;
using Robust.Server.Player;
using Robust.Shared.Enums;
using Robust.Shared.Player;

namespace Content.Server._Mini.Antag;

/// <summary>
/// After reconnect, <see cref="MindSystem.CreateMind"/> can orphan the antag mind
/// (UserId moves to an empty mind while objectives/roles stay on the old one).
/// Restore the rich assigned mind and re-add its PVS override.
/// </summary>
public sealed class AntagMindReconnectSystem : EntitySystem
{
    [Dependency] private readonly MindSystem _mind = default!;
    [Dependency] private readonly RoleSystem _roles = default!;
    [Dependency] private readonly IPlayerManager _players = default!;
    [Dependency] private readonly PvsOverrideSystem _pvs = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PlayerAttachedEvent>(OnPlayerAttached);
        _players.PlayerStatusChanged += OnPlayerStatusChanged;
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _players.PlayerStatusChanged -= OnPlayerStatusChanged;
    }

    private void OnPlayerStatusChanged(object? sender, SessionStatusEventArgs args)
    {
        if (args.NewStatus == SessionStatus.InGame)
            TryRestoreAntagMind(args.Session);
    }

    private void OnPlayerAttached(PlayerAttachedEvent args)
    {
        TryRestoreAntagMind(args.Player);
    }

    private void TryRestoreAntagMind(ICommonSession session)
    {
        EntityUid? currentMindId = null;
        MindComponent? currentMind = null;
        if (_mind.TryGetMind(session.UserId, out var existingMindId, out var existingMind))
        {
            currentMindId = existingMindId;
            currentMind = existingMind;
        }

        var currentScore = currentMindId != null && currentMind != null
            ? ScoreMind(currentMindId.Value, currentMind)
            : -1;

        EntityUid? bestMindId = null;
        MindComponent? bestMind = null;
        var bestScore = currentScore;

        var query = EntityQueryEnumerator<AntagSelectionComponent>();
        while (query.MoveNext(out _, out var antag))
        {
            foreach (var (assignedMindId, _) in antag.AssignedMinds)
            {
                if (!TryComp(assignedMindId, out MindComponent? assignedMind))
                    continue;

                if (assignedMind.UserId != session.UserId
                    && assignedMind.OriginalOwnerUserId != session.UserId)
                    continue;

                var score = ScoreMind(assignedMindId, assignedMind);
                if (score <= bestScore)
                    continue;

                bestScore = score;
                bestMindId = assignedMindId;
                bestMind = assignedMind;
            }
        }

        if (bestMindId == null || bestMind == null || bestMindId == currentMindId)
        {
            if (currentMindId != null)
                _pvs.AddSessionOverride(currentMindId.Value, session);
            return;
        }

        Log.Info(
            $"Restoring antag mind {ToPrettyString(bestMindId.Value)} for {session} after reconnect " +
            $"(was {ToPrettyString(currentMindId)} with score {currentScore}, restored score {bestScore})");

        _mind.SetUserId(bestMindId.Value, session.UserId, bestMind);

        var attachTarget = bestMind.CurrentEntity;
        if (attachTarget != null && !Deleted(attachTarget.Value))
        {
            if (!_mind.TryGetMind(attachTarget.Value, out var bodyMindId, out _)
                || bodyMindId != bestMindId.Value)
            {
                _mind.TransferTo(bestMindId.Value, attachTarget.Value, ghostCheckOverride: true, mind: bestMind);
            }

            _players.SetAttachedEntity(session, bestMind.CurrentEntity ?? attachTarget.Value);
        }

        _pvs.AddSessionOverride(bestMindId.Value, session);
    }

    private int ScoreMind(EntityUid mindId, MindComponent mind)
    {
        var score = mind.Objectives.Count * 100;
        if (_roles.MindIsAntagonist(mindId))
            score += 1000;
        if (mind.UserId != null)
            score += 10;
        return score;
    }
}
