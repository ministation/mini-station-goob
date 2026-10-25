// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections.Generic;
using System.Linq;
using Content.IntegrationTests.Pair;
using Content.Server._Mini.Ninja.Systems;
using Content.Shared._Mini.Ninja;
using Content.Shared._Mini.Ninja.Components;
using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.Interaction.Components;
using Content.Shared.Inventory;
using Content.Shared.Ninja.Components;
using Content.Shared.Power.EntitySystems;
using Content.Shared.PowerCell;
using Robust.Server.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.IntegrationTests.Tests.Ninja;

/// <summary>
/// Runtime checks of the Spider OS: opening the UI from the suit action, module selection and suit activation.
/// </summary>
[TestFixture]
public sealed class NinjaOsActivationTest
{
    /// <summary>
    /// Suit hardware per inventory slot, all of them are required by the boot checks.
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
    public async Task SuitGrantsOpenOsActionAndValidatesModuleSelection()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var server = pair.Server;
        var entMan = server.EntMan;
        var setup = await SetupSuit(pair);

        var actions = server.System<SharedActionsSystem>();
        var ui = server.System<UserInterfaceSystem>();
        var osSystem = server.System<NinjaOsSystem>();

        // A worn suit grants the action that opens the Spider OS.
        var granted = NinjaTestHelpers.GetActionPrototypeIds(entMan, actions, setup.Human);
        Assert.That(granted, Does.Contain("ActionOpenNinjaOs"),
            "The worn suit did not grant the ActionOpenNinjaOs action to the ninja");

        // Performing the action must authorize the wearer and open the UI.
        var openOsActions = actions.GetActions(setup.Human)
            .Where(action => entMan.GetComponent<MetaDataComponent>(action.Owner).EntityPrototype?.ID == "ActionOpenNinjaOs")
            .ToList();
        Assert.That(openOsActions, Has.Count.EqualTo(1),
            "The ninja must have exactly one granted ActionOpenNinjaOs action entity");

        await server.WaitPost(() =>
        {
            actions.PerformAction((setup.Human, default(ActionsComponent)), openOsActions[0]);
            Assert.That(ui.IsAnyUiOpen(setup.Suit), Is.True,
                "Performing ActionOpenNinjaOs did not open the Spider OS for the wearer");
        });

        var osComp = entMan.GetComponent<NinjaOsComponent>(setup.Suit);
        var modules = osSystem.GetModules(osComp);
        var moduleByTier = modules.GroupBy(module => module.Tier).ToDictionary(group => group.Key, group => group.First());

        Assert.Multiple(() =>
        {
            Assert.That(modules.Count, Is.EqualTo(NinjaTestHelpers.ExpectedModules.Count),
                "The Spider OS must offer all 14 path modules");
            Assert.That(moduleByTier.Keys, Is.EquivalentTo(Enumerable.Range(1, NinjaTestHelpers.MaxTier)),
                "The Spider OS must have at least one module for every tier 1..5");
        });

        // A foreign entity must not be able to pick modules even if it has the UI open.
        EntityUid intruder = default;
        await server.WaitPost(() =>
        {
            intruder = entMan.SpawnEntity("MobHuman", entMan.GetComponent<TransformComponent>(setup.Human).Coordinates);
            // The message is raised directly: OpenUi would run the interaction range check and
            // assert in debug for an entity that is not allowed to reach the suit.
        });

        await server.WaitRunTicks(1);

        await server.WaitPost(() =>
        {
            var first = moduleByTier[1];
            ui.RaiseUiMessage(setup.Suit, NinjaOsUiKey.Key,
                new NinjaOsSelectModuleMessage(first.Tier, first.Path) { Actor = intruder });

            Assert.That(osComp.SelectedModules, Is.Empty,
                "An unauthorized actor was able to select a suit module");
            Assert.That(osComp.LockedTiers, Is.Empty,
                "An unauthorized actor was able to lock a suit module tier");
        });

