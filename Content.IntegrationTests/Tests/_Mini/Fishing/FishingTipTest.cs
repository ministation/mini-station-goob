// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from tgstation/tgstation (code/modules/fishing, AGPL-3.0-or-later).

using Content.IntegrationTests.Tests.Interaction;
using Content.Shared.Item;
using Content.Shared.Paper;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._Mini.Fishing;

[TestFixture]
public sealed class FishingTipTest : InteractionTest
{
    [Test]
    public async Task TipsGetFilledIn()
    {
        var tip = SEntMan.GetEntity(await Spawn("FishingTip"));
        var instructions = SEntMan.GetEntity(await Spawn("LuresInstructions"));
        var lavaSign = SEntMan.GetEntity(await Spawn("HazardWarningLava"));
        var plasmaSign = SEntMan.GetEntity(await Spawn("HazardWarningPlasma"));

        await Server.WaitPost(() =>
        {
            // The server locale decides the language, so only check the shape of the text.
            var tipText = SEntMan.GetComponent<PaperComponent>(tip).Content;
            Assert.That(tipText.Length, Is.GreaterThan(80));
            Assert.That(tipText, Does.Not.Contain("{ $"));
            Assert.That(tipText.Split('\n').Length, Is.EqualTo(6));

            var instructionsText = SEntMan.GetComponent<PaperComponent>(instructions).Content;
            Assert.That(instructionsText.Length, Is.GreaterThan(200));
            Assert.That(instructionsText, Does.Not.Contain("{ $"));
            Assert.That(instructionsText.Split('\n').Length, Is.EqualTo(7));

            Assert.That(SEntMan.HasComponent<ItemComponent>(lavaSign), Is.True);
            Assert.That(SEntMan.HasComponent<ItemComponent>(plasmaSign), Is.True);
        });
    }
}
