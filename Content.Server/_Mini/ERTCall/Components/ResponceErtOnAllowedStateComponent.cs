// SPDX-License-Identifier: LicenseRef-MiniStation-1.0


using Content.Shared._Mini.ERT.Prototypes;
using Content.Shared.Mobs;
using Robust.Shared.Prototypes;

namespace Content.Server._Mini.ERT.Components;

[RegisterComponent]
public sealed partial class ResponceErtOnAllowedStateComponent : Component
{
    [DataField(required: true), ViewVariables(VVAccess.ReadOnly)]
    public ProtoId<ErtTeamPrototype> Team;

    [DataField, ViewVariables(VVAccess.ReadOnly)]
    public List<MobState> AllowedStates = new();

    [DataField, ViewVariables(VVAccess.ReadOnly)]
    public bool IsReady = false;
}
