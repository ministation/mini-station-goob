// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

using System.Collections.Generic;
using System.Numerics;
using Content.Server._Lavaland.Loot;
using Content.Shared.Climbing.Components;
using Content.Shared.Damage.Components;
using Content.Shared.Interaction.Events;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests._Lavaland;

/// <summary>
/// Jacob's ladder unfolds one unbreakable ladder on the user's tile and is consumed.
/// The fork has a single z level, so the TG chain of ladders across z levels cannot be built.
/// </summary>
[TestFixture]
[TestOf(typeof(JacobsLadderSystem))]
public sealed class JacobsLadderTest
{
    [Test]
    public async Task UsingTheLadderUnfoldsOneUnbreakableLadder()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var mapSys = server.System<SharedMapSystem>();

        var testMap = await pair.CreateTestMap();
        var grid = testMap.Grid;

        EntityUid user = default;
        EntityUid item = default;

        await server.WaitPost(() =>
        {
            user = entMan.SpawnEntity(null, testMap.GridCoords);
            item = entMan.SpawnEntity("LavalandJacobsLadder", new EntityCoordinates(user, Vector2.Zero));
        });

        await server.WaitRunTicks(2);

        await server.WaitPost(() => entMan.EventBus.RaiseLocalEvent(item, new UseInHandEvent(user)));

        // Let the queued deletion of the item process.
        await server.WaitRunTicks(2);

        await server.WaitAssertion(() =>
        {
            Assert.That(entMan.Deleted(item), Is.True, "using the item must consume it");

            var userTile = mapSys.LocalToTile(grid, grid.Comp, entMan.GetComponent<TransformComponent>(user).Coordinates);

            var ladders = new List<EntityUid>();
            foreach (var uid in mapSys.GetAnchoredEntities(grid, grid.Comp, userTile))
            {
                if (entMan.GetComponent<MetaDataComponent>(uid).EntityPrototype?.ID == "LavalandJacobsLadderStructure")
                    ladders.Add(uid);
            }

            Assert.That(ladders.Count, Is.EqualTo(1), "exactly one ladder must be unfolded on the user's tile");

            var ladder = ladders[0];
            Assert.That(entMan.GetComponent<TransformComponent>(ladder).Anchored, Is.True, "the ladder must stay anchored");
            Assert.That(entMan.HasComponent<ClimbableComponent>(ladder), Is.True, "the ladder must be climbable");
            Assert.That(entMan.HasComponent<DamageableComponent>(ladder), Is.False,
                "the TG jacob ladder is unbreakable, so the structure must have no Damageable");
        });

        await pair.CleanReturnAsync();
    }
}
