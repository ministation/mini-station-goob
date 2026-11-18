// SPDX-License-Identifier: AGPL-3.0-or-later

#nullable enable annotations

using System.Collections.Generic;
using System.Numerics;
using Content.Goobstation.Maths.FixedPoint;
using Content.Server._Lavaland.BossMechanics;
using Content.Server.Atmos.Components;
using Content.Shared._Lavaland.Aggression;
using Content.Shared._Lavaland.Anger.Components;
using Content.Shared._Lavaland.Damage.Components;
using Content.Shared._Lavaland.EntityShapes.Components;
using Content.Shared._Lavaland.Megafauna;
using Content.Shared._Lavaland.Megafauna.Components;
using Content.Shared._Lavaland.Megafauna.Events;
using Content.Shared._Lavaland.Megafauna.NumberSelectors;
using Content.Shared._Lavaland.Megafauna.Selectors;
using Content.Shared._Lavaland.Mobs;
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
        // inversePeriod makes the rings come every 0.2..0.7 s and the hierophant is at zero anger here,
        // so the second ring lands at 0.7 s: wait past it but before the 1 s blast tile lifetime.
        await server.WaitRunTicks(50);

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

    /// <summary>
    /// TG beam_range grows from 5 tiles at zero anger to 11 at max anger. The cross spawner must
    /// build an anger sized cross instead of a fixed one.
    /// </summary>
    [Test]
    public async Task BeamRadiusScalesWithAnger()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var mapSys = entMan.System<SharedMapSystem>();
        var testMap = await pair.CreateTestMap();

        EntityUid boss = default;
        EntityUid bigSpawner = default;
        EntityUid smallSpawner = default;

        await server.WaitPost(() =>
        {
            var tile = testMap.Tile.Tile;
            for (var x = -12; x <= 12; x++)
            {
                for (var y = -12; y <= 12; y++)
                    mapSys.SetTile(testMap.Grid, new Vector2i(x, y), tile);
            }

            boss = entMan.SpawnEntity("MobHierophant", testMap.GridCoords.Offset(new Vector2(-6f, 0f)));
            var anger = entMan.GetComponent<AngerComponent>(boss);
            anger.MaxAnger = 1f;
            anger.CurrentAnger = 1f;
            bigSpawner = SpawnAngerShape(entMan, "HierophantShapeCrossSequence", testMap.GridCoords, boss);
        });

        await server.WaitAssertion(() =>
        {
            Assert.That(entMan.GetComponent<ShapeSpawnerComponent>(bigSpawner).Shape.DefaultSize, Is.EqualTo(11),
                "max anger must resize the cross to the TG beam_range 11");
            Assert.That(CountSquaresNear(entMan, Vector2.Zero, 15f), Is.EqualTo(41),
                "the max anger cross must spawn 41 blast tiles, got " + CountSquaresNear(entMan, Vector2.Zero, 15f));

            DeleteSquares(entMan);
            entMan.DeleteEntity(bigSpawner);
        });

        await server.WaitPost(() =>
        {
            var anger = entMan.GetComponent<AngerComponent>(boss);
            anger.CurrentAnger = 0f;
            smallSpawner = SpawnAngerShape(entMan, "HierophantShapeCrossSequence", testMap.GridCoords, boss);
        });

        await server.WaitAssertion(() =>
        {
            Assert.That(entMan.GetComponent<ShapeSpawnerComponent>(smallSpawner).Shape.DefaultSize, Is.EqualTo(5),
                "zero anger must resize the cross to the TG beam_range 5");
            Assert.That(CountSquaresNear(entMan, Vector2.Zero, 15f), Is.EqualTo(17),
                "the zero anger cross must spawn 17 blast tiles, got " + CountSquaresNear(entMan, Vector2.Zero, 15f));
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// TG chaser steps every 0.3 seconds at zero anger and every 0.1 seconds at max anger; our speed
    /// is steps per second, so 3.33 to 10. TG blasts also add brute damage against fauna only.
    /// </summary>
    [Test]
    public async Task ChaserSpeedAndMonsterBoostMatchTg()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var proto = server.ResolveDependency<IPrototypeManager>();
        var mapSys = entMan.System<SharedMapSystem>();
        var testMap = await pair.CreateTestMap();

        EntityUid fauna = default;
        EntityUid plain = default;

        await server.WaitAssertion(() =>
        {
            var chaser = proto.Index<EntityPrototype>("LavalandHierophantChaser");
            Assert.That(chaser.TryGetComponent<AngerTileChaserComponent>(out var chaserComp, entMan.ComponentFactory), Is.True,
                "the hierophant chaser lost its AngerTileChaserComponent");
            Assert.That(chaserComp!.SpeedRange.X, Is.EqualTo(3.33f).Within(0.0001f),
                "the zero anger chaser must step every 0.3 seconds like TG");
            Assert.That(chaserComp.SpeedRange.Y, Is.EqualTo(10f).Within(0.0001f),
                "the max anger chaser must step every 0.1 seconds like TG");
            Assert.That(chaserComp.StepsRange, Is.EqualTo(new Vector2i(15, 40)),
                "the chaser step count must stay untouched");

            AssertMonsterBoost(proto, entMan, "LavalandHierophantSquare", 10);
            AssertMonsterBoost(proto, entMan, "LavalandHierophantBlinkSquare", 10);
            AssertMonsterBoost(proto, entMan, "LavalandHierophantBlinkSlamSquare", 10);
            AssertMonsterBoost(proto, entMan, "LavalandHierophantSquareSafe", 0);
            AssertMonsterBoost(proto, entMan, "LavalandLegionSkullBlast", 0);
            AssertMonsterBoost(proto, entMan, "LavalandDrakeMeteorDamage", 0);
        });

        await server.WaitPost(() =>
        {
            var tile = testMap.Tile.Tile;
            for (var x = -1; x <= 4; x++)
            {
                for (var y = -1; y <= 1; y++)
                    mapSys.SetTile(testMap.Grid, new Vector2i(x, y), tile);
            }

            fauna = entMan.SpawnEntity("MobCow", testMap.GridCoords);
            plain = entMan.SpawnEntity("MobCow", testMap.GridCoords.Offset(new Vector2(3f, 0f)));
            entMan.EnsureComponent<FaunaComponent>(fauna);

            // The empty test map is vacuum, so barotrauma adds its own 0.6 blunt damage
            // (0.15 * LowPressureDamage 4) whenever its one-second tick lands inside the test window.
            // That made the exact damage check depend on the pooled pair's barotrauma timer phase.
            entMan.RemoveComponent<BarotraumaComponent>(fauna);
            entMan.RemoveComponent<BarotraumaComponent>(plain);

            SpawnSquareOn(entMan, fauna);
            SpawnSquareOn(entMan, plain);
        });

        await server.WaitRunTicks(3);

        await server.WaitAssertion(() =>
        {
            Assert.That(entMan.GetComponent<DamageableComponent>(plain).TotalDamage, Is.EqualTo(FixedPoint2.New(24)),
                "a non fauna mob must take the plain 24 blast damage");
            Assert.That(entMan.GetComponent<DamageableComponent>(fauna).TotalDamage, Is.EqualTo(FixedPoint2.New(34)),
                "fauna must take the 24 blast damage plus the TG 10 brute monster boost");
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

    /// <summary>
    /// TG anger counters: the expanding burst caps at burst_range 7 and the cross spam fires up to
    /// cross_counter 7 crosses, starting at 3 and 1. The 5..11 anger sized beams stay untouched.
    /// </summary>
    [Test]
    public async Task BurstAndCrossCountersMatchTg()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var proto = server.ResolveDependency<IPrototypeManager>();
        var mapSys = entMan.System<SharedMapSystem>();
        var testMap = await pair.CreateTestMap();

        EntityUid boss = default;
        EntityUid boxMax = default;
        EntityUid crossMax = default;
        EntityUid boxMin = default;
        EntityUid crossMin = default;

        await server.WaitAssertion(() =>
        {
            AssertCounterRange(proto, entMan, "HierophantShapeBoxExpanding", 3, 7);
            AssertCounterRange(proto, entMan, "HierophantShapeCrossSequence", 1, 7);
            AssertCounterRange(proto, entMan, "HierophantShapeCrossSequenceEnraged", 1, 1);
            AssertBeamSizeRange(proto, entMan, "HierophantShapeCrossSequence", 5, 11);
            AssertBeamSizeRange(proto, entMan, "HierophantShapeCrossSequenceEnraged", 5, 11);
        });

        await server.WaitPost(() =>
        {
            var tile = testMap.Tile.Tile;
            for (var x = -14; x <= 14; x++)
            {
                for (var y = -14; y <= 14; y++)
                    mapSys.SetTile(testMap.Grid, new Vector2i(x, y), tile);
            }

            boss = entMan.SpawnEntity("MobHierophant", testMap.GridCoords.Offset(new Vector2(-7f, 0f)));
            var anger = entMan.GetComponent<AngerComponent>(boss);
            anger.MaxAnger = 1f;
            anger.CurrentAnger = 1f;

            boxMax = SpawnAngerShape(entMan, "HierophantShapeBoxExpanding", testMap.GridCoords, boss);
            crossMax = SpawnAngerShape(entMan, "HierophantShapeCrossSequence", testMap.GridCoords, boss);
        });

        await server.WaitAssertion(() =>
        {
            Assert.That(entMan.GetComponent<ShapeSpawnerCounterComponent>(boxMax).MaxCounter, Is.EqualTo(7),
                "at max anger the box must stop at the TG burst_range 7");
            Assert.That(entMan.GetComponent<ShapeSpawnerCounterComponent>(crossMax).MaxCounter, Is.EqualTo(7),
                "at max anger the cross spam must fire the TG cross_counter 7 crosses");
        });

        await server.WaitPost(() =>
        {
            entMan.GetComponent<AngerComponent>(boss).CurrentAnger = 0f;
            boxMin = SpawnAngerShape(entMan, "HierophantShapeBoxExpanding", testMap.GridCoords.Offset(new Vector2(5f, 0f)), boss);
            crossMin = SpawnAngerShape(entMan, "HierophantShapeCrossSequence", testMap.GridCoords.Offset(new Vector2(-5f, 0f)), boss);
        });

        await server.WaitAssertion(() =>
        {
            Assert.That(entMan.GetComponent<ShapeSpawnerCounterComponent>(boxMin).MaxCounter, Is.EqualTo(3),
                "at zero anger the box must start at the TG burst_range 3");
            Assert.That(entMan.GetComponent<ShapeSpawnerCounterComponent>(crossMin).MaxCounter, Is.EqualTo(1),
                "at zero anger the cross spam must fire a single TG cross_counter cross");
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// inversePeriod must actually invert: the counter period falls as anger rises, so the burst and
    /// the cross series speed up at max anger (0.2 s and 0.7 s) and slow down at zero anger (0.7 s
    /// and 1.2 s). Before the fix EntityShapeSystem read InverseCounter here and the field was dead.
    /// </summary>
    [Test]
    public async Task AngerPeriodsMatchTg()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var proto = server.ResolveDependency<IPrototypeManager>();
        var mapSys = entMan.System<SharedMapSystem>();
        var testMap = await pair.CreateTestMap();

        EntityUid boss = default;
        EntityUid boxMax = default;
        EntityUid crossMax = default;
        EntityUid boxMin = default;
        EntityUid crossMin = default;

        await server.WaitAssertion(() =>
        {
            AssertPeriodInverse(proto, entMan, "HierophantShapeBoxExpanding", 0.2f, 0.7f);
            AssertPeriodInverse(proto, entMan, "HierophantShapeCrossSequence", 0.7f, 1.2f);
        });

        await server.WaitPost(() =>
        {
            var tile = testMap.Tile.Tile;
            for (var x = -14; x <= 14; x++)
            {
                for (var y = -14; y <= 14; y++)
                    mapSys.SetTile(testMap.Grid, new Vector2i(x, y), tile);
            }

            boss = entMan.SpawnEntity("MobHierophant", testMap.GridCoords.Offset(new Vector2(-7f, 0f)));
            var anger = entMan.GetComponent<AngerComponent>(boss);
            anger.MaxAnger = 1f;
            anger.CurrentAnger = 1f;

            boxMax = SpawnAngerShape(entMan, "HierophantShapeBoxExpanding", testMap.GridCoords, boss);
            crossMax = SpawnAngerShape(entMan, "HierophantShapeCrossSequence", testMap.GridCoords, boss);
        });

        await server.WaitAssertion(() =>
        {
            Assert.That(PeriodOf(entMan, boxMax).TotalSeconds, Is.EqualTo(0.2).Within(0.001),
                "at max anger the box rings must come every 0.2 s");
            Assert.That(PeriodOf(entMan, crossMax).TotalSeconds, Is.EqualTo(0.7).Within(0.001),
                "at max anger the crosses must come every 0.7 s");
        });

        await server.WaitPost(() =>
        {
            entMan.GetComponent<AngerComponent>(boss).CurrentAnger = 0f;
            boxMin = SpawnAngerShape(entMan, "HierophantShapeBoxExpanding", testMap.GridCoords.Offset(new Vector2(5f, 0f)), boss);
            crossMin = SpawnAngerShape(entMan, "HierophantShapeCrossSequence", testMap.GridCoords.Offset(new Vector2(-5f, 0f)), boss);
        });

        await server.WaitAssertion(() =>
        {
            Assert.That(PeriodOf(entMan, boxMin).TotalSeconds, Is.EqualTo(0.7).Within(0.001),
                "at zero anger the box rings must come every 0.7 s");
            Assert.That(PeriodOf(entMan, crossMin).TotalSeconds, Is.EqualTo(1.2).Within(0.001),
                "at zero anger the crosses must come every 1.2 s");
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// While a cross series runs the hierophant AI must be paused, like TG sets blinking = TRUE, and
    /// must come back once the series is over.
    /// </summary>
    [Test]
    public async Task CrossSequencePausesAi()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var mapSys = entMan.System<SharedMapSystem>();
        var testMap = await pair.CreateTestMap();

        EntityUid boss = default;
        EntityUid cow = default;
        EntityUid series = default;

        await server.WaitPost(() =>
        {
            var tile = testMap.Tile.Tile;
            for (var x = -12; x <= 12; x++)
            {
                for (var y = -12; y <= 12; y++)
                    mapSys.SetTile(testMap.Grid, new Vector2i(x, y), tile);
            }

            boss = entMan.SpawnEntity("MobHierophant", testMap.GridCoords.Offset(new Vector2(-8f, 0f)));
            var anger = entMan.GetComponent<AngerComponent>(boss);
            anger.MaxAnger = 1f;
            anger.CurrentAnger = 0f;
            // A test has no player to draw aggro, so turn the AI on by hand and keep one aggressor:
            // the resume path only wakes the AI back up while the boss still has a target.
            cow = entMan.SpawnEntity("MobCow", testMap.GridCoords.Offset(new Vector2(0f, 6f)));
            entMan.GetComponent<AggressiveComponent>(boss).Aggressors.Add(cow);
            entMan.GetComponent<MegafaunaAiComponent>(boss).Active = true;

            series = SpawnAngerShape(entMan, "HierophantShapeCrossSequence", testMap.GridCoords, boss);
        });

        await server.WaitAssertion(() =>
        {
            Assert.That(entMan.GetComponent<MegafaunaAiComponent>(boss).Active, Is.False,
                "the cross series must pause the hierophant AI like TG blinking = TRUE");
        });

        // At zero anger the series is one cross with a 1.2 s period, then the spawner retires.
        await server.WaitRunTicks(90);

        await server.WaitAssertion(() =>
        {
            Assert.That(entMan.Deleted(series), Is.True, "the single cross series must have retired");
            Assert.That(entMan.GetComponent<MegafaunaAiComponent>(boss).Active, Is.True,
                "the hierophant AI must come back after the cross series ends");
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// The enraged all-directions blast is a single wave: TG fires one blasts(target, alldirs) call
    /// per attack and cross_counter does not apply to it.
    /// </summary>
    [Test]
    public async Task EnragedCrossSpawnsSingleWave()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var proto = server.ResolveDependency<IPrototypeManager>();
        var mapSys = entMan.System<SharedMapSystem>();
        var testMap = await pair.CreateTestMap();

        EntityUid boss = default;
        EntityUid series = default;

        await server.WaitAssertion(() => AssertCounterRange(proto, entMan, "HierophantShapeCrossSequenceEnraged", 1, 1));

        await server.WaitPost(() =>
        {
            var tile = testMap.Tile.Tile;
            for (var x = -14; x <= 14; x++)
            {
                for (var y = -14; y <= 14; y++)
                    mapSys.SetTile(testMap.Grid, new Vector2i(x, y), tile);
            }

            boss = entMan.SpawnEntity("MobHierophant", testMap.GridCoords.Offset(new Vector2(-13f, 0f)));
            var anger = entMan.GetComponent<AngerComponent>(boss);
            anger.MaxAnger = 1f;
            anger.CurrentAnger = 1f;

            series = SpawnAngerShape(entMan, "HierophantShapeCrossSequenceEnraged", testMap.GridCoords, boss);
        });

        await server.WaitAssertion(() =>
        {
            Assert.That(entMan.GetComponent<ShapeSpawnerCounterComponent>(series).MaxCounter, Is.EqualTo(1),
                "the enraged all-directions blast must be a single wave");
            var squares = CountSquaresNear(entMan, Vector2.Zero, 15f);
            Assert.That(squares, Is.EqualTo(81),
                "one enraged wave at max anger must be a single 81 tile all-directions cross, got " + squares);
        });

        // Past the 0.7 s max anger period the single wave spawner must be gone.
        await server.WaitRunTicks(50);

        await server.WaitAssertion(() =>
        {
            Assert.That(entMan.Deleted(series), Is.True, "the enraged wave spawner must retire after one wave");
        });

        await pair.CleanReturnAsync();
    }

    private static EntityUid SpawnAngerShape(IEntityManager entMan, string prototype, EntityCoordinates coords, EntityUid user)
    {
        // The action raises SpawnedByActionEvent before MapInit, that is what carries the anger.
        var spawned = entMan.CreateEntityUninitialized(prototype, coords);
        var ev = new SpawnedByActionEvent(user, null);
        entMan.EventBus.RaiseLocalEvent(spawned, ref ev);
        entMan.InitializeEntity(spawned);
        entMan.StartEntity(spawned);
        entMan.RunMapInit(spawned, entMan.GetComponent<MetaDataComponent>(spawned));
        return spawned;
    }

    private static void DeleteSquares(IEntityManager entMan)
    {
        var toDelete = new List<EntityUid>();
        var query = entMan.EntityQueryEnumerator<MetaDataComponent>();
        while (query.MoveNext(out var uid, out var meta))
        {
            if (meta.EntityPrototype?.ID == "LavalandHierophantSquare")
                toDelete.Add(uid);
        }

        foreach (var uid in toDelete)
            entMan.DeleteEntity(uid);
    }

    private static void AssertMonsterBoost(IPrototypeManager proto, IEntityManager entMan, string id, int blunt)
    {
        var squareProto = proto.Index<EntityPrototype>(id);
        Assert.That(squareProto.TryGetComponent<DamageSquareComponent>(out var square, entMan.ComponentFactory), Is.True,
            id + " lost its DamageSquareComponent");
        var bonus = square!.MonsterBonusDamage != null
            && square.MonsterBonusDamage.DamageDict.TryGetValue("Blunt", out var value)
                ? value
                : FixedPoint2.Zero;
        Assert.That(bonus, Is.EqualTo(FixedPoint2.New(blunt)), id + " monster bonus Blunt damage must be " + blunt);
    }

    private static void SpawnSquareOn(IEntityManager entMan, EntityUid target)
    {
        var coords = entMan.GetComponent<TransformComponent>(target).Coordinates;
        var square = entMan.SpawnEntity("LavalandHierophantSquare", coords);
        // Skip the 0.7 second telegraph, the test does not wait for it.
        entMan.GetComponent<DamageSquareComponent>(square).DamageTime = TimeSpan.Zero;
    }

    private static void AssertCounterRange(IPrototypeManager proto, IEntityManager entMan, string id, int min, int max)
    {
        var spawnerProto = proto.Index<EntityPrototype>(id);
        Assert.That(spawnerProto.TryGetComponent<AngerShapeSpawnerComponent>(out var angerSpawner, entMan.ComponentFactory), Is.True,
            id + " lost its AngerShapeSpawnerComponent");
        Assert.That(angerSpawner!.MaxCounterRange, Is.Not.Null, id + " has no counterRange");
        Assert.That(angerSpawner.MaxCounterRange!.Value.X, Is.EqualTo(min), id + " counterRange min");
        Assert.That(angerSpawner.MaxCounterRange!.Value.Y, Is.EqualTo(max), id + " counterRange max");
    }

    private static TimeSpan PeriodOf(IEntityManager entMan, EntityUid uid)
        => entMan.GetComponent<ShapeSpawnerCounterComponent>(uid).SpawnPeriod;

    private static void AssertPeriodInverse(IPrototypeManager proto, IEntityManager entMan, string id, float min, float max)
    {
        var spawnerProto = proto.Index<EntityPrototype>(id);
        Assert.That(spawnerProto.TryGetComponent<AngerShapeSpawnerComponent>(out var angerSpawner, entMan.ComponentFactory), Is.True,
            id + " lost its AngerShapeSpawnerComponent");
        Assert.That(angerSpawner!.InverseSpawnPeriod, Is.True, id + " must invert its spawn period with anger");
        Assert.That(angerSpawner.SpawnPeriodRange, Is.Not.Null, id + " has no periodRange");
        Assert.That(angerSpawner.SpawnPeriodRange!.Value.X, Is.EqualTo(min).Within(0.0001f), id + " period min");
        Assert.That(angerSpawner.SpawnPeriodRange!.Value.Y, Is.EqualTo(max).Within(0.0001f), id + " period max");
    }

    private static void AssertBeamSizeRange(IPrototypeManager proto, IEntityManager entMan, string id, int min, int max)
    {
        var spawnerProto = proto.Index<EntityPrototype>(id);
        Assert.That(spawnerProto.TryGetComponent<ShapeSpawnerComponent>(out var spawner, entMan.ComponentFactory), Is.True,
            id + " lost its ShapeSpawnerComponent");
        Assert.That(spawner!.AngerSizeRange, Is.Not.Null, id + " has no angerSizeRange");
        Assert.That(spawner.AngerSizeRange!.Value.X, Is.EqualTo(min), id + " beam size min");
        Assert.That(spawner.AngerSizeRange!.Value.Y, Is.EqualTo(max), id + " beam size max");
    }
}