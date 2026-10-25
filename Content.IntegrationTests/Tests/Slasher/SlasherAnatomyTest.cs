// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Goobstation.Shared.Slasher.Components;
using Content.Server.Body.Systems;
using Content.Shared.Body.Components;
using Content.Shared.Body.Part;
using Content.Shared._Shitmed.Body.Organ;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests.Slasher;

[TestFixture]
public sealed class SlasherAnatomyTest
{
    /// <summary>
    /// Slimes keep their brain (the sentient slime core) in the groin, not in the head. The slasher brain move
    /// must leave this anatomy alone, otherwise the core is pulled out of the body, the mob is debrained and the
    /// mind ends up inside the organ lying on the floor.
    /// </summary>
    [Test]
    public async Task SlimeSlasherKeepsItsBrainInsideBody()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        EntityUid slime = default;
        await server.WaitAssertion(() => slime = server.EntMan.Spawn("MobSlimePerson", map.MapCoords));
        await server.WaitAssertion(() => server.EntMan.EnsureComponent<SlasherComponent>(slime));
        await server.WaitRunTicks(1);

        await server.WaitAssertion(() =>
        {
            Assert.That(server.EntMan.HasComponent<DebrainedComponent>(slime), Is.False,
                "slime slasher must not be debrained");

            var body = server.EntMan.GetComponent<BodyComponent>(slime);
            var bodySystem = server.System<BodySystem>();

            Assert.That(bodySystem.TryGetBodyOrganEntityComps<BrainComponent>((slime, body), out var brains), Is.True,
                "slime must keep its slime core inside the body");
            Assert.That(brains![0].Comp2.Body, Is.EqualTo(slime), "slime core must stay attached to the body");
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// Standard humanoids keep the brain in the head, so the slasher brain still has to move into the chest.
    /// </summary>
    [Test]
    public async Task HumanSlasherMovesBrainToChest()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        EntityUid human = default;
        await server.WaitAssertion(() => human = server.EntMan.Spawn("MobHuman", map.MapCoords));
        await server.WaitAssertion(() => server.EntMan.EnsureComponent<SlasherComponent>(human));
        await server.WaitRunTicks(1);

        await server.WaitAssertion(() =>
        {
            Assert.That(server.EntMan.HasComponent<DebrainedComponent>(human), Is.False,
                "human slasher must not be debrained by the brain move");

            var body = server.EntMan.GetComponent<BodyComponent>(human);
            var bodySystem = server.System<BodySystem>();

            Assert.That(bodySystem.TryGetBodyOrganEntityComps<BrainComponent>((human, body), out var brains), Is.True);
            var brain = brains![0];

            Assert.That(bodySystem.GetParentPartOrNull(brain.Owner) is { } parent
                && server.EntMan.GetComponent<BodyPartComponent>(parent).PartType == BodyPartType.Chest,
                Is.True, "human slasher brain must be moved into the chest");
            Assert.That(brain.Comp2.Body, Is.EqualTo(human));
        });

        await pair.CleanReturnAsync();
    }
}
