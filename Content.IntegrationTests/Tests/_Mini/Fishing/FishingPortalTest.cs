// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from tgstation/tgstation (code/modules/fishing, AGPL-3.0-or-later).

using System;
using System.Collections.Generic;
using System.Linq;
using Content.IntegrationTests.Tests.Interaction;
using Content.Server._Mini.Fishing;
using Content.Shared._Mini.Fishing.Components;
using Content.Shared._Mini.Research.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Mini.Fishing;

[TestFixture]
public sealed class FishingPortalTest : InteractionTest
{
    [Test]
    public async Task PortalUnlocksDimensionsWithResearch()
    {
        var portal = SEntMan.GetEntity(await Spawn("FishingPortalGenerator"));

        await Server.WaitPost(() =>
        {
            var system = SEntMan.System<FishingPortalSystem>();
            var comp = SEntMan.GetComponent<FishingPortalComponent>(portal);

            List<string> Options(Func<ProtoId<ResearchExperimentPrototype>, bool> completed)
            {
                return system.GetOptions((portal, comp), completed).Select(o => o.SourceId).ToList();
            }

            // Nothing researched yet: only the aquarium dimension.
            Assert.That(Options(_ => false), Is.EquivalentTo(new[] { "FishingSourcePortal" }));

            // The first fish scanning experiment unlocks the beach, like in tg.
            var beach = Options(e => e.Id == "ScanningFish");
            Assert.That(beach, Does.Contain("FishingSourcePortalBeach"));
            Assert.That(beach, Does.Not.Contain("FishingSourcePortalOcean"));

            // All four experiments unlock every dimension.
            Assert.That(Options(_ => true), Is.EquivalentTo(new[]
            {
                "FishingSourcePortal",
                "FishingSourcePortalBeach",
                "FishingSourcePortalChasm",
                "FishingSourcePortalOcean",
                "FishingSourcePortalHyperspace",
            }));

            // Emagging unlocks the syndicate dimension, like in tg.
            comp.Emagged = true;
            Assert.That(Options(_ => false), Is.EquivalentTo(new[]
            {
                "FishingSourcePortal",
                "FishingSourcePortalSyndicate",
            }));

            Assert.That(Options(_ => true), Does.Not.Contain("FishingSourcePortalRandom"));
        });
    }
}
