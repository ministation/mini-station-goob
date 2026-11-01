// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from tgstation/tgstation (code/modules/fishing, AGPL-3.0-or-later).

using Content.IntegrationTests.Tests.Interaction;
using Content.Shared._Mini.Fishing.Components;
using Content.Shared._Mini.Fishing.Systems;
using Content.Shared.Item;

namespace Content.IntegrationTests.Tests._Mini.Fishing;

[TestFixture]
public sealed class FishDeadInhandTest : InteractionTest
{
    [Test]
    public async Task DeadChainsawfishUsesDeadInhandSprite()
    {
        var fish = SEntMan.GetEntity(await Spawn("FishChainsawfish"));

        await Server.WaitPost(() =>
        {
            var system = SEntMan.System<FishSystem>();
            var item = SEntMan.GetComponent<ItemComponent>(fish);
            Assert.That(item.HeldPrefix, Is.EqualTo("chainsawfish"));

            system.SetAlive((fish, SEntMan.GetComponent<FishComponent>(fish)), false);
            Assert.That(item.HeldPrefix, Is.EqualTo("chainsawfish_dead"));

            system.SetAlive((fish, SEntMan.GetComponent<FishComponent>(fish)), true);
            Assert.That(item.HeldPrefix, Is.EqualTo("chainsawfish"));
        });
    }
}
