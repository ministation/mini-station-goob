// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.Administration.Systems;
using Content.Server.Physics.Components;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests.Administration;

/// <summary>
/// The homing rod smite parks <see cref="ChasingWalkComponent.NextChangeVectorTime"/> in the far
/// future. That field is auto-paused, so unpausing the rod adds the pause duration to it. With
/// <see cref="TimeSpan.MaxValue"/> the addition overflows and the engine logs
/// "system.transform: Caught exception while attempting to detach an entity to nullspace.
/// ... TimeSpan overflowed because the duration is too long."
/// </summary>
[TestFixture, TestOf(typeof(AdminVerbSystem))]
public sealed class HomingRodPauseTest
{
    [Test]
    public async Task DeletingPausedHomingRodDoesNotOverflow()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.EntMan;
        var map = await pair.CreateTestMap();
        var mapSys = server.System<SharedMapSystem>();

        EntityUid rod = default;

        await server.WaitPost(() =>
        {
            var target = entMan.SpawnEntity(null, map.GridCoords);
            entMan.System<AdminVerbSystem>().HomingLaunchSequence(target, "ImmovableRodKeepTiles", 350f, 25f);

            var query = entMan.AllEntityQueryEnumerator<ChasingWalkComponent>();
            Assert.That(query.MoveNext(out rod, out _), Is.True);
        });

        await server.WaitRunTicks(1);
        await server.WaitPost(() => mapSys.SetPaused(map.MapId, true));
        await server.WaitRunTicks(2);

        await server.WaitAssertion(() =>
        {
            // The rod must really be paused: only then does the detach raise EntityUnpausedEvent.
            Assert.That(entMan.IsPaused(rod), Is.True);

            // Deleting the rod detaches it from the paused map to nullspace, which unpauses it.
            Assert.DoesNotThrow(() => entMan.DeleteEntity(rod));
            Assert.That(entMan.Deleted(rod), Is.True);
        });

        await pair.CleanReturnAsync();
    }
}
