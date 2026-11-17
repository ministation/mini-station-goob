// SPDX-License-Identifier: AGPL-3.0-or-later

#nullable enable annotations

using System.Collections.Generic;
using System.Numerics;
using Content.Goobstation.Maths.FixedPoint;
using Content.Server._Lavaland.Ruins;
using Content.Shared._Lavaland.Aggression;
using Content.Server._Lavaland.BossMechanics;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.Sprite;
using Content.Shared.Weapons.Melee.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Lavaland.Megafauna;

/// <summary>
/// TG parity checks for the demonic frost miner: the arena prisms follow the boss
/// (violet on enrage, gray on death) and its melee knocks targets back.
/// </summary>
[TestFixture]
public sealed class FrostMinerParityTest
{
    private static IReadOnlyList<EntityUid> SpawnPrisms(IEntityManager entMan, EntityCoordinates center, int count)
    {
        var prisms = new List<EntityUid>();
        for (var i = 0; i < count; i++)
            prisms.Add(entMan.SpawnEntity("LavalandFrostMinerPrism", center.Offset(new Vector2(i * 2f - count, 0f))));

        return prisms;
    }

    private static Color? PrismTint(IEntityManager entMan, EntityUid prism)
    {
        var sprite = entMan.GetComponent<RandomSpriteComponent>(prism);
        return sprite.Selected.TryGetValue("enum.DamageStateVisualLayers.Base", out var entry)
            ? entry.Color
            : null;
    }

    /// <summary>
    /// Enraging the miner turns the prisms violet, killing it dims them to gray (TG reaction).
    /// </summary>
    [Test]
    public async Task PrismsFollowTheBoss()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var damage = server.System<DamageableSystem>();
        var proto = server.ResolveDependency<IPrototypeManager>();
        var mapSys = entMan.System<SharedMapSystem>();

        var testMap = await pair.CreateTestMap();
        EntityUid boss = default;
        IReadOnlyList<EntityUid> prisms = new List<EntityUid>();

        await server.WaitPost(() =>
        {
            var tile = testMap.Tile.Tile;
            for (var x = -14; x <= 14; x++)
            {
                for (var y = -14; y <= 14; y++)
                    mapSys.SetTile(testMap.Grid, new Vector2i(x, y), tile);
            }

            boss = entMan.SpawnEntity("LavalandBossDemonicFrostMiner", testMap.GridCoords);
            prisms = SpawnPrisms(entMan, testMap.GridCoords, 2);

            var aggressive = entMan.GetComponent<AggressiveComponent>(boss);
            entMan.System<AggressorsSystem>().AddAggressor(
                (boss, aggressive), entMan.SpawnEntity("MobCow", testMap.GridCoords.Offset(new Vector2(3f, 0f))));

            // 1410 of 1875 is past the 25% enrage threshold, same value FrostMinerBossTest uses.
            var enrage = new DamageSpecifier(proto.Index<DamageTypePrototype>("Blunt"), 1410);
            damage.TryChangeDamage(boss, enrage, ignoreResistances: true);
        });

        await server.WaitRunTicks(3);

        await server.WaitAssertion(() =>
        {
            Assert.That(entMan.GetComponent<FrostMinerEnrageComponent>(boss).Enraged, Is.True,
                "the frost miner never enrages in the test");
            Assert.That(entMan.HasComponent<RandomSpriteComponent>(boss), Is.True,
                "the enrage did not run (no sprite swap on the boss)");

            foreach (var prism in prisms)
            {
                var tint = PrismTint(entMan, prism);
                Assert.That(tint, Is.Not.Null,
                    "the prism received no sprite tint on the boss enrage");
                Assert.That(tint!.Value.R, Is.GreaterThan(0.5f),
                    "the prism did not turn violet (red channel up) on the boss enrage");
                Assert.That(tint.Value.G, Is.LessThan(0.8f),
                    "the prism did not turn violet (green channel down) on the boss enrage");
            }
        });

        await server.WaitPost(() =>
        {
            var lethal = new DamageSpecifier(proto.Index<DamageTypePrototype>("Blunt"), 5000);
            damage.TryChangeDamage(boss, lethal, ignoreResistances: true);
        });

        await server.WaitRunTicks(3);

