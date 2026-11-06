// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.DeviceLinking.Systems;
using Content.Shared.DeviceLinking;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.Tests.DeviceLinking;

/// <summary>
/// Reproduces the production line "system.device_link: Device source ... links to invalid entity: .../n0D".
/// A device link source kept a link to a sink that was already deleted, and removing the source component
/// during round cleanup reported the dead entity as an error even though there was nothing left to unlink.
/// </summary>
[TestFixture]
[TestOf(typeof(SharedDeviceLinkSystem))]
public sealed class DeviceLinkStaleSinkTest
{
    private const string SourceProtoId = "DeviceLinkStaleSinkSource";
    private const string SinkProtoId = "DeviceLinkStaleSinkTarget";

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
    public async Task RemovingSourceWithDeletedSinkDoesNotLogError()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.EntMan;
        var mapSys = server.System<SharedMapSystem>();
        var deviceLinkSys = server.System<DeviceLinkSystem>();

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
            deviceLinkSys.SaveLinks(null, source, sink, [("Output", "On")], sourceComp, sinkComp);

            // Copy the linked source component to an entity that never starts up: the copy keeps LinkedPorts,
            // but the sink is not added to the copy's LinkedSources because ComponentStartup never runs.
            var staleSource = entMan.CreateEntityUninitialized(null);
            entMan.CopyComponents(source, staleSource, null, sourceComp);

            // Deleting the sink unlinks the live source, while the detached copy keeps pointing at the dead entity.
            entMan.DeleteEntity(sink);
            entMan.DeleteEntity(staleSource);

            mapSys.DeleteMap(mapId);
        });

        await pair.CleanReturnAsync();
    }
}
