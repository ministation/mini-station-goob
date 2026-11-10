// SPDX-License-Identifier: AGPL-3.0-or-later

using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Content.Shared.Damage;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Throwing;
using Content.Shared._White.Grab;
using Robust.Shared.GameObjects;
using Robust.Shared.Log;
using Robust.Shared.Map;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Timing;
using Serilog.Events;

namespace Content.IntegrationTests.Tests.Physics;

/// <summary>
/// A thrown item that falls asleep must not run content that deletes or wakes it from inside the
/// physics sleep event. The engine raises <c>PhysicsSleepEvent</c> while the body is still mid-sleep,
/// so a handler that gibs the item (grab-throw damage applied by <see cref="GrabThrownSystem"/>)
/// wakes the body again and the engine reports "Found a corrupted physics awake state".
/// </summary>
[TestFixture]
[TestOf(typeof(ThrownItemComponent))]
public sealed class ThrownItemSleepTest
{
    [Test]
    public async Task SleepingGrabThrownCorpseKeepsAwakeStateConsistent()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var logMan = server.ResolveDependency<ILogManager>();
        var timing = server.ResolveDependency<IGameTiming>();
        var mobState = server.System<MobStateSystem>();
        var physics = server.System<SharedPhysicsSystem>();
        var grabThrown = server.System<GrabThrownSystem>();

        var errors = new ErrorLogCapture();
        logMan.RootSawmill.AddHandler(errors);
        logMan.GetSawmill("system.physics").AddHandler(errors);

        var map = await pair.CreateTestMap();
        EntityUid moth = default;
        EntityUid butterfly = default;

        try
        {
            await server.WaitPost(() =>
            {
                // The wall gives the thrown body a hard contact. Destroying that contact when the
                // corpse is gibbed is what wakes the body mid-sleep.
                entMan.SpawnEntity("WallSolid", new EntityCoordinates(map.Grid, 1.5f, 0.5f));
                moth = entMan.SpawnEntity("MobMoth", new EntityCoordinates(map.Grid, 1.0f, 0.5f));
                butterfly = entMan.SpawnEntity("MobButterfly", new EntityCoordinates(map.Grid, 1.5f, 0.5f));
            });

            await server.WaitRunTicks(10);

            Exception? sleepException = null;

            await server.WaitAssertion(() =>
            {
                mobState.ChangeMobState(butterfly, MobState.Dead);

                // Grab-throw the corpse into the wall. GrabThrownSystem applies this damage when the
                // throw stops, and it is enough to gib the corpse.
                var damage = new DamageSpecifier();
                damage.DamageDict["Blunt"] = 30;
                grabThrown.Throw(butterfly, moth, Vector2.One, 7f, damage);

                var body = entMan.GetComponent<PhysicsComponent>(butterfly);
                Assert.That(body.Awake, Is.True, "the grab throw should wake the corpse before it sleeps");

                var contacts = physics.GetContacts(butterfly);
                Assert.That(contacts.MoveNext(out _), Is.True, "the corpse should be touching the wall");

                // Physics sleeps a resting body after physics.timetosleep (0.2s). Do it directly to
                // keep the test independent of the physics step timing.
                try
                {
                    physics.SetAwake((butterfly, body), false);
                }
                catch (Exception e)
                {
                    sleepException = e;
                }
            });

            await server.WaitRunTicks(5);

            Assert.Multiple(() =>
            {
                Assert.That(sleepException, Is.Null,
                    "the physics sleep event must not wake or delete the body mid-sleep");
                Assert.That(errors.Snapshot().Any(message => message.Contains("corrupted physics awake state")),
                    Is.False, "corrupted physics awake state was logged:\n" + string.Join('\n', errors.Snapshot()));
                Assert.That(entMan.EntityExists(butterfly), Is.False,
                    "the grab-throw damage should have gibbed the corpse, otherwise the test is vacuous");
            });
        }
        finally
        {
            logMan.RootSawmill.RemoveHandler(errors);
            logMan.GetSawmill("system.physics").RemoveHandler(errors);
        }

        await pair.CleanReturnAsync();
    }

    private sealed class ErrorLogCapture : ILogHandler
    {
        private readonly List<string> _messages = [];

        public void Log(string sawmillName, LogEvent message)
        {
            if (message.Level < LogEventLevel.Error)
                return;

            lock (_messages)
                _messages.Add(sawmillName + ": " + message.RenderMessage());
        }

        public string[] Snapshot()
        {
            lock (_messages)
                return _messages.ToArray();
        }
    }
}
