// SPDX-License-Identifier: AGPL-3.0-or-later

#nullable enable annotations

using System.Collections.Generic;
using System.Numerics;
using Content.Goobstation.Maths.FixedPoint;
using Content.Server._Lavaland.BossMechanics;
using Content.Shared._Lavaland.Aggression;
using Content.Shared._Lavaland.Anger.Components;
using Content.Shared._Lavaland.Damage.Components;
using Content.Shared._Lavaland.EntityShapes.Components;
using Content.Shared._Lavaland.Megafauna;
using Content.Shared._Lavaland.Megafauna.Components;
using Content.Shared._Lavaland.Megafauna.Events;
using Content.Shared._Lavaland.Megafauna.NumberSelectors;
using Content.Shared._Lavaland.Megafauna.Selectors;
using Content.Shared._Lavaland.TileChaser;
using Content.Shared.Actions.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Weapons.Melee;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Lavaland.Megafauna;

/// <summary>
/// TG parity checks for the hierophant: strong blink blasts, the melee burst
/// and the four-chaser swarm. The TG death burst is gone on purpose.
/// </summary>
[TestFixture]
public sealed class HierophantParityTest
{
    /// <summary>
    /// Blink blasts go from the TG 30 burn to 36 with the fork damage buff.
    /// </summary>
    [Test]
    public async Task BlinkBlastIsThirtySixBurn()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var proto = server.ResolveDependency<IPrototypeManager>();

