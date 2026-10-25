// SPDX-FileCopyrightText: 2026 Mini Station
// Мини-станция, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/ministation/mini-station-goob/master/LICENSE.TXT

using System.Collections.Generic;
using System.Linq;
using Content.IntegrationTests.Pair;
using Content.Shared._Mini.Ninja;
using Content.Shared._Mini.Ninja.Appearance;
using Content.Shared._Mini.Ninja.Components;
using Content.Shared.Inventory;
using Content.Shared.Ninja.Components;
using Robust.Server.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.Ninja;

/// <summary>
/// Checks that the suit personalisation is chosen in the Spider OS and applied on activation.
/// </summary>
[TestFixture]
public sealed class NinjaAppearanceTest
{
    private static readonly (string Slot, string Prototype)[] HardwareParts = new[]
    {
        ("eyes", "ClothingEyesVisorNinja"),
        ("head", "ClothingHeadHelmetSpaceNinja"),
        ("mask", "ClothingMaskNinja"),
        ("gloves", "ClothingHandsGlovesSpaceNinja"),
        ("shoes", "ClothingShoesSpaceNinja"),
    };

    [Test]
    public async Task AppearanceIsAppliedOnActivation()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var server = pair.Server;
        var entMan = server.EntMan;
        var ui = server.System<UserInterfaceSystem>();

        var (human, suit) = await SetupSuit(pair);
        var os = entMan.GetComponent<NinjaOsComponent>(suit);

        // Pick the blue colour and the scarf before activation.
        await server.WaitPost(() =>
        {
            ui.OpenUi(suit, NinjaOsUiKey.Key, human);
            ui.RaiseUiMessage(suit, NinjaOsUiKey.Key,
                new NinjaOsSetAppearanceMessage(NinjaColorway.Blue, NinjaStyle.Old, false) { Actor = human });
        });

        await server.WaitAssertion(() =>
        {
            Assert.That(os.PendingColorway, Is.EqualTo(NinjaColorway.Blue), "The colour was not saved in the OS");
            Assert.That(os.PendingStyle, Is.EqualTo(NinjaStyle.Old), "The style was not saved in the OS");
            Assert.That(os.PendingHelmet, Is.False, "The scarf option was not saved in the OS");
        });

        // The new style always forces the helmet.
        await server.WaitPost(() =>
        {
            ui.RaiseUiMessage(suit, NinjaOsUiKey.Key,
                new NinjaOsSetAppearanceMessage(NinjaColorway.Red, NinjaStyle.New, false) { Actor = human });
        });

        await server.WaitAssertion(() =>
        {
            Assert.That(os.PendingStyle, Is.EqualTo(NinjaStyle.New), "The new style was not saved");
            Assert.That(os.PendingHelmet, Is.True, "The new style must always use the helmet");
        });

        // Switch back to the scarf, pick every module and activate the suit.
        await server.WaitPost(() =>
        {
            ui.RaiseUiMessage(suit, NinjaOsUiKey.Key,
                new NinjaOsSetAppearanceMessage(NinjaColorway.Blue, NinjaStyle.Old, false) { Actor = human });

            for (var tier = 1; tier <= 5; tier++)
            {
                os.SelectedModules[tier] = NinjaPath.Ghost;
                os.LockedTiers.Add(tier);
            }

            ui.RaiseUiMessage(suit, NinjaOsUiKey.Key, new NinjaOsSetSuitPowerMessage(true) { Actor = human });
        });

        await server.WaitAssertion(() =>
        {
            Assert.That(os.SuitActivated, Is.True, "The suit did not activate");

            var appearance = entMan.GetComponent<NinjaAppearanceComponent>(suit);
            Assert.That(appearance.Colorway, Is.EqualTo(NinjaColorway.Blue), "The suit colour was not applied");
            Assert.That(appearance.Style, Is.EqualTo(NinjaStyle.Old), "The suit style was not applied");
            Assert.That(appearance.ScarfShown, Is.True, "The scarf was not shown");
            Assert.That(appearance.HelmetShown, Is.False, "The helmet was not hidden");
        });

        // The suit parts and the scarf must be marked for the client-side recolouring.
        var protoMan = server.ResolveDependency<IPrototypeManager>();
        foreach (var id in new[]
                 {
                     "ClothingOuterSuitSpaceNinja", "ClothingHeadHelmetSpaceNinja", "ClothingMaskNinja",
                     "ClothingHandsGlovesSpaceNinja", "ClothingShoesSpaceNinja", "ClothingNeckScarfNinja",
                     "ClothingBeltNinjaSheath", "EnergyKatana", "ClothingHeadsetNinja", "ClothingBackpackSatchelNinja",
                 })
        {
            Assert.That(protoMan.TryIndex<EntityPrototype>(id, out var prototype), Is.True, $"Missing prototype '{id}'");
            Assert.That(prototype!.Components.ContainsKey("NinjaAppearanceItem"), Is.True,
                $"Prototype '{id}' is missing the NinjaAppearanceItem marker");
        }

        Assert.That(protoMan.TryIndex<EntityPrototype>("ClothingNeckScarfNinja", out var scarf), Is.True);
        var scarfItem = (NinjaAppearanceItemComponent) scarf!.Components["NinjaAppearanceItem"].Component;
        Assert.That(scarfItem.ItemType, Is.EqualTo(NinjaAppearanceItemType.Scarf), "The scarf has the wrong appearance type");

        await pair.CleanReturnAsync();
    }

    private static async Task<(EntityUid Human, EntityUid Suit)> SetupSuit(TestPair pair)
    {
        var server = pair.Server;
        var entMan = server.EntMan;
        var inventory = server.System<InventorySystem>();
        var map = await pair.CreateTestMap();

        EntityUid human = default;
        EntityUid suit = default;

        await server.WaitPost(() =>
        {
            human = entMan.SpawnEntity("MobHuman", map.GridCoords);
            entMan.EnsureComponent<SpaceNinjaComponent>(human);

            suit = entMan.SpawnEntity("ClothingOuterSuitSpaceNinja", map.GridCoords);
            Assert.That(inventory.TryEquip(human, suit, "outerClothing", force: true), Is.True,
                "Could not equip the ninja suit");

            foreach (var (slot, prototypeId) in HardwareParts)
            {
                var part = entMan.SpawnEntity(prototypeId, map.GridCoords);
                Assert.That(inventory.TryEquip(human, part, slot, force: true), Is.True,
                    $"Could not equip '{prototypeId}' into slot '{slot}'");
            }
        });

        await server.WaitRunTicks(2);
        return (human, suit);
    }
}
