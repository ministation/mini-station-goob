// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

﻿using Robust.Shared.GameObjects;
using Robust.Shared.Serialization.Manager.Attributes;

namespace Content.Server.RPSX.DarkForces.Ratvar.Righteous.Progress.Objectives.Power;

[RegisterComponent]
public sealed partial class RatvarPowerObjectiveComponent : Component
{
    [DataField]
    public int RequiredCount = 20000;
}
