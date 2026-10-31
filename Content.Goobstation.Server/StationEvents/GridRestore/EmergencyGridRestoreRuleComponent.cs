// SPDX-FileCopyrightText: 2026 Goob Station Contributors
//
// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.Goobstation.Server.StationEvents.GridRestore;

[RegisterComponent, Access(typeof(EmergencyGridRestoreRuleSystem))]
public sealed partial class EmergencyGridRestoreRuleComponent : Component
{
    [DataField]
    public float ApcChargeFraction = 1f;

    [DataField]
    public float SmesChargeFraction = 0.5f;
}
