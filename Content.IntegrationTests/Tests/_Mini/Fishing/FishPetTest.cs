// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from tgstation/tgstation (code/modules/fishing, AGPL-3.0-or-later).

using Content.IntegrationTests.Tests.Interaction;
using Content.Shared._Mini.Fishing;
using Content.Shared._Mini.Fishing.Aquarium;
using Content.Shared._Mini.Fishing.Components;
using Content.Shared._Mini.Fishing.Prototypes;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.IntegrationTests.Tests._Mini.Fishing;

[TestFixture]
public sealed class FishPetTest : InteractionTest
{
    [Test]
    public async Task PettingMakesFishHappier()
    {
        var goldfish = SEntMan.GetEntity(await Spawn("FishGoldfish"));

        await Server.WaitPost(() =>
        {
            var proto = Server.ResolveDependency<IPrototypeManager>();
            var timing = Server.ResolveDependency<IGameTiming>();
            var aquarium = SEntMan.System<SharedAquariumSystem>();

            var fish = SEntMan.GetComponent<FishComponent>(goldfish);
            Assert.That(proto.TryIndex(fish.Fish, out FishPrototype? fishProto), Is.True);

            // Put the fish in the wrong water at the wrong temperature, so petting is what tips the scale.
            var before = aquarium.GetHappiness(fish, fishProto, AquariumFluid.Saltwater, 400f, timing.CurTime);
            fish.LastPetted = timing.CurTime;
            var after = aquarium.GetHappiness(fish, fishProto, AquariumFluid.Saltwater, 400f, timing.CurTime);

            Assert.That(after, Is.GreaterThan(before));
        });
    }
}
