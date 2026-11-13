// SPDX-License-Identifier: AGPL-3.0-or-later

#nullable enable
using System.Linq;
using Content.Server.Body.Components;
using Content.Server.Nyanotrasen.Abilities.Felinid;
using Content.Shared._Shitmed.Body.Organ;
using Content.Shared.Body.Components;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.EntityEffects;
using Content.Shared.Mobs.Systems;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._Trauma.Genetics;

[TestFixture]
public sealed class GeneticsBodyEffectsTest
{
    [Test]
    public async Task HarsMutationKeepsCarrierAliveAndRegrowsHead()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        var map = await pair.CreateTestMap();
        var entMan = server.EntMan;
        var body = server.System<SharedBodySystem>();
        var effects = server.System<SharedEntityEffectsSystem>();
        var mobState = server.System<MobStateSystem>();
        var protoMan = server.ProtoMan;

        EntityUid human = default;
        await server.WaitPost(() => human = entMan.Spawn("MobHuman", map.MapCoords));
        await server.WaitRunTicks(1);

        void Apply(string id)
        {
            var proto = protoMan.Index<EntityEffectPrototype>(id);
            effects.ApplyEffects(human, proto.Effects);
        }

        int CountHeads()
            => body.GetBodyChildrenOfType(human, BodyPartType.Head).Count();

        EntityUid? FindOrgan(string protoId)
        {
            foreach (var (organ, _) in body.GetBodyOrgans(human))
            {
                if (entMan.GetComponent<MetaDataComponent>(organ).EntityPrototype?.ID == protoId)
                    return organ;
            }

            return null;
        }

        Assert.That(CountHeads(), Is.EqualTo(1), "human should start with a head");
        Assert.That(FindOrgan("OrganHumanBrain"), Is.Not.Null, "human should start with a brain");

        await server.WaitPost(() => Apply("MutationRemoveHead"));
        await server.WaitRunTicks(2);

        Assert.Multiple(() =>
        {
            Assert.That(CountHeads(), Is.EqualTo(0), "H.A.R.S. should remove the head");
            Assert.That(FindOrgan("OrganHumanBrain"), Is.Not.Null, "the brain must be moved to the chest, not gibbed with the head");
            Assert.That(entMan.HasComponent<DebrainedComponent>(human), Is.False, "the carrier must not be debrained");
            Assert.That(mobState.IsAlive(human), Is.True, "removing the head must not kill the carrier");
        });

        await server.WaitPost(() => Apply("MutationAddHead"));
        await server.WaitRunTicks(2);

        Assert.Multiple(() =>
        {
            Assert.That(CountHeads(), Is.EqualTo(1), "the head should come back when H.A.R.S. is removed");
            Assert.That(entMan.HasComponent<DebrainedComponent>(human), Is.False);
            Assert.That(mobState.IsAlive(human), Is.True);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task FelinidMutationAddsAndRevertsEarsTailAndMetabolism()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        var map = await pair.CreateTestMap();
        var entMan = server.EntMan;
        var body = server.System<SharedBodySystem>();
        var effects = server.System<SharedEntityEffectsSystem>();
        var protoMan = server.ProtoMan;

        EntityUid human = default;
        await server.WaitPost(() => human = entMan.Spawn("MobHuman", map.MapCoords));
        await server.WaitRunTicks(1);

        void Apply(string id)
        {
            var proto = protoMan.Index<EntityEffectPrototype>(id);
            effects.ApplyEffects(human, proto.Effects);
        }

        EntityUid? FindOrgan(string protoId)
        {
            foreach (var (organ, _) in body.GetBodyOrgans(human))
            {
                if (entMan.GetComponent<MetaDataComponent>(organ).EntityPrototype?.ID == protoId)
                    return organ;
            }

            return null;
        }

        await server.WaitPost(() => Apply("MakeFelinid"));
        await server.WaitRunTicks(2);

        var heart = FindOrgan("OrganHumanHeart");
        Assert.That(heart, Is.Not.Null, "the carrier should still have a heart");

        var metabolizer = entMan.GetComponent<MetabolizerComponent>(heart!.Value);
        Assert.Multiple(() =>
        {
            Assert.That(FindOrgan("OrganFelinidEars"), Is.Not.Null, "felinization should add cat ears");
            Assert.That(FindOrgan("OrganFelinidTail"), Is.Not.Null, "felinization should add a cat tail");
            Assert.That(entMan.HasComponent<FelinidComponent>(human), Is.True);
            Assert.That(metabolizer.MetabolizerTypes, Is.Not.Null);
            Assert.That(metabolizer.MetabolizerTypes!.Contains("Animal"), Is.True);
            Assert.That(metabolizer.MetabolizerTypes!.Contains("Human"), Is.False);
        });

        await server.WaitPost(() => Apply("RevertFelinid"));
        await server.WaitRunTicks(2);

        Assert.Multiple(() =>
        {
            Assert.That(FindOrgan("OrganFelinidEars"), Is.Null, "reverting should remove the cat ears");
            Assert.That(FindOrgan("OrganFelinidTail"), Is.Null, "reverting should remove the cat tail");
            Assert.That(entMan.HasComponent<FelinidComponent>(human), Is.False);
            Assert.That(metabolizer.MetabolizerTypes!.Contains("Human"), Is.True);
            Assert.That(metabolizer.MetabolizerTypes!.Contains("Animal"), Is.False);
        });

        await pair.CleanReturnAsync();
    }
}
