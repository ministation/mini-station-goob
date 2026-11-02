// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections.Generic;
using Content.Server._Lavaland.Procedural.Systems;
using Content.Shared._Lavaland.Necropolis;
using Content.Shared.CCVar;
using Robust.Shared.Configuration;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests._Lavaland.Procedural;

/// <summary>
/// Full Lavaland planet generation through the batched path the GameTicker runs at
/// round start: SetupLavalandPlanet(withRuins:false) + ProcessRuinsBatch passes.
/// Guards the TG-parity guarantee that every guaranteed ruin, its boss and the
/// grid-ruin contents actually end up on the planet, initialized.
/// </summary>
[TestFixture]
[TestOf(typeof(LavalandSystem))]
public sealed class LavalandPlanetGenTest
{
    [Test]
    public async Task BatchedGenerationPlacesAllGuaranteedRuinsAndBosses()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();

        EntityUid planet = default;
        await server.WaitPost(() =>
        {
            // The shared pool keeps lavaland off (it round-starts a planet); this test needs it.
            var config = server.ResolveDependency<IConfigurationManager>();
            config.SetCVar(CCVars.LavalandEnabled, true);

            var lavaland = entMan.System<LavalandSystem>();
            Assert.That(lavaland.LavalandEnabled, Is.True, "lavaland.enabled cvar must be on");
            lavaland.EnsurePreloaderMap();
            Assert.That(lavaland.GetPreloaderEntity(), Is.Not.Null, "preloader entity must exist");
            Assert.That(lavaland.SetupLavalandPlanet("Lavaland", out var map, seed: 42, withRuins: false),
                Is.True, "planet setup should succeed");
            planet = map!.Value.Owner;

            // Drain the ruin batch exactly like GameTicker.MapPreload does: 200 ms passes.
            var done = false;
            while (!done)
                done = lavaland.ProcessRuinsBatch(TimeSpan.FromMilliseconds(200));
        });

        await server.WaitRunTicks(5);

        await server.WaitAssertion(() =>
        {
            var found = CollectPrototypesOnPlanet(entMan, planet);
            var xformQuery = entMan.GetEntityQuery<TransformComponent>();

            void Expect(string protoId, string what)
            {
                Assert.That(found, Does.Contain(protoId), $"{what} missing from the generated planet");
            }

            // Guaranteed marker ruins: the placed spawners themselves.
            Expect("LavalandNecropolisGateSpawner", "necropolis gate ruin");
            Expect("LavalandDrakeDenSpawner", "drake den ruin");
            Expect("LavalandColossusArenaSpawner", "colossus arena ruin");
            Expect("LavalandBubblegumLairSpawner", "bubblegum lair ruin");
            Expect("LavalandBloodDrunkDenSpawner", "blood-drunk den ruin");
            Expect("LavalandEliteTumorSpawner", "elite tumor ruin");
            Expect("LavalandAshwalkerDenSpawner", "ashwalker den ruin");
            Expect("LavalandSeedVaultSpawner", "seed vault ruin");
            Expect("LavalandWishgranterSpawner", "wishgranter shrine ruin");

            // What those spawners build on map init.
            Expect("LavalandBossAshDrake", "ash drake");
            Expect("LavalandCrateDragon", "dragon chest");
            Expect("LavalandBossColossus", "colossus");
            Expect("LavalandCrateColossus", "colossus chest");
            Expect("LavalandBossBubblegum", "bubblegum");
            Expect("LavalandCrateSarcophagus", "ancient sarcophagus");
            Assert.That(
                found.Contains("LavalandBossBloodDrunk") ||
                found.Contains("LavalandBossBloodDrunkGuidance") ||
                found.Contains("LavalandBossBloodDrunkHunter"),
                Is.True, "blood-drunk hunter (any variant) missing from the generated planet");
            Expect("LavalandEliteTumor", "elite tumor");
            Expect("LavalandAshwalkerNest", "ashwalker nest");
            Expect("LavalandSeedVaultKeeperPod", "seed vault keeper pod");
            Expect("LavalandWishgranter", "wishgranter");

            // Gate structure built by the gate spawner.
            var gateFound = false;
            var gateQuery = entMan.EntityQueryEnumerator<NecropolisGateComponent>();
            while (gateQuery.MoveNext(out var uid, out _))
            {
                if (!entMan.Deleted(uid) && entMan.GetComponent<TransformComponent>(uid).MapUid == planet)
                    gateFound = true;
            }
            Assert.That(gateFound, Is.True, "gate structure should be built on the planet");

            // Grid ruins: contents must land on the planet map-initialized. MobHierophant
            // lives inside the hierophant arena grid ruin and only spawns from its marker
            // when MapInit actually fired (regression guard for the staged-preload path).
            var hierophantMap = EntityUid.Invalid;
            var hierophantStage = EntityLifeStage.PreInit;
            var diagQuery = entMan.EntityQueryEnumerator<MetaDataComponent>();
            while (diagQuery.MoveNext(out var uid, out var meta))
            {
                if (meta.EntityPrototype == null || entMan.Deleted(uid))
                    continue;
                if (meta.EntityPrototype.ID != "MobHierophant")
                    continue;
                if (!xformQuery.TryComp(uid, out var xf))
                    continue;
                hierophantMap = xf.MapUid ?? EntityUid.Invalid;
                hierophantStage = meta.EntityLifeStage;
            }

            Assert.That(hierophantMap.IsValid(), Is.True, "MobHierophant not found anywhere");
            Assert.That(hierophantMap, Is.EqualTo(planet), "MobHierophant is not on the planet map");
            Assert.That(hierophantStage, Is.EqualTo(EntityLifeStage.MapInitialized),
                "MobHierophant must be map-initialized");
        });

        // Keep the pool clean for the other tests: drop the planet and the preloader,
        // and restore the shared-pool cvar.
        await server.WaitPost(() =>
        {
            var lavaland = entMan.System<LavalandSystem>();
            var preloader = lavaland.GetPreloaderEntity();
            if (preloader != null)
                entMan.DeleteEntity(preloader.Value.Owner);
            entMan.DeleteEntity(planet);
            server.ResolveDependency<IConfigurationManager>().SetCVar(CCVars.LavalandEnabled, false);
        });
        await server.WaitRunTicks(10);

        await pair.CleanReturnAsync();
    }

    private static HashSet<string> CollectPrototypesOnPlanet(IEntityManager entMan, EntityUid planet)
    {
        var found = new HashSet<string>();
        var xformQuery = entMan.GetEntityQuery<TransformComponent>();
        var metaQuery = entMan.GetEntityQuery<MetaDataComponent>();

        var query = entMan.EntityQueryEnumerator<MetaDataComponent>();
        while (query.MoveNext(out var uid, out var meta))
        {
            if (meta.EntityPrototype == null || entMan.Deleted(uid))
                continue;
            if (!xformQuery.TryComp(uid, out var xform) || xform.MapUid != planet)
                continue;
            found.Add(meta.EntityPrototype.ID);
        }

        return found;
    }
}
