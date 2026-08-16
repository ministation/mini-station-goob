using Content.Shared._Mini.Construction.Prototypes;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Mini.Construction.Components;

[RegisterComponent, NetworkedComponent]
public sealed partial class MachinePartComponent : Component
{
    [DataField(required: true)]
    public ProtoId<MachinePartPrototype> Part;

    [DataField]
    public int Tier = 1;

    // TODO: Hook into machine power scaling when machine consumers expose part-based energy coefficients.
    [DataField]
    public int EnergyRating = 1;
}
