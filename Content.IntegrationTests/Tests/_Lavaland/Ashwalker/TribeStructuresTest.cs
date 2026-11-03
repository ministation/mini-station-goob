// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server._Lavaland.Ashwalker;
using Content.Shared._Lavaland.Ashwalker;
using Content.Shared.Atmos.Components;
using Content.Shared.Construction.Prototypes;
using Content.Shared.IgnitionSource;
using Content.Shared.Interaction;
using Content.Shared.Kitchen.Components;
using Content.Shared.Temperature.Components;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Lavaland.Ashwalker;

/// <summary>
/// Guards the tribal structures of the ash walker den: the tribal bonfire and the head pike.
/// </summary>
[TestFixture]
[TestOf(typeof(TribeBonfireSystem))]
[TestOf(typeof(HeadPikeSystem))]
public sealed class TribeStructuresTest
{
    [Test]
    public async Task PrototypesExist()
    {
        await using var pair = await PoolManager.GetServerClient();
        var protoMan = pair.Server.ResolveDependency<IPrototypeManager>();

        Assert.Multiple(() =>
        {
            Assert.That(protoMan.HasIndex<EntityPrototype>("TribeBonfire"), Is.True);
            Assert.That(protoMan.HasIndex<EntityPrototype>("TribeBonfirePrelit"), Is.True);
            Assert.That(protoMan.HasIndex<EntityPrototype>("TribeBonfireStake"), Is.True);
            Assert.That(protoMan.HasIndex<EntityPrototype>("TribeHeadPike"), Is.True);
            Assert.That(protoMan.HasIndex<ConstructionPrototype>("TribeBonfire"), Is.True);
            Assert.That(protoMan.HasIndex<ConstructionPrototype>("TribeBonfireStake"), Is.True);
            Assert.That(protoMan.HasIndex<ConstructionPrototype>("TribeHeadPike"), Is.True);
            Assert.That(protoMan.HasIndex<EntityPrototype>("KitchenSpike"), Is.True);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task UnlitBonfireIsCold()
    {
        await using var pair = await PoolManager.GetServerClient();

        var server = pair.Server;
        var testMap = await pair.CreateTestMap();
        var entMan = server.ResolveDependency<IEntityManager>();
        var appearance = entMan.System<SharedAppearanceSystem>();

        EntityUid bonfire = default;
        await server.WaitPost(() => bonfire = entMan.SpawnEntity("TribeBonfire", testMap.GridCoords));
        await server.WaitRunTicks(2);

        await server.WaitAssertion(() =>
        {
            Assert.That(entMan.GetComponent<TribeBonfireComponent>(bonfire).Lit, Is.False);
            Assert.That(entMan.HasComponent<AlwaysHotComponent>(bonfire), Is.False);
            Assert.That(appearance.TryGetData(bonfire, TribeBonfireVisuals.Lit, out bool lit), Is.True);
            Assert.That(lit, Is.False);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task HotItemLightsTheBonfire()
    {
        await using var pair = await PoolManager.GetServerClient();

        var server = pair.Server;
        var testMap = await pair.CreateTestMap();
        var entMan = server.ResolveDependency<IEntityManager>();

        EntityUid bonfire = default;
        EntityUid user = default;
        EntityUid hotItem = default;
        EntityUid coldItem = default;
        await server.WaitPost(() =>
        {
            bonfire = entMan.SpawnEntity("TribeBonfire", testMap.GridCoords);
            user = entMan.SpawnEntity("MobHuman", testMap.GridCoords);
            hotItem = entMan.SpawnEntity("AnomalyCorePyroclastic", testMap.GridCoords);
            coldItem = entMan.SpawnEntity("Crowbar", testMap.GridCoords);
        });
        await server.WaitRunTicks(2);

        await server.WaitPost(() =>
        {
            entMan.EventBus.RaiseLocalEvent(bonfire,
                new InteractUsingEvent(user, coldItem, bonfire, testMap.GridCoords));
        });
        await server.WaitRunTicks(2);

        await server.WaitAssertion(() =>
        {
            Assert.That(entMan.GetComponent<TribeBonfireComponent>(bonfire).Lit, Is.False,
                "a cold item must not light the bonfire");
        });

        await server.WaitPost(() =>
        {
            entMan.EventBus.RaiseLocalEvent(bonfire,
                new InteractUsingEvent(user, hotItem, bonfire, testMap.GridCoords));
        });
        await server.WaitRunTicks(2);

        await server.WaitAssertion(() =>
        {
            Assert.That(entMan.GetComponent<TribeBonfireComponent>(bonfire).Lit, Is.True,
                "a hot item must light the bonfire");
            Assert.That(entMan.GetComponent<IgnitionSourceComponent>(bonfire).Ignited, Is.True);
            Assert.That(entMan.HasComponent<AlwaysHotComponent>(bonfire), Is.True);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task PrelitBonfireLightsAndBurns()
    {
        await using var pair = await PoolManager.GetServerClient();

        var server = pair.Server;
        var testMap = await pair.CreateTestMap();
        var entMan = server.ResolveDependency<IEntityManager>();
        var appearance = entMan.System<SharedAppearanceSystem>();

        EntityUid bonfire = default;
        EntityUid mob = default;
        await server.WaitPost(() =>
        {
            bonfire = entMan.SpawnEntity("TribeBonfirePrelit", testMap.GridCoords);
            mob = entMan.SpawnEntity("MobHuman", testMap.GridCoords);

            // The test map is a vacuum and the engine puts out fires in an oxygenless
            // environment, so the extinguish path is disabled to test the ignite path itself.
            entMan.GetComponent<FlammableComponent>(mob).CanExtinguish = false;
        });
        await server.WaitRunTicks(130);

        await server.WaitAssertion(() =>
        {
            Assert.That(entMan.GetComponent<TribeBonfireComponent>(bonfire).Lit, Is.True);
            Assert.That(entMan.HasComponent<AlwaysHotComponent>(bonfire), Is.True);
            Assert.That(appearance.TryGetData(bonfire, TribeBonfireVisuals.Lit, out bool lit), Is.True);
            Assert.That(lit, Is.True);

            Assert.That(entMan.TryGetComponent<FlammableComponent>(mob, out var flammable), Is.True);
            Assert.That(flammable!.FireStacks, Is.GreaterThan(0f), "the bonfire must ignite everything on its tile");
            Assert.That(flammable.OnFire, Is.True);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task HeadPikeTakesAndReturnsAHead()
    {
        await using var pair = await PoolManager.GetServerClient();

        var server = pair.Server;
        var testMap = await pair.CreateTestMap();
        var entMan = server.ResolveDependency<IEntityManager>();
        var appearance = entMan.System<SharedAppearanceSystem>();

        EntityUid pike = default;
        EntityUid user = default;
        EntityUid head = default;
        await server.WaitPost(() =>
        {
            pike = entMan.SpawnEntity("TribeHeadPike", testMap.GridCoords);
            user = entMan.SpawnEntity("MobHuman", testMap.GridCoords);
            head = entMan.SpawnEntity("HeadHuman", testMap.GridCoords);
        });
        await server.WaitRunTicks(2);

        await server.WaitPost(() =>
        {
            var ev = new InteractUsingEvent(user, head, pike, testMap.GridCoords);
            entMan.EventBus.RaiseLocalEvent(pike, ev);
        });
        await server.WaitRunTicks(2);

        await server.WaitAssertion(() =>
        {
            Assert.That(entMan.GetComponent<HeadPikeComponent>(pike).Head, Is.EqualTo(head));
            Assert.That(appearance.TryGetData(pike, HeadPikeVisuals.HasHead, out bool hasHead), Is.True);
            Assert.That(hasHead, Is.True);
            Assert.That(entMan.GetComponent<MetaDataComponent>(pike).EntityName,
                Does.Contain(entMan.GetComponent<MetaDataComponent>(head).EntityName));
        });

        await server.WaitPost(() =>
        {
            var ev = new InteractHandEvent(user, pike);
            entMan.EventBus.RaiseLocalEvent(pike, ev);
        });
        await server.WaitRunTicks(2);

        await server.WaitAssertion(() =>
        {
            Assert.That(entMan.GetComponent<HeadPikeComponent>(pike).Head, Is.Null);
            Assert.That(appearance.TryGetData(pike, HeadPikeVisuals.HasHead, out bool hasHead), Is.True);
            Assert.That(hasHead, Is.False);
            Assert.That(entMan.EntityExists(head), Is.True, "the removed head must not be deleted");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task KitchenSpikeHooksACorpse()
    {
        await using var pair = await PoolManager.GetServerClient();

        var server = pair.Server;
        var testMap = await pair.CreateTestMap();
        var entMan = server.ResolveDependency<IEntityManager>();

        EntityUid spike = default;
        EntityUid corpse = default;
        await server.WaitPost(() =>
        {
            spike = entMan.SpawnEntity("KitchenSpike", testMap.GridCoords);
            corpse = entMan.SpawnEntity("MobHuman", testMap.GridCoords);
        });
        await server.WaitRunTicks(2);

        await server.WaitPost(() =>
        {
            var spikeComp = entMan.GetComponent<KitchenSpikeComponent>(spike);
            entMan.System<SharedContainerSystem>().Insert(corpse, spikeComp.BodyContainer);
        });
        await server.WaitRunTicks(2);

        await server.WaitAssertion(() =>
        {
            var spikeComp = entMan.GetComponent<KitchenSpikeComponent>(spike);
            Assert.That(spikeComp.BodyContainer.ContainedEntity, Is.EqualTo(corpse),
                "the meat spike must accept a corpse");
        });

        await pair.CleanReturnAsync();
    }
}
