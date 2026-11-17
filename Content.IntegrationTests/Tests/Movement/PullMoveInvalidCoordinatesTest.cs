// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections.Generic;
using System.Linq;
using Content.Server.Movement.Components;
using Content.Server.Movement.Systems;
using Content.Shared.Input;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Movement.Pulling.Systems;
using Robust.Shared.Input;
using Robust.Shared.Log;
using Robust.Shared.Map;
using Serilog.Events;

namespace Content.IntegrationTests.Tests.Movement;

/// <summary>
/// Reproduces the production lines
/// "system.transform: Attempted to convert coordinates with invalid entity: EntId=0, X=0,00, Y=0,00",
/// "system.transform: Attempted to convert map coordinates Map=0, X=0,00, Y=0,00 to entity coordinates on a
/// different map" and "system.transform: Attempted to convert coordinates with invalid entity: Map=617,
/// X=не число, Y=не число". A MovePulledObject command with coordinates that do not resolve was converted blindly:
/// the missing entity fell back to nullspace (the cross-map line) and the empty offset was normalized into NaN
/// (the second invalid line).
/// </summary>
[TestFixture]
[TestOf(typeof(PullController))]
public sealed class PullMoveInvalidCoordinatesTest : MovementTest
{
    [Test]
    public async Task InvalidMovePulledObjectCoordinatesDoNotLogErrors()
    {
        await SpawnTarget("MobHuman");

        var server = Pair.Server;
        var entMan = server.EntMan;
        var logMan = server.ResolveDependency<ILogManager>();
        var errors = new ErrorLogCapture();
        logMan.RootSawmill.AddHandler(errors);

        try
        {
            await Server.WaitAssertion(() =>
            {
                var pulling = server.System<PullingSystem>();
                Assert.That(pulling.TryStartPull(SPlayer, STarget!.Value), Is.True);
            });

            errors.Clear();

            // The client can send a pointer command whose coordinates do not resolve to any entity.
            await PressKey(ContentKeyFunctions.MovePulledObject, coordinates: NetCoordinates.Invalid);
            await RunTicks(2);

            await Server.WaitAssertion(() =>
            {
                var messages = errors.Snapshot()
                    .Where(message => message.Contains("Attempted to convert"))
                    .ToArray();
                Assert.That(messages, Is.Empty,
                    $"coordinates that do not resolve must not be converted: {string.Join('\n', messages)}");
                Assert.That(entMan.HasComponent<PullMovingComponent>(STarget!.Value), Is.False,
                    "a request with unresolvable coordinates must be ignored");
            });
        }
        finally
        {
            logMan.RootSawmill.RemoveHandler(errors);
        }
    }

    private sealed class ErrorLogCapture : ILogHandler
    {
        private readonly List<string> _messages = new();

        public void Log(string sawmillName, LogEvent message)
        {
            if (message.Level < LogEventLevel.Error)
                return;

            lock (_messages)
                _messages.Add(message.RenderMessage());
        }

        public void Clear()
        {
            lock (_messages)
                _messages.Clear();
        }

        public string[] Snapshot()
        {
            lock (_messages)
                return _messages.ToArray();
        }
    }
}
