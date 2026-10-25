// SPDX-FileCopyrightText: 2026 Mini Station
// Мини-станция, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/ministation/mini-station-goob/master/LICENSE.TXT

namespace Content.Shared._Mini.Ninja;

/// <summary>
/// Raised on the suit when the ninja is revealed: attacked, damaged or otherwise exposed.
/// Modules with active effects (cloak, disguise) should disable themselves.
/// </summary>
[ByRefEvent]
public readonly record struct NinjaSuitRevealedEvent(EntityUid Suit, EntityUid Wearer);
