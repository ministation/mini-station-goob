// SPDX-FileCopyrightText: 2026 Mini Station
// Мини-станция, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/ministation/mini-station-goob/master/LICENSE.TXT

using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Content.IntegrationTests.Pair;
using Content.Server._Mini.Ninja.Systems;
using Content.Shared._Mini.Ninja;
using Content.Shared._Mini.Ninja.Abilities.Snake;
using Content.Shared._Mini.Ninja.Components;
using Content.Shared._Mini.Ninja.Systems;
using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.Inventory;
using Content.Shared.Ninja.Components;
using Content.Shared.Trigger.Systems;
using Robust.Server.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Spawners;
using Robust.Shared.Timing;

namespace Content.IntegrationTests.Tests.Ninja;

/// <summary>
/// Runtime checks of the energy caltrops of the snake path: one press places one permanent trap,
/// the station holds at most three traps and the third trap starts the recharge.
/// </summary>
[TestFixture]
public sealed class NinjaCaltropTest
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
    public async Task CaltropsAreSinglePermanentAndLimited()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var server = pair.Server;
        var entMan = server.EntMan;
        var actions = server.System<SharedActionsSystem>();
        var triggers = server.System<TriggerSystem>();
        var suitPower = server.System<NinjaSuitPowerSystem>();
        var timing = server.ResolveDependency<IGameTiming>();

        var (human, suit) = await SetupActivatedSnakeSuit(pair);

        var caltrops = entMan.GetComponent<NinjaCaltropComponent>(suit);
        Assert.That(caltrops.MaxTraps, Is.EqualTo(3), "The snake module must limit the traps to three");

        // Keep the test fast: one second of recharge instead of a minute.
        await server.WaitPost(() => caltrops.RechargeDelay = TimeSpan.FromSeconds(1));

        var action = FindAction(entMan, actions, human, "NinjaCaltropAction");
        Assert.That(action.Owner.IsValid(), Is.True, "NinjaCaltropAction was not granted to the ninja");

        // One press places exactly one trap.
        await server.WaitPost(() => actions.PerformAction((human, default(ActionsComponent)), action));
        await server.WaitRunTicks(4);

        var traps = GetTraps(entMan);
        Assert.That(caltrops.PlacedInBatch, Is.EqualTo(1), "The caltrop ability did not place a trap");
        Assert.That(traps, Has.Count.EqualTo(1), "One press did not place exactly one caltrop");
        Assert.That(entMan.HasComponent<TimedDespawnComponent>(traps[0]), Is.False,
            "The caltrop must stay until it triggers or is destroyed");

        // The second and the third trap.
        for (var i = 0; i < 2; i++)
        {
            await server.WaitPost(() => actions.PerformAction((human, default(ActionsComponent)), action));
            await server.WaitRunTicks(4);
        }

        Assert.That(GetTraps(entMan), Has.Count.EqualTo(3), "Three presses must place three caltrops");
        Assert.That(caltrops.PlacedInBatch, Is.EqualTo(3), "The placed traps were not counted");

        // The third trap puts the ability into recharge.
        var cooldown = entMan.GetComponent<ActionComponent>(action.Owner).Cooldown;
        Assert.That(cooldown, Is.Not.Null, "The caltrop action did not go into recharge after the third trap");
        Assert.That(cooldown!.Value.End, Is.GreaterThan(timing.CurTime + TimeSpan.FromSeconds(0.5)),
            "The trap recharge is too short");

        // A station that already holds three traps refuses the next press without spending the suit charge.
        var chargeBefore = suitPower.GetCharge(human);
        await server.WaitPost(() => actions.PerformAction((human, default(ActionsComponent)), action));
        await server.WaitRunTicks(2);
        var chargeAfter = suitPower.GetCharge(human);

        Assert.That(GetTraps(entMan), Has.Count.EqualTo(3), "The station limit of three caltrops was exceeded");
        Assert.That(chargeBefore - chargeAfter, Is.LessThan(caltrops.EnergyCost),
            "The refused caltrop still consumed the suit charge");

        // A destroyed trap frees the limit, and the expired recharge starts a new batch.
        var placed = GetTraps(entMan);
        await server.WaitPost(() => entMan.DeleteEntity(placed[0]));
        await server.WaitRunTicks(45);

        Assert.That(caltrops.PlacedInBatch, Is.Zero, "The trap batch was not reset after the recharge");

        await server.WaitPost(() => actions.PerformAction((human, default(ActionsComponent)), action));
        await server.WaitRunTicks(4);

        Assert.That(GetTraps(entMan), Has.Count.EqualTo(3), "The trap was not placed after the recharge");
        Assert.That(caltrops.PlacedInBatch, Is.EqualTo(1), "A new batch did not start after the recharge");

        // A triggered trap burns out and stops counting towards the limit.
        var triggered = GetTraps(entMan)[0];
        await server.WaitPost(() => triggers.Trigger(triggered, human));
        await server.WaitRunTicks(4);

        Assert.That(entMan.EntityExists(triggered), Is.False, "The caltrop was not removed after it triggered");
        Assert.That(GetTraps(entMan), Has.Count.EqualTo(2), "The triggered caltrop was still counted");

        await pair.CleanReturnAsync();
    }

    private static List<EntityUid> GetTraps(IEntityManager entMan)
    {
        var result = new List<EntityUid>();
        var query = entMan.EntityQueryEnumerator<NinjaCaltropTrapComponent>();

        while (query.MoveNext(out var uid, out _))
            result.Add(uid);

        return result;
    }

    private static Entity<ActionComponent> FindAction(
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

        return default;
    }

    private static async Task<(EntityUid Human, EntityUid Suit)> SetupActivatedSnakeSuit(TestPair pair)
    {
        var server = pair.Server;
        var entMan = server.EntMan;
        var inventory = server.System<InventorySystem>();
        var ui = server.System<UserInterfaceSystem>();
        var map = await pair.CreateTestMap();
        var mapSys = server.System<SharedMapSystem>();

        EntityUid human = default;
        EntityUid suit = default;

        await server.WaitPost(() =>
        {
            // The default test map is a single tile; the trap is placed 1.5 tiles away from the ninja.
            for (var x = -1; x <= 4; x++)
            {
                for (var y = -1; y <= 4; y++)
                    mapSys.SetTile(map.Grid.Owner, map.Grid.Comp, new EntityCoordinates(map.Grid, x, y), map.Tile.Tile);
            }

            human = entMan.SpawnEntity("MobHuman", new EntityCoordinates(map.Grid.Owner, new Vector2(2.5f, 2.5f)));
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

        var os = entMan.GetComponent<NinjaOsComponent>(suit);
        var osSystem = server.System<NinjaOsSystem>();
        var modules = osSystem.GetModules(os);
        var perTier = modules.GroupBy(module => module.Tier).ToDictionary(group => group.Key, group => group.First());
        perTier[4] = modules.First(module => module.Tier == 4 && module.Path == NinjaPath.Snake);

        await server.WaitPost(() =>
        {
            ui.OpenUi(suit, NinjaOsUiKey.Key, human);

            foreach (var (tier, module) in perTier)
                ui.RaiseUiMessage(suit, NinjaOsUiKey.Key, new NinjaOsSelectModuleMessage(tier, module.Path) { Actor = human });
        });

        await server.WaitRunTicks(1);

        await server.WaitPost(() =>
            ui.RaiseUiMessage(suit, NinjaOsUiKey.Key, new NinjaOsSetSuitPowerMessage(true) { Actor = human }));

        await server.WaitRunTicks(2);
        Assert.That(os.SuitActivated, Is.True, "The suit did not activate for the caltrop test");

        return (human, suit);
    }
}
