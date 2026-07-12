// SPDX-FileCopyrightText: 2026 Egorik1
// Мини-станция, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/ministation/mini-station-goob/master/LICENSE.TXT

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
