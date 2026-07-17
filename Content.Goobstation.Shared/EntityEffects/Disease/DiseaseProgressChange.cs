using Content.Goobstation.Shared.Disease;
using Content.Goobstation.Shared.Disease.Components;
using Content.Shared.EntityEffects;
using Content.Shared.EntityEffects.Effects;
using Content.Goobstation.Shared.EntityEffects.Disease;
using Robust.Shared.Prototypes;

namespace Content.Goobstation.Shared.EntityEffects.Disease;

/// <summary>
/// Уменьшает прогресс болезней выбранного типа на цели.
/// </summary>
public sealed partial class DiseaseProgressChangeSystem : EntityEffectSystem<DiseaseCarrierComponent, DiseaseProgressChange>
{
    protected override void Effect(Entity<DiseaseCarrierComponent> entity, ref EntityEffectEvent<DiseaseProgressChange> args)
    {
        var effect = args.Effect;

        var ev = new DiseaseProgressChange(
            effect.AffectedType,
            effect.ProgressModifier,
            effect.Scaled,
            args.Scale,
            effect.Quantity
        );

        EntityManager.EventBus.RaiseLocalEvent(entity.Owner, ev);
    }
}

public sealed partial class DiseaseProgressChange : EntityEffectBase<DiseaseProgressChange>
{
    /// <summary>
    /// Типы болезней, на которые действует эффект.
    /// </summary>
    [DataField]
    public ProtoId<DiseaseTypePrototype> AffectedType;

    /// <summary>
    /// Величина изменения прогресса болезни.
    /// </summary>
    [DataField]
    public float ProgressModifier = -0.02f;

    [DataField]
    public bool Scaled = true;

    [DataField]
    public float Scale = 1f;

    [DataField]
    public float Quantity = 1f;

    public DiseaseProgressChange() { }

    public DiseaseProgressChange(
        ProtoId<DiseaseTypePrototype> affectedType,
        float progressModifier,
        bool scaled,
        float scale,
        float quantity)
    {
        AffectedType = affectedType;
        ProgressModifier = progressModifier;
        Scaled = scaled;
        Scale = scale;
        Quantity = quantity;
    }

    public override string? EntityEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
    {
        return Loc.GetString("reagent-effect-guidebook-disease-progress-change",
            ("chance", Probability),
            ("type", prototype.Index<DiseaseTypePrototype>(AffectedType).LocalizedName),
            ("amount", ProgressModifier));
    }
}
