// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

﻿namespace Content.Shared._Lavaland.ItemUpgrades.Events;

[ByRefEvent]
public record struct BeforeItemUpgradeRelayEvent(bool Cancelled = false);
