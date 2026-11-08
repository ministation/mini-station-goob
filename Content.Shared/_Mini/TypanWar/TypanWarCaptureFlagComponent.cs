// SPDX-FileCopyrightText: 2026 Egorik1
// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

using Robust.Shared.GameStates;

namespace Content.Shared._Mini.TypanWar;

/// <summary>
/// Passable war capture flag in the center of a capture zone.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(raiseAfterAutoHandleState: true)]
[Access(typeof(SharedTypanWarCaptureZoneSystem))]
public sealed partial class TypanWarCaptureFlagComponent : Component
{
    [DataField, AutoNetworkedField]
    public TypanWarCaptureOwner CaptureOwner = TypanWarCaptureOwner.Neutral;

    [DataField]
    public EntityUid? Zone;
}
