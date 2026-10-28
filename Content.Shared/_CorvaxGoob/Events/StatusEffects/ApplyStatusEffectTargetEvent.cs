using Robust.Shared.Prototypes;

namespace Content.Shared._CorvaxGoob.Events.StatusEffects;

[Serializable, DataDefinition]
public sealed partial class ApplyStatusEffectTargetEvent : BaseTargetEvent
{
    [DataField]
    [AlwaysPushInheritance]
    public EntProtoId Effect;

    [DataField]
    [AlwaysPushInheritance]
    public float Time = 0;
}
