// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections.Generic;
using Content.Server.Mech.Systems;
using Content.Shared.DoAfter;
using Content.Shared.DragDrop;
using Content.Shared.Mech.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Log;
using Serilog.Events;

namespace Content.IntegrationTests.Tests.Mech;

/// <summary>
/// A dragged entity that can not run the mech entry do-after (a plain item like a glass shard,
/// or a mob without a <see cref="DoAfterComponent"/>) used to be passed to
/// <see cref="SharedDoAfterSystem.TryStartDoAfter"/> as the user, which logged
/// "Attempting to start a doAfter with invalid user".
/// </summary>
[TestFixture]
[TestOf(typeof(MechComponent))]
public sealed class MechEntryDoAfterTest
{
    [TestPrototypes]
    private const string Prototypes = """
        - type: entity
          id: MechEntryNoDoAfterTest
          components:
          - type: Physics
            bodyType: KinematicController
          - type: InputMover

        - type: entity
          id: MechEntryValidPilotTest
          components:
          - type: Physics
            bodyType: KinematicController
          - type: InputMover
          - type: DoAfter
        """;

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
    public async Task ValidDropStartsEntryDoAfter()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var map = await pair.CreateTestMap();

        await server.WaitPost(() =>
        {
            var mech = entMan.SpawnEntity("MechRipley", map.GridCoords);
            var pilot = entMan.SpawnEntity("MechEntryValidPilotTest", map.GridCoords);

            var mechSystem = server.System<MechSystem>();
            Assert.That(mechSystem.CanInsert(mech, pilot, entMan.GetComponent<MechComponent>(mech)), Is.True,
                "the test pilot must be a valid mech pilot");

            var ev = new DragDropTargetEvent(pilot, pilot);
            entMan.EventBus.RaiseLocalEvent(mech, ref ev);

            Assert.That(entMan.GetComponent<DoAfterComponent>(pilot).DoAfters, Is.Not.Empty,
                "a valid pilot should still get the entry do-after");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task InvalidDropDoesNotStartEntryDoAfter()
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
                var mech = entMan.SpawnEntity("MechRipley", map.GridCoords);
                var shard = entMan.SpawnEntity("ShardGlass", map.GridCoords);
                var noDoAfter = entMan.SpawnEntity("MechEntryNoDoAfterTest", map.GridCoords);
                var user = entMan.SpawnEntity("MechEntryValidPilotTest", map.GridCoords);

                var mechSystem = server.System<MechSystem>();
                var mechComponent = entMan.GetComponent<MechComponent>(mech);
                Assert.Multiple(() =>
                {
                    // A shard can not be a pilot at all.
                    Assert.That(mechSystem.CanInsert(mech, shard, mechComponent), Is.False);
                    Assert.That(entMan.HasComponent<DoAfterComponent>(shard), Is.False);

                    // This one passes the pilot checks but can not run the entry do-after.
                    Assert.That(mechSystem.CanInsert(mech, noDoAfter, mechComponent), Is.True);
                    Assert.That(entMan.HasComponent<DoAfterComponent>(noDoAfter), Is.False);
                });

                errors.Clear();

                var shardDrop = new DragDropTargetEvent(user, shard);
                entMan.EventBus.RaiseLocalEvent(mech, ref shardDrop);

                var noDoAfterDrop = new DragDropTargetEvent(user, noDoAfter);
                entMan.EventBus.RaiseLocalEvent(mech, ref noDoAfterDrop);

                Assert.That(errors.Snapshot(), Is.Empty,
                    "dropping a shard or an entity without DoAfterComponent on a mech must not attempt a do-after");
            });
        }
        finally
        {
            logMan.RootSawmill.RemoveHandler(errors);
        }

        await pair.CleanReturnAsync();
    }
}
