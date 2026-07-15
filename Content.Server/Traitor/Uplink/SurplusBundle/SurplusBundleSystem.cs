// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using Content.Server.Storage.EntitySystems;
using Content.Server.Store.Systems;
using Content.Goobstation.Maths.FixedPoint;
using Content.Shared.Store;
using Content.Shared.Store.Components;
using Robust.Shared.Random;

namespace Content.Server.Traitor.Uplink.SurplusBundle;

public sealed class SurplusBundleSystem : EntitySystem
{
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly EntityStorageSystem _entityStorage = default!;
    [Dependency] private readonly StoreSystem _store = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SurplusBundleComponent, MapInitEvent>(OnMapInit);
    }

    private void OnMapInit(EntityUid uid, SurplusBundleComponent component, MapInitEvent args)
    {
        if (!TryComp<StoreComponent>(uid, out var store))
            return;

        FillStorage((uid, component, store));
    }

    private void FillStorage(Entity<SurplusBundleComponent, StoreComponent> ent)
    {
        var cords = Transform(ent).Coordinates;
        var content = GetRandomContent(ent);
        foreach (var item in content)
        {
            var dode = Spawn(item.ProductEntity, cords);
            _entityStorage.Insert(dode, ent);
        }
    }

    // wow, is this leetcode reference?
    private List<ListingData> GetRandomContent(Entity<SurplusBundleComponent, StoreComponent> ent)
    {
        var ret = new List<ListingData>();

        var listings = _store.GetAvailableListings(ent, null, ent.Comp2.Categories)
            .OrderBy(p => p.Cost.Values.Sum())
            .ToList();

        if (listings.Count == 0)
            return ret;

        var totalCost = FixedPoint2.Zero;
        var index = 0;
        while (totalCost < ent.Comp1.TotalPrice)
        {
            // All data is sorted in price descending order
            // Find new item with the lowest acceptable price
            // All expansive items will be before index, all acceptable after
            var remainingBudget = ent.Comp1.TotalPrice - totalCost;
            while (listings[index].Cost.Values.Sum() > remainingBudget)
            {
                index++;
                if (index >= listings.Count)
                {
                    // Looks like no cheap items left
                    // It shouldn't be case for ss14 content
                    // Because there are 1 TC items
                    return ret;
                }
            }

            // Select random listing and add into crate
            var randomItem = ent.Comp1.CostWeightedSelection
                ? PickCostWeightedListing(listings, index, ent.Comp1.CostWeightExponent)
                : listings[_random.Next(index, listings.Count)];
            ret.Add(randomItem);
            totalCost += randomItem.Cost.Values.Sum();
        }

        return ret;
    }

    /// <summary>
    /// Picks a listing with weight inversely proportional to cost — expensive items are possible but rare.
    /// </summary>
    private ListingData PickCostWeightedListing(List<ListingData> listings, int startIndex, float exponent)
    {
        var totalWeight = 0f;
        Span<float> weights = stackalloc float[listings.Count - startIndex];

        for (var i = startIndex; i < listings.Count; i++)
        {
            var cost = MathF.Max((float) listings[i].Cost.Values.Sum(), 1f);
            var weight = 1f / MathF.Pow(cost, exponent);
            weights[i - startIndex] = weight;
            totalWeight += weight;
        }

        var roll = _random.NextFloat() * totalWeight;
        var accumulated = 0f;

        for (var i = 0; i < weights.Length; i++)
        {
            accumulated += weights[i];
            if (roll <= accumulated)
                return listings[startIndex + i];
        }

        return listings[^1];
    }
}