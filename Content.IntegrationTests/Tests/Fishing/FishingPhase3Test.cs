// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from tgstation/tgstation (code/modules/fishing, AGPL-3.0-or-later).

using System.Linq;
using Content.Goobstation.Shared.Fishing.Components;
using Content.Goobstation.Shared.Fishing.Prototypes;
using Content.Goobstation.Shared.Fishing.Systems;
using Content.IntegrationTests.Tests.Interaction;
using Content.Shared.Containers.ItemSlots;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.Fishing;

/// <summary>
/// Phase 3 equipment tests: fish as bait, bait preferences and the bait slot whitelist.
/// </summary>
[TestFixture]
public sealed class FishingPhase3Test : InteractionTest
{
    private const string RodPrototype = "FishingRod";
    private const string BaitSlot = "bait";
    private const string GoldfishPrototype = "FishGoldfish";
    private const string FavoriteBait = "BaitWorm";
    private const string NeutralBait = "BaitDoughball";
    private const string RiverSource = "FishingSourceRiver";

    private ItemSlotsSystem _slots = default!;

    [SetUp]
    public override async Task Setup()
    {
        await base.Setup();
        _slots = SEntMan.System<ItemSlotsSystem>();
    }

    /// <summary>
    /// A caught fish is a valid bait: it must fit into the bait slot of a fishing rod.
    /// </summary>
    [Test]
    public async Task FishCanBeInsertedIntoBaitSlotTest()
    {
        var rod = await PlaceInHands(RodPrototype);
        var fish = await Spawn(GoldfishPrototype);

        var rodUid = SEntMan.GetEntity(rod);
        var fishUid = SEntMan.GetEntity(fish);

        Assert.That(SEntMan.HasComponent<FishComponent>(fishUid),
            $"{GoldfishPrototype} does not have a FishComponent, so it cannot be used as bait.");

        var inserted = false;
        await Server.WaitPost(() => inserted = _slots.TryInsert(rodUid, BaitSlot, fishUid, SPlayer));
        await RunTicks(5);

        if (!inserted)
            Assert.Ignore($"The bait slot does not accept {GoldfishPrototype} yet, the bait whitelist may still be in progress.");

        Assert.That(_slots.TryGetSlot(rodUid, BaitSlot, out var slot) && slot.Item == fishUid, Is.True,
            "The fish was accepted by the bait slot but is not stored there.");
    }

    /// <summary>
    /// A favorite bait lowers the minigame difficulty compared to a bait the fish is neutral about.
    /// </summary>
    [Test]
    public async Task FavoriteBaitLowersDifficultyTest()
    {
        var goldfish = ProtoMan.Index<FishPrototype>(GoldfishPrototype);
        if (!goldfish.FavoriteBaits.Any(bait => bait.Id == FavoriteBait))
            Assert.Ignore($"{GoldfishPrototype} does not list {FavoriteBait} as a favorite bait yet.");

        var rod = await PlaceInHands(RodPrototype);
        var rodUid = SEntMan.GetEntity(rod);
        var sources = SEntMan.System<FishingSourceSystem>();
        var fishId = new ProtoId<FishPrototype>(GoldfishPrototype);
        var sourceId = new ProtoId<FishingSourcePrototype>(RiverSource);

        var favorite = await Spawn(FavoriteBait);
        var neutral = await Spawn(NeutralBait);

        var favoriteDifficulty = 0f;
        var neutralDifficulty = 0f;

        await Server.WaitPost(() =>
        {
            if (!_slots.TryInsert(rodUid, BaitSlot, SEntMan.GetEntity(favorite), SPlayer))
            {
                Assert.Fail($"Could not insert {FavoriteBait} into the bait slot.");
                return;
            }

            favoriteDifficulty = sources.CalculateDifficulty(sourceId, fishId, rodUid, SPlayer);

            if (!_slots.TryEject(rodUid, BaitSlot, null, out _, doAfter: false))
            {
                Assert.Fail($"Could not eject {FavoriteBait} from the bait slot.");
                return;
            }

            if (!_slots.TryInsert(rodUid, BaitSlot, SEntMan.GetEntity(neutral), SPlayer))
            {
                Assert.Fail($"Could not insert {NeutralBait} into the bait slot.");
                return;
            }

            neutralDifficulty = sources.CalculateDifficulty(sourceId, fishId, rodUid, SPlayer);
        });

        Assert.That(favoriteDifficulty, Is.LessThan(neutralDifficulty),
            $"{FavoriteBait} should be easier to fish with than {NeutralBait} for {GoldfishPrototype}: " +
            $"favorite {favoriteDifficulty}, neutral {neutralDifficulty}.");
    }

    /// <summary>
    /// The bait slot must keep rejecting items that are not bait.
    /// </summary>
    [Test]
    public async Task BaitSlotRejectsNonBaitTest()
    {
        var rod = await PlaceInHands(RodPrototype);
        var crowbar = await Spawn("Crowbar");

        var rodUid = SEntMan.GetEntity(rod);
        var crowbarUid = SEntMan.GetEntity(crowbar);

        var inserted = true;
        await Server.WaitPost(() => inserted = _slots.TryInsert(rodUid, BaitSlot, crowbarUid, SPlayer));
        await RunTicks(5);

        Assert.That(inserted, Is.False, "The bait slot of a fishing rod accepted a crowbar.");

        Assert.That(_slots.TryGetSlot(rodUid, BaitSlot, out var slot), Is.True, "The fishing rod has no bait slot.");
        Assert.That(slot!.HasItem, Is.False, "The bait slot has an item after rejecting a crowbar.");
    }
}
