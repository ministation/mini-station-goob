// SPDX-FileCopyrightText: 2026 Space Station 14 Contributors
//
// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.Server._Mini.StationsControl;

[RegisterComponent]
public sealed partial class StationsControlConsoleComponent : Component
{
    [DataField]
    public List<string> AllowedAlertLevels = new();

    [DataField]
    public bool AllowAnyAlertLevel = false;
}
