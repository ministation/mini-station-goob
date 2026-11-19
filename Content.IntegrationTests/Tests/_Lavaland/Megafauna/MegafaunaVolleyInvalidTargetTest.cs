// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Content.Shared.Actions;
using Robust.Shared.GameObjects;
using Robust.Shared.Log;
using Robust.Shared.Map;
using Serilog.Events;

namespace Content.IntegrationTests.Tests._Lavaland.Megafauna;

/// <summary>
/// Reproduces the server line
/// "system.transform: Attempted to convert coordinates with invalid entity: EntId=0, X=0,00, Y=0,00",
/// which failed <see cref="BossAttackDamageTest"/> in CI. A world-target action request whose target
/// failed validation is still performed with the event's unset (invalid) target, and the volley
/// handler converted it to map coordinates.
/// </summary>
[TestFixture]
[TestOf(typeof(Content.Server._Lavaland.BossMechanics.MegafaunaVolleySystem))]
public sealed class MegafaunaVolleyInvalidTargetTest
{
    [Test]
    public async Task OutOfRangeRequestDoesNotConvertInvalidCoordinates()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.EntMan;
        var map = await pair.CreateTestMap();

        var logMan = server.ResolveDependency<ILogManager>();
        var errors = new ErrorLogCapture();
        logMan.RootSawmill.AddHandler(errors);

        EntityUid boss = default;
        EntityUid action = default;

        try
        {
            await server.WaitPost(() =>
            {
                boss = entMan.SpawnEntity("MobGoliath", map.GridCoords);

                var actionId = server.System<SharedActionsSystem>().AddAction(boss, "ActionFrostMinerFrostOrbs");
                Assert.That(actionId, Is.Not.Null);
                action = actionId!.Value;
            });

            await server.WaitAssertion(() =>
            {
                // Beyond the action's target range: ValidateWorldTarget rejects this target, so the
                // action is performed with the event's default (invalid) target.
                var coords = map.GridCoords.Offset(new Vector2(40, 0));
                var request = new RequestPerformActionEvent(
                    entMan.GetNetEntity(action),
                    entMan.GetNetCoordinates(coords));

                server.System<SharedActionsSystem>().TryPerformAction(boss, request);
            });

            await server.WaitRunTicks(2);

            await server.WaitAssertion(() =>
            {
                var messages = errors.Snapshot()
                    .Where(message => message.Contains("Attempted to convert"))
                    .ToArray();
                Assert.That(messages, Is.Empty,
                    $"an invalid action target must not be converted: {string.Join('\n', messages)}");
            });
        }
        finally
        {
            logMan.RootSawmill.RemoveHandler(errors);
        }

        await pair.CleanReturnAsync();
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

        public string[] Snapshot()
        {
            lock (_messages)
                return _messages.ToArray();
        }
    }
}
