// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections.Generic;
using System.Linq;
using Content.Server.PAI;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.PAI;
using Robust.Shared.GameObjects;
using Robust.Shared.Log;
using Serilog.Events;

namespace Content.IntegrationTests.Tests.PAI;

/// <summary>
/// Reproduces the production line 'loc: "ru-RU"/"pai-system-pai-name": KeyNotFoundException:
/// Entity ... does not have a component of type MetaDataComponent'.
/// The PAI remembers the entity that activated it in hand, and the name localization string resolves
/// that entity to its display name. When the owner is deleted before a ghost takes the PAI over,
/// naming the PAI used to resolve the deleted entity and log the exception.
/// </summary>
[TestFixture]
[TestOf(typeof(PAISystem))]
public sealed class PAIDeletedOwnerNameTest
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
    public async Task NamingPaiAfterDeletedOwnerDoesNotLogError()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var logMan = server.ResolveDependency<ILogManager>();
        var map = await pair.CreateTestMap();

        var errors = new ErrorLogCapture();
        logMan.RootSawmill.AddHandler(errors);

        EntityUid pai = default;
        var paiName = string.Empty;

        try
        {
            await server.WaitPost(() =>
            {
                pai = entMan.SpawnEntity("PersonalAI", map.GridCoords);
                var owner = entMan.SpawnEntity("Crowbar", map.GridCoords);

                entMan.GetComponent<PAIComponent>(pai).LastUser = owner;
                entMan.DeleteEntity(owner);

                paiName = entMan.GetComponent<MetaDataComponent>(pai).EntityName;
            });

            await server.WaitPost(() =>
            {
                // A ghost takes the PAI over, so the mind gets added and the PAI names itself after its last user.
                entMan.EventBus.RaiseLocalEvent(pai, new MindAddedMessage(default, default));
            });

            await server.WaitAssertion(() =>
            {
                var nameErrors = errors.Snapshot()
                    .Where(message => message.Contains("pai-system-pai-name"))
                    .ToArray();
                Assert.That(nameErrors, Is.Empty,
                    $"naming the PAI must not resolve its deleted owner: {string.Join('\n', nameErrors)}");
                Assert.That(entMan.GetComponent<MetaDataComponent>(pai).EntityName,
                    Is.EqualTo(paiName),
                    "the PAI without a resolvable owner keeps its name");
            });
        }
        finally
        {
            logMan.RootSawmill.RemoveHandler(errors);
        }

        await pair.CleanReturnAsync();
    }
}