        await server.WaitAssertion(() =>
        {
            foreach (var prism in prisms)
            {
                var dead = entMan.GetComponent<FrostMinerPrismComponent>(prism).DeadColor;
                var tint = PrismTint(entMan, prism);
                Assert.That(tint, Is.Not.Null,
                    "the prism received no sprite tint on the boss death");
                Assert.That(MathF.Abs(tint!.Value.R - dead.R), Is.LessThan(0.05f),
                    "the prism did not dim to gray on the boss death");
                Assert.That(MathF.Abs(tint!.Value.G - dead.G), Is.LessThan(0.05f),
                    "the prism did not dim to gray on the boss death");
                Assert.That(MathF.Abs(tint!.Value.B - dead.B), Is.LessThan(0.05f),
                    "the prism did not dim to gray on the boss death");
            }
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// TG parity: the miner's melee throws its victim back (knockback 7).
    /// </summary>
    [Test]
    public async Task MinerMeleeThrowsTargetsBack()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var proto = server.ResolveDependency<IPrototypeManager>();

        await server.WaitAssertion(() =>
        {
            var boss = proto.Index<EntityPrototype>("LavalandBossDemonicFrostMiner");
            Assert.That(boss.TryGetComponent<MeleeThrowOnHitComponent>(out var throwHit, entMan.ComponentFactory), Is.True,
                "the frost miner lost its melee knockback");
            Assert.That(throwHit!.Distance, Is.EqualTo(7f),
                "the frost miner melee knockback must be 7 tiles like the TG knockback element");
        });

        await pair.CleanReturnAsync();
    }

    private static string DumpDamage(DamageableComponent damageable)
    {
        var text = "";
        foreach (var (type, amount) in damageable.Damage.DamageDict)
            text += type + "=" + amount.Float() + " ";
        return text.Length == 0 ? "none" : text;
    }

    /// <summary>
    /// TG parity: a turf or object impact is an EX_ACT(EXPLODE_HEAVY), which in TG only hits the
    /// struck atom. The YAML blast is a point explosion centered on that atom: the struck tile takes
    /// it, neighbors stay clean.
    /// </summary>
    [Test]
    public async Task OrbExplodesOnWallImpact()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var mapSys = entMan.System<SharedMapSystem>();
        var physics = server.System<SharedPhysicsSystem>();

        var testMap = await pair.CreateTestMap();
        EntityUid orb = default;
        EntityUid wall = default;
        EntityUid neighbor = default;

        await server.WaitPost(() =>
        {
            var tile = testMap.Tile.Tile;
            for (var x = -14; x <= 14; x++)
            {
                for (var y = -14; y <= 14; y++)
                    mapSys.SetTile(testMap.Grid, new Vector2i(x, y), tile);
            }

            // The orb flies along the wall row and bursts on the struck atom, so the blast lands on
            // the wall's tile. The dummy stands on the neighboring tile.
            var center = testMap.GridCoords.Offset(new Vector2(0.5f, 0.5f));
            wall = entMan.SpawnEntity("WallReinforced", center.Offset(new Vector2(5f, 0f)));
            entMan.System<SharedTransformSystem>().AnchorEntity(wall);
            neighbor = entMan.SpawnEntity("MobCow", center.Offset(new Vector2(4f, 1f)));

            orb = entMan.SpawnEntity("LavalandFrostOrb", center);
            physics.SetLinearVelocity(orb, new Vector2(10f, 0f));
        });

        await server.WaitRunTicks(60);

        await server.WaitAssertion(() =>
        {
            Assert.That(entMan.EntityExists(orb), Is.False,
                "the frost orb flew through the wall instead of stopping on it");

            Assert.That(entMan.TryGetComponent<DamageableComponent>(wall, out var wallDamage), Is.True,
                "the wall did not survive the impact, the blast is far stronger than EXPLODE_HEAVY");
            Assert.That(entMan.TryGetComponent<DamageableComponent>(neighbor, out var neighborDamage), Is.True,
                "the dummy lost its DamageableComponent");

            TestContext.Out.WriteLine("struck tile: wall " + DumpDamage(wallDamage!)
                + "| neighbor tile: dummy " + DumpDamage(neighborDamage!));

            // The projectile only deals Heat, so Structural on the wall comes from the Cryo blast alone.
            // ExplosionSystem damages anchored entities on the epicenter tile twice (the broadphase
            // tile pass plus the anchored entity list), so the struck wall sees two EXPLODE_HEAVY
            // passes: 2 x Structural (20 * 5) = 200. One pass is the TG heavy value.
            Assert.That(wallDamage!.Damage.DamageDict.TryGetValue("Structural", out var structural), Is.True,
                "the wall took no Structural damage, the point blast never landed on the struck tile");
            Assert.That(structural.Float(), Is.EqualTo(200f),
                "the struck wall did not take two EXPLODE_HEAVY structural passes");
            Assert.That(wallDamage.TotalDamage.Float(), Is.LessThan(300f),
                "the impact blast is far past EXPLODE_HEAVY scale, the intensity is too high");

            // The Cryo blast is the only cold source in this test and it also deals heavy blunt,
            // so cold or heavy damage on the neighbor tile would mean the blast spilled over.
            Assert.That(neighborDamage!.Damage.DamageDict.TryGetValue("Cold", out var neighborCold), Is.True,
                "the dummy has no Cold entry, its damage container changed");
            Assert.That(neighborCold.Float(), Is.EqualTo(0f),
                "the point blast reached the neighboring tile, it is still an AoE");
            Assert.That(neighborDamage.TotalDamage.Float(), Is.LessThan(5f),
                "the neighboring dummy took heavy damage, the blast spilled out of the impact tile");
        });

        await pair.CleanReturnAsync();
    }
}
