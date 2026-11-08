// SPDX-FileCopyrightText: 2026 Egorik1
// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

using Robust.Shared.GameStates;

namespace Content.Shared._Mini.TypanWar;

/// <summary>
/// Marks the shuttle console monitored for war drop shuttle replacement.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class TypanWarDropShuttleConsoleComponent : Component
{
    [DataField]
    public TypanWarSide Side;

    [DataField]
    public EntityUid Rule;

    [DataField]
    public EntityUid ShuttleGrid;
}
