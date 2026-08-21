using Robust.Shared.Prototypes;

namespace Content.Shared.EntityEffects;

public sealed partial class SharedEntityEffectsSystem
{
    [Dependency] private readonly IPrototypeManager _protoMan = default!;

    public void TryApplyEffect(EntityUid target, [ForbidLiteral] ProtoId<EntityEffectPrototype> id, float scale = 1f, EntityUid? user = null)
    {
        var proto = _protoMan.Index(id);
        if (_condition.TryConditions(target, proto.Conditions))
            ApplyEffects(target, proto.Effects, scale, user);
    }
}
