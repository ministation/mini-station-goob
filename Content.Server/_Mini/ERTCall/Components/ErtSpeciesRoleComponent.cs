// SPDX-License-Identifier: LicenseRef-MiniStation-1.0


using Content.Server._Mini.ERT;

namespace Content.Server._Mini.ERTCall;

[RegisterComponent, Access(typeof(ErtResponceSystem))]
public sealed partial class ErtSpeciesRoleComponent : Component
{
    [ViewVariables(VVAccess.ReadOnly)]
    public WaitingSpeciesSettings? Settings;
}
