// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from tgstation/tgstation (code/modules/fishing, AGPL-3.0-or-later).

using Robust.Shared.Prototypes;

namespace Content.Shared._Mini.Fishing.Components;

/// <summary>
/// A fish that grows into another entity after a while, like a tadpole into a frog or a baby carp into a space carp.
/// </summary>
[RegisterComponent]
public sealed partial class FishGrowthComponent : Component
{
    /// <summary>
    /// Entity spawned in place of the grown fish.
    /// </summary>
    [DataField(required: true)]
    public EntProtoId Result = default!;

    /// <summary>
    /// Seconds until the fish grows. tg scales this by size and weight, a fixed time is close enough.
    /// </summary>
    [DataField]
    public float GrowthTime = 150f;

    /// <summary>
    /// Server time at which the fish grows.
    /// </summary>
    public TimeSpan GrowAt;
}
