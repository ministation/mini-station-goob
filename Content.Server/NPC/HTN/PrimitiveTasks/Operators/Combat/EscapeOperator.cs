// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Threading;
using System.Threading.Tasks;
using Content.Server.NPC.Components;
using Content.Server.Storage.EntitySystems;
using Content.Shared.CombatMode;
using Content.Shared.Storage.Components;
using Robust.Server.Containers;

namespace Content.Server.NPC.HTN.PrimitiveTasks.Operators.Combat.Melee;

public sealed partial class EscapeOperator : HTNOperator, IHtnConditionalShutdown
{
    [Dependency] private readonly IEntityManager _entManager = default!;
    private ContainerSystem _container = default!;
    private EntityStorageSystem _entityStorage = default!;

    [DataField("shutdownState")]
    public HTNPlanState ShutdownState { get; private set; } = HTNPlanState.TaskFinished;

    [DataField("targetKey", required: true)]
    public string TargetKey = default!;

    public override void Initialize(IEntitySystemManager sysManager)
    {
        base.Initialize(sysManager);
        _container = sysManager.GetEntitySystem<ContainerSystem>();
        _entityStorage = sysManager.GetEntitySystem<EntityStorageSystem>();
    }

    public override void Startup(NPCBlackboard blackboard)
    {
        base.Startup(blackboard);
        var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);
        var target = blackboard.GetValue<EntityUid>(TargetKey);

        if (TryEscape(owner, target))
        {
            TaskShutdown(blackboard, HTNOperatorStatus.Finished);
            return;
        }

        var melee = _entManager.EnsureComponent<NPCMeleeCombatComponent>(owner);
        melee.MissChance = blackboard.GetValueOrDefault<float>(NPCBlackboard.MeleeMissChance, _entManager);
        melee.Target = target;
    }

    public override async Task<(bool Valid, Dictionary<string, object>? Effects)> Plan(NPCBlackboard blackboard,
        CancellationToken cancelToken)
    {
        var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);
        if (!blackboard.TryGetValue<EntityUid>(TargetKey, out var target, _entManager))
        {
            return (false, null);
        }

        if (!_container.IsEntityInContainer(owner))
        {
            return (false, null);
        }

        // Only attempt open during planning for real entity storage.
        // Non-storage containers (grinders, etc.) are handled in Startup/Update via remove.
        if (_entManager.HasComponent<EntityStorageComponent>(target) &&
            _entityStorage.TryOpenStorage(owner, target))
        {
            return (false, null);
        }

        return (true, null);
    }

    /// <summary>
    /// Opens entity storage if possible, otherwise force-removes from a regular container.
    /// Avoids Resolve spam when the container owner is not an <see cref="EntityStorageComponent"/>.
    /// </summary>
    private bool TryEscape(EntityUid owner, EntityUid target)
    {
        if (_entManager.HasComponent<EntityStorageComponent>(target))
            return _entityStorage.TryOpenStorage(owner, target);

        // Regular containers (grinders, inventories, etc.) — yank out instead of resolving EntityStorage.
        return _container.TryRemoveFromContainer(owner, force: true) && !_container.IsEntityInContainer(owner);
    }

    public void ConditionalShutdown(NPCBlackboard blackboard)
    {
        var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);
        _entManager.System<SharedCombatModeSystem>().SetInCombatMode(owner, false);
        _entManager.RemoveComponent<NPCMeleeCombatComponent>(owner);
        blackboard.Remove<EntityUid>(TargetKey);
    }

    public override void TaskShutdown(NPCBlackboard blackboard, HTNOperatorStatus status)
    {
        base.TaskShutdown(blackboard, status);

        ConditionalShutdown(blackboard);
    }

    public override void PlanShutdown(NPCBlackboard blackboard)
    {
        base.PlanShutdown(blackboard);

        ConditionalShutdown(blackboard);
    }

    public override HTNOperatorStatus Update(NPCBlackboard blackboard, float frameTime)
    {
        base.Update(blackboard, frameTime);
        var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);
        HTNOperatorStatus status;

        if (_entManager.TryGetComponent<NPCMeleeCombatComponent>(owner, out var combat) &&
            blackboard.TryGetValue<EntityUid>(TargetKey, out var target, _entManager))
        {
            combat.Target = target;

            // Success
            if (!_container.IsEntityInContainer(owner))
            {
                status = HTNOperatorStatus.Finished;
            }
            else
            {
                if (TryEscape(owner, target))
                {
                    status = HTNOperatorStatus.Finished;
                }
                else
                {
                    switch (combat.Status)
                    {
                        case CombatStatus.TargetOutOfRange:
                        case CombatStatus.Normal:
                            status = HTNOperatorStatus.Continuing;
                            break;
                        default:
                            status = HTNOperatorStatus.Failed;
                            break;
                    }
                }
            }
        }
        else
        {
            status = HTNOperatorStatus.Failed;
        }

        // Mark it as finished to continue the plan.
        if (status == HTNOperatorStatus.Continuing && ShutdownState == HTNPlanState.PlanFinished)
        {
            status = HTNOperatorStatus.Finished;
        }

        return status;
    }
}