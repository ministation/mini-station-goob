// SPDX-FileCopyrightText: 2026 Goob Station Contributors
//
// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.Goobstation.Common.Radio;

[ByRefEvent]
public record struct LanguageRadioAttemptEvent(string Language)
{
    public bool Allowed;
    public string? TranslateTo;
}
