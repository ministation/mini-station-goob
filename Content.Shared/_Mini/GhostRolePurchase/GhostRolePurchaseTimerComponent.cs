// SPDX-FileCopyrightText: 2026 Casha
// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._Mini.GhostRolePurchase;

[RegisterComponent, NetworkedComponent]
public sealed partial class GhostRolePurchaseTimerComponent : Component
{
    [DataField]
    public TimeSpan? TimerEndTime;

    [DataField]
    public bool IsBlocked = false;

    [DataField]
    public TimeSpan BlockDuration = TimeSpan.FromMinutes(15);
}
