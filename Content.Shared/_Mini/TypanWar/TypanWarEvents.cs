// SPDX-FileCopyrightText: 2026 Egorik1
// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

namespace Content.Shared._Mini.TypanWar;

/// <summary>
/// Raised on the server when station war combat begins.
/// </summary>
public sealed class TypanWarStartedEvent : EntityEventArgs
{
    public EntityUid Rule;
    public EntityUid NtStation;
    public EntityUid TypanStation;

    public TypanWarStartedEvent(EntityUid rule, EntityUid ntStation, EntityUid typanStation)
    {
        Rule = rule;
        NtStation = ntStation;
        TypanStation = typanStation;
    }
}
