// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server._Lavaland.Ashwalker;
using Content.Server._Lavaland.Procedural.Systems;
using Content.Server._Lavaland.Shuttles.Systems;
using Content.Server.Shuttles.Systems;
using Content.Shared._Lavaland.Procedural.Components;
using Content.Shared.CCVar;
using Robust.Shared.Configuration;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Utility;

namespace Content.IntegrationTests.Tests._Lavaland.Procedural;

/// <summary>
/// The mining shuttle must dock to the Lavaland outpost. The planet grid belongs to the ash walker
/// tribe station, so the docking console used to treat it as the FTL target and the shuttle fell back
/// to a proximity arrival with no docking.
/// </summary>
[TestFixture]
[TestOf(typeof(DockingConsoleSystem))]
public sealed class LavalandOutpostDockingTest
{
    [Test]
    public async Task MiningShuttleDocksToOutpostOnTribeStationPlanet()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var testMap = await pair.CreateTestMap();

        MapId lavalandMapId = default;
        EntityUid planet = default;
        EntityUid outpost = default;
        EntityUid shuttle = default;

        await server.WaitPost(() =>
        {
            var config = server.ResolveDependency<IConfigurationManager>();
            config.SetCVar(CCVars.LavalandEnabled, true);

            var lavaland = entMan.System<LavalandSystem>();
            lavaland.EnsurePreloaderMap();
            Assert.That(lavaland.SetupLavalandPlanet("Lavaland", out var map, seed: 42, withRuins: false), Is.True,
                "Lavaland planet setup failed");
            lavalandMapId = entMan.GetComponent<TransformComponent>(map!.Value.Owner).MapID;

            var done = false;
            while (!done)
                done = lavaland.ProcessRuinsBatch(TimeSpan.FromMilliseconds(200));

            Assert.That(entMan.System<LavalandTribeStationSystem>().EnsureTribeStation(), Is.True,
                "tribe station setup failed");

            var grids = entMan.EntityQueryEnumerator<MapGridComponent, TransformComponent>();
            while (grids.MoveNext(out var uid, out _, out var xform))
            {
                if (xform.MapID != lavalandMapId)
                    continue;

                if (entMan.HasComponent<LavalandMapComponent>(uid))
                    planet = uid;
                else if (entMan.HasComponent<LavalandStationComponent>(uid))
                    outpost = uid;
            }

            Assert.That(planet.IsValid(), Is.True, "Lavaland planet grid missing");
            Assert.That(outpost.IsValid(), Is.True, "Lavaland outpost grid missing");
            Assert.That(entMan.HasComponent<LavalandStationComponent>(planet), Is.False,
                "the outpost should be a separate grid from the planet");

            var loader = entMan.System<MapLoaderSystem>();
            Assert.That(
                loader.TryLoadGrid(testMap.MapId, new ResPath("/Maps/_CorvaxGoob/Lavaland/mining.yml"), out var loaded),
                Is.True, "failed to load the mining shuttle");
            shuttle = loaded!.Value.Owner;
        });

        await server.WaitRunTicks(5);

        await server.WaitAssertion(() =>
        {
            var console = entMan.System<DockingConsoleSystem>();
            Assert.That(console.FindLargestGrid(lavalandMapId), Is.EqualTo(outpost),
                "the docking console must target the outpost, not the planet grid the tribe station owns");

            var docking = entMan.System<DockingSystem>();
            var config = docking.GetDockingConfig(shuttle, outpost, "DockMining");
            Assert.That(config, Is.Not.Null,
                $"the mining shuttle has no valid docking config at the outpost " +
                $"(shuttle docks: {docking.GetDocks(shuttle).Count}, outpost docks: {docking.GetDocks(outpost).Count}, " +
                $"planet is a map entity: {entMan.HasComponent<MapComponent>(planet)})");
        });

        await pair.CleanReturnAsync();
    }
}
