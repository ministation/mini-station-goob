using Robust.Shared.Prototypes;

namespace Content.Shared.EntityConditions;

public sealed partial class SharedEntityConditionsSystem
{
    [Dependency] private readonly IPrototypeManager _protoMan = default!;

    public bool TryCondition(EntityUid target, [ForbidLiteral] ProtoId<EntityConditionPrototype> id, EntityUid? sourceEnt = null)
    {
        var proto = _protoMan.Index(id);
        return TryCondition(target, proto.Condition, sourceEnt: sourceEnt);
    }
}
