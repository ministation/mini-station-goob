// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Content.IntegrationTests.Pair;
using Content.Server._Mini.Ninja.Systems;
using Content.Server.Station.Systems;
using Content.Shared._Mini.Ninja;
using Content.Shared._Mini.Ninja.Abilities.Snake;
using Content.Shared._Mini.Ninja.Components;
using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.Damage.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Inventory;
using Content.Shared.Ninja.Components;
using Content.Shared.Power.Components;
using Content.Shared.Power.EntitySystems;
using Content.Shared.Roles;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Server.GameObjects;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.Ninja;

/// <summary>
/// Regression checks for the ninja ability bugs reported from live play:
/// caltrops that kept burning the victim forever, the katana recall that did nothing,
/// the kunai that could not be fired and the EMP module that did nothing.
/// </summary>
[TestFixture]
public sealed class NinjaAbilityRegressionTest
{
    [Test]
    public async Task CaltropStopsBurningAfterTrigger()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var server = pair.Server;
        var entMan = server.EntMan;
        var (human, _) = await SetupNinja(pair);

        var humanCoords = entMan.GetComponent<TransformComponent>(human).Coordinates;
        var trap = EntityUid.Invalid;

        await server.WaitPost(() => trap = entMan.SpawnEntity("NinjaCaltrop", humanCoords));
        await server.WaitRunTicks(10);

        await server.WaitAssertion(() =>
        {
            Assert.That(entMan.EntityExists(trap), Is.False,
                "The caltrop was not removed after it was triggered by a person stepping on it");
            Assert.That(entMan.HasComponent<DamagedByContactComponent>(human), Is.False,
                "The victim keeps the DamageContacts drain component after the caltrop is gone, which burns them forever");
        });

        var damageable = entMan.GetComponent<DamageableComponent>(human);
        var damageAfterTrigger = damageable.TotalDamage;

        Assert.That(damageAfterTrigger.Float(), Is.GreaterThan(0f),
            "The caltrop did not deal any heat damage when it triggered");

        await server.WaitRunTicks(120);

        await server.WaitAssertion(() => Assert.That(damageable.TotalDamage.Float(),
            Is.LessThanOrEqualTo(damageAfterTrigger.Float() + 0.5f),
            "The victim keeps taking heat damage after the caltrop is gone"));

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task KatanaRecallPullsKatanaOutOfSheath()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var server = pair.Server;
        var entMan = server.EntMan;
        var inventory = server.System<InventorySystem>();
        var containers = server.System<SharedContainerSystem>();
        var hands = server.System<SharedHandsSystem>();
        var actions = server.System<SharedActionsSystem>();

        var (human, _) = await SetupNinja(pair);

        var katana = EntityUid.Invalid;
        await server.WaitPost(() =>
        {
            Assert.That(inventory.TryGetSlotEntity(human, "belt", out var sheath), Is.True,
                "The ninja has no sheath in the belt slot");
            Assert.That(containers.TryGetContainer(sheath!.Value, "item", out var container), Is.True,
                "The ninja sheath has no item container");
            katana = container!.ContainedEntities.Single();
            Assert.That(entMan.HasComponent<EnergyKatanaComponent>(katana), Is.True,
                "The sheath does not contain the energy katana");
        });

        Entity<ActionComponent>? recall = null;
        await server.WaitPost(() =>
        {
            foreach (var action in actions.GetActions(human))
            {
                if (entMan.GetComponent<MetaDataComponent>(action.Owner).EntityPrototype?.ID == "ActionRecallKatana")
                    recall = action;
            }
        });

        Assert.That(recall, Is.Not.Null, "The ninja suit did not grant the katana recall action");

        await server.WaitPost(() => actions.PerformAction((human, default(ActionsComponent)), recall!.Value));
        await server.WaitRunTicks(4);

        await server.WaitAssertion(() => Assert.That(hands.IsHolding(human, katana), Is.True,
            "The recall action did nothing: the katana is still in the sheath, not in a hand"));

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task NinjaCanFireChainKunai()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var server = pair.Server;
        var entMan = server.EntMan;
        var actions = server.System<SharedActionsSystem>();
        var hands = server.System<SharedHandsSystem>();

        var (human, _) = await SetupNinja(pair, (NinjaPath.Snake, 1));

