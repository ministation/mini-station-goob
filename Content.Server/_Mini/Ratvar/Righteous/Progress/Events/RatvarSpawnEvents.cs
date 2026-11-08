// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

﻿using Robust.Shared.GameObjects;

namespace Content.Server.RPSX.DarkForces.Ratvar.Righteous.Progress.Events;

[ByRefEvent]
public record RatvarSpawnStartedEvent(EntityUid Portal);

[ByRefEvent]
public record RatvarSpawnedEvent(EntityUid Ratvar);

[ByRefEvent]
public record RatvarSpawnCanceledEvent;
