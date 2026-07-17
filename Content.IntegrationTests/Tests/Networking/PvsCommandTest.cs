// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.GameObjects;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.Networking;

[TestFixture]
public sealed class PvsCommandTest
{
    private static readonly EntProtoId TestEnt = "MobHuman";

    [Test]
    public async Task TestPvsCommands()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, EnableNetPvs = true });
        var (server, client) = pair;
        var map = await pair.CreateTestMap();

        // Spawn a complex entity near the player so PVS replicates it.
        EntityUid entity = default;
        await server.WaitPost(() =>
        {
            entity = server.EntMan.SpawnAtPosition(TestEnt, map.GridCoords);
            server.PlayerMan.SetAttachedEntity(pair.Player, entity, true);
        });
        await pair.RunTicksSync(30);

        // Check that the client has a variety pf entities.
        Assert.That(client.EntMan.EntityCount, Is.GreaterThan(0));
        Assert.That(client.EntMan.Count<MapComponent>, Is.GreaterThan(0));
        Assert.That(client.EntMan.Count<MapGridComponent>, Is.GreaterThan(0));

        var meta = client.MetaData(pair.ToClientUid(entity));
        var lastApplied = meta.LastStateApplied;

        // Dirty all entities
        await server.ExecuteCommand("dirty");
        await pair.RunTicksSync(5);
        Assert.That(meta.LastStateApplied, Is.GreaterThan(lastApplied));
        await pair.RunTicksSync(5);

        // Do a client-side full state reset
        await client.ExecuteCommand("resetallents");
        await pair.RunTicksSync(5);

        // Request a full server state.
        lastApplied = meta.LastStateApplied;
        await client.ExecuteCommand("fullstatereset");
        await pair.RunTicksSync(10);
        Assert.That(meta.LastStateApplied, Is.GreaterThan(lastApplied));

        await server.WaitPost(() => server.EntMan.DeleteEntity(entity));
        await pair.CleanReturnAsync();
    }
}