// SPDX-FileCopyrightText: 2026 Goob Station Contributors
//
// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.Goobstation.Common.Weapons;

[ByRefEvent]
public record struct ShoveAttemptEvent(EntityUid User, bool Cancelled = false);
