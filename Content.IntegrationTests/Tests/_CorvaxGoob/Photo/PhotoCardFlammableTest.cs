// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections.Generic;
using System.Linq;
using Content.Server.Atmos.EntitySystems;
using Content.Shared.Atmos.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Log;
using Serilog.Events;

namespace Content.IntegrationTests.Tests._CorvaxGoob.Photo;

/// <summary>
/// Reproduces the production line "system.flammable: Can't resolve
/// \"Robust.Shared.GameObjects.AppearanceComponent\" on entity фотография (PhotoCard)".
/// The photo card is flammable and has fire visuals, but its prototype had no
/// <see cref="AppearanceComponent"/>, so igniting it made
/// <see cref="FlammableSystem.UpdateAppearance"/> log an entity resolve error.
/// </summary>
[TestFixture]
[TestOf(typeof(FlammableSystem))]
public sealed class PhotoCardFlammableTest
{
    private const string CardProtoId = "PhotoCard";

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
    public async Task IgnitingPhotoCardDoesNotLogAppearanceError()
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
                var card = entMan.SpawnEntity(CardProtoId, map.GridCoords);
                var flammable = entMan.GetComponent<FlammableComponent>(card);
                server.System<FlammableSystem>().SetFireStacks(card, 1f, flammable, ignite: true);
            });

            var appearanceErrors = errors.Snapshot()
                .Where(message => message.Contains(nameof(AppearanceComponent)))
                .ToArray();
            Assert.That(appearanceErrors, Is.Empty,
                $"{CardProtoId} is flammable, so it must have an AppearanceComponent: {string.Join('\n', appearanceErrors)}");
        }
        finally
        {
            logMan.RootSawmill.RemoveHandler(errors);
        }

        await pair.CleanReturnAsync();
    }
}
