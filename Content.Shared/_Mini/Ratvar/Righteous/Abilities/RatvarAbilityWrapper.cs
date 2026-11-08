// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

﻿namespace Content.Shared.RPSX.DarkForces.Ratvar.Righteous.Abilities;

public interface IRatvarAbilityRelay
{
    TimeSpan UseTime { get; set; }
}

public sealed partial class RatvarAbilityWrapper<TEvent> : EntityEventArgs
{
    public TEvent Args;

    public RatvarAbilityWrapper(TEvent args)
    {
        Args = args;
    }
}
