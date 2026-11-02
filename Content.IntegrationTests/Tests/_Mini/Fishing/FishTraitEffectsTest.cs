// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from tgstation/tgstation (code/modules/fishing, AGPL-3.0-or-later).

#nullable enable

using System.Collections.Generic;
using System.Linq;
using Content.IntegrationTests.Tests.Interaction;
using Content.Server._Mini.Fishing.Traits;
using Content.Shared._Mini.Fishing.Components;
using Content.Shared._Mini.Fishing.Systems;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Damage;
using Content.Shared.Electrocution;
using Content.Shared.Eye.Blinding.Components;
using Content.Shared.Slippery;
using Content.Shared.StepTrigger.Components;
using Content.Shared.Weapons.Melee;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.GameObjects;
using Robust.Shared.Physics.Components;

namespace Content.IntegrationTests.Tests._Mini.Fishing;

/// <summary>
/// Tests the trait effects that live on the caught fish item: slippery lube, stinger venom, ink and revival.
/// </summary>
[TestFixture]
public sealed class FishTraitEffectsTest : InteractionTest
{
    private const string LubedFish = "FishClownfishLube";
    private const string StingerFish = "FishSwordfish";
    private const string VenomFish = "FishStingray";
    private const string InkFish = "FishSquid";
    private const string ElectricFish = "FishZipzap";
    private const string RevivalFish = "FishBoned";
    private const string TargetMob = "MobHuman";

    private SharedSolutionContainerSystem _solutions = default!;

    [SetUp]
    public override async Task Setup()
    {
        await base.Setup();
        _solutions = SEntMan.System<SharedSolutionContainerSystem>();
    }

    /// <summary>
    /// Traits that give the fish item extra components must do so when the fish spawns.
    /// </summary>
    [Test]
    public async Task TraitComponentsAppliedTest()
    {
        var lubed = SEntMan.GetEntity(await Spawn(LubedFish));
        var stinger = SEntMan.GetEntity(await Spawn(StingerFish));
        var electric = SEntMan.GetEntity(await Spawn(ElectricFish));

        Assert.Multiple(() =>
        {
            Assert.That(SEntMan.HasComponent<SlipperyComponent>(lubed), Is.True,
                "A lubed fish must be slippery.");
            Assert.That(SEntMan.HasComponent<StepTriggerComponent>(lubed), Is.True,
                "A slippery fish needs a step trigger to slip people.");
            Assert.That(SEntMan.HasComponent<PhysicsComponent>(lubed), Is.True,
                "A slippery fish needs physics to be stepped on.");
        });

        Assert.That(SEntMan.TryGetComponent<MeleeWeaponComponent>(stinger, out var melee), Is.True,
            "A stinger fish must be able to attack.");
        Assert.That(melee!.Damage.DamageDict.ContainsKey("Piercing"), Is.True,
            "A stinger fish must deal piercing damage.");

        Assert.That(SEntMan.TryGetComponent<ElectrifiedComponent>(electric, out var electrified), Is.True,
            "An electrogenesis fish must be electrified.");
        Assert.Multiple(() =>
        {
            Assert.That(electrified!.RequirePower, Is.False, "A fish does not need a power net to shock.");
            Assert.That(electrified.Enabled, Is.True, "A live electrogenesis fish shocks.");
        });
    }

    /// <summary>
    /// A stinger fish with a venom trait injects its reagent into whoever it hits.
    /// </summary>
    [Test]
    public async Task StingerInjectsVenomTest()
    {
        var fish = SEntMan.GetEntity(await Spawn(VenomFish));
        var target = SEntMan.GetEntity(await SpawnTarget(TargetMob));

        Solution? injected = null;
        await Server.WaitPost(() =>
        {
            var hit = new MeleeHitEvent(new List<EntityUid> { target }, SPlayer, fish, new DamageSpecifier(), null,
                SEntMan.GetComponent<TransformComponent>(target).Coordinates);
            SEntMan.EventBus.RaiseLocalEvent(fish, hit);

            if (_solutions.TryGetInjectableSolution(target, out _, out var solution))
                injected = solution;
        });

        Assert.That(injected, Is.Not.Null, "The target has no injectable solution to check the venom in.");
        Assert.That(injected!.Contents.Any(reagent => reagent.Reagent.Prototype == "Toxin"), Is.True,
            "The toxic barbs of a stinger fish must inject toxin on hit.");
    }

    /// <summary>
    /// An ink fish blinds whoever it hits, once per cooldown.
    /// </summary>
    [Test]
    public async Task InkBlindsTargetTest()
    {
        var fish = SEntMan.GetEntity(await Spawn(InkFish));
        var target = SEntMan.GetEntity(await SpawnTarget(TargetMob));

        await Server.WaitPost(() =>
        {
            var hit = new MeleeHitEvent(new List<EntityUid> { target }, SPlayer, fish, new DamageSpecifier(), null,
                SEntMan.GetComponent<TransformComponent>(target).Coordinates);
            SEntMan.EventBus.RaiseLocalEvent(fish, hit);
        });

        Assert.That(SEntMan.HasComponent<TemporaryBlindnessComponent>(target), Is.True,
            "An ink fish must blind whoever it hits.");
    }

    /// <summary>
    /// A fish with the Revival trait comes back to life on its own after the tg delay.
    /// </summary>
    [Test]
    public async Task RevivalBringsFishBackTest()
    {
        var fish = SEntMan.GetEntity(await Spawn(RevivalFish));
        var fishSystem = SEntMan.System<FishSystem>();
        var traitSystem = SEntMan.System<FishTraitSystem>();

        await Server.WaitPost(() =>
        {
            var comp = SEntMan.GetComponent<FishComponent>(fish);
            fishSystem.SetAlive((fish, comp), false);
            Assert.That(comp.Alive, Is.False);

            // Shortcut past the 60 to 120 seconds tg timer instead of running thousands of ticks.
            traitSystem.ReviveAt[fish] = STiming.CurTime;
            traitSystem.Update(1.1f);
        });

        var revived = false;
        await Server.WaitPost(() => revived = SEntMan.GetComponent<FishComponent>(fish).Alive);

        Assert.That(revived, Is.True, "A revival fish must come back to life when its timer runs out.");
    }
}
