// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Actions.Components;
using Content.Shared.Eye.Blinding.Components;
using Content.Shared.Eye.Blinding.Systems;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests.Eye;

/// <summary>
/// A body with naturally created eyelids gets an eye-toggle action. When the body's containers are
/// emptied before the body is deleted, the action is detached from the body's action holder while
/// still being attached to the body. The eyelid component then used to log
/// "Attempted to remove an action ... that it was never attached to" when the body was deleted.
/// </summary>
[TestFixture]
[TestOf(typeof(EyeClosingSystem))]
public sealed class EyeClosingSystemTest
{
    [Test]
    public async Task ShutdownAfterActionDetachedDoesNotLogError()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        EntityUid body = default;
        await server.WaitPost(() =>
        {
            body = server.EntMan.SpawnEntity("MobDwarf", map.GridCoords);

            // Blur at max magnitude but below max damage creates the natural eyelids and their action.
            server.System<BlindableSystem>().SetEyeDamage(body, 7);
        });

        await server.WaitAssertion(() =>
        {
            Assert.That(server.EntMan.TryGetComponent<EyeClosingComponent>(body, out var eyelids), Is.True);
            Assert.That(eyelids!.EyeToggleActionEntity, Is.Not.Null);
        });

        await server.WaitPost(() =>
        {
            // Reproduce corpse cleanup: empty the body's action container, then delete the body.
            var containers = server.System<SharedContainerSystem>();
            var actionContainer = server.EntMan.GetComponent<ActionsContainerComponent>(body).Container;
            containers.EmptyContainer(actionContainer, force: true, reparent: true);

            server.EntMan.QueueDeleteEntity(body);
        });

        // Deletion runs on the next tick; an error logged there fails the test.
        await server.WaitRunTicks(2);

        await pair.CleanReturnAsync();
    }
}
