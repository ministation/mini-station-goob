// SPDX-License-Identifier: AGPL-3.0-or-later

using System;
using System.Collections.Generic;
using System.Linq;
using Content.Server.Fluids.EntitySystems;
using Content.Shared.Chemistry.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Log;
using Serilog.Events;

namespace Content.IntegrationTests.Tests.Fluids;

/// <summary>
/// Reproduces the production line "system.smoke: Can't resolve
/// \"Content.Shared.Chemistry.Components.SmokeComponent\" on entity 3461575/n0D!".
/// An affected entity keeps a <see cref="SmokeAffectedComponent"/> that points at a smoke entity
/// which is already gone: the smoke despawned without the EndCollide handler cleaning the reference
/// up. <see cref="SmokeSystem.Update"/> then tried to resolve the deleted entity every tick.
/// </summary>
[TestFixture]
[TestOf(typeof(SmokeSystem))]
public sealed class SmokeAffectedDeletedEntityTest
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

        public string[] Snapshot()
        {
            lock (Messages)
                return Messages.ToArray();
        }
    }

    [Test]
    public async Task AffectedEntityDropsDeletedSmoke()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var logMan = server.ResolveDependency<ILogManager>();
        var map = await pair.CreateTestMap();

        var errors = new ErrorLogCapture();
        logMan.RootSawmill.AddHandler(errors);

        EntityUid victim = default;

        try
        {
            await server.WaitPost(() =>
            {
                // The smoke despawns while the entity still has the affected component pointing at it
                // (the collision never ended in a way that reached SmokeSystem.OnEndCollide).
                var smoke = entMan.SpawnEntity("Smoke", map.GridCoords);
                victim = entMan.SpawnEntity("MobMoth", map.GridCoords);

                var affected = entMan.AddComponent<SmokeAffectedComponent>(victim);
                affected.SmokeEntity = smoke;
                affected.NextSecond = TimeSpan.Zero;

                entMan.DeleteEntity(smoke);
            });

            await server.WaitRunTicks(5);

            await server.WaitAssertion(() =>
            {
                var resolveErrors = errors.Snapshot()
                    .Where(message => message.Contains("Can't resolve") && message.Contains(nameof(SmokeComponent)))
                    .ToArray();
                Assert.That(resolveErrors, Is.Empty,
                    $"a despawned smoke must not be resolved from a stale SmokeAffectedComponent: {string.Join('\n', resolveErrors)}");
                Assert.That(entMan.HasComponent<SmokeAffectedComponent>(victim), Is.False,
                    "the stale smoke reference must be dropped");
            });
        }
        finally
        {
            logMan.RootSawmill.RemoveHandler(errors);
        }

        await pair.CleanReturnAsync();
    }
}
