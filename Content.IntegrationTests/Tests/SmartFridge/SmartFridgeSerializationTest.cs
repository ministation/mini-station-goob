// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using Content.Shared.CCVar;
using Content.Shared.SmartFridge;
using Robust.Shared.Configuration;
using Robust.Shared.Containers;
using Robust.Shared.ContentPack;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Utility;

namespace Content.IntegrationTests.Tests.SmartFridge;

[TestFixture]
public sealed class SmartFridgeSerializationTest
{
    private const string SmartFridgeProtoId = "SmartFridge";
    private const string SampleItemProtoId = "FoodAmbrosiaVulgaris";

    /// <summary>
    /// A smart fridge tracks its contents in a dictionary keyed by <see cref="SmartFridgeEntry"/>.
    /// Saving a grid with a non-empty fridge must not throw (it used to, because the entry was a
    /// <c>DataRecord</c> and YAML mapping keys must serialize to a string).
    /// </summary>
    [Test]
    public async Task SaveGridWithContainedItemsTest()
    {
        var path = new ResPath("/smartfridge-serialization-test.yml");

        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var mapLoader = entMan.System<MapLoaderSystem>();
        var mapSystem = entMan.System<SharedMapSystem>();
        var containers = entMan.System<SharedContainerSystem>();
        var cfg = server.ResolveDependency<IConfigurationManager>();
        Assert.That(cfg.GetCVar(CCVars.GridFill), Is.False);

        await server.WaitAssertion(() =>
        {
            mapSystem.CreateMap(out var mapId);
            var grid = mapSystem.CreateGridEntity(mapId);
            entMan.RunMapInit(grid.Owner, entMan.GetComponent<MetaDataComponent>(grid.Owner));
            // The fridge is anchored, so it needs a tile to be parented to the grid.
            mapSystem.SetTile(grid, Vector2i.Zero, new Tile(1));

            var fridgeCoords = new EntityCoordinates(grid.Owner, new Vector2(0.5f, 0.5f));
            var fridge = entMan.SpawnEntity(SmartFridgeProtoId, fridgeCoords);
            var fridgeComp = entMan.GetComponent<SmartFridgeComponent>(fridge);
            Assert.That(entMan.GetComponent<TransformComponent>(fridge).ParentUid, Is.EqualTo(grid.Owner),
                "The fridge should be a child of the grid");

            var item = entMan.SpawnEntity(SampleItemProtoId, new EntityCoordinates(grid.Owner, Vector2.Zero));
            Assert.That(containers.TryGetContainer(fridge, fridgeComp.Container, out var inventory));
            containers.Insert(item, inventory);

            Assert.That(fridgeComp.ContainedEntries, Is.Not.Empty,
                "Inserting an item must populate the smart fridge's contained entries");

            Assert.That(mapLoader.TrySaveGrid(grid.Owner, path),
                "Saving a grid with a non-empty smart fridge must not throw");

            var userData = server.ResolveDependency<IResourceManager>().UserData;
            Assert.That(userData.Exists(path));
            using var reader = userData.OpenText(path);
            Assert.That(reader.ReadToEnd(), Does.Contain("SmartFridge"),
                "The fridge should be part of the saved grid");
        });

        await pair.CleanReturnAsync();
    }
}
