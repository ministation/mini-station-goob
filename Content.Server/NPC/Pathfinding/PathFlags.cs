// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.Server.NPC.Pathfinding;

[Flags]
public enum PathFlags : byte
{
    None = 0,

    /// <summary>
    /// Do we have any form of access.
    /// </summary>
    Access = 1 << 0,

    /// <summary>
    /// Can we pry airlocks if necessary.
    /// </summary>
    Prying = 1 << 1,

    /// <summary>
    /// Can stuff like walls be broken.
    /// </summary>
    Smashing = 1 << 2,

    /// <summary>
    /// Can we climb it like a table or railing.
    /// </summary>
    Climbing = 1 << 3,

    /// <summary>
    /// Can we open stuff that requires interaction (e.g. click-open doors).
    /// </summary>
    Interact = 1 << 4,

     Blob = 1 << 5,  // Goobstation - Blob

    /// <summary>
    /// Mini: this NPC opens doors the way a crew member does — with an ID it carries (bump or click) —
    /// instead of prying every single one. A door it has no access to is a dead end, not a puzzle.
    /// </summary>
    CrewDoors = 1 << 6,
}