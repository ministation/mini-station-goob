// SPDX-FileCopyrightText: 2026 Egorik1
// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

using Robust.Shared.GameStates;

namespace Content.Shared._Mini.TypanWar;

/// <summary>
/// Marks a war reinforcement shuttle grid and its tracked pilot console.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class TypanWarDropShuttleComponent : Component
{
    [DataField]
    public TypanWarSide Side;

    [DataField]
    public EntityUid Rule;

    [DataField]
    public EntityUid Console;
}
