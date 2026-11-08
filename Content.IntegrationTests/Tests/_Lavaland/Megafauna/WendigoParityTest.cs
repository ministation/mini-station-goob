// SPDX-License-Identifier: AGPL-3.0-or-later

#nullable enable annotations

using System.Collections.Generic;
using System.Numerics;
using Content.Goobstation.Maths.FixedPoint;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.Mobs.Components;
using Content.Shared._Lavaland.Megafauna.Components;
using Content.Shared.Projectiles;
using Content.Shared.Teleportation.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Lavaland.Megafauna;

/// <summary>
/// TG parity checks for the wendigo: the one-way exit portal left on death and
/// the bending spiral shockwaves.
/// </summary>
[TestFixture]
public sealed class WendigoParityTest
{
    /// <summary>
    /// Dying wendigo leaves a one-way portal at the arena spawn point, linked to an
    /// exit point outside the arena.
    /// </summary>
    [Test]
    public async Task DeathLeavesOneWayExitPortal()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var damage = server.System<DamageableSystem>();
        var proto = server.ResolveDependency<IPrototypeManager>();
        var mapSys = entMan.System<SharedMapSystem>();

        var testMap = await pair.CreateTestMap();
        EntityUid boss = default;
        Vector2 spawnPos = default;

        await server.WaitPost(() =>
        {
            var tile = testMap.Tile.Tile;
            for (var x = -14; x <= 14; x++)
            {
                for (var y = -14; y <= 14; y++)
                    mapSys.SetTile(testMap.Grid, new Vector2i(x, y), tile);
            }

            boss = entMan.SpawnEntity("LavalandBossWendigo", testMap.GridCoords);
            spawnPos = entMan.GetComponent<TransformComponent>(boss).Coordinates.Position;

            // MegafaunaGodmode blocks every damage change without a player origin, tests go around it.
            if (entMan.HasComponent<MegafaunaGodmodeComponent>(boss))
                entMan.RemoveComponent<MegafaunaGodmodeComponent>(boss);

            var lethal = new DamageSpecifier(proto.Index<DamageTypePrototype>("Blunt"), 5000);
            damage.TryChangeDamage(boss, lethal, ignoreResistances: true);
        });

        await server.WaitRunTicks(3);

        await server.WaitAssertion(() =>
        {
            EntityUid portal = default;
            var query = entMan.EntityQueryEnumerator<TransformComponent, MetaDataComponent>();
            var found = false;
            while (query.MoveNext(out var xform, out var meta))
            {
                if (meta.EntityPrototype?.ID != "LavalandWendigoExitPortal")
                    continue;
                if ((xform.Coordinates.Position - spawnPos).Length() > 2f)
                    continue;
                portal = xform.Owner;
                found = true;
                break;
            }

            Assert.That(found, Is.True,
                "the dead wendigo left no exit portal at the arena spawn point");

            Assert.That(entMan.TryGetComponent<LinkedEntityComponent>(portal, out var link), Is.True,
                "the exit portal has no link to its exit point");
            Assert.That(link!.LinkedEntities.Count, Is.EqualTo(1),
                "the exit portal must link exactly one exit point");

            var exit = System.Linq.Enumerable.First(link.LinkedEntities);
            Assert.That(entMan.GetComponent<MetaDataComponent>(exit).EntityPrototype?.ID,
                Is.EqualTo("LavalandWendigoExitPoint"),
                "the portal link leads somewhere other than the arena exit point");
            var exitPos = entMan.GetComponent<TransformComponent>(exit).Coordinates.Position;
            Assert.That((exitPos - spawnPos).Length(), Is.GreaterThan(8f),
                "the exit point must sit outside the arena patch");
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// The spiral shockwaves bend while flying, like TG's wave_speed.
    /// </summary>
    [Test]
    public async Task SpiralShockwavesCurve()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var mapSys = entMan.System<SharedMapSystem>();

        var testMap = await pair.CreateTestMap();
        var physics = server.System<SharedPhysicsSystem>();
        EntityUid bolt = default;
        var straight = new Vector2(10f, 0f);

        await server.WaitPost(() =>
        {
            var tile = testMap.Tile.Tile;
            for (var x = -14; x <= 14; x++)
            {
                for (var y = -14; y <= 14; y++)
                    mapSys.SetTile(testMap.Grid, new Vector2i(x, y), tile);
            }

            bolt = entMan.SpawnEntity("LavalandWendigoSpiralShockwave", testMap.GridCoords);

            // A projectile only bends if it is actually moving: give it flight speed first.
            physics.SetLinearVelocity(bolt, straight);
        });

        await server.WaitRunTicks(4);

        await server.WaitAssertion(() =>
        {
            Assert.That(entMan.EntityExists(bolt), Is.True, "the shockwave despawned too fast to curve");
            var angle = entMan.GetComponent<ProjectileComponent>(bolt).Angle;
            Assert.That(Math.Abs(angle.Theta), Is.GreaterThan(0.1),
                "the spiral shockwave flew straight, its flight must bend");

            // The cosmetic angle alone proves nothing: the velocity itself has to turn.
            var velocity = entMan.GetComponent<PhysicsComponent>(bolt).LinearVelocity;
            Assert.That(velocity.Length(), Is.GreaterThan(1f), "the shockwave lost its flight speed");
            var cos = Vector2.Dot(Vector2.Normalize(straight), Vector2.Normalize(velocity));
            var turned = MathF.Acos(Math.Clamp(cos, -1f, 1f));
            Assert.That(turned, Is.GreaterThan(0.05f), "the shockwave velocity still points straight, it did not curve");
        });

        await pair.CleanReturnAsync();
    }
}