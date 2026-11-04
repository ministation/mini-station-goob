// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Goobstation.Server.ChronoLegionnaire.Systems;
using Content.Goobstation.Shared.ChronoLegionnaire.Components;
using Content.Server.Storage.EntitySystems;
using Content.Shared.Storage.Components;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests.Goobstation;

/// <summary>
/// A stasis container applies stasis to the entities stored inside it. Observers and items have no
/// StatusEffectsComponent, so the stasis attempt used to log "Can't resolve StatusEffectsComponent"
/// for them.
/// </summary>
[TestFixture]
[TestOf(typeof(StasisSystem))]
public sealed class StasisContainerTest
{
    [Test]
    public async Task EntityWithoutStatusEffectsDoesNotLogError()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        EntityUid cage = default;
        EntityUid observer = default;
        await server.WaitPost(() =>
        {
            cage = server.EntMan.SpawnEntity("StasisCage", map.GridCoords);
            observer = server.EntMan.SpawnEntity("MobObserver", map.GridCoords);

            // A ghost-like observer has no StatusEffectsComponent, but can still end up inside a stasis cage.
            server.System<EntityStorageSystem>().Insert(observer, cage);
        });

        await server.WaitAssertion(() =>
        {
            Assert.That(server.EntMan.HasComponent<InsideEntityStorageComponent>(observer), Is.True);
            Assert.That(server.EntMan.HasComponent<InsideStasisComponent>(observer), Is.False);
        });

        await pair.CleanReturnAsync();
    }
}
