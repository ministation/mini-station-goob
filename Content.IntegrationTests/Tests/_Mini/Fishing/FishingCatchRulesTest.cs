// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from tgstation/tgstation (code/modules/fishing, AGPL-3.0-or-later).

using Content.Shared._Mini.Fishing;
using Content.Shared._Mini.Fishing.Components;
using Content.Shared._Mini.Fishing.Events;
using Content.Shared._Mini.Fishing.Prototypes;
using Content.Shared._Mini.Fishing.Systems;
using Content.IntegrationTests.Tests.Interaction;
using Content.Shared.Containers.ItemSlots;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Mini.Fishing;

/// <summary>
/// Catch table tests: bait quality, fish traits and the minigame rules they add.
/// </summary>
[TestFixture]
public sealed class FishingCatchRulesTest : InteractionTest
{
    private const string RodPrototype = "FishingRod";
    private const string BaitSlot = "bait";
    private const string HookSlot = "hook";
    private const string LineSlot = "line";
    private const string OceanSource = "FishingSourceOcean";

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

    private FishLootModifiersEvent RollLoot(EntityUid rod)
    {
        var ev = new FishLootModifiersEvent(SPlayer, rod, new ProtoId<FishingSourcePrototype>(OceanSource));
        SEntMan.EventBus.RaiseLocalEvent(rod, ref ev);
        return ev;
    }

    /// <summary>
    /// Bait quality must reshape the fish weight in the table, and only normal bait removes duds.
    /// </summary>
    [Test]
    public async Task BaitQualityChangesTheLootTableTest()
    {
        var rod = SEntMan.GetEntity(await PlaceInHands(RodPrototype));
        var worm = SEntMan.GetEntity(await Spawn("BaitWorm"));
        var superBait = SEntMan.GetEntity(await Spawn("BaitDoughballSuper"));
        var lure = SEntMan.GetEntity(await Spawn("FishingLureMinnow"));
        var magnet = SEntMan.GetEntity(await Spawn("FishingHookMagnet"));

        await Server.WaitPost(() =>
        {
            var noBait = RollLoot(rod);
            Assert.That(noBait.FishWeightMultiplier, Is.EqualTo(0.15f).Within(0.001f),
                "Fishing without bait must keep the tg 0.15x fish weight.");
            Assert.That(noBait.RemoveDuds, Is.False, "Without bait duds must still be possible.");

            ReplaceSlot(rod, BaitSlot, worm);
            var basic = RollLoot(rod);
            Assert.That(basic.FishWeightMultiplier, Is.EqualTo(2f).Within(0.001f),
                "Basic bait must double the fish weight.");
            Assert.That(basic.RemoveDuds, Is.True, "Normal bait must remove duds from the table.");

            ReplaceSlot(rod, BaitSlot, superBait);
            var great = RollLoot(rod);
            Assert.That(great.FishWeightMultiplier, Is.EqualTo(9f).Within(0.001f),
                "Great bait must multiply the fish weight by nine.");
            Assert.That(great.ExtraCatchChance, Is.GreaterThan(0f), "Great bait must grant the bonus catch chance.");

            ReplaceSlot(rod, BaitSlot, lure);
            var lureRoll = RollLoot(rod);
            Assert.That(lureRoll.FishWeightMultiplier, Is.EqualTo(2f).Within(0.001f),
                "An artificial lure still counts as basic bait quality.");
            Assert.That(lureRoll.RemoveDuds, Is.False, "Artificial lures must leave duds in the table.");

            ReplaceSlot(rod, HookSlot, magnet);
            var magnetRoll = RollLoot(rod);
            Assert.That(magnetRoll.FishWeightMultiplier, Is.EqualTo(0.4f).Within(0.001f),
                "The magnetic hook must scale the bait bonus down.");
        });
    }

    /// <summary>
    /// Fish traits decide whether the fish takes the bait at all.
    /// </summary>
    [Test]
    public async Task FishTraitsGateTheBiteTest()
    {
        var rod = SEntMan.GetEntity(await PlaceInHands(RodPrototype));
        var worm = SEntMan.GetEntity(await Spawn("BaitWorm"));
        var dough = SEntMan.GetEntity(await Spawn("BaitDoughball"));
        var superBait = SEntMan.GetEntity(await Spawn("BaitDoughballSuper"));
        var shinyHook = SEntMan.GetEntity(await Spawn("FishingHookShiny"));

        var squid = ProtoMan.Index<FishPrototype>("FishSquid");
        var clownfish = ProtoMan.Index<FishPrototype>("FishClownfish");
        var threeEyes = ProtoMan.Index<FishPrototype>("FishGoldfishThreeEyes");

        await Server.WaitPost(() =>
        {
            Assert.That(_sources.GetTraitCatchWeightMultiplier(squid, rod, null), Is.EqualTo(0f),
                "A carnivore must not bite a rod without bait.");
            Assert.That(_sources.GetTraitCatchWeightMultiplier(squid, rod, dough), Is.EqualTo(0f),
                "A carnivore must not bite a produce bait.");
            Assert.That(_sources.GetTraitCatchWeightMultiplier(squid, rod, worm), Is.EqualTo(1f).Within(0.001f),
                "A carnivore must bite a meat bait.");

            Assert.That(_sources.GetTraitCatchWeightMultiplier(clownfish, rod, null), Is.EqualTo(0f),
                "A picky eater must not bite a rod without bait.");
            Assert.That(_sources.GetTraitCatchWeightMultiplier(clownfish, rod, worm), Is.EqualTo(0f),
                "A picky eater must not bite plain bait it does not like.");
            Assert.That(_sources.GetTraitCatchWeightMultiplier(clownfish, rod, superBait), Is.EqualTo(1f).Within(0.001f),
                "A picky eater must bite good bait.");

            Assert.That(_sources.GetTraitCatchWeightMultiplier(threeEyes, rod, worm), Is.EqualTo(0.5f).Within(0.001f),
                "A shiny lover must be twice as hard to find without a shiny hook.");

            ReplaceSlot(rod, HookSlot, shinyHook);
            Assert.That(_sources.GetTraitCatchWeightMultiplier(threeEyes, rod, worm), Is.EqualTo(1f).Within(0.001f),
                "A shiny hook must remove the shiny lover penalty.");
        });
    }

