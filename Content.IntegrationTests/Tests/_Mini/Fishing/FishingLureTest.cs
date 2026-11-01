// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from tgstation/tgstation (code/modules/fishing, AGPL-3.0-or-later).

using Content.Shared._Mini.Fishing;
using Content.Shared._Mini.Fishing.Components;
using Content.Shared._Mini.Fishing.Prototypes;
using Content.Shared._Mini.Fishing.Systems;
using Content.IntegrationTests.Tests.Interaction;
using Content.Shared.Containers.ItemSlots;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Mini.Fishing;

/// <summary>
/// Artificial lures: they only catch the fish their filter allows, they attract shiny lovers and
/// they have to be spun while the fish is not biting.
/// </summary>
[TestFixture]
public sealed class FishingLureTest : InteractionTest
{
    private const string RodPrototype = "FishingRod";
    private const string BaitSlot = "bait";
    private const string LineSlot = "line";

    private static readonly ProtoId<FishingSourcePrototype> OceanSource = "FishingSourceOcean";
    private static readonly ProtoId<FishingSourcePrototype> ToiletSource = "FishingSourceToilet";

    private ItemSlotsSystem _slots = default!;
    private FishingSourceSystem _sources = default!;

    [SetUp]
    public override async Task Setup()
    {
        await base.Setup();
        _slots = SEntMan.System<ItemSlotsSystem>();
        _sources = SEntMan.System<FishingSourceSystem>();
    }

    private void ReplaceSlot(EntityUid rod, string slot, EntityUid? item)
    {
        _slots.TryEject(rod, slot, null, out _, doAfter: false);

        if (item is { } itemUid)
            Assert.That(_slots.TryInsert(rod, slot, itemUid, SPlayer), Is.True, $"Could not insert into the {slot} slot.");
    }

    private FishPrototype Fish(string id)
    {
        Assert.That(ProtoMan.TryIndex(id, out FishPrototype? fish), Is.True, $"Unknown fish {id}");
        return fish!;
    }

    [Test]
    public async Task LureCatchFiltersTest()
    {
        var rod = SEntMan.GetEntity(await PlaceInHands(RodPrototype));
        var worm = SEntMan.GetEntity(await Spawn("BaitWorm"));
        var minnow = SEntMan.GetEntity(await Spawn("FishingLureMinnow"));
        var spoon = SEntMan.GetEntity(await Spawn("FishingLureSpoon"));
        var artificialFly = SEntMan.GetEntity(await Spawn("FishingLureArtificialFly"));
        var led = SEntMan.GetEntity(await Spawn("FishingLureLed"));
        var luckyCoin = SEntMan.GetEntity(await Spawn("FishingLureLuckyCoin"));
        var algae = SEntMan.GetEntity(await Spawn("FishingLureAlgae"));
        var grub = SEntMan.GetEntity(await Spawn("FishingLureGrub"));
        var buzzbait = SEntMan.GetEntity(await Spawn("FishingLureBuzzbait"));
        var spinnerbait = SEntMan.GetEntity(await Spawn("FishingLureSpinnerbait"));
        var daisyChain = SEntMan.GetEntity(await Spawn("FishingLureDaisyChain"));
        var dropping = SEntMan.GetEntity(await Spawn("FishingLureDropping"));

        await Server.WaitPost(() =>
        {
            float Weight(EntityUid lure, string fish, ProtoId<FishingSourcePrototype>? source = null)
            {
                return _sources.GetLureCatchWeightMultiplier(Fish(fish), rod, lure, source ?? OceanSource);
            }

            // Normal bait has no filter and stays at one times the weight.
            Assert.That(_sources.GetLureCatchWeightMultiplier(Fish("FishPike"), rod, worm, OceanSource),
                Is.EqualTo(1f).Within(0.001f), "Normal bait must not filter the species.");

            // Minnow: small and medium fish, no picky, vegan, nocturnal or heavy prey.
            Assert.That(Weight(minnow, "FishGoldfishThreeEyes"), Is.EqualTo(10f).Within(0.001f),
                "The minnow must catch a small freshwater fish.");
            Assert.That(Weight(minnow, "FishBumpy"), Is.EqualTo(0f), "The minnow must not catch a vegan fish.");
            Assert.That(Weight(minnow, "FishPike"), Is.EqualTo(0f), "The minnow must not catch a fish bigger than normal.");

            // Spoon and spinnerbait: freshwater fish only.
            Assert.That(Weight(spoon, "FishCatfish"), Is.EqualTo(10f).Within(0.001f),
                "The spoon must catch a small freshwater fish.");
            Assert.That(Weight(spoon, "FishSquid"), Is.EqualTo(0f), "The spoon must not catch a saltwater fish.");
            Assert.That(Weight(spinnerbait, "FishPike"), Is.EqualTo(10f).Within(0.001f),
                "The spinnerbait must catch a freshwater predator.");
            Assert.That(Weight(spinnerbait, "FishSquid"), Is.EqualTo(0f),
                "The spinnerbait must not catch a saltwater predator.");
            Assert.That(Weight(daisyChain, "FishSquid"), Is.EqualTo(10f).Within(0.001f),
                "The daisy chain must catch a saltwater predator.");
            Assert.That(Weight(daisyChain, "FishChainsawfish"), Is.EqualTo(0f),
                "The daisy chain must not catch a freshwater predator.");

            // Trait specialists.
            Assert.That(Weight(artificialFly, "FishClownfish"), Is.EqualTo(10f).Within(0.001f),
                "The artificial fly must catch a picky eater.");
            Assert.That(Weight(led, "FishLanternfish"), Is.EqualTo(10f).Within(0.001f),
                "The LED lure must catch a nocturnal fish.");
            Assert.That(Weight(led, "FishLavaloop"), Is.EqualTo(10f).Within(0.001f),
                "The LED lure must catch a heavy fish.");
            Assert.That(Weight(algae, "FishBumpy"), Is.EqualTo(10f).Within(0.001f),
                "The algae lure must catch a vegan fish.");
            Assert.That(Weight(grub, "FishGoldfishThreeEyes"), Is.EqualTo(10f).Within(0.001f),
                "The grub must catch a small fish.");
            Assert.That(Weight(buzzbait, "FishJumpercable"), Is.EqualTo(10f).Within(0.001f),
                "The buzzbait must catch an electric fish.");

            // Junk lure: the toilet source and the disgustingly ugly fish.
            Assert.That(Weight(dropping, "FishSlimefish", ToiletSource), Is.EqualTo(10f).Within(0.001f),
                "The dropping must catch the fish of the toilet.");
            Assert.That(Weight(dropping, "FishSlimefish"), Is.EqualTo(0f),
                "The dropping must not catch a beautiful ocean fish.");
            Assert.That(Weight(dropping, "FishMossglob"), Is.EqualTo(10f).Within(0.001f),
                "The dropping must catch a disgusting fish anywhere.");

            // The lucky coin also attracts shiny lovers.
            Assert.That(Weight(luckyCoin, "FishGoldfishThreeEyes"), Is.EqualTo(10f).Within(0.001f),
                "The lucky coin must catch a shiny lover.");
            var shiny = Fish("FishGoldfishThreeEyes");
            var plainWeight = _sources.GetTraitCatchWeightMultiplier(shiny, rod, worm);
            var coinWeight = _sources.GetTraitCatchWeightMultiplier(shiny, rod, luckyCoin);
            Assert.That(plainWeight, Is.EqualTo(0.5f).Within(0.001f),
                "Shiny lovers must be twice as hard to find without a shiny bait.");
            Assert.That(coinWeight, Is.EqualTo(1f).Within(0.001f),
                "The lucky coin must remove the shiny lover penalty.");
        });
    }

