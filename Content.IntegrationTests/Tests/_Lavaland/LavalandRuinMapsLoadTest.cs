// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

using Content.Shared.CCVar;
using Robust.Shared.Configuration;
using Robust.Shared.ContentPack;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Utility;

namespace Content.IntegrationTests.Tests._Lavaland;

/// <summary>
/// The sin vaults and misc TG ruin maps sat in Maps/Lavaland without being wired
/// into the ruin pool. Wiring them in means planet generation now loads them at
/// round start: an unknown prototype inside any of them would crash the planet,
/// so each map is load-checked here.
/// </summary>
[TestFixture]
public sealed class LavalandRuinMapsLoadTest
{
    [Test]
    [TestCase("Envy")]
    [TestCase("Gluttony")]
    [TestCase("Greed")]
    [TestCase("Pride")]
    [TestCase("Wrath")]
    [TestCase("sloth")]
    [TestCase("pizza_party")]
    [TestCase("ripley")]
    public async Task RuinMapLoads(string name)
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        var cfg = server.ResolveDependency<IConfigurationManager>();
        cfg.SetCVar(CCVars.ICRandomSpeciesWeights, string.Empty);

        var entManager = server.ResolveDependency<IEntityManager>();
        var res = server.ResolveDependency<IResourceManager>();
        var mapLoader = entManager.System<MapLoaderSystem>();
        var mapSys = entManager.System<SharedMapSystem>();

        await server.WaitPost(() =>
        {
            var path = new ResPath($"/Maps/Lavaland/{name}.yml");
            Assert.That(res.ContentFileExists(path), Is.True, $"Map file missing: {path}");

            mapSys.CreateMap(out var mapId);
            Assert.That(mapLoader.TryLoadGrid(mapId, path, out _), Is.True, $"Failed to load: {path}");
        });

        await server.WaitRunTicks(5);
        await pair.CleanReturnAsync();
    }
}
