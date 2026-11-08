// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

using Content.Shared.Actions;

namespace Content.Shared._Lavaland.Body;

[RegisterComponent]
public sealed partial class CursedHeartComponent : Component
{
    public EntityUid? PumpActionEntity;

    public TimeSpan LastPump = TimeSpan.Zero;

    [DataField]
    public float MaxDelay = 5f;
}

public sealed partial class PumpHeartActionEvent : InstantActionEvent;