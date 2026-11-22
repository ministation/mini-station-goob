// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections.Generic;
using System.IO;
using System.Linq;
using Content.Shared.Buckle;
using Content.Shared.Buckle.Components;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Log;
using Serilog.Events;

namespace Content.IntegrationTests.Tests.Buckle;

/// <summary>
/// A strap that is written into a grid file must not reference an entity that the same save skips.
/// The production log line was
/// "entity_serializer: Encountered a reference to a missing entity: ... while serializing кресло пилота (..., ChairPilotSeat)".
/// </summary>
[TestFixture]
[TestOf(typeof(SharedBuckleSystem))]
public sealed class StrapSerializationTest
{
    private const string SavableStrapId = "SavableStrapSaveTestDummy";
    private const string SavableBuckleId = "SavableBuckleSaveTestDummy";

    [TestPrototypes]
    private const string Prototypes = $@"
- type: entity
  id: {SavableStrapId}
  components:
  - type: Strap

- type: entity
  id: {SavableBuckleId}
  components:
  - type: Buckle
  - type: Hands
";

    /// <summary>
    /// Player mobs use "save: false", so the serializer skips them. When such a mob sits in a
    /// chair, the chair's strap used to be saved with an invalid reference and log an error.
    /// </summary>
    [Test]
    public async Task NonSavableBuckleIsNotSerialized()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entMan = server.ResolveDependency<IEntityManager>();
        var mapLoader = entMan.System<MapLoaderSystem>();
        var buckleSystem = entMan.System<SharedBuckleSystem>();
        var logMan = server.ResolveDependency<ILogManager>();

        var errors = new ErrorLogCapture();
        logMan.RootSawmill.AddHandler(errors);

        EntityUid chair = default;
        EntityUid mob = default;
        var yaml = string.Empty;

        try
        {
            await server.WaitAssertion(() =>
            {
                chair = entMan.SpawnEntity("ChairPilotSeat", map.GridCoords);
                mob = entMan.SpawnEntity("MobHuman", map.GridCoords);

                Assert.That(entMan.GetComponent<MetaDataComponent>(mob).EntityPrototype?.MapSavable, Is.False,
                    "player mobs are expected to be non-savable, otherwise this test would not reproduce the error");
                Assert.That(buckleSystem.TryBuckle(mob, mob, chair), Is.True);
                Assert.That(entMan.GetComponent<StrapComponent>(chair).BuckledEntities, Does.Contain(mob));

                using var writer = new StringWriter();
                Assert.That(mapLoader.TrySaveGrid(map.Grid.Owner, writer), Is.True, "failed to save the grid");
                yaml = writer.ToString();
            });

            var referenceErrors = errors.Snapshot()
                .Where(message => message.Contains("Encountered a reference to a missing entity"))
                .ToArray();
            Assert.That(referenceErrors, Is.Empty,
                $"a grid save must not reference entities it skips: {string.Join('\n', referenceErrors)}");
            Assert.That(yaml, Does.Not.Contain("- invalid"),
                "the saved grid must not contain invalid entity references");
        }
        finally
        {
            logMan.RootSawmill.RemoveHandler(errors);
        }

        await server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(entMan.GetComponent<StrapComponent>(chair).BuckledEntities, Is.Empty,
                    "the skipped mob was removed from the strap");
                Assert.That(entMan.GetComponent<BuckleComponent>(mob).Buckled, Is.False,
                    "the skipped mob was unbuckled, so buckle and strap stay in sync");
            });
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// A savable buckled entity is part of the save and must stay buckled after it.
    /// </summary>
    [Test]
    public async Task SavableBuckleIsKept()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entMan = server.ResolveDependency<IEntityManager>();
        var mapLoader = entMan.System<MapLoaderSystem>();
        var buckleSystem = entMan.System<SharedBuckleSystem>();
        var logMan = server.ResolveDependency<ILogManager>();

        var errors = new ErrorLogCapture();
        logMan.RootSawmill.AddHandler(errors);

        EntityUid chair = default;
        EntityUid mob = default;
        var yaml = string.Empty;

        try
        {
            await server.WaitAssertion(() =>
            {
                chair = entMan.SpawnEntity(SavableStrapId, map.GridCoords);
                mob = entMan.SpawnEntity(SavableBuckleId, map.GridCoords);

                Assert.That(entMan.GetComponent<MetaDataComponent>(mob).EntityPrototype?.MapSavable, Is.True);
                Assert.That(buckleSystem.TryBuckle(mob, mob, chair), Is.True);

                using var writer = new StringWriter();
                Assert.That(mapLoader.TrySaveGrid(map.Grid.Owner, writer), Is.True, "failed to save the grid");
                yaml = writer.ToString();
            });

            var referenceErrors = errors.Snapshot()
                .Where(message => message.Contains("Encountered a reference to a missing entity"))
                .ToArray();
            Assert.That(referenceErrors, Is.Empty, string.Join('\n', referenceErrors));
            Assert.That(yaml, Does.Contain("buckledEntities"),
                "a savable buckled entity is part of the grid and must be written as a reference");
        }
        finally
        {
            logMan.RootSawmill.RemoveHandler(errors);
        }

        await server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(entMan.GetComponent<StrapComponent>(chair).BuckledEntities, Does.Contain(mob),
                    "savable buckles must not be pruned");
                Assert.That(entMan.GetComponent<BuckleComponent>(mob).BuckledTo, Is.EqualTo(chair));
            });
        });

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
