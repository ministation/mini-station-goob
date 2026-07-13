using Content.Server._CorvaxGoob.StationGoal;
using Content.Server._Mini.Typan.StationGoal;
using Content.Server._TT.StationHandleJob;
using Content.Server.GameTicking;
using Content.Server.Mind;
using Content.Server.Station.Systems;
using Content.Shared.GameTicking;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Robust.Shared.Prototypes;

namespace Content.Server._Mini.Typan.StationGoal;

/// <summary>
/// Assigns Typan station fax goals to all Typan crew as personal objectives.
/// </summary>
public sealed class TypanStationGoalObjectiveSystem : EntitySystem
{
    private const string ObjectiveProto = "TypanStationGoalObjective";

    [Dependency] private readonly MindSystem _mind = default!;
    [Dependency] private readonly MetaDataSystem _metaData = default!;
    [Dependency] private readonly StationSystem _station = default!;

    private readonly Dictionary<EntityUid, (ProtoId<StationGoalPrototype> Id, string Title, string Description)> _activeGoals = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawnComplete);
    }

    public void OnStationGoalSent(EntityUid station, StationGoalPrototype goal)
    {
        if (!HasComp<TTStationHandleJobComponent>(station))
            return;

        var stationName = MetaData(station).EntityName;
        var (title, description) = TypanStationGoalCardText.Build(goal, stationName);

        _activeGoals[station] = (goal.ID, title, description);
        AssignGoalToStationMinds(station, goal.ID, title, description);
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
    /// Re-assigns the active Typan station goal after reconnect/latejoin/respawn.
    /// </summary>
    public void TryAssignActiveGoalToMind(EntityUid mindId, MindComponent mind, EntityUid station)
    {
        if (!_activeGoals.TryGetValue(station, out var goal))
            return;

        TryAssignGoal(mindId, mind, goal.Id, goal.Title, goal.Description);
    }

    private void OnPlayerSpawnComplete(PlayerSpawnCompleteEvent args)
    {
        if (args.JobId == null)
            return;

        var station = args.Station;
        if (!HasComp<TTStationHandleJobComponent>(station))
            return;

        if (!_activeGoals.TryGetValue(station, out var goal))
            return;

        if (!_mind.TryGetMind(args.Mob, out var mindId, out var mind))
            return;

        TryAssignGoal(mindId, mind, goal.Id, goal.Title, goal.Description);
    }

    private void AssignGoalToStationMinds(EntityUid station, ProtoId<StationGoalPrototype> goalId, string title, string description)
    {
        var minds = EntityQueryEnumerator<MindComponent>();
        while (minds.MoveNext(out var mindId, out var mind))
        {
            if (!IsMindOnStation(mind, station))
                continue;

            TryAssignGoal(mindId, mind, goalId, title, description);
        }
    }

    private bool IsMindOnStation(MindComponent mind, EntityUid station)
    {
        var entity = mind.CurrentEntity;
        if (entity == null || !entity.Value.IsValid())
            return false;

        return _station.GetOwningStation(entity.Value) == station;
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
        EnsureComp<TypanStationGoalObjectiveComponent>(objective).GoalId = goalId;
        _metaData.SetEntityName(objective, title, MetaData(objective));
        _metaData.SetEntityDescription(objective, description, MetaData(objective));
        _mind.AddObjective(mindId, mind, objective);
    }

    private bool HasGoal(MindComponent mind, ProtoId<StationGoalPrototype> goalId)
    {
        var query = GetEntityQuery<TypanStationGoalObjectiveComponent>();
        foreach (var objective in mind.Objectives)
        {
            if (query.TryGetComponent(objective, out var comp) && comp.GoalId == goalId)
                return true;
        }

        return false;
    }
}
