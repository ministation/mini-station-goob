// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from tgstation/tgstation (code/modules/fishing, AGPL-3.0-or-later).

using Robust.Shared.GameStates;

namespace Content.Shared._Mini.Fishing.Components;

/// <summary>
/// A handheld scanner that reads the status and the traits of caught fish.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class FishAnalyzerComponent : Component
{
    /// <summary>
    /// Fish read by the last scan, in the order they are shown in the window.
    /// Server side only: the window state is rebuilt from this list on every open.
    /// </summary>
    public List<EntityUid> ScannedFish = new();
}