    /// <summary>
    /// Traits must also change how the fight itself plays out.
    /// </summary>
    [Test]
    public async Task FishTraitsShapeTheFightTest()
    {
        var squid = ProtoMan.Index<FishPrototype>("FishSquid");
        var starfish = ProtoMan.Index<FishPrototype>("FishStarfishChrystarfish");
        var lubefish = ProtoMan.Index<FishPrototype>("FishClownfishLube");

        await Server.WaitPost(() =>
        {
            Assert.That(_sources.GetTraitMinigameRules(squid) & FishingMinigameRules.Camo,
                Is.EqualTo(FishingMinigameRules.Camo), "A camouflaged fish must add the camo effect to the fight.");
            Assert.That(_sources.GetTraitMinigameRules(starfish) & FishingMinigameRules.Antigrav,
                Is.EqualTo(FishingMinigameRules.Antigrav), "An antigravity fish must add the antigrav effect.");
            Assert.That(_sources.GetTraitIdleVelocityBonus(squid), Is.EqualTo(-10f).Within(0.001f),
                "A demersal fish must sink faster in the fight.");
            Assert.That(_sources.GetTraitControlSpeedMultiplier(lubefish), Is.EqualTo(1.4f).Within(0.001f),
                "A slippery fish must make the bait harder to control.");
        });
    }

    /// <summary>
    /// A cloaked line hides the line from wary fish.
    /// </summary>
    [Test]
    public async Task CloakedLineCalmsWaryFishTest()
    {
        var rod = SEntMan.GetEntity(await PlaceInHands(RodPrototype));
        var cloaked = SEntMan.GetEntity(await Spawn("FishingLineCloaked"));

        var fishId = new ProtoId<FishPrototype>("FishSquid");
        var sourceId = new ProtoId<FishingSourcePrototype>(OceanSource);

        var normalDifficulty = 0f;
        var cloakedDifficulty = 0f;

        await Server.WaitPost(() =>
        {
            normalDifficulty = _sources.CalculateDifficulty(sourceId, fishId, rod, SPlayer);
            ReplaceSlot(rod, LineSlot, cloaked);
            cloakedDifficulty = _sources.CalculateDifficulty(sourceId, fishId, rod, SPlayer);
        });

        Assert.That(normalDifficulty - cloakedDifficulty, Is.EqualTo(8f).Within(0.01f),
            "A cloaked line must remove the wary penalty and give its own -3 difficulty.");
    }

    /// <summary>
    /// Catching a fish with the kill rule must leave the catch dead.
    /// </summary>
    [Test]
    public async Task KillRuleKillsTheCatchTest()
    {
        await SpawnTarget("FloorWaterEntity");
        await PlaceInHands(RodPrototype);
        await Interact();
        await RunTicks(5);

        var player = ToServer(Player);
        var challenge = SEntMan.GetComponent<FishingChallengeComponent>(player);

        await Server.WaitPost(() => challenge.NextPhaseTime = STiming.CurTime - TimeSpan.FromSeconds(1));
        await RunTicks(10);

        await Server.WaitPost(() =>
        {
            challenge.Reward = "FishGoldfish";
            challenge.RewardEntity = null;
            challenge.Rules = FishingMinigameRules.Kill;
            challenge.Phase = FishingPhase.Minigame;
            challenge.Completion = 99f;
            challenge.CompletionGain = 100f;
            challenge.BaitHeight = 300f;
            challenge.FishHeight = 50f;
            challenge.FishPosition = 500f;
            challenge.BaitPosition = 500f;
            challenge.BaitVelocity = 0f;
            challenge.ReelDirection = 0;
        });

        await RunTicks(20);

        var deadFish = 0;
        await Server.WaitPost(() =>
        {
            var query = SEntMan.EntityQueryEnumerator<FishComponent>();
            while (query.MoveNext(out _, out var fish))
            {
                if (!fish.Alive)
                    deadFish++;
            }
        });

        Assert.That(deadFish, Is.GreaterThan(0), "A fish caught with the kill rule must be dead.");
    }

    /// <summary>
    /// The auto-reel line must skip the bite window and start the fight right away.
    /// </summary>
    [Test]
    public async Task AutoreelLineSkipsTheBiteWindowTest()
    {
        var rod = SEntMan.GetEntity(await PlaceInHands(RodPrototype));
        var line = SEntMan.GetEntity(await Spawn("FishingLineAutoReel"));

        await Server.WaitPost(() => ReplaceSlot(rod, LineSlot, line));

        await SpawnTarget("FloorWaterEntity");
        await Interact();
        await RunTicks(5);

        var player = ToServer(Player);
        var challenge = SEntMan.GetComponent<FishingChallengeComponent>(player);

        Assert.That(challenge.Phase, Is.EqualTo(FishingPhase.Waiting),
            "The cast should start with the waiting phase.");

        await Server.WaitPost(() => challenge.NextPhaseTime = STiming.CurTime - TimeSpan.FromSeconds(1));
        await RunTicks(10);

        Assert.That(challenge.Phase, Is.EqualTo(FishingPhase.Minigame),
            "The auto-reel line must skip the bite window and start the fight right away.");
    }
}
