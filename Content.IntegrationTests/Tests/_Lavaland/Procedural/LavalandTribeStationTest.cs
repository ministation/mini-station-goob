// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using Content.Server._Lavaland.Ashwalker;
using Content.Server._Lavaland.Procedural.Systems;
using Content.Server.Ghost.Roles.Components;
using Content.Server.Spawners.Components;
using Content.Server.Station.Components;
using Content.Server.Station.Systems;
using Content.Shared._Lavaland.Procedural.Components;
using Content.Shared.CCVar;
using Content.Shared.Players.PlayTimeTracking;
using Content.Shared.Roles;
using Content.Shared.StatusIcon;
using Content.Shared.Station.Components;
using Robust.Shared.Configuration;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Lavaland.Procedural;

/// <summary>
/// The ash walker tribe station must appear on the Lavaland planet at round start with
/// the AshWalker job, its spawn points at the den, and the job must use a body without
/// a ghost role (that one belongs to the nest eggs).
/// </summary>
[TestFixture]
[TestOf(typeof(LavalandTribeStationSystem))]
public sealed class LavalandTribeStationTest
{
    [Test]
    public async Task TribeStationProvidesAshWalkerJobAndSpawnPoints()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var prototypes = server.ResolveDependency<IPrototypeManager>();
        var compFactory = server.ResolveDependency<IComponentFactory>();

        EntityUid planet = default;
        EntityUid station = default;

        await server.WaitPost(() =>
        {
            var config = server.ResolveDependency<IConfigurationManager>();
            config.SetCVar(CCVars.LavalandEnabled, true);

            var lavaland = entMan.System<LavalandSystem>();
            lavaland.EnsurePreloaderMap();
            Assert.That(lavaland.GetPreloaderEntity(), Is.Not.Null, "preloader entity should exist");
            Assert.That(lavaland.SetupLavalandPlanet("Lavaland", out var map, seed: 42, withRuins: false),
                Is.True, "planet setup should succeed");
            planet = map!.Value.Owner;

            // Drain the ruin batch like the staged preload does, so the den and its markers exist.
            var done = false;
            while (!done)
                done = lavaland.ProcessRuinsBatch(TimeSpan.FromMilliseconds(200));

            var tribe = entMan.System<LavalandTribeStationSystem>();
            Assert.That(tribe.EnsureTribeStation(), Is.True, "tribe station should be created on the planet");
            Assert.That(tribe.EnsureTribeStation(), Is.True, "tribe station setup must be idempotent");
        });

        await server.WaitRunTicks(5);

        await server.WaitAssertion(() =>
        {
            // Find the station that owns the AshWalker job.
            var stationJobsSystem = entMan.System<StationJobsSystem>();
            var stations = entMan.EntityQueryEnumerator<StationJobsComponent>();
            while (stations.MoveNext(out var uid, out _))
            {
                if (!stationJobsSystem.GetJobs(uid).ContainsKey("AshWalker"))
                    continue;

                station = uid;
                break;
            }

            Assert.That(station.IsValid(), Is.True, "no station with AshWalker job slots found");

            // Exactly one job slot pair is expected: 2 round start and 2 total.
            var jobs = stationJobsSystem.GetJobs(station);
            Assert.That(jobs["AshWalker"], Is.EqualTo(2));
            Assert.That(entMan.GetComponent<StationJobsComponent>(station).TotalJobs, Is.EqualTo(2));

            // The station sits on the Lavaland planet grid.
            var data = entMan.GetComponent<StationDataComponent>(station);
            Assert.That(data.Grids.Count, Is.EqualTo(1), "tribe station should own exactly the Lavaland grid");
            var grid = data.Grids.First();
            Assert.That(entMan.HasComponent<LavalandMapComponent>(grid), Is.True,
                "tribe station must be attached to the Lavaland planet");

            // One round start job spawner at the den, plus the latejoin one.
            var stationSystem = entMan.System<StationSystem>();
            var jobPoints = 0;
            var lateJoinPoints = 0;
            var points = entMan.EntityQueryEnumerator<SpawnPointComponent, TransformComponent>();
            while (points.MoveNext(out var uid, out var spawn, out var xform))
            {
                if (xform.MapUid != grid)
                    continue;

                if (spawn.SpawnType == SpawnPointType.Job && spawn.Job?.Id == "AshWalker")
                {
                    jobPoints++;
                    Assert.That(stationSystem.GetOwningStation(uid, xform), Is.EqualTo(station),
                        "ash walker spawn point must be owned by the tribe station");
                }
                else if (spawn.SpawnType == SpawnPointType.LateJoin)
                {
                    lateJoinPoints++;
                }
            }

            Assert.That(jobPoints, Is.EqualTo(1), "expected one AshWalker job spawn point at the den");
            Assert.That(lateJoinPoints, Is.GreaterThan(0), "expected a latejoin spawn point at the den");

            // Job prototype wiring.
            var job = prototypes.Index<JobPrototype>("AshWalker");
            Assert.Multiple(() =>
            {
                Assert.That(job.JobEntity?.Id, Is.EqualTo("MobAshwalkerPlayable"));
                Assert.That(job.Icon.Id, Is.EqualTo("JobIconAshwalker"));
                Assert.That(job.SetPreference, Is.True);
                Assert.That(prototypes.HasIndex<PlayTimeTrackerPrototype>(job.PlayTimeTracker), Is.True,
                    "job needs a valid play time tracker");
                Assert.That(prototypes.HasIndex<JobIconPrototype>("JobIconAshwalker"), Is.True);
            });

            // Bodies: the egg ghost role body keeps its ghost role (and the job), the job body must not have one.
            var eggBody = prototypes.Index<EntityPrototype>("MobAshwalker");
            var jobBody = prototypes.Index<EntityPrototype>("MobAshwalkerPlayable");
            Assert.That(eggBody.TryGetComponent<GhostRoleComponent>(out var ghostRole, compFactory), Is.True);
            Assert.That(ghostRole!.JobProto?.Id, Is.EqualTo("AshWalker"),
                "egg born ash walkers should get the Ash Walker job");
            Assert.That(eggBody.TryGetComponent<GhostTakeoverAvailableComponent>(out _, compFactory), Is.True);
            Assert.That(jobBody.TryGetComponent<GhostRoleComponent>(out _, compFactory), Is.False,
                "the job body must not register a ghost role");
        });

        // Keep the shared pool clean.
        await server.WaitPost(() =>
        {
            if (station.IsValid() && !entMan.Deleted(station))
                entMan.DeleteEntity(station);
            entMan.DeleteEntity(planet);
            var lavaland = entMan.System<LavalandSystem>();
            var preloader = lavaland.GetPreloaderEntity();
            if (preloader != null)
                entMan.DeleteEntity(preloader.Value.Owner);
            server.ResolveDependency<IConfigurationManager>().SetCVar(CCVars.LavalandEnabled, false);
        });
        await server.WaitRunTicks(10);

        await pair.CleanReturnAsync();
    }
}
