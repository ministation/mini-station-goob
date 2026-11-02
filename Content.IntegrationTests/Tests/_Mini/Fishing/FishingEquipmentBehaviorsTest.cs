// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from tgstation/tgstation (code/modules/fishing, AGPL-3.0-or-later).

using System.Numerics;
using Content.Server._Mini.Fishing;
using Content.Shared._Mini.Fishing;
using Content.Shared._Mini.Fishing.Components;
using Content.Shared._Mini.Fishing.Events;
using Content.Shared._Mini.Fishing.Prototypes;
using Content.Shared._Mini.Fishing.Systems;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Interaction;
using Content.Shared.Materials;
using Content.IntegrationTests.Tests.Interaction;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Mini.Fishing;

/// <summary>
/// Equipment parity with tg: lava proof lines, the automatic reel pulling in snagged items,
/// the magnetic hook, anomalous lure hooks and poisonous bait.
/// </summary>
[TestFixture]
public sealed class FishingEquipmentBehaviorsTest : InteractionTest
{
    private const string RodPrototype = "FishingRod";
    private const string HookSlot = "hook";
    private const string LineSlot = "line";
    private const string BaitSlot = "bait";
    private const string AnomalySlot = "anomaly";
    private const string LavaPrototype = "FloorLavaEntity";
    private const string OceanSource = "FishingSourceOcean";
    private const string LavaSource = "FishingSourceLava";

    private ItemSlotsSystem _slots = default!;
    private FishingEquipmentSystem _equipment = default!;

    [SetUp]
    public override async Task Setup()
    {
        await base.Setup();
        _slots = SEntMan.System<ItemSlotsSystem>();
        _equipment = SEntMan.System<FishingEquipmentSystem>();
    }

    private void ReplaceSlot(EntityUid rod, string slot, EntityUid? item)
    {
        _slots.TryEject(rod, slot, null, out _, doAfter: false);

        if (item is { } itemUid)
            Assert.That(_slots.TryInsert(rod, slot, itemUid, SPlayer), Is.True, $"Could not insert into the {slot} slot.");
    }

    private void RaiseAfterInteract(EntityUid rod, EntityUid target)
    {
        SEntMan.EventBus.RaiseLocalEvent(rod,
            new AfterInteractEvent(SPlayer, rod, target, Xform(target).Coordinates, true));
    }

    private float DistanceToPlayer(EntityUid uid)
    {
        return (Transform.GetWorldPosition(Xform(uid)) - Transform.GetWorldPosition(Xform(SPlayer))).Length();
    }

    private FishFightModifiersEvent FightModifiers(EntityUid rod)
    {
        var ev = new FishFightModifiersEvent(10f);
        SEntMan.EventBus.RaiseLocalEvent(rod, ref ev);
        return ev;
    }

    private FishLootModifiersEvent LootModifiers(EntityUid rod)
    {
        var ev = new FishLootModifiersEvent(SPlayer, rod, new ProtoId<FishingSourcePrototype>(OceanSource));
        SEntMan.EventBus.RaiseLocalEvent(rod, ref ev);
        return ev;
    }

    [Test]
    public async Task OrdinaryLineCannotFishLava()
    {
        var rod = SEntMan.GetEntity(await PlaceInHands(RodPrototype));
        var lava = SEntMan.GetEntity(await SpawnTarget(LavaPrototype));

        await Server.WaitPost(() =>
        {
            RaiseAfterInteract(rod, lava);

            Assert.That(SEntMan.GetComponent<FishingRodComponent>(rod).FishingLure, Is.Null,
                "An ordinary line must not survive lava.");
            Assert.That(SEntMan.HasComponent<FishingChallengeComponent>(SPlayer), Is.False,
                "An ordinary line must not start a fishing challenge in lava.");
        });
    }

