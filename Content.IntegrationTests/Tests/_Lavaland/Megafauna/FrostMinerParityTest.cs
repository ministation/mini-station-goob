// SPDX-License-Identifier: AGPL-3.0-or-later

#nullable enable annotations

using System.Collections.Generic;
using System.Numerics;
using Content.Goobstation.Maths.FixedPoint;
using Content.Server._Lavaland.Ruins;
using Content.Shared._Lavaland.Aggression;
using Content.Server._Lavaland.BossMechanics;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.Sprite;
using Content.Shared.Weapons.Melee.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
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

            // 1320 of 1650 is past the 25% enrage threshold, same value FrostMinerBossTest uses.
            var enrage = new DamageSpecifier(proto.Index<DamageTypePrototype>("Blunt"), 1320);
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
}
