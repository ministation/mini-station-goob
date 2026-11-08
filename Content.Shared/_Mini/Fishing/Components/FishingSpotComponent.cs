// SPDX-License-Identifier: LicenseRef-MiniStation-1.0
// Mechanics reference: tgstation/tgstation (code/modules/fishing).

using Content.Shared.EntityTable.EntitySelectors;
using Robust.Shared.Prototypes;

namespace Content.Shared._Mini.Fishing.Components;

/// <summary>
/// Legacy Goob fishing spot. New spots use <see cref="FishingSourceComponent"/>.
/// </summary>
[RegisterComponent]
public sealed partial class FishingSpotComponent : Component
{
    /// <summary>
    /// All possible fishes to catch here
    /// </summary>
    [DataField(required: true)]
    public EntityTableSelector FishList;

    /// <summary>
    /// Default time for fish to occur
    /// </summary>
    [DataField]
    public float FishDefaultTimer;

    /// <summary>
    /// Variety number that FishDefaultTimer can go up or down to randomly
    /// </summary>
    [DataField]
    public float FishTimerVariety;
}
