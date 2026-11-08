// SPDX-License-Identifier: AGPL-3.0-or-later

#nullable enable annotations

using System.Numerics;
using Content.Goobstation.Maths.FixedPoint;
using Content.Server._Lavaland.Crusher;
using Content.Shared.Damage.Components;
using Content.Shared.Projectiles;
using Content.Server._Lavaland.Mobs;
using Content.Shared._Lavaland.Projectiles;
using Robust.Shared.Maths;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Lavaland.CrusherTrophies;

/// <summary>
/// TG parity for the raptor feather crusher trophy: shots flagged by the trophy
/// fly through the shooter's allies, and raptors drop the feather on crusher kills.
/// </summary>
[TestFixture]
public sealed class RaptorFeatherTest
{
    /// <summary>
    /// The trophy exists, is wired onto raptors and enables the pass-through flag.
    /// </summary>
    [Test]
    public async Task FeatherIsWiredUp()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var proto = server.ResolveDependency<IPrototypeManager>();

        await server.WaitAssertion(() =>
        {
            var feather = proto.Index<EntityPrototype>("LavalandCrusherTrophyRaptorFeather");
            Assert.That(feather.TryGetComponent<FaunaCrusherTrophyComponent>(out var trophy, entMan.ComponentFactory), Is.True,
                "the raptor feather lost its FaunaCrusherTrophyComponent");
            Assert.That(trophy!.PassThroughAllies, Is.True,
                "the raptor feather must enable pass-through-allies");

            var raptor = proto.Index<EntityPrototype>("LavalandMobRaptorRed");
            Assert.That(raptor.TryGetComponent<SpawnLootOnDeathComponent>(out var loot, entMan.ComponentFactory), Is.True,
                "raptors do not drop a trophy on death");
            Assert.That(loot!.SpecialTable, Is.Not.Null, "raptor trophy drop has no special table");
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// A projectile flagged with the pass-through component must not hurt the shooter's ally.
    /// </summary>
    [Test]
    public async Task FlaggedShotPassesThroughAlly()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var mapSys = entMan.System<SharedMapSystem>();

        var testMap = await pair.CreateTestMap();
        EntityUid shooter = default;
        EntityUid ally = default;
        EntityUid bolt = default;

        await server.WaitPost(() =>
        {
            var tile = testMap.Tile.Tile;
            for (var x = -8; x <= 8; x++)
            {
                for (var y = -8; y <= 8; y++)
                    mapSys.SetTile(testMap.Grid, new Vector2i(x, y), tile);
            }

            shooter = entMan.SpawnEntity("MobCow", testMap.GridCoords);
            ally = entMan.SpawnEntity("MobCow", testMap.GridCoords.Offset(new Vector2(2f, 0f)));

            // A shot fired by the shooter, aimed straight at the ally two tiles away.
            bolt = entMan.SpawnEntity("LavalandWendigoShockwave", testMap.GridCoords.Offset(new Vector2(0.9f, 0f)));
            var projectile = entMan.GetComponent<ProjectileComponent>(bolt);
            projectile.Shooter = shooter;
            projectile.IgnoreShooter = false;
            entMan.AddComponent<ProjectileIgnoreAlliesComponent>(bolt);
        });

        await server.WaitRunTicks(4);

        await server.WaitAssertion(() =>
        {
            Assert.That(entMan.EntityExists(ally), Is.True, "the ally was deleted");
            // Cow ambient damage (unsuitable atmosphere) may tick; a real hit would land
            // the full 25 blunt of the shockwave.
            var damage = entMan.GetComponent<DamageableComponent>(ally).TotalDamage;
            Assert.That(damage, Is.LessThan(FixedPoint2.New(5)),
                "the flagged shot hit the shooter's ally instead of flying through");
        });

        await pair.CleanReturnAsync();
    }
}
