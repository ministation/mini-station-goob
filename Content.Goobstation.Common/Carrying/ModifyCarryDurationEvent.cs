// SPDX-FileCopyrightText: 2026 Goob Station Contributors
//
// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.Goobstation.Common.Carrying;

[ByRefEvent]
public record struct ModifyCarryDurationEvent(EntityUid Carried, float Multiplier = 1f);
