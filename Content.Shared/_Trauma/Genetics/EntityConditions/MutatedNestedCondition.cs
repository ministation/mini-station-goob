using Content.Shared.EntityConditions;
using Content.Shared._Trauma.Genetics.Mutations;

namespace Content.Shared._Trauma.Genetics.EntityConditions;

public sealed partial class MutatedNestedCondition : EntityConditionBase<MutatedNestedCondition>
{
    [DataField(required: true)]
    public EntityCondition Condition = default!;

    public override string EntityConditionGuidebookText(IPrototypeManager prototype)
        => Condition.EntityConditionGuidebookText(prototype);
}

public sealed partial class MutatedNestedConditionSystem : EntityConditionSystem<MutationComponent, MutatedNestedCondition>
{
    [Dependency] private SharedEntityConditionsSystem _conditions = default!;

    protected override void Condition(Entity<MutationComponent> ent, ref EntityConditionEvent<MutatedNestedCondition> args)
    {
        if (ent.Comp.Target is { } target)
            args.Result = _conditions.TryCondition(target, args.Condition.Condition, args.SourceEnt);
    }
}
