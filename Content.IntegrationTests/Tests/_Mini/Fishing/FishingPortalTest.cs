// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from tgstation/tgstation (code/modules/fishing, AGPL-3.0-or-later).

using System.Linq;
using Content.IntegrationTests.Tests.Interaction;
using Content.Server._Mini.Fishing;
using Content.Shared._Mini.Fishing.Components;

namespace Content.IntegrationTests.Tests._Mini.Fishing;

[TestFixture]
public sealed class FishingPortalTest : InteractionTest
{
    [Test]
    public async Task PortalOffersOnlyPortalDimensions()
    {
        var portal = SEntMan.GetEntity(await Spawn("FishingPortalGenerator"));

        await Server.WaitPost(() =>
        {
            var system = SEntMan.System<FishingPortalSystem>();
            var comp = SEntMan.GetComponent<FishingPortalComponent>(portal);

            var options = system.GetOptions((portal, comp)).Select(o => o.SourceId).ToList();
            Assert.That(options, Is.EquivalentTo(new[]
            {
                "FishingSourcePortal",
                "FishingSourcePortalBeach",
                "FishingSourcePortalChasm",
                "FishingSourcePortalOcean",
                "FishingSourcePortalHyperspace",
            }));

            // Emagging unlocks the syndicate dimension, like in tg.
            comp.Emagged = true;
            var emagged = system.GetOptions((portal, comp)).Select(o => o.SourceId).ToList();
            Assert.That(emagged, Does.Contain("FishingSourcePortalSyndicate"));
            Assert.That(emagged, Does.Not.Contain("FishingSourcePortalRandom"));
        });
    }
}
