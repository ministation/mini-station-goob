// SPDX-FileCopyrightText: 2026 Mini Station
// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

namespace Content.Shared._Mini.Ninja;

/// <summary>
/// Raised on the suit when the ninja is revealed: attacked, damaged or otherwise exposed.
/// Modules with active effects (cloak, disguise) should disable themselves.
/// </summary>
[ByRefEvent]
public readonly record struct NinjaSuitRevealedEvent(EntityUid Suit, EntityUid Wearer);
