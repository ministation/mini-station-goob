// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Movement.Pulling.Systems;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests.Puller;

/// <summary>
/// A polymorph jaunt or cryostorage moves a body to a paused map. A pull that starts in the same tick used to
/// create a pull joint between bodies on different maps, which the engine reports as
/// "system.joint: Tried to add joint to ineligible bodies".
/// </summary>
[TestFixture]
[TestOf(typeof(PullingSystem))]
public sealed class PullAcrossMapsTest
{
    [Test]
    public async Task CannotStartPullBetweenDifferentMaps()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.EntMan;
        var mapA = await pair.CreateTestMap();
        var mapB = await pair.CreateTestMap();

        EntityUid puller = default;
        EntityUid target = default;

        await server.WaitAssertion(() =>
        {
            // Same local coordinates on different maps: the positions match, but a physics joint cannot link them.
            puller = entMan.SpawnEntity("MobHuman", mapB.GridCoords);
            target = entMan.SpawnEntity("MobHuman", mapA.GridCoords);

            var pulling = server.System<PullingSystem>();
            Assert.That(pulling.TryStartPull(puller, target), Is.False);
            Assert.That(entMan.GetComponent<PullerComponent>(puller).Pulling, Is.Null);
            Assert.That(entMan.GetComponent<PullableComponent>(target).Puller, Is.Null);
        });

        await pair.CleanReturnAsync();
    }
}
