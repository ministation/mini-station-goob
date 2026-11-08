// SPDX-License-Identifier: LicenseRef-MiniStation-1.0


using Content.Shared.Cargo.Prototypes;
using Content.Shared._Mini.ERT.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Server._Mini.ERTCall;

[RegisterComponent]
public sealed partial class ErtResponceConsoleComponent : Component
{
    [DataField]
    public List<ProtoId<ErtTeamPrototype>> Teams = new();

    [DataField]
    public ProtoId<CargoAccountPrototype> Account = "Security";
}
