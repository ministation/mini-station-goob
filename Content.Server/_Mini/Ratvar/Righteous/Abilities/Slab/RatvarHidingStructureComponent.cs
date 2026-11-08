// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

﻿using Robust.Shared.GameObjects;
using Robust.Shared.Serialization.Manager.Attributes;

namespace Content.Server.RPSX.DarkForces.Ratvar.Righteous.Abilities.Slab;

[RegisterComponent]
public sealed partial class RatvarHidingStructureComponent : Component
{
    [DataField]
    public EntityUid? OriginalStructure;

    [DataField]
    public EntityUid? HidingSlab;
}
