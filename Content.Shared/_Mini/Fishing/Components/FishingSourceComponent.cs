// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from tgstation/tgstation (code/modules/fishing, AGPL-3.0-or-later).

using Robust.Shared.Prototypes;

using Content.Shared._Mini.Fishing.Prototypes;

namespace Content.Shared._Mini.Fishing.Components;

/// <summary>
/// Marks an entity as a fishing spot with a fixed fish source. Water, lava and chasm
/// floor entities, toilets, aquariums and similar places can carry this.
/// </summary>
[RegisterComponent]
public sealed partial class FishingSourceComponent : Component
{
    [DataField(required: true)]
    public ProtoId<FishingSourcePrototype> Source = default!;
}