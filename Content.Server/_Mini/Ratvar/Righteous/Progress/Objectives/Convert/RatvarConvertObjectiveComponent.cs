// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

﻿using Robust.Shared.GameObjects;
using Robust.Shared.Serialization.Manager.Attributes;

namespace Content.Server.RPSX.DarkForces.Ratvar.Righteous.Progress.Objectives.Convert;

[RegisterComponent]
public sealed partial class RatvarConvertObjectiveComponent : Component
{
    [DataField]
    public float RequiredCount = 5f;
}
