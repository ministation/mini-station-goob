// SPDX-FileCopyrightText: 2026 Goob Station Contributors
//
// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.Goobstation.Common.Cuffs;

[ByRefEvent]
public record struct ModifyCuffDurationEvent(EntityUid Target, float Duration);
