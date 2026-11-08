// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

using Content.Shared._White.RadialSelector;
using Robust.Shared.GameStates;

namespace Content.Shared.RPSX.DarkForces.Ratvar.Construction;

[RegisterComponent, NetworkedComponent]
public sealed partial class RatvarConstructionComponent : Component
{
    [DataField(required: true)]
    public List<RadialSelectorEntry> Entries = new();
}
