// SPDX-FileCopyrightText: 2026 Egorik1
// Мини-станция, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/ministation/mini-station-goob/master/LICENSE.TXT

namespace Content.Shared._Mini.TypanWar;

/// <summary>
/// Raised after war stations are merged onto one map and trade posts repositioned.
/// </summary>
public sealed class TypanWarLayoutReadyEvent : EntityEventArgs
{
    public EntityUid Rule;
    public EntityUid NtStation;
    public EntityUid TypanStation;

    public TypanWarLayoutReadyEvent(EntityUid rule, EntityUid ntStation, EntityUid typanStation)
    {
        Rule = rule;
        NtStation = ntStation;
        TypanStation = typanStation;
    }
}
