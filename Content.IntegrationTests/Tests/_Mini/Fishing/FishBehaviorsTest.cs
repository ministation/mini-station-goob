// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from tgstation/tgstation (code/modules/fishing, AGPL-3.0-or-later).

using Content.IntegrationTests.Tests.Interaction;
using Content.Shared._Mini.Fishing.Components;
using Content.Shared._Mini.Fishing.Systems;

namespace Content.IntegrationTests.Tests._Mini.Fishing;

[TestFixture]
public sealed class FishBehaviorsTest : InteractionTest
{
    [Test]
    public async Task FritterishGetsARandomVariant()
    {
        var fish = SEntMan.GetEntity(await Spawn("FishFryishFritterish"));
        var variant = SEntMan.GetComponent<FishVariantComponent>(fish);

        Assert.That(variant.Variant, Is.InRange(0, variant.Variants.Count - 1));
    }

    [Test]
    public async Task DeadBabbelfishKillsNearbyFish()
    {
        var neighbor = SEntMan.GetEntity(await Spawn("FishPerch"));
        var babbelfish = SEntMan.GetEntity(await Spawn("FishBabbelfish"));

        Assert.That(SEntMan.GetComponent<FishComponent>(neighbor).Alive, Is.True);

        await Server.WaitPost(() =>
        {
            var fishSystem = SEntMan.System<FishSystem>();
            fishSystem.SetAlive((babbelfish, SEntMan.GetComponent<FishComponent>(babbelfish)), false);
        });
        await RunTicks(1);

        Assert.That(SEntMan.GetComponent<FishComponent>(neighbor).Alive, Is.False,
            "The psychic wail must kill fish in range.");
    }
}
