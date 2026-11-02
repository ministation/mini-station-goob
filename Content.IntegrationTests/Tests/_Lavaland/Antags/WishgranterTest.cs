// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using Content.Server._Lavaland.Wishgranter;
using Content.Shared._Lavaland.Wishgranter;
using Content.Shared._Trauma.Genetics.Mutations;
using Content.Shared.Interaction;
using Content.Shared.Mind;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests._Lavaland.Antags;

[TestFixture]
[TestOf(typeof(WishgranterSystem))]
public sealed class WishgranterTest
{
    [Test]
    public async Task SecondTouchGrantsMutationsAndHijackObjective()
    {
        await using var pair = await PoolManager.GetServerClient();

        var server = pair.Server;
        var testMap = await pair.CreateTestMap();
        var entMan = server.ResolveDependency<IEntityManager>();
        var mindSystem = entMan.EntitySysManager.GetEntitySystem<SharedMindSystem>();

        EntityUid mob = default;
        EntityUid granter = default;
        EntityUid mind = default;

        await server.WaitPost(() =>
        {
            mob = entMan.SpawnEntity("MobHuman", testMap.GridCoords);
            mind = mindSystem.CreateMind(null).Owner;
            mindSystem.TransferTo(mind, mob);
            granter = entMan.SpawnEntity("LavalandWishgranter", testMap.GridCoords);
        });

        await server.WaitRunTicks(2);

        await server.WaitAssertion(() =>
        {
            var granterComp = entMan.GetComponent<WishgranterComponent>(granter);

            // The first touch only wakes the cube up.
            entMan.EventBus.RaiseLocalEvent(granter, new InteractHandEvent(mob, granter));
            Assert.That(granterComp.Insisting, Is.True, "first touch must make the granter listen");
            Assert.That(entMan.HasComponent<WishgranterAvatarComponent>(mob), Is.False,
                "first touch must not grant the wish");

            // The second touch grants the wish.
            entMan.EventBus.RaiseLocalEvent(granter, new InteractHandEvent(mob, granter));

            Assert.That(entMan.HasComponent<WishgranterAvatarComponent>(mob), Is.True,
                "the wisher must become the wish granter avatar");
            Assert.That(entMan.TryGetComponent<MutatableComponent>(mob, out var mutatable), Is.True,
                "humanoids must be mutable for the wish powers");
            Assert.That(mutatable!.Mutations.Keys.Select(mutation => mutation.Id), Does.Contain("MutationHulk"),
                "the wish must grant the hulk mutation");
            Assert.That(mutatable.Mutations, Is.Not.Empty, "the wish must grant mutations");

            var mindComp = entMan.GetComponent<MindComponent>(mind);
            Assert.That(mindComp.Objectives, Is.Not.Empty, "the wish must hand out a hijack objective");
        });

        await pair.CleanReturnAsync();
    }
}
