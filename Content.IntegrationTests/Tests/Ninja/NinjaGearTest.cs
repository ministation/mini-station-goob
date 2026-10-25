// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using Content.Shared._Shitmed.Targeting;
using Content.Shared.Armor;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.Inventory;
using Content.Server.Station.Systems;
using Content.Shared.Roles;
using Content.Shared.Storage;
using Content.Shared.Weapons.Melee;
using Robust.Shared.Audio;
using Robust.Shared.ContentPack;
using Robust.Shared.GameObjects;
using Robust.Shared.Localization;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.IntegrationTests.Tests.Ninja;

/// <summary>
/// Checks the starting gear of the space ninja: the learning scroll, the armor of every kit part
/// and the hit sound of the energy katana.
/// </summary>
[TestFixture]
public sealed class NinjaGearTest
{
    private const string StartingGearId = "SpaceNinjaGear";
    private const string ScrollPrototypeId = "NinjutsuScroll";
    private const string KatanaPrototypeId = "EnergyKatana";
    private const string KatanaHitSound = "/Audio/_Mini/Ninja/Weapons/energykatana_hit.ogg";

    /// <summary>
    /// Inventory slot, prototype and expected slash coefficient of every armored kit part.
    /// </summary>
    private static readonly (string Slot, string Prototype, float Coefficient)[] ArmoredParts = new[]
    {
        ("outerClothing", "ClothingOuterSuitSpaceNinja", 0.8f),
        ("head", "ClothingHeadHelmetSpaceNinja", 0.8f),
        ("eyes", "ClothingEyesVisorNinja", 0.9f),
        ("mask", "ClothingMaskNinja", 0.9f),
        ("gloves", "ClothingHandsGlovesSpaceNinja", 0.9f),
        ("shoes", "ClothingShoesSpaceNinja", 0.9f),
        ("jumpsuit", "ClothingUniformJumpsuitNinja", 0.95f),
        ("neck", "ClothingNeckScarfNinja", 0.95f),
        ("ears", "ClothingHeadsetNinja", 0.95f),
        ("belt", "ClothingBeltNinjaSheath", 0.95f),
    };

    /// <summary>
    /// All equipment of the starting gear, so a regression in the kit is caught here.
    /// </summary>
    private static readonly (string Slot, string Prototype)[] ExpectedEquipment = new[]
    {
        ("jumpsuit", "ClothingUniformJumpsuitNinja"),
        ("back", "ClothingBackpackSatchelNinja"),
        ("mask", "ClothingMaskNinja"),
        ("head", "ClothingHeadHelmetSpaceNinja"),
        ("eyes", "ClothingEyesVisorNinja"),
        ("gloves", "ClothingHandsGlovesSpaceNinja"),
        ("outerClothing", "ClothingOuterSuitSpaceNinja"),
        ("shoes", "ClothingShoesSpaceNinja"),
        ("id", "NinjaPDA"),
        ("ears", "ClothingHeadsetNinja"),
        ("belt", "ClothingBeltNinjaSheath"),
        ("neck", "ClothingNeckScarfNinja"),
    };

    [Test]
    public async Task StartingGearContainsScrollAndFullKit()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var server = pair.Server;
        var entMan = server.EntMan;
        var protoMan = server.ResolveDependency<IPrototypeManager>();
        var spawning = server.System<StationSpawningSystem>();
        var map = await pair.CreateTestMap();

        Assert.That(protoMan.TryIndex<StartingGearPrototype>(StartingGearId, out var gear), Is.True,
            $"Missing starting gear prototype '{StartingGearId}'");
        Assert.That(protoMan.HasIndex<EntityPrototype>(ScrollPrototypeId), Is.True,
            $"Missing learning scroll prototype '{ScrollPrototypeId}'");

        // The scroll has to be part of the storage fill, otherwise a fresh ninja cannot learn Ninjutsu.
        Assert.That(gear!.Storage.TryGetValue("back", out var backStorage), Is.True,
            $"Starting gear '{StartingGearId}' has no storage fill for the back slot");
        Assert.That(backStorage!.Select(proto => proto.Id), Does.Contain(ScrollPrototypeId),
            $"Starting gear '{StartingGearId}' does not put '{ScrollPrototypeId}' into the back storage");

        EntityUid human = default;
        await server.WaitPost(() =>
        {
            human = entMan.SpawnEntity("MobHuman", map.GridCoords);
            spawning.EquipStartingGear(human, new ProtoId<StartingGearPrototype>(StartingGearId));
        });
        await server.WaitRunTicks(2);

