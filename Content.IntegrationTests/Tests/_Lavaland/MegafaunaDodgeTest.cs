// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using Content.Shared._Lavaland.Megafauna.Components;
using Content.Shared.NPC.Components;
using Content.Shared.NPC.Systems;
using Content.Shared.Projectiles;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests._Lavaland;

/// <summary>
/// Guards the megafauna projectile dodge: staged bosses get the component without a YAML edit,
/// hostile shots are dodged with a short blink, while own shots and cooldown shots are not.
/// </summary>
[TestFixture]
public sealed class MegafaunaDodgeTest
{
    [Test]
    public async Task StagedBossDodgesHostileProjectile()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var testMap = await pair.CreateTestMap();
        var entMan = server.ResolveDependency<IEntityManager>();
        var mapSys = server.System<SharedMapSystem>();
        var faction = server.System<NpcFactionSystem>();

        EntityUid boss = default;
        EntityUid shooter = default;
        EntityUid bolt = default;

        // CreateTestMap is a single tile, the boss needs room to blink to a clear tile.
        await server.WaitPost(() =>
        {
            for (var x = -5; x <= 5; x++)
            {
                for (var y = -5; y <= 5; y++)
                    mapSys.SetTile(testMap.Grid, new EntityCoordinates(testMap.Grid, x, y), testMap.Tile.Tile);
            }
        });

        await server.WaitPost(() =>
        {
            boss = entMan.SpawnEntity("LavalandBossAshDrake", testMap.GridCoords);
            shooter = entMan.SpawnEntity("MobHuman", testMap.GridCoords.Offset(new Vector2(4f, 0f)));
            entMan.EnsureComponent<NpcFactionMemberComponent>(shooter);
            faction.AddFaction(shooter, "NanoTrasen");

            // Any real projectile will do, only its Shooter decides if the shot is hostile.
            bolt = entMan.SpawnEntity("LavalandHeraldBolt", testMap.GridCoords.Offset(new Vector2(0f, 4f)));
        });

        await server.WaitRunTicks(5);

        await server.WaitAssertion(() =>
        {
            Assert.That(entMan.HasComponent<MegafaunaDodgeComponent>(boss), Is.True,
                "staged megafauna must get the dodge without a prototype edit");

            var dodge = entMan.GetComponent<MegafaunaDodgeComponent>(boss);
            dodge.DodgeChance = 1f; // deterministic: always roll the dodge
            dodge.Cooldown = TimeSpan.FromSeconds(60);
        });

        var start = Vector2.Zero;
        await server.WaitAssertion(() =>
            start = entMan.GetComponent<TransformComponent>(boss).Coordinates.Position);

        // The boss's own projectile must never be dodged.
        await server.WaitAssertion(() =>
        {
            var projectile = entMan.GetComponent<ProjectileComponent>(bolt);
            projectile.Shooter = boss;
            var ev = new ProjectileReflectAttemptEvent(bolt, projectile, false);
            entMan.EventBus.RaiseLocalEvent(boss, ref ev);
            Assert.That(ev.Cancelled, Is.False, "the boss must not dodge its own projectile");
        });

        // A crew shot is hostile fire: the hit is cancelled and the boss blinks away.
        await server.WaitAssertion(() =>
        {
            var projectile = entMan.GetComponent<ProjectileComponent>(bolt);
            projectile.Shooter = shooter;
            var ev = new ProjectileReflectAttemptEvent(bolt, projectile, false);
            entMan.EventBus.RaiseLocalEvent(boss, ref ev);
            Assert.That(ev.Cancelled, Is.True, "the boss should dodge a shot fired by crew");
        });

        await server.WaitAssertion(() =>
        {
            var moved = entMan.GetComponent<TransformComponent>(boss).Coordinates.Position;
            Assert.That(moved, Is.Not.EqualTo(start), "dodging must blink the boss away from its tile");
        });

        // The cooldown keeps the boss from dodging every single shot.
        await server.WaitAssertion(() =>
        {
            var projectile = entMan.GetComponent<ProjectileComponent>(bolt);
            var ev = new ProjectileReflectAttemptEvent(bolt, projectile, false);
            entMan.EventBus.RaiseLocalEvent(boss, ref ev);
            Assert.That(ev.Cancelled, Is.False, "the dodge cooldown must skip the next shot");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task AnchoredBossDoesNotDodge()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var testMap = await pair.CreateTestMap();
        var entMan = server.ResolveDependency<IEntityManager>();

        EntityUid boss = default;
        EntityUid shooter = default;
        EntityUid bolt = default;

        await server.WaitPost(() =>
        {
            boss = entMan.SpawnEntity("LavalandBossColossus", testMap.GridCoords);
            shooter = entMan.SpawnEntity("MobHuman", testMap.GridCoords.Offset(new Vector2(4f, 0f)));
            bolt = entMan.SpawnEntity("LavalandHeraldBolt", testMap.GridCoords.Offset(new Vector2(0f, 4f)));
        });

        await server.WaitRunTicks(5);

        await server.WaitAssertion(() =>
        {
            Assert.That(entMan.HasComponent<MegafaunaAnchorComponent>(boss), Is.True,
                "test setup: the colossus has to be an anchored megafauna");

            var dodge = entMan.GetComponent<MegafaunaDodgeComponent>(boss);
            dodge.DodgeChance = 1f;
            dodge.Cooldown = TimeSpan.Zero;

            var start = entMan.GetComponent<TransformComponent>(boss).Coordinates.Position;
            var projectile = entMan.GetComponent<ProjectileComponent>(bolt);
            projectile.Shooter = shooter;
            var ev = new ProjectileReflectAttemptEvent(bolt, projectile, false);
            entMan.EventBus.RaiseLocalEvent(boss, ref ev);

            Assert.Multiple(() =>
            {
                Assert.That(ev.Cancelled, Is.False, "stationary megafauna must not dodge projectiles");
                Assert.That(entMan.GetComponent<TransformComponent>(boss).Coordinates.Position,
                    Is.EqualTo(start), "stationary megafauna must not teleport away");
            });
        });

        await pair.CleanReturnAsync();
    }
}
