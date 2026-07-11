// SPDX-FileCopyrightText: 2026 Egorik1
// Мини-станция, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/ministation/mini-station-goob/master/LICENSE.TXT

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