        await server.WaitAssertion(() =>
        {
            var inventory = server.System<InventorySystem>();

            Assert.Multiple(() =>
            {
                foreach (var (slot, prototypeId) in ExpectedEquipment)
                {
                    Assert.That(inventory.TryGetSlotEntity(human, slot, out var equipped), Is.True,
                        $"Slot '{slot}' is empty after the starting gear was equipped");
                    Assert.That(entMan.GetComponent<MetaDataComponent>(equipped!.Value).EntityPrototype?.ID,
                        Is.EqualTo(prototypeId), $"Slot '{slot}' does not hold '{prototypeId}'");
                }

                // The scroll must actually end up inside the satchel, not just be listed in the prototype.
                Assert.That(inventory.TryGetSlotEntity(human, "back", out var back), Is.True,
                    "The ninja has no back item after the starting gear was equipped");
                Assert.That(entMan.TryGetComponent<StorageComponent>(back, out var satchel), Is.True,
                    "The ninja satchel has no StorageComponent");

                var storedIds = satchel!.StoredItems.Keys
                    .Select(item => entMan.GetComponent<MetaDataComponent>(item).EntityPrototype?.ID)
                    .ToList();
                Assert.That(storedIds, Does.Contain(ScrollPrototypeId),
                    "The learning scroll was not placed into the ninja satchel");
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task EveryKitPartHasVisibleArmor()
    {
        await using var pair = await PoolManager.GetServerClient();
        var protoMan = pair.Server.ResolveDependency<IPrototypeManager>();

        Assert.Multiple(() =>
        {
            foreach (var (slot, prototypeId, _) in ArmoredParts)
            {
                Assert.That(protoMan.TryIndex<EntityPrototype>(prototypeId, out var prototype), Is.True,
                    $"Missing kit prototype '{prototypeId}' for slot '{slot}'");
                Assert.That(prototype!.Components.TryGetValue("Armor", out var registration), Is.True,
                    $"Kit part '{prototypeId}' (slot '{slot}') has no Armor component");

                var armor = (ArmorComponent) registration.Component;
                Assert.That(armor.ShowArmorOnExamine, Is.True,
                    $"Kit part '{prototypeId}' hides its armor in examine");
                Assert.That(armor.ArmorCoverage, Is.Not.Empty,
                    $"Kit part '{prototypeId}' has an empty armor coverage, so the armor never applies");
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task FullKitAppliesArmorToDamage()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var server = pair.Server;
        var entMan = server.EntMan;
        var protoMan = server.ResolveDependency<IPrototypeManager>();
        var spawning = server.System<StationSpawningSystem>();
        var inventory = server.System<InventorySystem>();
        var damageable = server.System<DamageableSystem>();
        var map = await pair.CreateTestMap();

        EntityUid naked = default;
        EntityUid armored = default;
        await server.WaitPost(() =>
        {
            naked = entMan.SpawnEntity("MobHuman", map.GridCoords);
            armored = entMan.SpawnEntity("MobHuman", map.GridCoords);
            spawning.EquipStartingGear(armored, new ProtoId<StartingGearPrototype>(StartingGearId));
        });
        await server.WaitRunTicks(2);

        var slash = protoMan.Index<DamageTypePrototype>("Slash");
        var chestDamage = new DamageSpecifier(slash, 20);
        var headDamage = new DamageSpecifier(slash, 20);

        float nakedChest = 0, nakedHead = 0, armoredChest = 0, armoredHead = 0;

        await server.WaitAssertion(() =>
        {
            damageable.TryChangeDamage(naked, chestDamage, out var nakedChestApplied,
                targetPart: TargetBodyPart.Chest, canMiss: false);
            damageable.TryChangeDamage(naked, headDamage, out var nakedHeadApplied,
                targetPart: TargetBodyPart.Head, canMiss: false);
            damageable.TryChangeDamage(armored, chestDamage, out var armoredChestApplied,
                targetPart: TargetBodyPart.Chest, canMiss: false);
            damageable.TryChangeDamage(armored, headDamage, out var armoredHeadApplied,
                targetPart: TargetBodyPart.Head, canMiss: false);

            nakedChest = nakedChestApplied.GetTotal().Float();
            nakedHead = nakedHeadApplied.GetTotal().Float();
            armoredChest = armoredChestApplied.GetTotal().Float();
            armoredHead = armoredHeadApplied.GetTotal().Float();

            // Every worn armor piece relays its coefficients, so the aggregate has to be below 1
            // and has to match the product of the pieces.
            var ev = new CoefficientQueryEvent(SlotFlags.All);
            entMan.EventBus.RaiseLocalEvent(armored, ev);

            var expected = 1f;
            foreach (var (slot, _, coefficient) in ArmoredParts)
            {
                if (inventory.TryGetSlotEntity(armored, slot, out _))
                    expected *= coefficient;
            }

            Assert.Multiple(() =>
            {
                Assert.That(ev.DamageModifiers.Coefficients.TryGetValue("Slash", out var aggregate), Is.True,
                    "The full kit does not relay any Slash armor coefficient");
                Assert.That(aggregate, Is.EqualTo(expected).Within(0.0001f),
                    "The aggregate armor coefficient does not match the product of the worn kit parts");
                Assert.That(aggregate, Is.LessThan(0.7f),
                    "The full kit must not exceed the planned total armor budget");

                // The suit covers the chest for 0.8 and the jumpsuit for 0.95: 20 * 0.8 * 0.95 = 15.2.
                Assert.That(nakedChest, Is.EqualTo(20f).Within(0.5f), "The naked chest took unexpected damage");
                Assert.That(armoredChest, Is.EqualTo(20f * 0.8f * 0.95f).Within(0.6f),
                    "The chest damage was not reduced by the suit and the jumpsuit");

                // Head: helmet 0.8, visor 0.9, mask 0.9, headset 0.95 and scarf 0.95.
                Assert.That(nakedHead, Is.EqualTo(20f).Within(0.5f), "The naked head took unexpected damage");
                Assert.That(armoredHead, Is.EqualTo(20f * 0.8f * 0.9f * 0.9f * 0.95f * 0.95f).Within(0.6f),
                    "The head damage was not reduced by the helmet, visor, mask, headset and scarf");
                Assert.That(armoredChest, Is.LessThan(nakedChest), "The full kit did not reduce chest damage");
                Assert.That(armoredHead, Is.LessThan(nakedHead), "The full kit did not reduce head damage");
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task EnergyKatanaHasEnergyHitSound()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var protoMan = server.ResolveDependency<IPrototypeManager>();
        var resMan = server.ResolveDependency<IResourceManager>();

        Assert.That(protoMan.TryIndex<EntityPrototype>(KatanaPrototypeId, out var katana), Is.True,
            $"Missing '{KatanaPrototypeId}' prototype");
        Assert.That(katana!.Components.TryGetValue("MeleeWeapon", out var registration), Is.True,
            $"'{KatanaPrototypeId}' has no MeleeWeapon component");

        var melee = (MeleeWeaponComponent) registration.Component;
        Assert.That(melee.HitSound, Is.Not.Null, $"'{KatanaPrototypeId}' has no hit sound");
        Assert.That(melee.HitSound, Is.InstanceOf<SoundPathSpecifier>(),
            $"'{KatanaPrototypeId}' hit sound is not a sound path");

        var path = ((SoundPathSpecifier) melee.HitSound!).Path;
        Assert.That(path, Is.EqualTo(new ResPath(KatanaHitSound)),
            $"'{KatanaPrototypeId}' does not use the ninja hit sound");
        Assert.That(resMan.ContentFileExists(path), Is.True, $"Hit sound file '{path}' does not exist");

        // Guard against a broken or empty audio file: an OGG Vorbis stream starts with the OggS magic.
        using var stream = resMan.ContentFileRead(path);
        var header = new byte[4];
        Assert.That(stream.Read(header, 0, header.Length), Is.EqualTo(4), "The hit sound file is too short");
        Assert.That(System.Text.Encoding.ASCII.GetString(header), Is.EqualTo("OggS"),
            $"Hit sound file '{path}' is not an Ogg stream");

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task SnakeDescriptionsAreLocalized()
    {
        await using var pair = await PoolManager.GetServerClient();
        var locMan = pair.Server.ResolveDependency<ILocalizationManager>();

        string[] keys =
        {
            "ninja-module-snake-healing-cocktail-desc",
            "ninja-module-snake-caltrop-desc",
            "ent-ActionNinjaHealingCocktail",
            "ent-NinjaCaltropAction",
            "ninja-caltrop-limit",
            "ninja-caltrop-no-room",
        };

        Assert.Multiple(() =>
        {
            foreach (var key in keys)
                NinjaTestHelpers.AssertLocalizedInBothCultures(locMan, key);
        });

        await pair.CleanReturnAsync();
    }
}
