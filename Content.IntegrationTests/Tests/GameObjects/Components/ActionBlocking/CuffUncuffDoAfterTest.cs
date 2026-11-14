// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections.Generic;
using Content.Server.Cuffs;
using Content.Shared.Cuffs.Components;
using Content.Shared.DoAfter;
using Robust.Shared.GameObjects;
using Robust.Shared.Log;
using Serilog.Events;

namespace Content.IntegrationTests.Tests.GameObjects.Components.ActionBlocking;

/// <summary>
/// A player-controlled construct (e.g. a juggernaut) has no <see cref="DoAfterComponent"/>, so
/// uncuffing another entity as the construct used to be passed to
/// <see cref="SharedDoAfterSystem.TryStartDoAfter"/> as the user, which logged
/// "Attempting to start a doAfter with invalid user".
/// </summary>
[TestFixture]
[TestOf(typeof(CuffableComponent))]
public sealed class CuffUncuffDoAfterTest
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
    public async Task UserWithoutDoAfterCannotUncuff()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var logMan = server.ResolveDependency<ILogManager>();
        var map = await pair.CreateTestMap();

        var errors = new ErrorLogCapture();
        logMan.RootSawmill.AddHandler(errors);

        try
        {
            await server.WaitPost(() =>
            {
                var cuffed = entMan.SpawnEntity("MobHuman", map.GridCoords);
                var construct = entMan.SpawnEntity("ConstructJuggernaut", map.GridCoords);
                var cuffs = entMan.SpawnEntity("Handcuffs", map.GridCoords);

                var cuffableSystem = server.System<CuffableSystem>();

                Assert.Multiple(() =>
                {
                    Assert.That(cuffableSystem.TryAddNewCuffs(cuffed, cuffed, cuffs), Is.True,
                        "the test target must be cuffed");
                    Assert.That(entMan.HasComponent<DoAfterComponent>(cuffed), Is.True,
                        "a human must be able to run a do-after");
                    Assert.That(entMan.HasComponent<DoAfterComponent>(construct), Is.False,
                        "a construct must not have a DoAfterComponent");
                });

                errors.Clear();

                cuffableSystem.TryUncuff(cuffed, construct);

                Assert.That(errors.Snapshot(), Is.Empty,
                    "uncuffing as a user without DoAfterComponent must not start a do-after or log an error");
            });
        }
        finally
        {
            logMan.RootSawmill.RemoveHandler(errors);
        }

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task UserWithDoAfterStartsUncuffDoAfter()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var map = await pair.CreateTestMap();

        await server.WaitPost(() =>
        {
            var cuffed = entMan.SpawnEntity("MobHuman", map.GridCoords);
            var helper = entMan.SpawnEntity("MobHuman", map.GridCoords);
            var cuffs = entMan.SpawnEntity("Handcuffs", map.GridCoords);

            var cuffableSystem = server.System<CuffableSystem>();
            Assert.That(cuffableSystem.TryAddNewCuffs(cuffed, cuffed, cuffs), Is.True,
                "the test target must be cuffed");

            cuffableSystem.TryUncuff(cuffed, helper);

            Assert.That(entMan.GetComponent<DoAfterComponent>(helper).DoAfters, Is.Not.Empty,
                "a valid user should still get the uncuff do-after");
        });

        await pair.CleanReturnAsync();
    }
}
