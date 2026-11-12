// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

using System.Numerics;
using Content.Shared._Lavaland.Aggression;
using Content.Shared.Armor;
using Content.Shared.Clothing.Components;
using Robust.Shared.Containers;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Projectiles;
using Content.Shared.Standing;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Events;
using Robust.Shared.Prototypes;
using Content.Shared.Movement.Pulling.Events;
using Content.Shared.Polymorph;
using NUnit.Framework;
using Robust.UnitTesting;

namespace Content.IntegrationTests.Tests._Lavaland;

/// <summary>
/// Regression tests for the discord bug report: megafauna could be dodged by lying down,
/// dragged around, put into stamina crit; dragon's blood could delete gear or hand out a
/// fireball; the drake suit rejected guns and mining gear.
/// </summary>
[TestFixture]
public sealed class LavalandBossFightFixesTest
{
    private static readonly string[] Bosses =
    {
        "LavalandBossBubblegum",
        "LavalandBossColossus",
        "LavalandBossAshDrake",
        "MobHierophant",
        "LavalandBossWendigo",
        "LavalandBossDemonicFrostMiner",
        "LavalandBossThing",
        "LavalandBossBloodDrunk",
        "LavalandBossMegaLegion",
    };

    [Test]
    public async Task MegafaunaProjectilesHitProneTargets()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var testMap = await pair.CreateTestMap();
        var entMan = server.ResolveDependency<IEntityManager>();

        await server.WaitPost(() =>
        {
            var prone = entMan.SpawnEntity("MobHuman", testMap.MapCoords);
            entMan.EventBus.RaiseLocalEvent(prone, new DownedEvent());

            var player = entMan.SpawnEntity("MobHuman", new MapCoordinates(new Vector2(2, 0), testMap.MapId));
            var boss = entMan.SpawnEntity("LavalandBossColossus", new MapCoordinates(new Vector2(4, 0), testMap.MapId));
            var bolt = entMan.SpawnEntity("LavalandColossusBolt", new MapCoordinates(new Vector2(1, 0), testMap.MapId));

            var projectile = entMan.GetComponent<ProjectileComponent>(bolt);
            projectile.TargetCoordinates = new Vector2(50, 50);
            var physics = entMan.GetComponent<PhysicsComponent>(prone);

            // A player shot still misses the prone target (existing crawl feature).
            projectile.Shooter = player;
            var playerShot = new PreventCollideEvent(prone, bolt, physics, physics, null!, null!);
            entMan.EventBus.RaiseLocalEvent(prone, ref playerShot);
            Assert.That(playerShot.Cancelled, Is.True, "prone target should dodge an aimed player shot");

            // A megafauna shot must not be dodged by lying down.
            projectile.Shooter = boss;
            var bossShot = new PreventCollideEvent(prone, bolt, physics, physics, null!, null!);
            entMan.EventBus.RaiseLocalEvent(prone, ref bossShot);
            Assert.That(bossShot.Cancelled, Is.False, "prone target should still be hit by a megafauna projectile");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task MegafaunaCannotBePulledOrStamCrit()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var testMap = await pair.CreateTestMap();
        var entMan = server.ResolveDependency<IEntityManager>();
        var stamina = server.System<SharedStaminaSystem>();

        await server.WaitPost(() =>
        {
            var puller = entMan.SpawnEntity("MobHuman", testMap.MapCoords);

            foreach (var bossProto in Bosses)
            {
                var boss = entMan.SpawnEntity(bossProto, new MapCoordinates(new Vector2(2, 0), testMap.MapId));

                Assert.That(entMan.HasComponent<AggressiveComponent>(boss), Is.True, bossProto);

                var pull = new PullAttemptEvent(puller, boss);
                entMan.EventBus.RaiseLocalEvent(boss, pull);
                Assert.That(pull.Cancelled, Is.True, $"{bossProto} should not be pullable");

                // Bosses without a stamina pool (Bubblegum runs on plain HTN) are already immune.
                if (!entMan.TryGetComponent<StaminaComponent>(boss, out var staminaComp))
                    continue;

                stamina.TakeStaminaDamage(boss, 1000f);
                Assert.That(staminaComp.StaminaDamage, Is.LessThan(1f), $"{bossProto} should resist stamina damage");
                Assert.That(staminaComp.Critical, Is.False, $"{bossProto} should not be stamina crit");
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task LootAndDragonsBloodMatchReport()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var testMap = await pair.CreateTestMap();
        var entMan = server.ResolveDependency<IEntityManager>();

        await server.WaitPost(() =>
        {
            var coords = testMap.MapCoords;

            // Berserker armor keeps its helmet attached, like a hardsuit.
            var containers = server.System<SharedContainerSystem>();
            var berserker = entMan.SpawnEntity("LavalandBerserkerArmor", coords);
            Assert.That(entMan.HasComponent<ToggleableClothingComponent>(berserker), Is.True);
            Assert.That(containers.TryGetContainer(berserker, "toggleable-clothing", out var helmetContainer), Is.True);
            Assert.That(helmetContainer!.ContainedEntities, Has.Count.EqualTo(1));
            Assert.That(entMan.GetComponent<MetaDataComponent>(helmetContainer.ContainedEntities[0]).EntityPrototype?.ID,
                Is.EqualTo("LavalandBerserkerHelmet"));

            // Drake suit takes guns and mining melee in the suit storage slot and is fireproof.
            var drake = entMan.SpawnEntity("LavalandEquipmentDrakeArmour", coords);
            var whitelist = server.System<Content.Shared.Whitelist.EntityWhitelistSystem>();
            Assert.That(entMan.TryGetComponent<AllowSuitStorageComponent>(drake, out var suitStorage), Is.True);
            var gun = entMan.SpawnEntity("WeaponPistolMk58", coords);
            var crusher = entMan.SpawnEntity("WeaponCrusher", coords);
            Assert.That(whitelist.IsWhitelistPass(suitStorage!.Whitelist, gun), Is.True, "drake suit should take a gun");
            Assert.That(whitelist.IsWhitelistPass(suitStorage.Whitelist, crusher), Is.True, "drake suit should take a crusher");
            Assert.That(entMan.GetComponent<ArmorComponent>(drake).Modifiers.Coefficients["Heat"], Is.EqualTo(0f));

            // Skeleton curse keeps gear and never reverts; the fireball outcome is gone.
            var protoMan = server.ResolveDependency<IPrototypeManager>();
            var skeleton = protoMan.Index<PolymorphPrototype>("DragonsBloodSkeleton").Configuration;
            Assert.That(skeleton.Inventory, Is.EqualTo(PolymorphInventoryChange.Transfer));
            Assert.That(skeleton.RevertOnCrit, Is.False);
            Assert.That(skeleton.RevertOnDeath, Is.False);
            Assert.That(skeleton.RevertOnDelete, Is.False);

            Assert.That(protoMan.HasIndex<EntityPrototype>("ActionDragonsBloodDragonForm"), Is.True);
            Assert.That(protoMan.HasIndex<PolymorphPrototype>("DragonsBloodDragon"), Is.True);
            Assert.That(protoMan.Index<PolymorphPrototype>("DragonsBloodDragon").Configuration.Entity!.Value.Id,
                Is.EqualTo("LavalandBossAshDrake"));
        });

        await pair.CleanReturnAsync();
    }
}
