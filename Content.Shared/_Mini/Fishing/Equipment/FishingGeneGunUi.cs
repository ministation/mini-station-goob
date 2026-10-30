// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from tgstation/tgstation (code/modules/fishing, AGPL-3.0-or-later).

using Content.Shared._Mini.Fishing.Prototypes;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Mini.Fishing;

[Serializable, NetSerializable]
public enum FishingGeneGunUiKey : byte
{
    Key,
}

/// <summary>
/// One trait the player can extract from the fish.
/// </summary>
[Serializable, NetSerializable]
public sealed class FishingGeneGunTraitState
{
    public readonly ProtoId<FishTraitPrototype> Trait;

    /// <summary>
    /// Localization key of the trait name.
    /// </summary>
    public readonly LocId Name;

    public FishingGeneGunTraitState(ProtoId<FishTraitPrototype> trait, LocId name)
    {
        Trait = trait;
        Name = name;
    }
}

[Serializable, NetSerializable]
public sealed class FishingGeneGunBoundUserInterfaceState : BoundUserInterfaceState
{
    public readonly List<FishingGeneGunTraitState> Traits;

    public FishingGeneGunBoundUserInterfaceState(List<FishingGeneGunTraitState> traits)
    {
        Traits = traits;
    }
}

/// <summary>
/// Sent when the player picks the trait to extract from the fish.
/// </summary>
[Serializable, NetSerializable]
public sealed class FishingGeneGunSelectTraitMessage : BoundUserInterfaceMessage
{
    public readonly ProtoId<FishTraitPrototype> Trait;

    public FishingGeneGunSelectTraitMessage(ProtoId<FishTraitPrototype> trait)
    {
        Trait = trait;
    }
}
