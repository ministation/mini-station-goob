// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from tgstation/tgstation (code/modules/fishing, AGPL-3.0-or-later).

using Content.Shared.Chemistry.Reagent;
using Robust.Shared.Prototypes;

namespace Content.Goobstation.Shared.Fishing.Aquarium;

/// <summary>
/// A can of fish feed. Using it on an aquarium feeds every fish once.
/// </summary>
[RegisterComponent]
public sealed partial class FishFeedComponent : Component
{
    /// <summary>
    /// How many times the can can feed every fish in an aquarium.
    /// </summary>
    [DataField]
    public int Uses = 5;

    /// <summary>
    /// Reagent the feed is made of. Fish that need another food ignore it.
    /// </summary>
    [DataField]
    public ProtoId<ReagentPrototype> Reagent = "Nutriment";
}

/// <summary>
/// A decorative prop that can be put in an aquarium to make it prettier.
/// </summary>
[RegisterComponent]
public sealed partial class AquariumPropComponent : Component
{
    [DataField]
    public int Beauty = 150;
}

/// <summary>
/// An upgrade kit that turns a plain aquarium into an improved one.
/// </summary>
[RegisterComponent]
public sealed partial class AquariumUpgradeComponent : Component
{
    [DataField(required: true)]
    public AquariumUpgradeFlags Upgrade = AquariumUpgradeFlags.None;
}