    [Test]
    public async Task ReinforcedLineCanFishLava()
    {
        var rod = SEntMan.GetEntity(await PlaceInHands(RodPrototype));
        var line = SEntMan.GetEntity(await Spawn("FishingLineReinforced"));
        var lava = SEntMan.GetEntity(await SpawnTarget(LavaPrototype));

        await Server.WaitPost(() =>
        {
            ReplaceSlot(rod, LineSlot, line);
            RaiseAfterInteract(rod, lava);

            Assert.That(SEntMan.GetComponent<FishingRodComponent>(rod).FishingLure, Is.Not.Null,
                "A reinforced line must allow fishing in lava.");
            Assert.That(SEntMan.GetComponent<FishingChallengeComponent>(SPlayer).Source.Id, Is.EqualTo(LavaSource));
        });
    }

    [Test]
    public async Task SinewLineCanFishLava()
    {
        var rod = SEntMan.GetEntity(await PlaceInHands(RodPrototype));
        var line = SEntMan.GetEntity(await Spawn("FishingLineSinew"));
        var lava = SEntMan.GetEntity(await SpawnTarget(LavaPrototype));

        await Server.WaitPost(() =>
        {
            ReplaceSlot(rod, LineSlot, line);
            RaiseAfterInteract(rod, lava);

            Assert.That(SEntMan.GetComponent<FishingRodComponent>(rod).FishingLure, Is.Not.Null,
                "A sinew line must allow fishing in lava.");
        });
    }

    [Test]
    public async Task AutoReelLinePullsInSnaggedItems()
    {
        var rod = SEntMan.GetEntity(await PlaceInHands(RodPrototype));
        var line = SEntMan.GetEntity(await Spawn("FishingLineAutoReel"));
        var item = SEntMan.GetEntity(await Spawn("Crowbar"));

        await Server.WaitPost(() =>
        {
            ReplaceSlot(rod, LineSlot, line);
            Transform.SetCoordinates(item, Xform(SPlayer).Coordinates.Offset(new Vector2(1.5f, 0f)));
            RaiseAfterInteract(rod, item);

            Assert.That(SEntMan.GetComponent<FishingRodComponent>(rod).FishingLure, Is.Null,
                "The auto-reel line must reel the snagged item in right away.");
        });

        Assert.That(DistanceToPlayer(item), Is.LessThan(1.5f),
            "The auto-reel line must pull the snagged item towards the fisher.");
    }

    [Test]
    public async Task MagnetHookRemovesDudsAndBiasesLoot()
    {
        var rod = SEntMan.GetEntity(await PlaceInHands(RodPrototype));
        var hook = SEntMan.GetEntity(await Spawn("FishingHookMagnet"));
        var lure = SEntMan.GetEntity(await Spawn("FishingLureMinnow"));

        await Server.WaitPost(() =>
        {
            ReplaceSlot(rod, HookSlot, hook);
            ReplaceSlot(rod, BaitSlot, lure);

            var loot = LootModifiers(rod);
            Assert.That(loot.RemoveDuds, Is.True, "The magnetic hook must remove duds from the table.");
            Assert.That(loot.FishWeightMultiplier, Is.EqualTo(0.4f).Within(0.001f),
                "The magnetic hook must keep the tg bias towards non-fish catches.");
        });
    }

