// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.DeviceLinking.Systems;
using Content.Shared.DeviceLinking;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Utility;

namespace Content.IntegrationTests.Tests.DeviceLinking;

/// <summary>
/// Reproduces "entity_serializer: Encountered a reference to a deleted entity ... while serializing ... (SignalButtonWindows)".
/// A failed link attempt used to register the sink in the source's LinkedPorts before the link was validated,
/// leaving an empty entry behind. Deleting that sink later left the source referencing a dead entity, which a
/// grid save reported as an entity_serializer error.
/// </summary>
[TestFixture]
[TestOf(typeof(SharedDeviceLinkSystem))]
public sealed class DeviceLinkFailedToggleTest
{
    private const string SourceProtoId = "DeviceLinkFailedToggleSource";
    private const string SinkProtoId = "DeviceLinkFailedToggleSink";

    [TestPrototypes]
    private const string Prototypes = $@"
- type: entity
  id: {SourceProtoId}
  components:
  - type: DeviceLinkSource
    ports:
    - Output
- type: entity
  id: {SinkProtoId}
  components:
  - type: DeviceLinkSink
    ports:
    - On
";

    [Test]
    public async Task FailedToggleDoesNotLeaveStaleLink()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.EntMan;
        var mapSys = server.System<SharedMapSystem>();
        var deviceLinkSys = server.System<DeviceLinkSystem>();
        var mapLoader = server.System<MapLoaderSystem>();

        await server.WaitAssertion(() =>
        {
            mapSys.CreateMap(out var mapId);
            var grid = mapSys.CreateGridEntity(mapId);
            mapSys.SetTile(grid.Owner, grid.Comp, Vector2i.Zero, new Tile(1));
            var coords = new EntityCoordinates(grid.Owner, 0, 0);

            var source = entMan.SpawnEntity(SourceProtoId, coords);
            var sink = entMan.SpawnEntity(SinkProtoId, coords);
            Assert.That(entMan.TryGetComponent<DeviceLinkSourceComponent>(source, out var sourceComp));
            Assert.That(entMan.TryGetComponent<DeviceLinkSinkComponent>(sink, out var sinkComp));

            // The sink has no "Off" port, so the attempt fails after the source used to register the sink.
            Assert.That(deviceLinkSys.ToggleLink(null, source, sink, "Output", "Off", sourceComp, sinkComp), Is.False);
            Assert.That(sourceComp.LinkedPorts, Is.Empty,
                "A failed link attempt must not leave an empty LinkedPorts entry behind");

            // With an empty entry left behind, deleting the sink would turn it into a reference to a dead entity,
            // and the map save below would log an entity_serializer error.
            entMan.DeleteEntity(sink);
            Assert.That(sourceComp.LinkedPorts, Is.Empty,
                "Deleting the sink must not leave a stale reference in the source's LinkedPorts");
            Assert.That(mapLoader.TrySaveGrid(grid.Owner, new ResPath("/device-link-failed-toggle.yml")),
                "Saving a grid with the source must not fail");

            mapSys.DeleteMap(mapId);
        });

        await pair.CleanReturnAsync();
    }
}
