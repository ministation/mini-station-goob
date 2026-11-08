// SPDX-FileCopyrightText: 2026 Egorik1
// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

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
