// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using Content.Goobstation.Shared.FloorGoblin;
using Content.Shared.Actions;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests.Actions;

/// <summary>
/// Reproduces the production line
/// "resolve: Can't resolve "Robust.Shared.GameObjects.TransformComponent" on entity 0/n0D!".
/// An entity-target action request whose target failed validation was still performed: the action
/// event was raised with a default (invalid) target, and the handler resolved its transform.
/// </summary>
[TestFixture]
[TestOf(typeof(SharedActionsSystem))]
public sealed class EntityTargetActionInvalidTargetTest
{
    [Test]
    public async Task OutOfRangeTargetDoesNotPerformAction()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        EntityUid goblin = default;
        EntityUid target = default;
        EntityUid action = default;

        await server.WaitPost(() =>
        {
            goblin = server.EntMan.SpawnEntity("MobFloorGoblin", map.GridCoords);

            // Far beyond the action's interaction range: ValidateEntityTarget rejects this target.
            target = server.EntMan.SpawnEntity("MobHuman", map.GridCoords.Offset(new Vector2(10, 0)));

            Assert.That(server.EntMan.TryGetComponent<StealShoesComponent>(goblin, out var steal), Is.True);
            Assert.That(steal!.StealAction, Is.Not.Null);
            action = steal.StealAction!.Value;
        });

        await server.WaitAssertion(() =>
        {
            var request = new RequestPerformActionEvent(
                server.EntMan.GetNetEntity(action),
                server.EntMan.GetNetEntity(target));

            Assert.That(server.System<SharedActionsSystem>().TryPerformAction(goblin, request), Is.False);
        });

        // Without the fix the action runs and StealShoesSystem resolves the invalid target's
        // TransformComponent, logging an error, which fails CleanReturnAsync.
        await pair.CleanReturnAsync();
    }
}
