// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Humanoid;
using Content.Shared.Store;
using Content.Shared.Humanoid.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype.Set;
using Content.Shared.Mind;

namespace Content.Server.Store.Conditions;

/// <summary>
/// Allows a store entry to be filtered out based on the user's species.
/// Supports both blacklists and whitelists.
/// </summary>
public sealed partial class BuyerSpeciesCondition : ListingCondition
{
    /// <summary>
    /// A whitelist of species that can purchase this listing.
    /// </summary>
    [DataField("whitelist", customTypeSerializer: typeof(PrototypeIdHashSetSerializer<SpeciesPrototype>))]
    public HashSet<string>? Whitelist;

    /// <summary>
    /// A blacklist of species that cannot purchase this listing.
    /// </summary>
    [DataField("blacklist", customTypeSerializer: typeof(PrototypeIdHashSetSerializer<SpeciesPrototype>))]
    public HashSet<string>? Blacklist;

    public override bool Condition(ListingConditionArgs args)
    {
        var ent = args.EntityManager;

        // Buyer is usually the mob opening the store, not the mind entity.
        EntityUid body = args.Buyer;
        if (ent.TryGetComponent<MindComponent>(args.Buyer, out var mindComp))
        {
            if (mindComp.OwnedEntity is not { } owned)
                return Whitelist == null;
            body = owned;
        }
        else if (ent.System<SharedMindSystem>().TryGetMind(args.Buyer, out _, out mindComp))
        {
            if (mindComp.OwnedEntity is { } owned)
                body = owned;
        }

        if (!ent.TryGetComponent<HumanoidAppearanceComponent>(body, out var appearance))
        {
            // Non-humanoids: hide whitelist-only listings, allow unrestricted ones.
            return Whitelist == null;
        }

        if (Blacklist != null && Blacklist.Contains(appearance.Species))
            return false;

        if (Whitelist != null && !Whitelist.Contains(appearance.Species))
            return false;

        return true;
    }
}
