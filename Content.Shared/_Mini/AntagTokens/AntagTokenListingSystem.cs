// SPDX-FileCopyrightText: 2026 Casha
// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.Shared._Mini.AntagTokens;

public sealed class AntagTokenListingSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _proto = default!;

    private readonly Dictionary<string, AntagRoleDefinition> _byId = new();
    private List<AntagRoleDefinition> _sorted = new();

    public override void Initialize()
    {
        base.Initialize();
        RebuildCache();
        SubscribeLocalEvent<PrototypesReloadedEventArgs>(OnProtoReload);
    }

    private void OnProtoReload(PrototypesReloadedEventArgs args)
    {
        if (args.WasModified<AntagTokenCatalogPrototype>())
            RebuildCache();
    }

    private void RebuildCache()
    {
        _byId.Clear();
        _sorted.Clear();

        if (!_proto.TryIndex<AntagTokenCatalogPrototype>(AntagTokenCatalogPrototype.DefaultId, out var catalog))
            return;

        foreach (var entry in catalog.Listings)
        {
            var def = entry.ToDefinition();
            _byId[def.Id] = def;
            _sorted.Add(def);
        }
    }

    public bool TryGetListing(string roleId, [NotNullWhen(true)] out AntagRoleDefinition? definition)
    {
        return _byId.TryGetValue(roleId, out definition);
    }

    public IReadOnlyList<AntagRoleDefinition> ListingsOrdered => _sorted;

    public int ListingCount => _byId.Count;
}
