// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections.Generic;
using System.Linq;
using Content.Server.Zombies;
using Content.Shared.Body.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Zombies;
using Robust.Shared.Log;
using Serilog.Events;

namespace Content.IntegrationTests.Tests.Zombies;

/// <summary>
/// Reproduces the production line "system.bloodstream: Can't resolve
/// \"Content.Shared.Body.Components.BloodstreamComponent\" on entity часовой механизм (592)
/// (3825665/n3825665, MobClockworkDefender)!". MobClockworkDefender inherits LavalandMobBase without
/// <see cref="BloodstreamComponent"/>, but a zombie bite gives it <see cref="ZombifyOnDeathComponent"/>.
/// When it died, <see cref="ZombieSystem.ZombifyEntity"/> called the bloodstream API unconditionally.
/// </summary>
[TestFixture]
[TestOf(typeof(ZombieSystem))]
public sealed class ZombifyMissingBloodstreamTest
{
    private sealed class ErrorLogCapture : ILogHandler
    {
        public readonly List<string> Messages = [];

        public void Log(string sawmillName, LogEvent message)
        {
            if (message.Level < LogEventLevel.Error)
                return;

            lock (Messages)
                Messages.Add(message.RenderMessage());
        }

        public void Clear()
        {
            lock (Messages)
                Messages.Clear();
        }

        public string[] Snapshot()
        {
            lock (Messages)
                return Messages.ToArray();
        }
    }

    [Test]
    public async Task ZombifyBloodlessMobDoesNotLogResolveError()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var server = pair.Server;
        var entMan = server.EntMan;
        var logMan = server.ResolveDependency<ILogManager>();
        var map = await pair.CreateTestMap();

        var errors = new ErrorLogCapture();
        logMan.RootSawmill.AddHandler(errors);

        try
        {
            await server.WaitPost(() =>
            {
                var defender = entMan.SpawnEntity("MobClockworkDefender", map.GridCoords);
                Assert.That(entMan.HasComponent<BloodstreamComponent>(defender), Is.False,
                    "MobClockworkDefender is expected to have no bloodstream");

                // A zombie bite infects a living mob and zombifies it when it dies.
                entMan.AddComponent<ZombifyOnDeathComponent>(defender);

                errors.Clear();
                server.System<MobStateSystem>().ChangeMobState(defender, MobState.Dead);
            });

            await server.WaitRunTicks(2);

            await server.WaitAssertion(() =>
            {
                var resolveErrors = errors.Snapshot()
                    .Where(message => message.Contains("Can't resolve") && message.Contains(nameof(BloodstreamComponent)))
                    .ToArray();
                Assert.That(resolveErrors, Is.Empty,
                    $"zombifying a mob without a bloodstream must not resolve the missing component: {string.Join('\n', resolveErrors)}");

                // Make sure the zombification really happened, so the bloodstream code path was reached.
                Assert.That(entMan.Count<ZombieComponent>(), Is.GreaterThan(0),
                    "the clockwork defender was not zombified, so the regression was not exercised");
            });
        }
        finally
        {
            logMan.RootSawmill.RemoveHandler(errors);
        }

        await pair.CleanReturnAsync();
    }
}
