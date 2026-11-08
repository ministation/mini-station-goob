// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

﻿using Content.Shared.Actions;

namespace Content.Shared.RPSX.DarkForces.Ratvar.Righteous.Abilities.Weapons;

public sealed partial class RatvarHammerKnockOffEvent : InstantActionEvent, IRatvarAbilityRelay
{
    public TimeSpan UseTime { get; set; } = TimeSpan.FromSeconds(5);
}
