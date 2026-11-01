// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from tgstation/tgstation (code/modules/fishing, AGPL-3.0-or-later).

using System.Collections.Generic;
using Content.IntegrationTests.Tests.Interaction;
using Content.Server._Mini.Fishing.Aquarium;
using Content.Shared._Mini.Fishing;
using Content.Shared._Mini.Fishing.Aquarium;
using Content.Shared._Mini.Fishing.Components;
using Content.Shared._Mini.Fishing.Systems;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._Mini.Fishing;

/// <summary>
/// Tests the fish trait behaviour that only runs while the fish lives in an aquarium.
/// </summary>
[TestFixture]
public sealed class AquariumTraitBiologyTest : InteractionTest
{
    private const string AquariumPrototype = "Aquarium";
    private const string AnxiousFish = "FishZipzap";
    private const string PlainFish = "FishAngelfish";
    private const string SecondPlainFish = "FishGoldfish";
    private const string NecrophageFish = "FishRatfish";
    private const string YuckyFish = "FishDonkfish";
    private const string EmulsifierFish = "FishEmulsijack";
    private const string ImmuneFish = "FishSlimefish";
    private const string MixotrophFish = "FishStarfish";

    private SharedAquariumSystem _aquarium = default!;
    private AquariumSystem _aquariumSystem = default!;
    private FishSystem _fish = default!;
    private SharedContainerSystem _container = default!;

    [SetUp]
    public override async Task Setup()
    {
        await base.Setup();
        _aquarium = SEntMan.System<SharedAquariumSystem>();
        _aquariumSystem = SEntMan.System<AquariumSystem>();
        _fish = SEntMan.System<FishSystem>();
        _container = SEntMan.System<SharedContainerSystem>();
    }

    /// <summary>
    /// An anxious fish dies as soon as it has to share a tank with more than three fish.
    /// </summary>
    [Test]
    public async Task AnxietyKillsCrowdedFishTest()
    {
        var (tank, fishes) = await SpawnTank(AnxiousFish, PlainFish, PlainFish, PlainFish);

        await TickTraitEffects(tank, fishes, 1f);

        var anxiousAlive = await Alive(fishes[0]);
        var otherAlive = await Alive(fishes[1]);
        Assert.Multiple(() =>
        {
            Assert.That(anxiousAlive, Is.False, "An anxious fish must die in a crowded tank.");
            Assert.That(otherAlive, Is.True, "The other fish must survive.");
        });
    }

    /// <summary>
    /// A hungry necrophage eats a dead fish and refuses to eat a yucky one.
    /// </summary>
    [Test]
    public async Task NecrophageEatsDeadFishTest()
    {
        var (tank, fishes) = await SpawnTank(NecrophageFish, PlainFish);
        await PrepareNecrophage(fishes[0], fishes[1]);

        await TickTraitEffects(tank, fishes, 1f);
        await RunTicks(2);

        Assert.That(SEntMan.EntityExists(fishes[1]), Is.False, "The carcass must be eaten by the necrophage.");

        var satiated = false;
        await Server.WaitPost(() =>
        {
            var comp = SEntMan.GetComponent<FishComponent>(fishes[0]);
            var proto = _fish.GetProto(comp);
            satiated = _aquarium.GetHunger(comp, proto, STiming.CurTime) < 0.1f;
        });
        Assert.That(satiated, Is.True, "Eating a carcass must sate the necrophage.");
    }

    /// <summary>
    /// Yucky fish are never eaten by necrophages.
    /// </summary>
    [Test]
    public async Task NecrophageIgnoresYuckyFishTest()
    {
        var (tank, fishes) = await SpawnTank(NecrophageFish, YuckyFish);
        await PrepareNecrophage(fishes[0], fishes[1]);

        await TickTraitEffects(tank, fishes, 1f);
        await RunTicks(2);

        Assert.That(SEntMan.EntityExists(fishes[1]), Is.True, "Yucky carcasses must be left alone.");
    }

    /// <summary>
    /// An emulsifier kills every fish around it, except those with toxin immunity.
    /// </summary>
    [Test]
    public async Task EmulsijackSparesToxinImmuneFishTest()
    {
        var (tank, fishes) = await SpawnTank(EmulsifierFish, PlainFish, ImmuneFish);

        await TickTraitEffects(tank, fishes, 40f);

        var victimAlive = await Alive(fishes[1]);
        var immuneAlive = await Alive(fishes[2]);
        Assert.Multiple(() =>
        {
            Assert.That(victimAlive, Is.False, "The emulsifier must kill unprotected fish in the tank.");
            Assert.That(immuneAlive, Is.True, "Toxin immunity must protect from the emulsifier.");
        });
    }

    /// <summary>
    /// A territorial fish in a tank with five or more fish attacks its tank mates.
    /// </summary>
    [Test]
    public async Task TerritorialAttacksInCrowdedTankTest()
    {
        var (tank, fishes) = await SpawnTank(PlainFish, SecondPlainFish, SecondPlainFish, SecondPlainFish,
            SecondPlainFish);

        await TickTraitEffects(tank, fishes, 100f);

        var health = 0f;
        await Server.WaitPost(() => health = _aquariumSystem.GetFishHealth(fishes[1]));
        Assert.That(health, Is.LessThan(100f), "A territorial fish must hurt the other fish in a crowded tank.");
    }

