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
        var swordfish = SEntMan.GetEntity(await Spawn("FishSwordfish"));
        var goldfish = SEntMan.GetEntity(await Spawn("FishGoldfish"));

        await Server.WaitPost(() =>
        {
            Assert.That(SEntMan.HasComponent<MultiHandedItemComponent>(swordfish), Is.True,
                "A huge fish must need both hands, like tg require_twohands");
            Assert.That(SEntMan.HasComponent<MultiHandedItemComponent>(goldfish), Is.False,
                "A small fish fits in one hand");
        });
    }
}
