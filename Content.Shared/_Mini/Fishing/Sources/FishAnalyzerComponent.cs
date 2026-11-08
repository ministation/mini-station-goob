// SPDX-License-Identifier: LicenseRef-MiniStation-1.0
// Mechanics reference: tgstation/tgstation (code/modules/fishing).

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