    [Test]
    public async Task LureSpinWindowTest()
    {
        var rod = SEntMan.GetEntity(await PlaceInHands(RodPrototype));
        var lure = SEntMan.GetEntity(await Spawn("FishingLureMinnow"));

        await Server.WaitPost(() => ReplaceSlot(rod, BaitSlot, lure));

        await SpawnTarget("FloorWaterEntity");
        await Interact();
        await RunTicks(5);

        var player = ToServer(Player);
        var challenge = SEntMan.GetComponent<FishingChallengeComponent>(player);

        Assert.That(challenge.Phase, Is.EqualTo(FishingPhase.Waiting));
        Assert.That(challenge.SpinActive, Is.True, "A lure must have to be spun while waiting.");
        Assert.That(challenge.SpinReady, Is.False);

        // The green window opens after the minimum spin frequency.
        await Server.WaitPost(() => challenge.SpinReadyTime = STiming.CurTime - TimeSpan.FromSeconds(1));
        await RunTicks(3);
        Assert.That(challenge.SpinReady, Is.True, "The lure light must turn green at the spin time.");

        // Missing the window restarts the wait with a penalty of three to five seconds.
        var before = STiming.CurTime + TimeSpan.FromSeconds(1);
        await Server.WaitPost(() =>
        {
            challenge.NextPhaseTime = before;
            challenge.SpinMissTime = STiming.CurTime - TimeSpan.FromSeconds(1);
        });
        await RunTicks(3);

        Assert.That(challenge.Phase, Is.EqualTo(FishingPhase.Waiting));
        Assert.That(challenge.SpinReady, Is.False, "A missed spin must close the window.");
        Assert.That(challenge.NextPhaseTime, Is.GreaterThan(before), "A missed spin must add a wait penalty.");
    }

    [Test]
    public async Task AutoReelLineSpinsTheLureTest()
    {
        var rod = SEntMan.GetEntity(await PlaceInHands(RodPrototype));
        var lure = SEntMan.GetEntity(await Spawn("FishingLureMinnow"));
        var line = SEntMan.GetEntity(await Spawn("FishingLineAutoReel"));

        await Server.WaitPost(() =>
        {
            ReplaceSlot(rod, BaitSlot, lure);
            ReplaceSlot(rod, LineSlot, line);
        });

        await SpawnTarget("FloorWaterEntity");
        await Interact();
        await RunTicks(5);

        var player = ToServer(Player);
        var challenge = SEntMan.GetComponent<FishingChallengeComponent>(player);

        await Server.WaitPost(() => challenge.SpinReadyTime = STiming.CurTime - TimeSpan.FromSeconds(1));
        await RunTicks(3);

        Assert.That(challenge.Phase, Is.EqualTo(FishingPhase.Waiting));
        Assert.That(challenge.SpinReady, Is.False, "The auto-reel line must spin the lure for the player.");
        Assert.That(challenge.SpinReadyTime, Is.GreaterThan(STiming.CurTime),
            "The interval must restart after the automatic spin.");
    }
}
