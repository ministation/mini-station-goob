// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

using Content.Shared._Lavaland.Chasm;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests._Lavaland.Megafauna;

/// <summary>
/// Bosses are the anchors of their arenas, a chasm opening under one must move it aside
/// instead of deleting the fight.
/// </summary>
[TestFixture]
[TestOf(typeof(PreventChasmFallingComponent))]
public sealed class BossChasmTest
{
    private static readonly string[] Bosses =
    {
        "LavalandBossAshDrake",
        "LavalandBossBloodDrunk",
        "LavalandBossBubblegum",
        "LavalandBossColossus",
        "MobHierophant",
        "LavalandBossMegaLegion",
    };

    [Test]
    public async Task BossesClimbOutOfChasms()
    {
        await using var pair = await PoolManager.GetServerClient();

        var server = pair.Server;
        var testMap = await pair.CreateTestMap();
        var entMan = server.ResolveDependency<IEntityManager>();

        foreach (var proto in Bosses)
        {
            EntityUid boss = default;
            var start = EntityCoordinates.Invalid;

            await server.WaitPost(() => boss = entMan.SpawnEntity(proto, testMap.GridCoords));
            await server.WaitRunTicks(3);

            await server.WaitPost(() =>
            {
                start = entMan.GetComponent<TransformComponent>(boss).Coordinates;
                // The chasm opening under the boss must not delete it.
                entMan.SpawnEntity("FloorChasmEntity", start);
            });

            await server.WaitRunTicks(130);

            await server.WaitAssertion(() =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(entMan.Deleted(boss), Is.False, $"{proto} fell into the void and was deleted");
                    Assert.That(entMan.HasComponent<PreventChasmFallingComponent>(boss), Is.True,
                        $"{proto} must never fall into a chasm");
                    Assert.That(entMan.GetComponent<TransformComponent>(boss).Coordinates, Is.Not.EqualTo(start),
                        $"{proto} should be moved to a safe tile instead of standing on the chasm");
                });
            });

            await server.WaitPost(() =>
            {
                if (!entMan.Deleted(boss))
                    entMan.DeleteEntity(boss);
            });
        }

        await pair.CleanReturnAsync();
    }
}
