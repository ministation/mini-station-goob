// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using Content.Goobstation.Common.Flammability;
using Content.Goobstation.Common.Temperature.Components;
using Content.Goobstation.Maths.FixedPoint;
using Content.IntegrationTests.Pair;
using Content.Server.Atmos.Components;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Heretic.EntitySystems;
using Content.Shared.Atmos.Components;
using Content.Shared.Damage.Components;
using Content.Shared.Heretic;
using Content.Shared.Mind;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Utility;

namespace Content.IntegrationTests.Tests._Goobstation.Heretic;

/// <summary>
/// The ascended heretic of the Ash path must be immune to fire damage while the flames on them stay as a
/// visual effect. The immunity is granted by the AshlordRite knowledge through its added components.
/// </summary>
[TestFixture]
public sealed class AshAscensionFireImmunityTest
{
    private static readonly ResPath BreathableGrid = new("Maps/Test/Breathing/3by3-20oxy-80nit.yml");

    private static async Task<(EntityUid Mob, EntityUid Mind)> SpawnMob(TestPair pair)
    {
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var mapLoader = entMan.System<MapLoaderSystem>();
        var mapSys = entMan.System<SharedMapSystem>();

        EntityUid grid = default;
        await server.WaitPost(() =>
        {
            mapSys.CreateMap(out var mapId);
            Assert.That(mapLoader.TryLoadGrid(mapId, BreathableGrid, out var gridEnt), Is.True,
                "breathing test grid must load");
            grid = gridEnt!.Value.Owner;
        });

        await server.WaitIdleAsync();

        EntityUid mob = default;
        EntityUid mindId = default;
        await server.WaitAssertion(() =>
        {
            mob = entMan.SpawnEntity("MobHuman", new EntityCoordinates(grid, new Vector2(0.5f, 0.5f)));

            var mindSys = server.System<SharedMindSystem>();
            var mind = mindSys.CreateMind(null);
            mindId = mind.Owner;
            mindSys.TransferTo(mind, mob, mind: mind.Comp);
        });

        await server.WaitIdleAsync();
        return (mob, mindId);
    }

    private static void Ignite(TestPair pair, EntityUid mob)
    {
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var flammable = entMan.GetComponent<FlammableComponent>(mob);
        server.System<FlammableSystem>().AdjustFireStacks(mob, 5f, flammable, true);
        Assert.That(flammable.OnFire, Is.True, "test setup must set the mob on fire");
    }

    [Test]
    public async Task AscensionRitualKnowledgeGivesFireImmunity()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();

        var (mob, mindId) = await SpawnMob(pair);

        await server.WaitAssertion(() =>
        {
            var heretic = entMan.EnsureComponent<HereticComponent>(mindId);
            var hereticSystem = server.System<HereticSystem>();

            Assert.That(hereticSystem.TryAddKnowledge((mindId, heretic), "AscensionAsh", mob), Is.True);

            Assert.Multiple(() =>
            {
                Assert.That(entMan.HasComponent<FireImmunityComponent>(mob), Is.True,
                    "buying the Ash ascension ritual must grant fire immunity");
                Assert.That(entMan.HasComponent<SpecialHighTempImmunityComponent>(mob), Is.True,
                    "buying the Ash ascension ritual must grant high temperature immunity");
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task AscendedAshHereticTakesNoFireDamage()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();

        var (mob, mindId) = await SpawnMob(pair);

        await server.WaitAssertion(() =>
        {
            var heretic = entMan.EnsureComponent<HereticComponent>(mindId);
            var hereticSystem = server.System<HereticSystem>();

            Assert.That(hereticSystem.TryAddKnowledge((mindId, heretic), "AshlordRite", mob), Is.True,
                "AshlordRite knowledge must be added to the ascended heretic");

            Assert.Multiple(() =>
            {
                Assert.That(entMan.HasComponent<FireImmunityComponent>(mob), Is.True,
                    "AshlordRite must grant FireImmunity");
                Assert.That(entMan.HasComponent<SpecialHighTempImmunityComponent>(mob), Is.True,
                    "AshlordRite must grant high temperature immunity");
            });

            Ignite(pair, mob);
        });

        // Several FlammableSystem updates happen during this: they are one second apart.
        await server.WaitRunTicks(240);

        await server.WaitAssertion(() =>
        {
            var damageable = entMan.GetComponent<DamageableComponent>(mob);
            Assert.That(damageable.TotalDamage, Is.EqualTo(FixedPoint2.Zero),
                "an ascended Ash heretic must not take damage from their own fire");

            // The fire has to stay on them as a visual effect, it must not be put out by the immunity.
            Assert.That(entMan.GetComponent<FlammableComponent>(mob).OnFire, Is.True,
                "fire visual must be kept on the ascended heretic");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task NonAscendedHerecticTakesFireDamage()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();

        var (mob, mindId) = await SpawnMob(pair);

        await server.WaitAssertion(() =>
        {
            // The heretic exists but has not ascended yet, so no fire immunity is granted to them.
            entMan.EnsureComponent<HereticComponent>(mindId);
            Assert.That(entMan.HasComponent<FireImmunityComponent>(mob), Is.False);

            Ignite(pair, mob);
        });

        await server.WaitRunTicks(240);

        await server.WaitAssertion(() =>
        {
            var damageable = entMan.GetComponent<DamageableComponent>(mob);
            Assert.That(damageable.TotalDamage, Is.GreaterThan(FixedPoint2.Zero),
                "a heretic that has not ascended must still take fire damage");
        });

        await pair.CleanReturnAsync();
    }
}