        // The wearer can pick exactly one module per tier, tiers are locked after the pick.
        await server.WaitPost(() =>
        {
            ui.RaiseUiMessage(setup.Suit, NinjaOsUiKey.Key,
                new NinjaOsSelectModuleMessage(NinjaTestHelpers.MaxTier, NinjaPath.Steel) { Actor = setup.Human });
            Assert.That(osComp.SelectedModules, Is.Empty,
                "A module was installed for a path/tier combination that does not exist (Steel tier 5)");

            for (var tier = 1; tier <= NinjaTestHelpers.MaxTier; tier++)
            {
                var module = moduleByTier[tier];
                ui.RaiseUiMessage(setup.Suit, NinjaOsUiKey.Key,
                    new NinjaOsSelectModuleMessage(tier, module.Path) { Actor = setup.Human });

                Assert.That(osComp.SelectedModules, Does.ContainKey(tier), $"Tier {tier} module was not installed");
                Assert.That(osComp.SelectedModules[tier], Is.EqualTo(module.Path),
                    $"Tier {tier} was installed with the wrong path");
                Assert.That(osComp.LockedTiers, Does.Contain(tier), $"Tier {tier} was not locked after the pick");

                // Once the tier is locked the choice must not be replaceable.
                var otherPath = modules.FirstOrDefault(candidate => candidate.Tier == tier && candidate.Path != module.Path);
                if (otherPath == null)
                    continue;

                ui.RaiseUiMessage(setup.Suit, NinjaOsUiKey.Key,
                    new NinjaOsSelectModuleMessage(tier, otherPath.Path) { Actor = setup.Human });
                Assert.That(osComp.SelectedModules[tier], Is.EqualTo(module.Path),
                    $"Tier {tier} was replaced after it had been locked");
            }

            Assert.That(osComp.SelectedModules, Has.Count.EqualTo(NinjaTestHelpers.MaxTier),
                "The ninja must be able to fill every tier");
            Assert.That(osComp.LockedTiers, Has.Count.EqualTo(NinjaTestHelpers.MaxTier),
                "Every filled tier must be locked");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ActivationInstallsModulesGrantsActionsAndLocksHardware()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var server = pair.Server;
        var entMan = server.EntMan;
        var setup = await SetupSuit(pair);

        var actions = server.System<SharedActionsSystem>();
        var inventory = server.System<InventorySystem>();
        var osSystem = server.System<NinjaOsSystem>();
        var powerCell = server.System<PowerCellSystem>();
        var ui = server.System<UserInterfaceSystem>();
        var factory = server.ResolveDependency<IComponentFactory>();

        var osComp = entMan.GetComponent<NinjaOsComponent>(setup.Suit);
        var modules = osSystem.GetModules(osComp);
        var moduleByTier = modules.GroupBy(module => module.Tier).ToDictionary(group => group.Key, group => group.First());

        Assert.That(moduleByTier.Keys, Is.EquivalentTo(Enumerable.Range(1, NinjaTestHelpers.MaxTier)),
            "The activation test needs at least one module for every tier 1..5");

        await server.WaitPost(() => ui.OpenUi(setup.Suit, NinjaOsUiKey.Key, setup.Human));
        await server.WaitRunTicks(1);

        // The boot checks must reject activation while no modules are installed.
        await server.WaitPost(() =>
        {
            ui.RaiseUiMessage(setup.Suit, NinjaOsUiKey.Key,
                new NinjaOsSetSuitPowerMessage(true) { Actor = setup.Human });
            Assert.That(osComp.SuitActivated, Is.False,
                "The suit activated without the full module loadout");
        });

        await server.WaitPost(() =>
        {
            for (var tier = 1; tier <= NinjaTestHelpers.MaxTier; tier++)
            {
                var module = moduleByTier[tier];
                ui.RaiseUiMessage(setup.Suit, NinjaOsUiKey.Key,
                    new NinjaOsSelectModuleMessage(tier, module.Path) { Actor = setup.Human });
            }

            Assert.That(osComp.SelectedModules, Has.Count.EqualTo(NinjaTestHelpers.MaxTier),
                "The ninja could not fill every tier through the UI messages");
        });

        // The boot checks must reject activation without the full hardware set.
        var helmet = setup.Parts["head"];
        await server.WaitPost(() => Assert.That(inventory.TryUnequip(setup.Human, "head", force: true), Is.True,
            "Could not unequip the helmet for the boot check test"));
        await server.WaitPost(() =>
        {
            ui.RaiseUiMessage(setup.Suit, NinjaOsUiKey.Key,
                new NinjaOsSetSuitPowerMessage(true) { Actor = setup.Human });
            Assert.That(osComp.SuitActivated, Is.False,
                "The suit activated without the helmet");
        });

        await server.WaitPost(() => Assert.That(inventory.TryEquip(setup.Human, helmet, "head", force: true), Is.True,
            "Could not equip the helmet back"));

        // Full loadout and full hardware: activation succeeds.
        await server.WaitPost(() =>
        {
            ui.RaiseUiMessage(setup.Suit, NinjaOsUiKey.Key,
                new NinjaOsSetSuitPowerMessage(true) { Actor = setup.Human });
            Assert.That(osComp.SuitActivated, Is.True,
                "The suit did not activate with the full module loadout and hardware set");
        });

        await server.WaitRunTicks(1);

        Assert.Multiple(() =>
        {
            // Components of every installed module must be added to the suit.
            foreach (var (tier, path) in osComp.SelectedModules)
            {
                Assert.That(osSystem.TryGetModule(osComp, path, tier, out var module), Is.True,
                    $"Could not resolve the installed module for tier {tier}");
                foreach (var (componentName, entry) in module!.Components)
                {
                    Assert.That(entMan.HasComponent(setup.Suit, entry.Component.GetType()), Is.True,
                        $"Suit is missing component '{componentName}' of module {module.ID} (tier {tier})");
                }
            }

            // Actions of every installed module must be granted to the wearer.
            var granted = NinjaTestHelpers.GetActionPrototypeIds(entMan, actions, setup.Human);
            foreach (var (tier, path) in osComp.SelectedModules)
            {
                osSystem.TryGetModule(osComp, path, tier, out var module);
                if (module?.Actions == null)
                    continue;

                foreach (var action in module.Actions)
                {
                    Assert.That(granted, Does.Contain(action.Id),
                        $"Action '{action.Id}' of module {module.ID} (tier {tier}) was not granted to the ninja");
                }
            }

            // While the suit is active every hardware part is locked in place.
            Assert.That(entMan.HasComponent<UnremoveableComponent>(setup.Suit), Is.True,
                "Active suit is not locked (Unremoveable)");
            foreach (var (slot, part) in setup.Parts)
            {
                Assert.That(entMan.HasComponent<UnremoveableComponent>(part), Is.True,
                    $"Suit part in slot '{slot}' is not locked while the suit is active");
            }
        });

        Assert.That(powerCell.TryGetBatteryFromSlot(setup.Suit, out var battery), Is.True,
            "The suit has no power cell to drain");
        var batterySystem = server.System<SharedBatterySystem>();
        var chargeBefore = batterySystem.GetCharge(battery!.Value.AsNullable());

        await server.WaitRunTicks(60);

        var idleDraw = chargeBefore - batterySystem.GetCharge(battery.Value.AsNullable());
        Assert.That(idleDraw, Is.GreaterThan(0f),
            "Active suit does not drain its power cell (IdleDraw is not applied)");
        Assert.That(idleDraw, Is.LessThan(20f),
            $"Active suit drained {idleDraw} units in one second, expected the idle 2/s drain");

        // Deactivation removes the granted actions and unlocks the hardware.
        await server.WaitPost(() =>
        {
            ui.RaiseUiMessage(setup.Suit, NinjaOsUiKey.Key,
                new NinjaOsSetSuitPowerMessage(false) { Actor = setup.Human });
            Assert.That(osComp.SuitActivated, Is.False, "The suit did not deactivate");
        });

        await server.WaitRunTicks(1);

        Assert.Multiple(() =>
        {
            Assert.That(entMan.HasComponent<UnremoveableComponent>(setup.Suit), Is.False,
                "Suit is still locked after deactivation");
            foreach (var (slot, part) in setup.Parts)
            {
                Assert.That(entMan.HasComponent<UnremoveableComponent>(part), Is.False,
                    $"Suit part in slot '{slot}' is still locked after deactivation");
            }

            var grantedAfter = NinjaTestHelpers.GetActionPrototypeIds(entMan, actions, setup.Human);
            foreach (var (tier, path) in osComp.SelectedModules)
            {
                osSystem.TryGetModule(osComp, path, tier, out var module);
                if (module?.Actions == null)
                    continue;

                foreach (var action in module.Actions)
                {
                    Assert.That(grantedAfter, Does.Not.Contain(action.Id),
                        $"Action '{action.Id}' of module {module.ID} is still granted after deactivation");
                }
            }
        });

        await pair.CleanReturnAsync();
    }

    private static async Task<NinjaSetup> SetupSuit(TestPair pair)
    {
        var server = pair.Server;
        var entMan = server.EntMan;
        var inventory = server.System<InventorySystem>();
        var map = await pair.CreateTestMap();

        EntityUid human = default;
        EntityUid suit = default;
        var parts = new Dictionary<string, EntityUid>();

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
                parts[slot] = part;
                Assert.That(inventory.TryEquip(human, part, slot, force: true), Is.True,
                    $"Could not equip '{prototypeId}' into slot '{slot}'");
            }
        });

        await server.WaitRunTicks(2);

        return new NinjaSetup(human, suit, parts);
    }

    private sealed class NinjaSetup
    {
        public readonly EntityUid Human;
        public readonly EntityUid Suit;
        public readonly Dictionary<string, EntityUid> Parts;

        public NinjaSetup(EntityUid human, EntityUid suit, Dictionary<string, EntityUid> parts)
        {
            Human = human;
            Suit = suit;
            Parts = parts;
        }
    }
}
