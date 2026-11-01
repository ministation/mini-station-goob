// SPDX-FileCopyrightText: 2026 Space Station 14 Contributors
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections.Generic;
using System.Linq;
using Content.Shared._Mini.Fishing.Components;
using Content.Shared._Mini.Fishing.Prototypes;
using Content.Shared.EntityTable;
using Content.Shared.EntityTable.EntitySelectors;
using Robust.Shared.GameObjects;
using Robust.Shared.Localization;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Mini.Fishing;

/// <summary>
/// Every fish species must be catchable from at least one fishing source, and the
/// fish-porter dimensions must be fully described for the portal menu and the catalog.
/// </summary>
[TestFixture]
public sealed class FishingSourcesCoverageTest
{
    /// <summary>
    /// Fish that only exist as breeding or evolution results, so no source table can ever
    /// catch them. tgstation keeps them out of every fish table as well.
    /// </summary>
    private static readonly string[] NonWildFish =
    {
        "FishGoldfishGill",
        "FishGoldfishThreeEyesGill",
        "FishSludgefishPurple",
        "FishTadpole",
    };

    private static readonly string[] PortalSources =
    {
        "FishingSourcePortal",
        "FishingSourcePortalBeach",
        "FishingSourcePortalChasm",
        "FishingSourcePortalOcean",
        "FishingSourcePortalHyperspace",
        "FishingSourcePortalSyndicate",
        "FishingSourcePortalRandom",
    };

    [Test]
    public async Task EveryFishIsCatchable()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { DummyTicker = true });

        await pair.Server.WaitAssertion(() =>
        {
            var proto = pair.Server.ResolveDependency<IPrototypeManager>();
            var factory = pair.Server.ResolveDependency<IComponentFactory>();

            var caught = new HashSet<string>();
            foreach (var source in proto.EnumeratePrototypes<FishingSourcePrototype>())
                CollectIds(source.Table, caught, proto);

            foreach (var id in NonWildFish)
            {
                Assert.That(proto.HasIndex<EntityPrototype>(id), Is.True,
                    $"{id} is not a fish prototype anymore, remove it from the exception list");
            }

            var uncovered = proto.EnumeratePrototypes<EntityPrototype>()
                .Where(ent => !ent.Abstract
                              && ent.TryComp<FishComponent>(out _, factory)
                              && !caught.Contains(ent.ID)
                              && !NonWildFish.Contains(ent.ID))
                .Select(ent => ent.ID)
                .ToList();

            Assert.That(uncovered, Is.Empty,
                "Fish species with no fishing source: " + string.Join(", ", uncovered));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task PortalDimensionsAreDescribed()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { DummyTicker = true });

        await pair.Server.WaitAssertion(() =>
        {
            var proto = pair.Server.ResolveDependency<IPrototypeManager>();
            var loc = pair.Server.ResolveDependency<ILocalizationManager>();

            foreach (var id in PortalSources)
            {
                Assert.That(proto.TryIndex<FishingSourcePrototype>(id, out var source), Is.True,
                    $"Fishing source {id} is missing");

                Assert.That(source!.RadialName, Is.Not.Null, $"Fishing source {id} has no radial name");
                Assert.That(loc.HasString(source.RadialName!.Value), Is.True,
                    $"Radial name of {id} has no translation");

                if (id == "FishingSourcePortalRandom")
                {
                    Assert.That(source.CatalogDescription, Is.Null,
                        "The randomizer is a secret and must stay out of the catalog");
                    Assert.That(HasDud(source.Table), Is.False,
                        "The randomizer never rolls a dud in tgstation");
                    continue;
                }

                Assert.That(source.CatalogDescription, Is.Not.Null,
                    $"Fishing source {id} has no catalog description");
                Assert.That(loc.HasString(source.CatalogDescription!.Value), Is.True,
                    $"Catalog description of {id} has no translation");
            }
        });

        await pair.CleanReturnAsync();
    }

    private static void CollectIds(EntityTableSelector selector, HashSet<string> ids, IPrototypeManager proto)
    {
        switch (selector)
        {
            case EntSelector ent:
                ids.Add(ent.Id.Id);
                break;
            case GroupSelector group:
                foreach (var child in group.Children)
                    CollectIds(child, ids, proto);
                break;
            case AllSelector all:
                foreach (var child in all.Children)
                    CollectIds(child, ids, proto);
                break;
            case NestedSelector nested:
                if (proto.TryIndex(nested.TableId, out EntityTablePrototype table))
                    CollectIds(table.Table, ids, proto);
                break;
        }
    }

    private static bool HasDud(EntityTableSelector selector)
    {
        switch (selector)
        {
            case NoneSelector:
                return true;
            case GroupSelector group:
                return group.Children.Any(HasDud);
            case AllSelector all:
                return all.Children.Any(HasDud);
            default:
                return false;
        }
    }
}
