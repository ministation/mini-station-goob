// SPDX-License-Identifier: LicenseRef-MiniStation-1.0


using Content.Shared._Mini.ERT.Prototypes;
using Content.Shared.Mobs;
using Robust.Shared.Prototypes;

namespace Content.Server._Mini.ERT.Components;

[RegisterComponent]
public sealed partial class ResponceErtImplantComponent : Component
{
    [DataField(required: true)]
    public ProtoId<ErtTeamPrototype> Team;

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public List<MobState> AllowedStates = new();
}