        await server.WaitAssertion(() =>
        {
            var blink = proto.Index<EntityPrototype>("LavalandHierophantBlinkSquare");
            Assert.That(blink.TryGetComponent<DamageSquareComponent>(out var square, entMan.ComponentFactory), Is.True,
                "the blink square lost its DamageSquareComponent");
            Assert.That(square!.Damage.DamageDict.TryGetValue("Heat", out var heat), Is.True,
                "the blink square deals no heat damage");
            Assert.That(heat, Is.EqualTo(FixedPoint2.New(36)),
                "the blink square must deal 36 heat: TG 30 times the fork 1.2");

            var strongShape = proto.Index<EntityPrototype>("HierophantShapeBlinkStrong");
            Assert.That(strongShape.TryGetComponent<ShapeSpawnerComponent>(out var spawner, entMan.ComponentFactory), Is.True,
                "the strong blink shape lost its ShapeSpawnerComponent");
            Assert.That(spawner!.Spawn, Is.EqualTo(new EntProtoId("LavalandHierophantBlinkSquare")),
                "the strong blink shape must spawn the 36 heat squares");
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// A melee hit must release a burst of blast squares around the hierophant.
    /// </summary>
    [Test]
    public async Task MeleeHitReleasesBurst()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var mapSys = entMan.System<SharedMapSystem>();

        var testMap = await pair.CreateTestMap();
        EntityUid boss = default;
        EntityUid cow = default;

        await server.WaitPost(() =>
        {
            var tile = testMap.Tile.Tile;
            for (var x = -12; x <= 12; x++)
            {
                for (var y = -12; y <= 12; y++)
                    mapSys.SetTile(testMap.Grid, new Vector2i(x, y), tile);
            }

            boss = entMan.SpawnEntity("MobHierophant", testMap.GridCoords);
            cow = entMan.SpawnEntity("MobCow", testMap.GridCoords.Offset(new Vector2(1.5f, 0f)));
        });

        await server.WaitRunTicks(2);

        await server.WaitPost(() =>
        {
            var hit = new MeleeHitEvent(
                new List<EntityUid> { cow }, boss, boss, null, null, entMan.GetComponent<TransformComponent>(boss).Coordinates);
            entMan.EventBus.RaiseLocalEvent(boss, hit);
        });

        // The burst must grow: the expanding spawner releases several rings, not one static ring.
        await server.WaitRunTicks(25);

        await server.WaitAssertion(() =>
        {
            var bossPos = entMan.GetComponent<TransformComponent>(boss).Coordinates.Position;
            Assert.That(CountSquaresNear(entMan, bossPos, 6f), Is.GreaterThan(8),
                "the hierophant melee hit released one static ring instead of an expanding burst");
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// The chaser swarm action must release four chasers at once.
    /// </summary>
    [Test]
    public async Task ChaserSwarmSpawnsFourChasers()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var mapSys = entMan.System<SharedMapSystem>();

        var testMap = await pair.CreateTestMap();
        EntityUid boss = default;
        EntityUid cow = default;

        await server.WaitPost(() =>
        {
            var tile = testMap.Tile.Tile;
            for (var x = -12; x <= 12; x++)
            {
                for (var y = -12; y <= 12; y++)
                    mapSys.SetTile(testMap.Grid, new Vector2i(x, y), tile);
            }

            boss = entMan.SpawnEntity("MobHierophant", testMap.GridCoords);
            cow = entMan.SpawnEntity("MobCow", testMap.GridCoords.Offset(new Vector2(6f, 0f)));
            var ev = new SpawnEntityActionEvent
            {
                Spawn = "LavalandHierophantChaser",
                SpawnAtUser = true,
                Amount = 4,
                Target = entMan.GetComponent<TransformComponent>(boss).Coordinates,
                Entity = cow,
                Performer = boss,
            };
            entMan.EventBus.RaiseLocalEvent(boss, ev, broadcast: true);
        });

        // Check the spawn tiles before any chaser crawls: four chasers on one tile would walk
        // the same path and stack their squares on the same tiles.
        var spawnTiles = new HashSet<Vector2i>();
        await server.WaitAssertion(() =>
        {
            var query = entMan.EntityQueryEnumerator<TransformComponent, MetaDataComponent>();
            while (query.MoveNext(out var xform, out var meta))
            {
                if (meta.EntityPrototype?.ID == "LavalandHierophantChaser")
                    spawnTiles.Add(new Vector2i((int) MathF.Round(xform.Coordinates.Position.X), (int) MathF.Round(xform.Coordinates.Position.Y)));
            }
        });

        await server.WaitRunTicks(1);

        await server.WaitAssertion(() =>
        {
            Assert.That(CountSquares(entMan, "LavalandHierophantChaser"), Is.EqualTo(4),
                "the chaser swarm must release four chasers, got " + CountSquares(entMan, "LavalandHierophantChaser"));
            Assert.That(spawnTiles.Count, Is.EqualTo(4),
                "the chaser swarm spawned on " + spawnTiles.Count + " distinct tiles instead of four");
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// Death must not release the old TG expanding burst: it kept blasting the arena for
    /// seconds after the boss died, so the feedback fix removed it.
    /// </summary>
    [Test]
    public async Task DeathReleasesNoBlastSquares()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var damage = server.System<DamageableSystem>();
        var proto = server.ResolveDependency<IPrototypeManager>();
        var mapSys = entMan.System<SharedMapSystem>();

        var testMap = await pair.CreateTestMap();
        EntityUid boss = default;

        await server.WaitPost(() =>
        {
            var tile = testMap.Tile.Tile;
            for (var x = -12; x <= 12; x++)
            {
                for (var y = -12; y <= 12; y++)
                    mapSys.SetTile(testMap.Grid, new Vector2i(x, y), tile);
            }

            boss = entMan.SpawnEntity("MobHierophant", testMap.GridCoords);
            var aggressive = entMan.GetComponent<AggressiveComponent>(boss);
            entMan.System<AggressorsSystem>().AddAggressor(
                (boss, aggressive), entMan.SpawnEntity("MobCow", testMap.GridCoords.Offset(new Vector2(2f, 0f))));

            // MegafaunaGodmode blocks every damage change without a player origin, tests go around it.
            entMan.RemoveComponent<MegafaunaGodmodeComponent>(boss);

            var lethal = new DamageSpecifier(proto.Index<DamageTypePrototype>("Blunt"), 5000);
            damage.TryChangeDamage(boss, lethal, ignoreResistances: true);
        });

        // The removed burst used to spawn rings for four seconds after death; watch long enough
        // that any leftover post-death attack would show up.
        await server.WaitRunTicks(90);

        await server.WaitAssertion(() =>
        {
            var deleted = !entMan.EntityExists(boss)
                || entMan.GetComponent<MetaDataComponent>(boss).EntityLifeStage == EntityLifeStage.Deleted;
            Assert.That(deleted, Is.True,
                "the hierophant must still delete itself on death");

            Assert.That(CountSquares(entMan, "LavalandHierophantSquare"), Is.Zero,
                "the dead hierophant still released blast squares, got " + CountSquares(entMan, "LavalandHierophantSquare"));
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// Boss stats follow the difficulty policy: the stronger of the TG value and our old one,
    /// then the fork multipliers on top.
    /// </summary>
    [Test]
    public async Task BossStatsMatchDifficultyPolicy()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var proto = server.ResolveDependency<IPrototypeManager>();
        var thresholdSys = server.System<MobThresholdSystem>();
        var testMap = await pair.CreateTestMap();

        EntityUid boss = default;
        await server.WaitPost(() => boss = entMan.SpawnEntity("MobHierophant", testMap.GridCoords));

        await server.WaitAssertion(() =>
        {
            Assert.That(thresholdSys.TryGetThresholdForState(boss, MobState.Dead, out var deathHp), Is.True,
                "the hierophant lost its death threshold");
            Assert.That(deathHp, Is.Not.Null);
            Assert.That(deathHp!.Value, Is.EqualTo(FixedPoint2.New(3275)),
                "the hierophant must die at 3275 HP instead of 2850");

            var protoBoss = proto.Index<EntityPrototype>("MobHierophant");

            Assert.That(protoBoss.TryGetComponent<AngerComponent>(out var anger, entMan.ComponentFactory), Is.True,
                "the hierophant lost its AngerComponent");
            Assert.That(anger!.TotalHp, Is.EqualTo(FixedPoint2.New(3275)),
                "Anger totalHp must follow the new death threshold");

            Assert.That(protoBoss.TryGetComponent<MeleeWeaponComponent>(out var melee, entMan.ComponentFactory), Is.True,
                "the hierophant lost its MeleeWeaponComponent");
            Assert.That(melee!.Damage.DamageDict["Blunt"], Is.EqualTo(FixedPoint2.New(18)),
                "melee must go from the TG 15 to 18");
            Assert.That(melee.Damage.ArmorPenetration, Is.EqualTo(0.5f).Within(0.0001f),
                "melee armour penetration must not fall under the TG 50");
            Assert.That(melee.AttackRate, Is.EqualTo(1.1f).Within(0.0001f),
                "melee attack rate must be 1.1");

            Assert.That(protoBoss.TryGetComponent<MegafaunaAiComponent>(out var ai, entMan.ComponentFactory), Is.True,
                "the hierophant lost its MegafaunaAiComponent");
            var aiDelay = (MegafaunaConstantNumberSelector) ai!.ActionDelaySelector;
            Assert.That(aiDelay.Value, Is.EqualTo(0.4f).Within(0.0001f),
                "AI actionDelay must drop from 0.5 to 0.4");

            Assert.That(protoBoss.TryGetComponent<BossEnrageComponent>(out var enrage, entMan.ComponentFactory), Is.True,
                "the hierophant lost its BossEnrageComponent");
            Assert.That(enrage!.ActionDelay, Is.Not.Null);
            Assert.That(enrage.ActionDelay!.Value, Is.EqualTo(0.3f).Within(0.0001f),
                "enrage actionDelay must not fall under the 0.3 floor");

            Assert.That(protoBoss.TryGetComponent<HierophantBlinkSlamComponent>(out var slam, entMan.ComponentFactory), Is.True,
                "the hierophant lost its HierophantBlinkSlamComponent");
            Assert.That(slam!.SlamDelay, Is.EqualTo(0.7f).Within(0.0001f),
                "the blink slam delay must drop from 0.9");

            Assert.That(protoBoss.TryGetComponent<HierophantArenaTrapComponent>(out var trap, entMan.ComponentFactory), Is.True,
                "the hierophant lost its HierophantArenaTrapComponent");
            Assert.That(trap!.Cooldown, Is.EqualTo(TimeSpan.FromSeconds(15)),
                "the arena trap cooldown must drop from the TG 20 seconds");
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// Damage of every hierophant blast follows ceil(max(now, TG) * 1.2) and stays under the 50 per hit cap.
    /// </summary>
    [Test]
    public async Task AbilityDamageMatchesPolicy()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var proto = server.ResolveDependency<IPrototypeManager>();

        await server.WaitAssertion(() =>
        {
            AssertSquareDamage(proto, entMan, "LavalandHierophantSquare", "Blunt", 12);
            AssertSquareDamage(proto, entMan, "LavalandHierophantSquare", "Heat", 12);
            AssertSquareDamage(proto, entMan, "LavalandHierophantBlinkSquare", "Heat", 36);
            AssertSquareDamage(proto, entMan, "LavalandHierophantBlinkSlamSquare", "Blunt", 18);
            AssertSquareDamage(proto, entMan, "LavalandHierophantBlinkSlamSquare", "Heat", 18);

            var chaser = proto.Index<EntityPrototype>("LavalandHierophantChaser");
            Assert.That(chaser.TryGetComponent<TileChaserComponent>(out var tile, entMan.ComponentFactory), Is.True,
                "the chaser lost its TileChaserComponent");
            Assert.That(tile!.Spawn, Is.EqualTo(new EntProtoId("LavalandHierophantSquare")),
                "the chaser must leave the same blast tiles");

            foreach (var id in new[] { "LavalandHierophantSquare", "LavalandHierophantBlinkSquare", "LavalandHierophantBlinkSlamSquare" })
            {
                var squareProto = proto.Index<EntityPrototype>(id);
                Assert.That(squareProto.TryGetComponent<DamageSquareComponent>(out var square, entMan.ComponentFactory), Is.True,
                    id + " lost its DamageSquareComponent");
                Assert.That(square!.Damage.GetTotal().Float(), Is.LessThanOrEqualTo(50f),
                    id + " deals more than the 50 damage per hit cap");
            }
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// Action cooldowns and the AI feedback delays take the TG value where it is shorter, then x0.75.
    /// </summary>
    [Test]
    public async Task ActionCooldownsMatchPolicy()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var proto = server.ResolveDependency<IPrototypeManager>();

        await server.WaitAssertion(() =>
        {
            var baseAction = proto.Index<EntityPrototype>("ActionHierophantBox");
            Assert.That(baseAction.TryGetComponent<ActionComponent>(out var action, entMan.ComponentFactory), Is.True,
                "the box action lost its ActionComponent");
            Assert.That(action!.UseDelay, Is.Not.Null);
            Assert.That(action.UseDelay!.Value.TotalSeconds, Is.EqualTo(1.5).Within(0.0001),
                "the shared hierophant action useDelay must drop from 2 to 1.5");

            AssertActionCooldown(proto, entMan, "ActionHierophantBox", 1.15, 3.0);
            AssertActionCooldown(proto, entMan, "ActionHierophantCrosses", 1.15, 3.0);
            AssertActionCooldown(proto, entMan, "ActionHierophantChasers", 5.25, 7.6);
            AssertActionCooldown(proto, entMan, "ActionHierophantChaserSwarm", 7.6, 7.6);
            AssertActionCooldown(proto, entMan, "ActionHierophantBlink", 2.25, 3.0);
            AssertActionCooldown(proto, entMan, "ActionHierophantBlinkSlam", 3.0, 6.75);

            AssertSelectorDelay(proto, "ActionHierophantBox", 0.75f, 3.0f);
            AssertSelectorDelay(proto, "ActionHierophantChasers", 0.1f, 0.4f);
            AssertSelectorDelay(proto, "ActionHierophantChaserSwarm", 3.0f, 7.5f);
            AssertSelectorDelay(proto, "ActionHierophantBlink", 0.2f, 0.75f);
            AssertSelectorDelay(proto, "ActionHierophantCrossesEnraged", 0.4f, 1.15f);
        });

        await pair.CleanReturnAsync();
    }

    private static void AssertSquareDamage(IPrototypeManager proto, IEntityManager entMan, string id, string type, int value)
    {
        var squareProto = proto.Index<EntityPrototype>(id);
        Assert.That(squareProto.TryGetComponent<DamageSquareComponent>(out var square, entMan.ComponentFactory), Is.True,
            id + " lost its DamageSquareComponent");
        Assert.That(square!.Damage.DamageDict.TryGetValue(type, out var damage), Is.True,
            id + " deals no " + type + " damage");
        Assert.That(damage, Is.EqualTo(FixedPoint2.New(value)), id + " " + type + " damage");
    }

    private static void AssertActionCooldown(IPrototypeManager proto, IEntityManager entMan, string id, double min, double max)
    {
        var actionProto = proto.Index<EntityPrototype>(id);
        Assert.That(actionProto.TryGetComponent<AngerDelayActionComponent>(out var delay, entMan.ComponentFactory), Is.True,
            id + " lost its AngerDelayActionComponent");
        Assert.That(delay!.MinDelay.TotalSeconds, Is.EqualTo(min).Within(0.0001), id + " min cooldown");
        Assert.That(delay.MaxDelay.TotalSeconds, Is.EqualTo(max).Within(0.0001), id + " max cooldown");
    }

    private static void AssertSelectorDelay(IPrototypeManager proto, string id, float min, float max)
    {
        var perform = proto.Index<MegafaunaSelectorPrototype>(id).Selector as PerformActionSelector;
        Assert.That(perform, Is.Not.Null, id + " is not a PerformActionSelector");
        var delay = perform!.DelaySelector as AngerNumberSelector;
        Assert.That(delay, Is.Not.Null, id + " delay is not anger scaled");
        Assert.That(delay!.Range.X, Is.EqualTo(min).Within(0.0001f), id + " selector min delay");
        Assert.That(delay.Range.Y, Is.EqualTo(max).Within(0.0001f), id + " selector max delay");
    }

    private static int CountSquaresNear(IEntityManager entMan, Vector2 center, float radius)
    {
        var squares = 0;
        var query = entMan.EntityQueryEnumerator<TransformComponent, MetaDataComponent>();
        while (query.MoveNext(out var xform, out var meta))
        {
            if (meta.EntityPrototype?.ID != "LavalandHierophantSquare")
                continue;
            if ((xform.Coordinates.Position - center).Length() > radius)
                continue;
            squares++;
        }

        return squares;
    }

    private static int CountSquares(IEntityManager entMan, string prototype)
    {
        var squares = 0;
        var query = entMan.EntityQueryEnumerator<MetaDataComponent>();
        while (query.MoveNext(out var meta))
        {
            if (meta.EntityPrototype?.ID == prototype)
                squares++;
        }

        return squares;
    }
}