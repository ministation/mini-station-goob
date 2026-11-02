// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from tgstation/tgstation (code/modules/fishing, AGPL-3.0-or-later).

using Content.IntegrationTests.Tests.Interaction;
using Content.Shared.Item;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._Mini.Fishing;

[TestFixture]
public sealed class FishTwoHandsTest : InteractionTest
{
    [Test]
    public async Task BigFishNeedsBothHands()
    {
        var mastodon = SEntMan.GetEntity(await Spawn("FishMastodon"));
        var pike = SEntMan.GetEntity(await Spawn("FishPike"));
        var goldfish = SEntMan.GetEntity(await Spawn("FishGoldfish"));

        await Server.WaitPost(() =>
        {
            // tg FISH_SIZE_TWO_HANDS_REQUIRED is 135, the mastodon is always above it.
            Assert.That(SEntMan.HasComponent<MultiHandedItemComponent>(mastodon), Is.True,
                "A fish above the two hands size must need both hands");

            // tg TRAIT_FISH_SHOULD_TWOHANDED: pikes need both hands once bulky.
            Assert.That(SEntMan.HasComponent<MultiHandedItemComponent>(pike), Is.True,
                "A bulky fish with the two hands trait must need both hands");

            Assert.That(SEntMan.HasComponent<MultiHandedItemComponent>(goldfish), Is.False,
                "A small fish fits in one hand");
        });
    }
}
