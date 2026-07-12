// SPDX-FileCopyrightText: 2026 Egorik1
// Мини-станция, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/ministation/mini-station-goob/master/LICENSE.TXT

using Robust.Shared.GameObjects;

namespace Content.Shared._Mini.TypanWar;

/// <summary>
/// Raised when station war layout cannot be applied and combat should abort.
/// </summary>
public sealed class TypanWarLayoutFailedEvent : EntityEventArgs
{
    public EntityUid Rule;

    public TypanWarLayoutFailedEvent(EntityUid rule)
    {
        Rule = rule;
    }
}
