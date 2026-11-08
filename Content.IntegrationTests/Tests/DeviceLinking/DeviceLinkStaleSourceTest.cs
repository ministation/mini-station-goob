// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.DeviceLinking.Systems;
using Content.Shared.DeviceLinking;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.Tests.DeviceLinking;

/// <summary>
/// Reproduces the production line "system.device_link: Device sink ... source list contains invalid entity: .../n0D".
/// A device link sink kept a reference to a source that was already deleted, and removing the sink component
/// during round cleanup reported the dead entity as an error even though there was nothing left to unlink.
/// </summary>
[TestFixture]
[TestOf(typeof(SharedDeviceLinkSystem))]
public sealed class DeviceLinkStaleSourceTest
{
    private const string SourceProtoId = "DeviceLinkStaleSourceSource";
    private const string SinkProtoId = "DeviceLinkStaleSourceTarget";

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
    public async Task RemovingSinkWithDeletedSourceDoesNotLogError()
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

            // SaveLinks always adds the source to the sink's LinkedSources, even when no link is stored because
            // the sink has no matching port (a cancelled link attempt behaves the same). The sink therefore
            // lists a source that never links back to it.
            deviceLinkSys.SaveLinks(null, source, sink, [("Output", "Off")], sourceComp, sinkComp);

            // Deleting the source cannot clean up the sink's list because the source has no link entry for it.
            entMan.DeleteEntity(source);
            entMan.DeleteEntity(sink);

            mapSys.DeleteMap(mapId);
        });

        await pair.CleanReturnAsync();
    }
}