    [Test]
    public async Task AnomalyHookCoreMappingAndGravity()
    {
        var rod = SEntMan.GetEntity(await PlaceInHands(RodPrototype));
        var hook = SEntMan.GetEntity(await Spawn("FishingHookAnomaly"));
        var bait = SEntMan.GetEntity(await Spawn("BaitDoughballSuper"));
        var gravity = SEntMan.GetEntity(await Spawn("AnomalyCoreGravity"));
        var bluespace = SEntMan.GetEntity(await Spawn("AnomalyCoreBluespace"));
        var flesh = SEntMan.GetEntity(await Spawn("AnomalyCoreFlesh"));

        await Server.WaitPost(() =>
        {
            ReplaceSlot(rod, HookSlot, hook);
            ReplaceSlot(rod, BaitSlot, bait);

            Assert.That(_slots.TryInsert(hook, AnomalySlot, gravity, SPlayer), Is.True);
            Assert.That(SEntMan.GetComponent<FishingHookComponent>(hook).AnomalyCoreInstalled, Is.True,
                "Slotting a core must update the hook overlay state.");
            Assert.That(_equipment.GetAnomalyEffect(hook), Is.EqualTo(FishingAnomalyEffect.Gravity));
            Assert.That(_equipment.GetAnomalyEffectChance(rod), Is.EqualTo(41f).Within(0.001f),
                "Great bait must add sixteen percent to the base twenty five percent chance.");
            Assert.That(FightModifiers(rod).Gravity, Is.EqualTo(0.4f).Within(0.001f),
                "A gravity core must make the lure sink slower.");

            _slots.TryEject(hook, AnomalySlot, null, out _);
            Assert.That(_slots.TryInsert(hook, AnomalySlot, bluespace, SPlayer), Is.True);
            Assert.That(_equipment.GetAnomalyEffect(hook), Is.EqualTo(FishingAnomalyEffect.DimensionalMaterials));

            _slots.TryEject(hook, AnomalySlot, null, out _);
            Assert.That(_slots.TryInsert(hook, AnomalySlot, flesh, SPlayer), Is.True);
            Assert.That(_equipment.GetAnomalyEffect(hook), Is.EqualTo(FishingAnomalyEffect.Bioscramble));
        });
    }

    [Test]
    public async Task AnomalyBluespaceCoreAltersCatchMaterials()
    {
        var rod = SEntMan.GetEntity(await PlaceInHands(RodPrototype));
        var hook = SEntMan.GetEntity(await Spawn("FishingHookAnomaly"));
        var bait = SEntMan.GetEntity(await Spawn("BaitDoughballSuper"));
        var core = SEntMan.GetEntity(await Spawn("AnomalyCoreBluespace"));
        var fish = SEntMan.GetEntity(await Spawn("FishAngelfish"));

        await Server.WaitPost(() =>
        {
            ReplaceSlot(rod, HookSlot, hook);
            ReplaceSlot(rod, BaitSlot, bait);
            Assert.That(_slots.TryInsert(hook, AnomalySlot, core, SPlayer), Is.True);

            var applied = false;
            for (var i = 0; i < 200 && !applied; i++)
                applied = _equipment.TryApplyAnomalyCatchEffect(rod, fish);

            Assert.That(applied, Is.True, "The dimensional core effect must trigger within the chance window.");
            Assert.That(SEntMan.TryGetComponent<PhysicalCompositionComponent>(fish, out var composition), Is.True,
                "The bluespace core must give the catch a material composition.");
            Assert.That(composition!.MaterialComposition, Is.Not.Empty);
            Assert.That(composition.MaterialComposition.Count, Is.InRange(1, 3));
        });
    }

    [Test]
    public async Task AnomalyFleshCoreBioscramblesCatch()
    {
        var rod = SEntMan.GetEntity(await PlaceInHands(RodPrototype));
        var hook = SEntMan.GetEntity(await Spawn("FishingHookAnomaly"));
        var bait = SEntMan.GetEntity(await Spawn("BaitDoughballSuper"));
        var core = SEntMan.GetEntity(await Spawn("AnomalyCoreFlesh"));
        var fish = SEntMan.GetEntity(await Spawn("FishAngelfish"));

        await Server.WaitPost(() =>
        {
            ReplaceSlot(rod, HookSlot, hook);
            ReplaceSlot(rod, BaitSlot, bait);
            Assert.That(_slots.TryInsert(hook, AnomalySlot, core, SPlayer), Is.True);

            var applied = false;
            for (var i = 0; i < 200 && !applied; i++)
                applied = _equipment.TryApplyAnomalyCatchEffect(rod, fish);

            Assert.That(applied, Is.True, "The bioscrambler core effect must trigger within the chance window.");
            Assert.That(SEntMan.GetComponent<FishComponent>(fish).ExtraTraits, Is.Not.Empty,
                "The flesh core must inject a random fish trait.");
        });
    }