    /// <summary>
    /// A mixotroph never grows hungry, unlike a regular fish.
    /// </summary>
    [Test]
    public async Task MixotrophNeverStarvesTest()
    {
        var mixotroph = SEntMan.GetEntity(await Spawn(MixotrophFish));
        var plain = SEntMan.GetEntity(await Spawn(SecondPlainFish));

        var mixotrophHunger = 1f;
        var plainHunger = 0f;
        await Server.WaitPost(() =>
        {
            var old = STiming.CurTime - TimeSpan.FromHours(10);
            var mixotrophComp = SEntMan.GetComponent<FishComponent>(mixotroph);
            mixotrophComp.LastFed = old;
            SEntMan.Dirty(mixotroph, mixotrophComp);

            var plainComp = SEntMan.GetComponent<FishComponent>(plain);
            plainComp.LastFed = old;
            SEntMan.Dirty(plain, plainComp);

            mixotrophHunger = _aquarium.GetHunger(mixotrophComp, _fish.GetProto(mixotrophComp), STiming.CurTime);
            plainHunger = _aquarium.GetHunger(plainComp, _fish.GetProto(plainComp), STiming.CurTime);
        });

        Assert.Multiple(() =>
        {
            Assert.That(mixotrophHunger, Is.EqualTo(0f), "A mixotroph must never be hungry.");
            Assert.That(plainHunger, Is.GreaterThan(0f), "The control fish must be hungry.");
        });
    }

    /// <summary>
    /// A stunted fish does not grow inside a tank, while a regular fish does.
    /// </summary>
    [Test]
    public async Task StuntedFishDoesNotGrowTest()
    {
        var (tank, fishes) = await SpawnTank(PlainFish, PlainFish);

        var stuntedQuid = fishes[0];
        var regularUid = fishes[1];
        float stuntedBefore = 0f, regularBefore = 0f;
        await Server.WaitPost(() =>
        {
            _fish.AddTrait((stuntedQuid, SEntMan.GetComponent<FishComponent>(stuntedQuid)), "stunted");
            stuntedBefore = SEntMan.GetComponent<FishComponent>(stuntedQuid).Size;
            regularBefore = SEntMan.GetComponent<FishComponent>(regularUid).Size;
        });

        await Server.WaitPost(() =>
            _aquariumSystem.TickAquarium(Tank(tank), 3600f, STiming.CurTime));

        float stuntedAfter = 0f, regularAfter = 0f;
        await Server.WaitPost(() =>
        {
            stuntedAfter = SEntMan.GetComponent<FishComponent>(stuntedQuid).Size;
            regularAfter = SEntMan.GetComponent<FishComponent>(regularUid).Size;
        });

        Assert.Multiple(() =>
        {
            Assert.That(stuntedAfter, Is.EqualTo(stuntedBefore).Within(0.01f),
                "A stunted fish must not grow.");
            Assert.That(regularAfter, Is.GreaterThan(regularBefore),
                "The control fish must grow.");
        });
    }

    private async Task<(EntityUid Tank, List<EntityUid> Fishes)> SpawnTank(params string[] prototypes)
    {
        var tank = SEntMan.GetEntity(await SpawnTarget(AquariumPrototype));
        var fishes = new List<EntityUid>();
        foreach (var proto in prototypes)
            fishes.Add(SEntMan.GetEntity(await Spawn(proto)));

        await Server.WaitPost(() =>
        {
            var ent = Tank(tank);
            Assert.That(_aquarium.TryGetFishContainer(ent, out var container), Is.True,
                "The aquarium has no fish container.");
            foreach (var fish in fishes)
                Assert.That(_container.Insert(fish, container!), Is.True, "Failed to put the fish into the tank.");
        });

        return (tank, fishes);
    }

    private Entity<AquariumComponent> Tank(EntityUid tank)
    {
        return (tank, SEntMan.GetComponent<AquariumComponent>(tank));
    }

    private async Task TickTraitEffects(EntityUid tank, List<EntityUid> fishes, float seconds)
    {
        await Server.WaitPost(() =>
        {
            var ent = Tank(tank);
            _aquariumSystem.TickTraitEffects(ent, _aquarium.GetFish(ent), seconds, STiming.CurTime);
        });
    }

    private async Task PrepareNecrophage(EntityUid necrophage, EntityUid carcass)
    {
        await Server.WaitPost(() =>
        {
            var carcassComp = SEntMan.GetComponent<FishComponent>(carcass);
            _fish.SetAlive((carcass, carcassComp), false);

            var necrophageComp = SEntMan.GetComponent<FishComponent>(necrophage);
            necrophageComp.LastFed = STiming.CurTime - TimeSpan.FromHours(1);
            SEntMan.Dirty(necrophage, necrophageComp);
        });
    }

    private async Task<bool> Alive(EntityUid fish)
    {
        var alive = false;
        await Server.WaitPost(() => alive = SEntMan.GetComponent<FishComponent>(fish).Alive);
        return alive;
    }
}
