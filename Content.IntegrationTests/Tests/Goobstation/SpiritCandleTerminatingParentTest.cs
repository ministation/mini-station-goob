// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Goobstation.Shared.Wraith.SpiritCandle;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests.Goobstation;

/// <summary>
/// A lit spirit candle re-attaches its reveal area to itself when it leaves a container. During deletion the
/// candle is first flagged as terminating and only then detached from its container, so the removal handler
/// used to spawn the area and attach it to the terminating candle. The engine reports that as
/// "system.transform: ... SpiritCandleRevealArea is attempting to attach itself to a terminating entity ... SpiritCandle".
/// </summary>
[TestFixture]
[TestOf(typeof(SharedSpiritCandleSystem))]
public sealed class SpiritCandleTerminatingParentTest
{
    [Test]
    public async Task DeletingLitCandleInContainerDoesNotAttachAreaToTerminatingCandle()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.EntMan;
        var map = await pair.CreateTestMap();

        EntityUid candle = default;
        EntityUid holder = default;

        await server.WaitPost(() =>
        {
            var containerSys = server.System<SharedContainerSystem>();

            holder = entMan.SpawnEntity(null, map.GridCoords);
            var container = containerSys.MakeContainer<Container>(holder, "test");

            candle = entMan.SpawnEntity("SpiritCandle", map.GridCoords);
            Assert.That(containerSys.Insert(candle, container), Is.True);

            // A candle that was lit before it ended up in a container, e.g. a burning candle in a backpack.
            entMan.GetComponent<SpiritCandleComponent>(candle).Active = true;
        });

        // Deleting the candle flags it as terminating, then detaches it from the container, which raises the
        // container removal event. Without a guard the reveal area is spawned and attached to the terminating
        // candle there, the engine logs the error and the orphaned area stays in nullspace.
        await server.WaitPost(() => entMan.DeleteEntity(candle));

        await server.WaitAssertion(() =>
        {
            Assert.That(entMan.Deleted(candle), Is.True);
            Assert.That(entMan.Count<SpiritCandleAreaComponent>(), Is.Zero);
        });

        // The engine error would also be reported by the pooled server log here.
        await pair.CleanReturnAsync();
    }
}
