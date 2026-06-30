using Content.Goobstation.Shared.Xenobiology;
using Content.Server.NPC;
using Content.Server.NPC.HTN.Preconditions;

namespace Content.Goobstation.Server.Xenobiology.HTN;

public sealed partial class SlimeNotMergedClusterPrecondition : HTNPrecondition
{
    [Dependency] private readonly IEntityManager _entManager = default!;

    public override bool IsMet(NPCBlackboard blackboard)
    {
        var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);
        return !SlimeClusterHelper.IsMergedCluster(owner, _entManager);
    }
}
