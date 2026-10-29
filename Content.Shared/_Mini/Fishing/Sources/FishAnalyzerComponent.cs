// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from tgstation/tgstation (code/modules/fishing, AGPL-3.0-or-later).

using Robust.Shared.GameStates;

namespace Content.Shared._Mini.Fishing.Components;

/// <summary>
/// A handheld scanner that reads the status and the traits of a caught fish.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class FishAnalyzerComponent : Component
{
    /// <summary>
    /// The last fish scanned by this analyzer. Server side only.
    /// </summary>
    public EntityUid? Scanned;
}