        Entity<ActionComponent>? toggle = null;
        await server.WaitPost(() =>
        {
            foreach (var action in actions.GetActions(human))
            {
                if (entMan.GetComponent<MetaDataComponent>(action.Owner).EntityPrototype?.ID == "ToggleNinjaJohyoAbility")
                    toggle = action;
            }
        });

        Assert.That(toggle, Is.Not.Null, "The snake tier 1 module did not grant the chain kunai action");

        await server.WaitPost(() => actions.PerformAction((human, default(ActionsComponent)), toggle!.Value));
        await server.WaitRunTicks(4);

        var kunai = EntityUid.Invalid;
        await server.WaitAssertion(() =>
        {
            var query = entMan.EntityQueryEnumerator<NinjaJohyoComponent>();
            while (query.MoveNext(out var uid, out _))
            {
                if (hands.IsHolding(human, uid))
                    kunai = uid;
            }
        });

        Assert.That(kunai, Is.Not.EqualTo(EntityUid.Invalid), "The kunai action did not put the chain kunai into a hand");

        var gun = entMan.GetComponent<GunComponent>(kunai);
        var ev = new ShotAttemptedEvent { User = human, Used = (kunai, gun) };
        entMan.EventBus.RaiseLocalEvent(human, ref ev);

        Assert.That(ev.Cancelled, Is.False,
            "The ninja code still forbids firing the suit chain kunai (gun-disabled)");

        var guns = server.System<SharedGunSystem>();
        var fired = false;
        await server.WaitPost(() => fired = guns.AttemptShoot(human, (kunai, gun),
            new EntityCoordinates(kunai, new Vector2(1f, 0f))));

        Assert.That(fired, Is.True, "The chain kunai action allows the shot, but the gun itself did not fire");

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task EmpModuleDrainsNearbyBattery()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var server = pair.Server;
        var entMan = server.EntMan;
        var actions = server.System<SharedActionsSystem>();
        var batteries = server.System<SharedBatterySystem>();

        var (human, _) = await SetupNinja(pair, (NinjaPath.Steel, 3));

        EntityUid cell = default;
        await server.WaitPost(() =>
        {
            var grid = entMan.GetComponent<TransformComponent>(human).GridUid!.Value;
            cell = entMan.SpawnEntity("PowerCellSmall", new EntityCoordinates(grid, new Vector2(4.5f, 2.5f)));
        });
        await server.WaitRunTicks(2);

        var battery = entMan.GetComponent<BatteryComponent>(cell);
        var chargeBefore = batteries.GetCharge((cell, (BatteryComponent?) battery));
        Assert.That(chargeBefore, Is.GreaterThan(0f), "The test battery is empty");

        Entity<ActionComponent>? emp = null;
        await server.WaitPost(() =>
        {
            foreach (var action in actions.GetActions(human))
            {
                if (entMan.GetComponent<MetaDataComponent>(action.Owner).EntityPrototype?.ID == "ActionNinjaEmp")
                    emp = action;
            }
        });

        Assert.That(emp, Is.Not.Null, "The steel tier 3 module did not grant the EMP action");

        await server.WaitPost(() => actions.PerformAction((human, default(ActionsComponent)), emp!.Value));
        await server.WaitRunTicks(4);

        var chargeAfter = batteries.GetCharge((cell, (BatteryComponent?) battery));
        Assert.That(chargeAfter, Is.LessThan(chargeBefore),
            "The EMP pulse did not drain the nearby battery");

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ReactivatingSuitRestoresAbilityActions()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var server = pair.Server;
        var entMan = server.EntMan;
        var actions = server.System<SharedActionsSystem>();
        var ui = server.System<UserInterfaceSystem>();
        var osSystem = server.System<NinjaOsSystem>();

        var (human, suit) = await SetupNinja(pair, (NinjaPath.Snake, 1), (NinjaPath.Steel, 3));
        var os = entMan.GetComponent<NinjaOsComponent>(suit);

        Assert.That(FindAction(entMan, actions, human, "ActionNinjaEmp"), Is.Not.Null,
            "The EMP action was not granted after the first suit activation");
        Assert.That(FindAction(entMan, actions, human, "ToggleNinjaJohyoAbility"), Is.Not.Null,
            "The chain kunai action was not granted after the first suit activation");

