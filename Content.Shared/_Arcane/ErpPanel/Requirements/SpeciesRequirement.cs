using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Prototypes;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Arcane.ErpPanel.Requirements;

[Serializable, NetSerializable]
public sealed partial class SpeciesRequirement : ErpRequirement
{
    [DataField(required: true)]
    public HashSet<ProtoId<SpeciesPrototype>> Species = new();

    public override bool IsAvailable(EntityUid uid, IEntityManager entityManager)
    {
        if (!entityManager.TryGetComponent<HumanoidAppearanceComponent>(uid, out var humanoid))
            return false;

        return Species.Contains(humanoid.Species);
    }
}
