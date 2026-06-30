using System.Linq;
using Content.Server.RPSX.DarkForces.Ratvar.Righteous.Progress.Objectives.Summon;
using Content.Server.RPSX.DarkForces.Ratvar.Righteous.Structures.Portal;
using Content.Server.Mind;
using Content.Server.Objectives;
using Content.Shared.GameTicking;
using Content.Shared.Objectives.Components;
using Content.Shared.RPSX.DarkForces.Ratvar.Righteous.Roles;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.Manager.Attributes;
using Robust.Shared.Timing;
using Content.Shared.Access.Components;

namespace Content.Server.RPSX.DarkForces.Ratvar.Righteous.Progress;

public sealed partial class RatvarProgressSystem : EntitySystem
{
    [Dependency] private readonly MindSystem _mindSystem = default!;
    [Dependency] private readonly ObjectivesSystem _objectivesSystem = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    [ValidatePrototypeId<EntityPrototype>]
    private const string ProgressPrototype = "RatvarProgress";

    [ValidatePrototypeId<EntityPrototype>]
    private const string BeaconsObjectivePrototype = "RatvarBeaconsObjective";

    [ValidatePrototypeId<EntityPrototype>]
    private const string ConvertObjectivePrototype = "RatvarConvertObjective";

    [ValidatePrototypeId<EntityPrototype>]
    private const string PowerObjectivePrototype = "RatvarPowerObjective";

    [ValidatePrototypeId<EntityPrototype>]
    private const string RatvarSummonObjectivePrototype = "RatvarSummonObjective";

    private Entity<RatvarProgressComponent>? _progressEntity;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);
        SubscribeLocalEvent<RatvarProgressComponent, ComponentShutdown>(OnProgressShutdown);

        InitializeStructuresAndItems();
    }

    private void InitializeProgress(EntityUid uid, RatvarProgressComponent component)
    {
        CreateObjective(BeaconsObjectivePrototype, ref component.RatvarBeaconsObjective);
        CreateObjective(ConvertObjectivePrototype, ref component.RatvarConvertObjective);
        CreateObjective(PowerObjectivePrototype, ref component.RatvarPowerObjective);

        component.NextObjectivesCheckTick = _timing.CurTime + component.ObjectivesCheckPeriod;
    }

    private void OnProgressShutdown(EntityUid uid, RatvarProgressComponent component, ComponentShutdown args)
    {
        DeleteObjective(ref component.RatvarBeaconsObjective);
        DeleteObjective(ref component.RatvarConvertObjective);
        DeleteObjective(ref component.RatvarPowerObjective);
        DeleteObjective(ref component.RatvarSummonObjective);

        if (_progressEntity?.Owner == uid)
            _progressEntity = null;
    }

    private void DeleteObjective(ref EntityUid objective)
    {
        if (objective == EntityUid.Invalid)
            return;

        QueueDel(objective);
        objective = EntityUid.Invalid;
    }

    private void OnRoundRestart(RoundRestartCleanupEvent ev)
    {
        if (_progressEntity != null)
            QueueDel(_progressEntity);

        _progressEntity = null;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_progressEntity == null)
            return;

        var comp = _progressEntity.Value.Comp;

        var curTime = _timing.CurTime;
        if (comp.RatvarSummonObjective != EntityUid.Invalid || comp.NextObjectivesCheckTick > curTime)
            return;

        comp.NextObjectivesCheckTick = curTime + comp.ObjectivesCheckPeriod;

        if (!IsObjectiveComplete(comp.RatvarConvertObjective) || !IsObjectiveComplete(comp.RatvarBeaconsObjective) ||
            !IsObjectiveComplete(comp.RatvarPowerObjective))
            return;

        CreateObjective(RatvarSummonObjectivePrototype, ref comp.RatvarSummonObjective);
        AddObjectivesToRighteouses(comp.RatvarSummonObjective);
    }

    public void CreateProgress()
    {
        var progress = Spawn(ProgressPrototype);
        var component = Comp<RatvarProgressComponent>(progress);
        InitializeProgress(progress, component);
        _progressEntity = (progress, component);
    }

    private void AddObjectivesToRighteouses(params EntityUid[] objectiveEntities)
    {
        var query = EntityQueryEnumerator<RatvarRighteousComponent>();
        while (query.MoveNext(out var uid, out _))
        {
            AddObjectivesToRighteous(uid, objectiveEntities);
        }
        var query_ = EntityQueryEnumerator<RatvarMarauderShellComponent>();
        while (query_.MoveNext(out var uid, out _))
        {
            AddObjectivesToRighteous(uid, objectiveEntities);
        }
    }

    private void AddObjectivesToRighteous(EntityUid user, params EntityUid[] objectiveEntities)
    {
        if (!_mindSystem.TryGetMind(user, out var mindId, out var mind))
            return;

        var access = EnsureComp<AccessComponent>(user);
        access.Tags.Add("RatvarRighteous");

        foreach (var objective in objectiveEntities)
        {
            if (objective == EntityUid.Invalid || mind.Objectives.Contains(objective))
                continue;

            _mindSystem.AddObjective(mindId, mind, objective);
        }
    }

    private void CreateObjective(string objective, ref EntityUid uidToSafe)
    {
        var objectiveId = Spawn(objective);

        if (objectiveId != EntityUid.Invalid)
            uidToSafe = objectiveId;
    }

    private bool IsObjectiveComplete(EntityUid objective)
    {
        var ev = new ObjectiveGetProgressEvent();
        RaiseLocalEvent(objective, ref ev);

        return ev.Progress is >= 1f;
    }

    public bool TryRequestChangePower(int value)
    {
        if (_progressEntity?.Comp is not { } comp)
            return false;

        comp.CurrentPower += value;
        return true;
    }

    public int GetCurrentPower()
    {
        return _progressEntity?.Comp?.CurrentPower ?? 0;
    }

    public bool IsEntityAtSummonPoint(EntityUid uid)
    {
        if (_progressEntity?.Comp is not { } comp)
            return false;

        if (comp.RatvarSummonObjective == EntityUid.Invalid)
            return false;

        if (!TryComp<RatvarSummonObjectiveComponent>(comp.RatvarSummonObjective, out var objectiveComponent))
            return false;

        if (objectiveComponent.Target == null)
            return false;

        var targetTransform = Transform(objectiveComponent.Target.Value);
        var uidTransform = Transform(uid);

        return uidTransform.Coordinates.InRange(EntityManager, targetTransform.Coordinates, 6f);
    }

    public bool IsPortalInProgress()
    {
        return EntityQuery<RatvarPortalComponent>().Any();
    }
}