        await server.WaitPost(() =>
            ui.RaiseUiMessage(suit, NinjaOsUiKey.Key, new NinjaOsSetSuitPowerMessage(false) { Actor = human }));
        await server.WaitRunTicks(2);
        Assert.That(os.SuitActivated, Is.False, "The suit did not deactivate");

        await server.WaitPost(() =>
            ui.RaiseUiMessage(suit, NinjaOsUiKey.Key, new NinjaOsSetSuitPowerMessage(true) { Actor = human }));
        await server.WaitRunTicks(2);
        Assert.That(os.SuitActivated, Is.True, "The suit did not reactivate");
        Assert.That(osSystem.AllModulesSelected(os), Is.True, "The module selection was lost on reactivation");

        Assert.That(FindAction(entMan, actions, human, "ActionNinjaEmp"), Is.Not.Null,
            "The EMP action was not granted again after reactivating the suit");
        Assert.That(FindAction(entMan, actions, human, "ToggleNinjaJohyoAbility"), Is.Not.Null,
            "The chain kunai action was not granted again after reactivating the suit");

        await pair.CleanReturnAsync();
    }

    private static Entity<ActionComponent>? FindAction(
        IEntityManager entMan,
        SharedActionsSystem actions,
        EntityUid holder,
        string prototypeId)
    {
        foreach (var action in actions.GetActions(holder))
        {
            if (entMan.GetComponent<MetaDataComponent>(action.Owner).EntityPrototype?.ID == prototypeId)
                return action;
        }

        return null;
    }

    /// <summary>
    /// Spawns a human in the full ninja starting gear, binds the suit and activates the Spider OS.
    /// The given modules override the first module of their tier.
    /// </summary>
    private static async Task<(EntityUid Human, EntityUid Suit)> SetupNinja(
        TestPair pair,
        params (NinjaPath Path, int Tier)[] moduleOverrides)
    {
        var server = pair.Server;
        var entMan = server.EntMan;
        var inventory = server.System<InventorySystem>();
        var ui = server.System<UserInterfaceSystem>();
        var spawning = server.System<StationSpawningSystem>();
        var map = await pair.CreateTestMap();
        var mapSys = server.System<SharedMapSystem>();

        EntityUid human = default;
        EntityUid suit = default;

        await server.WaitPost(() =>
        {
            for (var x = -1; x <= 6; x++)
            {
                for (var y = -1; y <= 6; y++)
                    mapSys.SetTile(map.Grid.Owner, map.Grid.Comp, new EntityCoordinates(map.Grid, x, y), map.Tile.Tile);
            }

            human = entMan.SpawnEntity("MobHuman", new EntityCoordinates(map.Grid.Owner, new Vector2(2.5f, 2.5f)));
            entMan.EnsureComponent<SpaceNinjaComponent>(human);
            spawning.EquipStartingGear(human, new ProtoId<StartingGearPrototype>("SpaceNinjaGear"));

            Assert.That(inventory.TryGetSlotEntity(human, "outerClothing", out var suitUid), Is.True,
                "The ninja starting gear did not equip the suit");
            suit = suitUid!.Value;
        });

        await server.WaitRunTicks(2);

        var os = entMan.GetComponent<NinjaOsComponent>(suit);
        var osSystem = server.System<NinjaOsSystem>();
        var modules = osSystem.GetModules(os);
        var perTier = modules.GroupBy(module => module.Tier).ToDictionary(group => group.Key, group => group.First());

        foreach (var (path, tier) in moduleOverrides)
            perTier[tier] = modules.First(module => module.Tier == tier && module.Path == path);

        await server.WaitPost(() =>
        {
            ui.OpenUi(suit, NinjaOsUiKey.Key, human);

            foreach (var (moduleTier, module) in perTier)
                ui.RaiseUiMessage(suit, NinjaOsUiKey.Key, new NinjaOsSelectModuleMessage(moduleTier, module.Path) { Actor = human });
        });

        await server.WaitRunTicks(1);

        await server.WaitPost(() =>
            ui.RaiseUiMessage(suit, NinjaOsUiKey.Key, new NinjaOsSetSuitPowerMessage(true) { Actor = human }));

        await server.WaitRunTicks(2);
        Assert.That(os.SuitActivated, Is.True, "The ninja suit did not activate");

        return (human, suit);
    }
}
