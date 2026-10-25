// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections.Generic;
using System.Linq;
using Content.Shared._Mini.Ninja;
using Content.Shared._Mini.Ninja.Base.Refill;
using Content.Shared._Mini.Ninja.Components;
using Content.Shared._Mini.Ninja.Prototypes;
using Content.Shared.MedicalScanner;
using Content.Shared.Movement.Components;
using Content.Shared.Power.Components;
using Content.Shared.PowerCell;
using Robust.Server.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.Ninja;

/// <summary>
/// Checks the ninja suit prototype, the hardware markers of the suit parts and the cell it spawns with.
/// </summary>
[TestFixture]
public sealed class NinjaSuitTest
{
    private const string SuitPrototypeId = "ClothingOuterSuitSpaceNinja";

    /// <summary>
    /// Suit part prototype per inventory slot. Every part must carry the NinjaSuitItem marker.
    /// </summary>
    private static readonly (string Slot, string Prototype)[] HardwareParts = new[]
    {
        ("eyes", "ClothingEyesVisorNinja"),
        ("head", "ClothingHeadHelmetSpaceNinja"),
        ("mask", "ClothingMaskNinja"),
        ("gloves", "ClothingHandsGlovesSpaceNinja"),
        ("shoes", "ClothingShoesSpaceNinja"),
    };

    [Test]
    public async Task SuitPrototypeHasBaseModules()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var protoMan = server.ResolveDependency<IPrototypeManager>();

        Assert.That(protoMan.TryIndex<EntityPrototype>(SuitPrototypeId, out var suit), Is.True,
            $"Missing suit prototype '{SuitPrototypeId}'");

        string[] requiredComponents =
        {
            "NinjaSuit",
            "NinjaOs",
            "NinjaSuitItem",
            "NinjaAutoDust",
            "NinjaSelfHealthAnalyzer",
            "HealthAnalyzer",
            "NinjaSuitRefill",
            "Jetpack",
            "PowerCellSlot",
            "UserInterface",
        };

        Assert.Multiple(() =>
        {
            foreach (var component in requiredComponents)
            {
                Assert.That(suit!.Components.ContainsKey(component), Is.True,
                    $"Suit prototype '{SuitPrototypeId}' is missing the '{component}' component");
            }

            // The Spider OS starts with the base configuration.
            var os = (NinjaOsComponent) suit!.Components["NinjaOs"].Component;
            Assert.That(os.Os.Id, Is.EqualTo("NinjaOs"), "NinjaOs must point at the NinjaOs prototype");
            Assert.That(os.IdleDraw, Is.EqualTo(2f), "The active suit must drain 2 units of cell charge per second");
            Assert.That(protoMan.HasIndex<NinjaOsPrototype>(os.Os.Id), Is.True, "Missing 'NinjaOs' prototype");

            // The built-in jetpack must not require a gas tank and must use the ninja toggle action.
            var jetpack = (JetpackComponent) suit.Components["Jetpack"].Component;
            Assert.That(jetpack.RequiresGas, Is.False, "The built-in suit jetpack must not require a gas tank");
            Assert.That(jetpack.ToggleAction.Id, Is.EqualTo("NinjaActionToggleJetpack"),
                "The suit jetpack must use the ninja toggle action");
            Assert.That(protoMan.HasIndex<EntityPrototype>(jetpack.ToggleAction.Id), Is.True,
                $"Jetpack toggle action '{jetpack.ToggleAction.Id}' does not exist");

            // Material refill entries of the limited-charge injector actions.
            var refill = (NinjaSuitRefillComponent) suit.Components["NinjaSuitRefill"].Component;
            Assert.That(refill.ActionMaterials.Keys.Select(action => action.Id),
                Is.SupersetOf(new[] { "ActionNinjaHealingCocktail", "ActionNinjaAdrenal" }),
                "NinjaSuitRefill must be able to refill the healing cocktail and the adrenal injector");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task HardwaresHaveSuitItemMarker()
    {
        await using var pair = await PoolManager.GetServerClient();
        var protoMan = pair.Server.ResolveDependency<IPrototypeManager>();

        Assert.Multiple(() =>
        {
            foreach (var (slot, prototypeId) in HardwareParts)
            {
                Assert.That(protoMan.TryIndex<EntityPrototype>(prototypeId, out var prototype), Is.True,
                    $"Missing suit part prototype '{prototypeId}' for slot '{slot}'");
                Assert.That(prototype!.Components.ContainsKey("NinjaSuitItem"), Is.True,
                    $"Suit part '{prototypeId}' (slot '{slot}') is missing the NinjaSuitItem marker");
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task SuitSpawnsWithSmallCellAndInterfaces()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var server = pair.Server;
        var entMan = server.EntMan;
        var map = await pair.CreateTestMap();

        EntityUid suit = default;
        EntityUid wearer = default;

        await server.WaitPost(() =>
        {
            wearer = entMan.SpawnEntity("MobHuman", map.GridCoords);
            suit = entMan.SpawnEntity(SuitPrototypeId, map.GridCoords);
        });

        await server.WaitRunTicks(2);

        var powerCell = server.System<PowerCellSystem>();
        var ui = server.System<UserInterfaceSystem>();

        // Entity and UI access has to happen on the server main thread.
        await server.WaitAssertion(() =>
        {
            Assert.That(entMan.GetComponent<MetaDataComponent>(suit).EntityPrototype?.ID, Is.EqualTo(SuitPrototypeId));

            Assert.That(powerCell.TryGetBatteryFromSlot(suit, out var battery), Is.True,
                $"Suit '{SuitPrototypeId}' did not spawn a power cell in its cell slot");

            var batteryMeta = entMan.GetComponent<MetaDataComponent>(battery!.Value.Owner);
            Assert.That(batteryMeta.EntityPrototype?.ID, Is.EqualTo("PowerCellSmall"),
                "The suit must start with the PowerCellSmall cell");
            Assert.That(entMan.GetComponent<BatteryComponent>(battery.Value.Owner).MaxCharge, Is.GreaterThan(0f),
                "The starting power cell has no capacity");

            // Both UIs declared on the suit must be registered and openable.
            Assert.Multiple(() =>
            {
                Assert.That(ui.TryOpenUi(suit, NinjaOsUiKey.Key, wearer), Is.True,
                    "The suit does not open the NinjaOsUiKey interface");
                Assert.That(ui.TryOpenUi(suit, HealthAnalyzerUiKey.Key, wearer), Is.True,
                    "The suit does not open the HealthAnalyzerUiKey interface");
            });
        });

        await pair.CleanReturnAsync();
    }
}