    [Test]
    public async Task PoisonousBaitKillsOnlyUnfavoredFish()
    {
        var rod = SEntMan.GetEntity(await PlaceInHands(RodPrototype));
        var bait = SEntMan.GetEntity(await Spawn("BaitWorm"));
        var favored = SEntMan.GetEntity(await Spawn("FishGoldfish"));
        var unfavored = SEntMan.GetEntity(await Spawn("FishAngelfish"));
        var immune = SEntMan.GetEntity(await Spawn("FishSlimefish"));

        await Server.WaitPost(() =>
        {
            ReplaceSlot(rod, BaitSlot, bait);
            var fishing = SEntMan.System<FishingSystem>();

            Assert.That(fishing.ShouldPoisonKillCatch(rod, unfavored), Is.False,
                "Ordinary bait must not poison any catch.");

            SEntMan.GetComponent<FishingBaitComponent>(bait).Poisonous = true;
            Assert.That(fishing.ShouldPoisonKillCatch(rod, unfavored), Is.True,
                "Poisonous bait must kill a fish that does not favour it.");
            Assert.That(fishing.ShouldPoisonKillCatch(rod, favored), Is.False,
                "A fish that favours the bait must survive it.");
            Assert.That(fishing.ShouldPoisonKillCatch(rod, immune), Is.False,
                "Toxin immune fish must survive poisonous bait.");
        });
    }

    [Test]
    public async Task RodEquipmentModifiersAreApplied()
    {
        var rod = SEntMan.GetEntity(await PlaceInHands(RodPrototype));
        var gyro = SEntMan.GetEntity(await Spawn("FishingHookGyro"));
        var weighted = SEntMan.GetEntity(await Spawn("FishingHookWeighted"));
        var bouncy = SEntMan.GetEntity(await Spawn("FishingLineBouncy"));
        var cloaked = SEntMan.GetEntity(await Spawn("FishingLineCloaked"));
        var bluespace = SEntMan.GetEntity(await Spawn("FishingLineBluespace"));

        await Server.WaitPost(() =>
        {
            var rodComp = SEntMan.GetComponent<FishingRodComponent>(rod);
            var baseRange = rodComp.CastRange;

            ReplaceSlot(rod, HookSlot, gyro);
            var gyroMods = FightModifiers(rod);
            Assert.That(gyroMods.Gravity, Is.EqualTo(0f),
                "The gyro-stabilized hook must remove the gravity of the lure.");
            Assert.That(gyroMods.Rules & FishingMinigameRules.Bidirectional, Is.Not.EqualTo(FishingMinigameRules.None),
                "The gyro-stabilized hook must allow reeling in both directions.");

            ReplaceSlot(rod, HookSlot, weighted);
            Assert.That(FightModifiers(rod).Bounciness, Is.EqualTo(0.5f).Within(0.001f),
                "The weighted hook must halve the bounciness of the lure.");

            ReplaceSlot(rod, LineSlot, bouncy);
            Assert.That(FightModifiers(rod).CompletionLoss, Is.EqualTo(3.6f).Within(0.001f),
                "The flexible line must reduce the progress lost when the bait slips.");
            Assert.That(rodComp.CastRange, Is.EqualTo(baseRange + 1f).Within(0.001f),
                "The flexible line must add one extra tile of casting range.");

            ReplaceSlot(rod, LineSlot, cloaked);
            Assert.That(FightModifiers(rod).Difficulty, Is.EqualTo(7f).Within(0.001f),
                "The cloaked line must lower the difficulty by three.");

            ReplaceSlot(rod, LineSlot, bluespace);
            Assert.That(rodComp.CastRange, Is.EqualTo(baseRange + 4f).Within(0.001f),
                "The bluespace line must add four extra tiles of casting range over the default line.");
        });
    }
}
