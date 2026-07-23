// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server._Mini.Objectives;
using Content.Server.Antag.Components;
using Content.Server.Mind;
using Content.Server.Roles;
using Content.Server.Roles.Jobs;
using Content.Shared.CharacterInfo;
using Content.Shared.Mind;
using Content.Shared.Objectives;
using Content.Shared.Objectives.Components;
using Content.Shared.Objectives.Systems;
using Robust.Shared.Player;
using System.Diagnostics.CodeAnalysis;

namespace Content.Server.CharacterInfo;

public sealed class CharacterInfoSystem : EntitySystem
{
    [Dependency] private readonly JobSystem _jobs = default!;
    [Dependency] private readonly MindSystem _minds = default!;
    [Dependency] private readonly RoleSystem _roles = default!;
    [Dependency] private readonly SharedObjectivesSystem _objectives = default!;
    [Dependency] private readonly AntagObjectiveCoinRewardSystem _antagObjectiveRewards = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<RequestCharacterInfoEvent>(OnRequestCharacterInfoEvent);
    }

    private void OnRequestCharacterInfoEvent(RequestCharacterInfoEvent msg, EntitySessionEventArgs args)
    {
        if (!args.SenderSession.AttachedEntity.HasValue
            || args.SenderSession.AttachedEntity != GetEntity(msg.NetEntity))
            return;

        var entity = args.SenderSession.AttachedEntity.Value;

        var objectives = new Dictionary<string, List<ObjectiveInfo>>();
        var jobTitle = Loc.GetString("character-info-no-profession");
        string? briefing = null;
        var antagAllComplete = false;
        var antagCoinGranted = false;

        if (TryResolveCharacterMind(args.SenderSession, entity, out var mindId, out var mind))
        {
            foreach (var objective in mind.Objectives)
            {
                var info = _objectives.GetInfo(objective, mindId, mind);
                if (info == null)
                    continue;

                var issuer = Comp<ObjectiveComponent>(objective).LocIssuer;
                if (!objectives.ContainsKey(issuer))
                    objectives[issuer] = new List<ObjectiveInfo>();
                objectives[issuer].Add(info.Value);
            }

            if (_jobs.MindTryGetJobName(mindId, out var jobName))
                jobTitle = jobName;

            briefing = _roles.MindGetBriefing(mindId);
            antagAllComplete = _antagObjectiveRewards.AreAllObjectivesComplete(mindId, mind);
            antagCoinGranted = _antagObjectiveRewards.IsRewardGranted(mindId);
        }

        RaiseNetworkEvent(new CharacterInfoEvent(
            GetNetEntity(entity),
            jobTitle,
            objectives,
            briefing,
            antagAllComplete,
            antagCoinGranted), args.SenderSession);
    }

    /// <summary>
    /// Pick the richest mind for this player: body mind, UserId mind, or an assigned antag mind
    /// that still belongs to them (CreateMind can orphan the real antag mind on reconnect).
    /// </summary>
    private bool TryResolveCharacterMind(
        ICommonSession session,
        EntityUid attachedEntity,
        out EntityUid mindId,
        [NotNullWhen(true)] out MindComponent? mind)
    {
        mindId = default;
        mind = null;

        EntityUid? bestId = null;
        MindComponent? bestMind = null;
        var bestScore = -1;

        void Consider(EntityUid id, MindComponent candidate)
        {
            var score = candidate.Objectives.Count * 100;
            if (_roles.MindIsAntagonist(id))
                score += 1000;
            if (candidate.OwnedEntity == attachedEntity || candidate.VisitingEntity == attachedEntity)
                score += 50;
            if (candidate.UserId == session.UserId)
                score += 10;

            if (score < bestScore)
                return;

            bestScore = score;
            bestId = id;
            bestMind = candidate;
        }

        if (_minds.TryGetMind(attachedEntity, out var bodyMindId, out var bodyMind))
            Consider(bodyMindId, bodyMind);

        if (_minds.TryGetMind(session.UserId, out var sessionMindId, out var sessionMind))
            Consider(sessionMindId.Value, sessionMind);

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

                Consider(assignedMindId, assignedMind);
            }
        }

        if (bestId == null || bestMind == null)
            return false;

        mindId = bestId.Value;
        mind = bestMind;
        return true;
    }
}
