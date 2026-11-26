// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections.Generic;
using System.Linq;
using Content.Goobstation.Shared.Disease.Systems;
using Content.Goobstation.Shared.Virology;
using Content.Shared.DoAfter;
using Robust.Shared.GameObjects;
using Robust.Shared.Log;
using Serilog.Events;

namespace Content.IntegrationTests.Tests._Goobstation.Disease;

/// <summary>
/// Reproduces the production line "system.disease: Attempted to infect ... with disease ..., but it
/// had no DiseaseComponent". A swab gives its disease clone to the pen made by the vaccinator but
/// keeps the same clone itself, so when the swab is reused or destroyed the clone is deleted while
/// the pen still references it. Injecting with such a pen must be a normal failed attempt, not an
/// error in the log.
/// </summary>
[TestFixture]
[TestOf(typeof(SharedDiseaseSystem))]
public sealed class DeletedDiseaseInfectionTest
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
    public async Task InfectingWithDeletedDiseaseDoesNotLogError()
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
                var target = entMan.SpawnEntity("MobHuman", map.GridCoords);
                var pen = entMan.SpawnEntity("LiveInjector", map.GridCoords);
                var disease = entMan.SpawnEntity("DiseaseFlu", map.GridCoords);

                // The vaccinator stores the swab's disease clone, and the swab keeps the same clone.
                // Deleting the swab (sampling again, destroying it) deletes that shared entity.
                entMan.GetComponent<DiseasePenComponent>(pen).DiseaseUid = disease;
                entMan.DeleteEntity(disease);
                Assert.That(entMan.EntityExists(disease), Is.False);

                errors.Clear();

                // The injection do-after has completed: the shared pen system runs TryInfect.
                var user = entMan.SpawnEntity("MobHuman", map.GridCoords);
                var args = new DoAfterArgs(entMan, user, TimeSpan.Zero,
                    new DiseasePenInjectEvent(), pen, target: target);
                var injectEvent = (DiseasePenInjectEvent) args.Event;
                injectEvent.DoAfter = new Content.Shared.DoAfter.DoAfter(0, args, TimeSpan.Zero);
                entMan.EventBus.RaiseLocalEvent(pen, injectEvent);
            });

            await server.WaitAssertion(() =>
            {
                var infectionErrors = errors.Snapshot()
                    .Where(message => message.Contains("Attempted to infect"))
                    .ToArray();
                Assert.That(infectionErrors, Is.Empty,
                    $"infecting with an already deleted disease must not log an error: {string.Join('\n', infectionErrors)}");
            });
        }
        finally
        {
            logMan.RootSawmill.RemoveHandler(errors);
        }

        await pair.CleanReturnAsync();
    }
}
