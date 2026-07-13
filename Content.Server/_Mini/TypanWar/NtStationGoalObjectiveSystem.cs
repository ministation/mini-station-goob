// SPDX-FileCopyrightText: 2026 Egorik1
// Мини-станция, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/ministation/mini-station-goob/master/LICENSE.TXT

using Content.Server._CorvaxGoob.StationGoal;
using Content.Server._Mini.Typan.StationGoal;
using Content.Server._TT.StationHandleJob;
using Content.Server.Mind;
using Content.Shared._Mini.TypanWar;
using Content.Shared.Ghost;
using Content.Shared.Mind;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Roles;
using Content.Shared.Roles.Jobs;
using Robust.Shared.Prototypes;

namespace Content.Server._Mini.TypanWar;

/// <summary>
/// Tracks NT station goals and assigns them to command staff when war starts.
/// </summary>
public sealed class NtStationGoalObjectiveSystem : EntitySystem
{
    private const string ObjectiveProto = "NtStationGoalObjective";

    private static readonly HashSet<ProtoId<JobPrototype>> CommandJobs = new()
    {
        "Captain",
        "HeadOfSecurity",
        "ChiefEngineer",
        "HeadOfPersonnel",
    };

    [Dependency] private readonly MindSystem _mind = default!;
    [Dependency] private readonly MetaDataSystem _metaData = default!;
    [Dependency] private readonly SharedJobSystem _jobs = default!;

    private readonly Dictionary<EntityUid, (ProtoId<StationGoalPrototype> Id, string Title, string Description)> _activeGoals = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<TypanWarStartedEvent>(OnWarStarted);
    }

    public void OnStationGoalSent(EntityUid station, StationGoalPrototype goal)
    {
        if (HasComp<TTStationHandleJobComponent>(station))
            return;

        var stationName = MetaData(station).EntityName;
        var (title, description) = TypanStationGoalCardText.Build(goal, stationName);
        _activeGoals[station] = (goal.ID, title, description);
    }

    public bool TryGetActiveGoalTitle(EntityUid station, out string title)
    {
        if (_activeGoals.TryGetValue(station, out var goal))
        {
            title = goal.Title;
            return true;
        }

        title = string.Empty;
        return false;
    }

    /// <summary>
    /// Re-assigns the active NT station goal to command staff (e.g. after reconnect/latejoin).
    /// </summary>
    public void TryAssignActiveGoalToMind(EntityUid mindId, MindComponent mind, EntityUid station)
    {
        if (!_activeGoals.TryGetValue(station, out var goal))
            return;

        if (!_jobs.MindTryGetJobId(mindId, out var jobId) || jobId == null || !CommandJobs.Contains(jobId.Value))
            return;

        TryAssignGoal(mindId, mind, goal.Id, goal.Title, goal.Description);
    }

    private void OnWarStarted(TypanWarStartedEvent ev)
    {
        if (!_activeGoals.TryGetValue(ev.NtStation, out var goal))
            return;

        var minds = EntityQueryEnumerator<MindComponent>();
        while (minds.MoveNext(out var mindId, out var mind))
        {
            if (!IsMindAlive(mind))
                continue;

            if (!_jobs.MindTryGetJobId(mindId, out var jobId) || jobId == null || !CommandJobs.Contains(jobId.Value))
                continue;

            TryAssignGoal(mindId, mind, goal.Id, goal.Title, goal.Description);
        }
    }

    private void TryAssignGoal(
        EntityUid mindId,
        MindComponent mind,
        ProtoId<StationGoalPrototype> goalId,
        string title,
        string description)
    {
        if (HasGoal(mind, goalId))
            return;

        var objective = Spawn(ObjectiveProto);
        EnsureComp<NtStationGoalObjectiveComponent>(objective).GoalId = goalId;
        _metaData.SetEntityName(objective, title, MetaData(objective));
        _metaData.SetEntityDescription(objective, description, MetaData(objective));
        _mind.AddObjective(mindId, mind, objective);
    }

    private bool HasGoal(MindComponent mind, ProtoId<StationGoalPrototype> goalId)
    {
        var query = GetEntityQuery<NtStationGoalObjectiveComponent>();
        foreach (var objective in mind.Objectives)
        {
            if (query.TryGetComponent(objective, out var comp) && comp.GoalId == goalId)
                return true;
        }

        return false;
    }

    private bool IsMindAlive(MindComponent mind)
    {
        var entity = mind.CurrentEntity;
        if (entity == null || !entity.Value.IsValid())
            return false;

        if (HasComp<GhostComponent>(entity))
            return false;

        if (TryComp<MobStateComponent>(entity, out var mobState))
            return mobState.CurrentState != MobState.Dead;

        return true;
    }
}
