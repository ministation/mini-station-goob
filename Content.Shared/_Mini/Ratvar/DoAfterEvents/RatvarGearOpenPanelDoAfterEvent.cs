// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

﻿using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared.RPSX.DarkForces.Ratvar.Events;

[Serializable, NetSerializable]
public sealed partial class RatvarGearOpenPanelDoAfterEvent : DoAfterEvent
{
    public override DoAfterEvent Clone()
    {
        return this;
    }
}

[Serializable, NetSerializable]
public sealed partial class RatvarGearInsertDoAfterEvent : DoAfterEvent
{
    public override DoAfterEvent Clone()
    {
        return this;
    }
}
