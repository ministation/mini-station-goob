// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

namespace Content.Shared._Lavaland.Chasm;

[ByRefEvent]
public record struct BeforeChasmFallingEvent(EntityUid Entity, bool Cancelled = false);