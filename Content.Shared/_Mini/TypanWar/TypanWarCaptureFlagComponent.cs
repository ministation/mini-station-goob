// SPDX-FileCopyrightText: 2026 Egorik1
// Мини-станция, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/ministation/mini-station-goob/master/LICENSE.TXT

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
