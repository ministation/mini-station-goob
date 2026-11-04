// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.DoAfter;

namespace Content.Server._Lavaland.BossMechanics;

/// <summary>
/// Molecular accelerator of The Thing arena. Overloading it while the boss is down pushes
/// the boss into the next phase. Each machine only works once.
/// </summary>
[RegisterComponent]
public sealed partial class ThingAcceleratorComponent : Component
{
    /// <summary>
    /// The boss this machine is wired to, set by the arena spawner.
    /// </summary>
    [ViewVariables]
    public EntityUid? Boss;

    [ViewVariables]
    public bool Functional = true;

    [ViewVariables]
    public bool Deployed;

    [ViewVariables]
    public bool Busy;

    [DataField]
    public TimeSpan OverloadTime = TimeSpan.FromSeconds(1);
}

