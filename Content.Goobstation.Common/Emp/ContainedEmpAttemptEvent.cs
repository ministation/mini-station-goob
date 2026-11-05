// SPDX-FileCopyrightText: 2026 Goob Station Contributors
//
// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.Goobstation.Common.Emp;

[ByRefEvent]
public record struct ContainedEmpAttemptEvent(EntityUid Target)
{
    public bool Cancelled;
}
