// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from tgstation/tgstation (code/modules/fishing, AGPL-3.0-or-later).

#nullable enable

using Content.IntegrationTests.Tests.Interaction;
using Content.Shared._Mini.Fishing;
using Content.Shared._Mini.Fishing.Components;
using Content.Shared._Mini.Fishing.Prototypes;
using Content.Shared.Storage;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Mini.Fishing;

[TestFixture]
public sealed class FishCaseTest : InteractionTest
{
    [Test]
    public async Task CasesComeWithOneFish()
    {
        var syndicateCase = SEntMan.GetEntity(await Spawn("FishCaseSyndicate"));
        var blackmarketCase = SEntMan.GetEntity(await Spawn("FishCaseBlackmarket"));
        var freshwaterCase = SEntMan.GetEntity(await Spawn("FishCaseFreshwater"));
        var saltwaterCase = SEntMan.GetEntity(await Spawn("FishCaseSaltwater"));
        var bluespaceCase = SEntMan.GetEntity(await Spawn("FishCaseBluespace"));
        var randomCase = SEntMan.GetEntity(await Spawn("FishCase"));

        await Server.WaitPost(() =>
        {
            var container = SEntMan.System<SharedContainerSystem>();

            var syndicateFish = GetOnlyFish(container, syndicateCase);
            var syndicateId = SEntMan.GetComponent<MetaDataComponent>(syndicateFish).EntityPrototype?.ID;
            Assert.That(syndicateId, Is.AnyOf("FishDonkfish", "FishEmulsijack", "FishJumpercable", "FishChainsawfish", "FishPikeArmored"));

            var blackmarketFish = GetOnlyFish(container, blackmarketCase);
            Assert.That(SEntMan.GetComponent<FishComponent>(blackmarketFish).Alive, Is.False);

            AssertFishFitsFluid(container, freshwaterCase, AquariumFluid.Freshwater);
            AssertFishFitsFluid(container, saltwaterCase, AquariumFluid.Saltwater);

            Assert.That(SEntMan.HasComponent<FishComponent>(GetOnlyFish(container, bluespaceCase)), Is.True);
            Assert.That(SEntMan.HasComponent<FishComponent>(GetOnlyFish(container, randomCase)), Is.True);
        });
    }

    private void AssertFishFitsFluid(SharedContainerSystem container, EntityUid caseUid, AquariumFluid wanted)
    {
        var fishComp = SEntMan.GetComponent<FishComponent>(GetOnlyFish(container, caseUid));
        Assert.That(fishComp.Fish, Is.Not.Null);

        var proto = Server.ResolveDependency<IPrototypeManager>();
        Assert.That(proto.TryIndex(fishComp.Fish!.Value, out FishPrototype? fishProto), Is.True);
        Assert.That(fishProto!.Fluid, Is.AnyOf(wanted, AquariumFluid.AnyWater, AquariumFluid.Anadromous));
    }

    private EntityUid GetOnlyFish(SharedContainerSystem container, EntityUid caseUid)
    {
        Assert.That(container.TryGetContainer(caseUid, StorageComponent.ContainerId, out var storage), Is.True);
        Assert.That(storage!.ContainedEntities, Has.Count.EqualTo(1));
        return storage.ContainedEntities[0];
    }
}
