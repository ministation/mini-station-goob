// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from tgstation/tgstation (code/modules/fishing, AGPL-3.0-or-later).

using Content.IntegrationTests.Tests.Interaction;
using Content.Server.Cargo.Components;
using Content.Shared.Item;
using Content.Shared.Kitchen.Components;
using Content.Shared.Weapons.Melee;

namespace Content.IntegrationTests.Tests._Mini.Fishing;

[TestFixture]
public sealed class FishPhysicalTest : InteractionTest
{
    [Test]
    public async Task WeightAndSizeMatter()
    {
        var goldfish = SEntMan.GetEntity(await Spawn("FishGoldfish"));
        var mastodon = SEntMan.GetEntity(await Spawn("FishMastodon"));

        await Server.WaitPost(() =>
        {
            // Small fish stay tiny and light.
            Assert.That(SEntMan.GetComponent<ItemComponent>(goldfish).Size.Id, Is.EqualTo("Tiny"));
            Assert.That(SEntMan.HasComponent<HeldSpeedModifierComponent>(goldfish), Is.False);

            // Heavy fish slow the holder down and hit hard.
            var held = SEntMan.GetComponent<HeldSpeedModifierComponent>(mastodon);
            Assert.That(held.WalkModifier, Is.LessThan(1f));
            var melee = SEntMan.GetComponent<MeleeWeaponComponent>(mastodon);
            Assert.That(melee.Damage.GetTotal().Float(), Is.GreaterThan(0f));

            // Big fish sell for more than the flat base price.
            var price = SEntMan.GetComponent<StaticPriceComponent>(mastodon);
            Assert.That(price.Price, Is.GreaterThan(100d));

            // Edible fish can be ground for their reagents.
            Assert.That(SEntMan.GetComponent<ExtractableComponent>(goldfish).GrindableSolution, Is.EqualTo("food"));
        });
    }
}
