// SPDX-FileCopyrightText: 2026 Casha
// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Content.Shared._Mini.AntagTokens;
using Content.Shared.Roles;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.Shared._Mini.AntagUnlock;

public sealed class AntagUnlockListingSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly AntagTokenListingSystem _tokenListings = default!;

    private readonly Dictionary<ProtoId<AntagPrototype>, AntagUnlockListingEntry> _byAntag = new();
    private readonly Dictionary<ProtoId<AntagPrototype>, int> _shopCostByAntag = new();

    public override void Initialize()
    {
        base.Initialize();
        RebuildCache();
        SubscribeLocalEvent<PrototypesReloadedEventArgs>(OnProtoReload);
    }

    private void OnProtoReload(PrototypesReloadedEventArgs args)
    {
        if (args.WasModified<AntagUnlockCatalogPrototype>()
            || args.WasModified<AntagPrototype>()
            || args.WasModified<AntagTokenCatalogPrototype>())
        {
            RebuildCache();
        }
    }

    private void RebuildCache()
    {
        _byAntag.Clear();
        _shopCostByAntag.Clear();

        if (!_proto.HasIndex<AntagUnlockCatalogPrototype>(AntagUnlockCatalogPrototype.DefaultId))
            return;

        foreach (var listing in _tokenListings.ListingsOrdered)
        {
            if (string.IsNullOrWhiteSpace(listing.AntagId))
                continue;

            var antagId = new ProtoId<AntagPrototype>(listing.AntagId);
            if (!_shopCostByAntag.TryGetValue(antagId, out var existing) || listing.Cost < existing)
                _shopCostByAntag[antagId] = listing.Cost;
        }

        foreach (var antag in _proto.EnumeratePrototypes<AntagPrototype>())
        {
            if (!antag.SetPreference)
                continue;

            _byAntag[antag.ID] = new AntagUnlockListingEntry
            {
                AntagId = antag.ID,
            };
        }
    }

    public bool TryGetListing(ProtoId<AntagPrototype> antagId, [NotNullWhen(true)] out AntagUnlockListingEntry? entry)
    {
        return _byAntag.TryGetValue(antagId, out entry);
    }

    public bool TryGetShopTokenCost(ProtoId<AntagPrototype> antagId, out int cost)
    {
        return _shopCostByAntag.TryGetValue(antagId, out cost);
    }
}
