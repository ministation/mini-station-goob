// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

﻿using System.Numerics;
using Content.Shared.RPSX.DarkForces.Ratvar.Righteous.Roles;
using Content.Shared.StatusIcon.Components;
using Robust.Shared.Prototypes;

namespace Content.Client.RPSX.DarkForces.Ratvar.Roles;

public sealed class RatvarIconsSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prototype = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<RatvarRighteousComponent, GetStatusIconsEvent>(GetRatvarIcon);
    }

    private void GetRatvarIcon(Entity<RatvarRighteousComponent> ent, ref GetStatusIconsEvent args)
    {
        if (_prototype.TryIndex(ent.Comp.StatusIcon, out var iconPrototype))
            args.StatusIcons.Add(iconPrototype);
    }
}
