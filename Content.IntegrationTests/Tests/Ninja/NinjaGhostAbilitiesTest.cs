// SPDX-FileCopyrightText: 2026 Mini Station
// Мини-станция, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/ministation/mini-station-goob/master/LICENSE.TXT

using System.Collections.Generic;
using System.Linq;
using Content.IntegrationTests.Pair;
using Content.Shared._Mini.Ninja;
using Content.Shared._Mini.Ninja.Abilities.Ghost;
using Content.Shared._Mini.Ninja.Components;
using Content.Shared.Clothing.Components;
using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.Humanoid;
using Content.Shared.Inventory;
using Content.Shared.Ninja.Components;
using Content.Shared.Stealth;
using Content.Shared.Stealth.Components;
using Robust.Server.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.Ninja;

/// <summary>
/// Runtime checks of the ghost path abilities: the phase cloak and the chameleon disguise.
/// </summary>
[TestFixture]
public sealed class NinjaGhostAbilitiesTest
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
    public async Task CloakHidesWearerAndRemovesStealth()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var server = pair.Server;
        var entMan = server.EntMan;
        var actions = server.System<SharedActionsSystem>();
        var stealthSystem = server.System<SharedStealthSystem>();

        var (human, suit) = await SetupActivatedGhostSuit(pair);

        var cloakAction = FindAction(entMan, actions, human, "ActionNinjaGhostCloak");
        Assert.That(cloakAction.Owner.IsValid(), Is.True, "ActionNinjaGhostCloak was not granted");

        var osComp = entMan.GetComponent<NinjaOsComponent>(suit);
        var suitParent = entMan.GetComponent<TransformComponent>(suit).ParentUid;
        TestContext.Out.WriteLine($"DIAG cloak: comp={entMan.HasComponent<NinjaGhostCloakComponent>(suit)} " +
                                  $"parent={suitParent} human={human} " +
                                  $"activated={osComp.SuitActivated} container={cloakAction.Comp.Container} " +
                                  $"attached={cloakAction.Comp.AttachedEntity}");
        Assert.That(entMan.HasComponent<NinjaGhostCloakComponent>(suit), Is.True,
            "NinjaGhostCloakComponent was not installed on the suit");
        Assert.That(suitParent, Is.EqualTo(human), "The suit is not worn by the test human");
        Assert.That(osComp.SuitActivated, Is.True, "The suit is not activated");
        Assert.That(cloakAction.Comp.Container, Is.EqualTo(suit), "The cloak action is not contained by the suit");
        Assert.That(cloakAction.Comp.AttachedEntity, Is.EqualTo(human), "The cloak action is not granted to the human");

        await server.WaitPost(() =>
        {
            actions.PerformAction((human, default(ActionsComponent)), cloakAction);
        });
        await server.WaitRunTicks(45);

        Assert.That(entMan.HasComponent<StealthComponent>(human), Is.True,
            "The cloak did not add a StealthComponent to the wearer");
        var stealth = entMan.GetComponent<StealthComponent>(human);
        var visibility = stealthSystem.GetVisibility(human, stealth);
        Assert.That(stealth.MaxVisibility, Is.LessThanOrEqualTo(0f),
            "The cloak must not allow any visibility, otherwise moving would leave a visible shimmer");
        Assert.That(visibility, Is.LessThanOrEqualTo(0f),
            $"The wearer stayed visible to other players after cloaking (visibility {visibility})");

        await server.WaitPost(() => actions.PerformAction((human, default(ActionsComponent)), cloakAction));
        await server.WaitRunTicks(2);
        Assert.That(entMan.HasComponent<StealthComponent>(human), Is.False,
            "Toggling the cloak off did not remove the StealthComponent");

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task DisguiseScansAndCopiesAppearance()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var server = pair.Server;
        var entMan = server.EntMan;
        var actions = server.System<SharedActionsSystem>();

        var (human, suit) = await SetupActivatedGhostSuit(pair);

        EntityUid target = default;
        EntityUid targetJumpsuit = default;
        var coords = entMan.GetComponent<TransformComponent>(human).Coordinates;
        await server.WaitPost(() =>
        {
            target = entMan.SpawnEntity("MobHuman", coords);
            targetJumpsuit = entMan.SpawnEntity("ClothingUniformJumpsuitCargo", coords);
            var inventory = server.System<InventorySystem>();
            Assert.That(inventory.TryEquip(target, targetJumpsuit, "jumpsuit", force: true), Is.True,
                "Could not equip the target jumpsuit");
        });

        // Give the target a distinctive name and skin colour so the copy is observable.
        var targetName = "Scanned Target";
        await server.WaitPost(() =>
        {
            var meta = entMan.GetComponent<MetaDataComponent>(target);
            entMan.System<MetaDataSystem>().SetEntityName(target, targetName, meta);
            if (entMan.TryGetComponent<HumanoidAppearanceComponent>(target, out var humanoid))
                humanoid.SkinColor = Color.Red;
        });

        var scanAction = FindAction(entMan, actions, human, "ActionNinjaGhostDisguiseScan");
        Assert.That(scanAction.Owner.IsValid(), Is.True, "ActionNinjaGhostDisguiseScan was not granted");

        await server.WaitPost(() =>
        {
            actions.PerformAction((human, default(ActionsComponent)), scanAction,
                new NinjaGhostDisguiseScanEvent { Target = target });
        });
        await server.WaitRunTicks(90);

        var disguise = entMan.GetComponent<NinjaGhostDisguiseComponent>(suit);
        Assert.That(disguise.Entries, Is.Not.Empty, "The disguise scanner did not save the target appearance");

        var menuAction = FindAction(entMan, actions, human, "ActionNinjaGhostDisguiseMenu");
        Assert.That(menuAction.Owner.IsValid(), Is.True, "ActionNinjaGhostDisguiseMenu was not granted");

        await server.WaitPost(() => actions.PerformAction((human, default(ActionsComponent)), menuAction));
        await server.WaitRunTicks(90);

        Assert.That(disguise.Disguised, Is.True, "The chameleon did not apply the scanned appearance");
        Assert.That(entMan.GetComponent<MetaDataComponent>(human).EntityName, Is.EqualTo(targetName),
            "The chameleon did not copy the target name");

        var humanLook = entMan.GetComponent<HumanoidAppearanceComponent>(human);
        if (entMan.TryGetComponent<HumanoidAppearanceComponent>(target, out var targetLook))
            Assert.That(humanLook.SkinColor, Is.EqualTo(targetLook.SkinColor),
                "The chameleon did not copy the target skin colour");

        // The outfit of the disguise must reach the clients: they draw the scanned clothing over the ninja.
        await server.WaitAssertion(() =>
        {
            var jumpsuit = disguise.ActiveOutfit.FirstOrDefault(clothing => clothing.Slot == "jumpsuit");
            Assert.That(jumpsuit, Is.Not.Null, "The scanned jumpsuit was not replicated to the clients");
            Assert.That(jumpsuit!.Prototype.Id, Is.EqualTo("ClothingUniformJumpsuitCargo"),
                "The replicated outfit points at the wrong jumpsuit");

            Assert.That(disguise.ActiveOutfit.Any(clothing => clothing.Slot == "shoes"), Is.False,
                "The outfit must not contain slots the scanned target had nothing in");

            // The server no longer repaints the worn gear: the clothing visuals of an item are not networked,
            // so the visuals of the real suit must stay untouched and the client does the drawing.
            var protoMan = server.ResolveDependency<IPrototypeManager>();
            Assert.That(protoMan.TryIndex<EntityPrototype>("ClothingOuterSuitSpaceNinja", out var suitProto), Is.True);
            var originalSuitClothing = (ClothingComponent) suitProto!.Components["Clothing"].Component;
            Assert.That(entMan.GetComponent<ClothingComponent>(suit).RsiPath,
                Is.EqualTo(originalSuitClothing.RsiPath),
                "The server changed the visuals of the real suit, the clients can not see that");
        });

        // Dropping the disguise must clear the replicated outfit so the clients restore the real gear.
        await server.WaitPost(() => actions.PerformAction((human, default(ActionsComponent)), menuAction));
        await server.WaitRunTicks(90);

        Assert.That(disguise.Disguised, Is.False, "The chameleon did not reset the disguise");
        await server.WaitAssertion(() =>
        {
            Assert.That(disguise.ActiveOutfit, Is.Empty,
                "The replicated outfit was not cleared when the disguise was dropped");
        });

        await pair.CleanReturnAsync();
    }

    private static Entity<ActionComponent> FindAction(IEntityManager entMan, SharedActionsSystem actions, EntityUid holder, string prototypeId)
    {
        foreach (var action in actions.GetActions(holder))
        {
            if (entMan.GetComponent<MetaDataComponent>(action.Owner).EntityPrototype?.ID == prototypeId)
                return action;
        }

        return default;
    }

    private static async Task<(EntityUid Human, EntityUid Suit)> SetupActivatedGhostSuit(TestPair pair)
    {
        var server = pair.Server;
        var entMan = server.EntMan;
        var inventory = server.System<InventorySystem>();
        var ui = server.System<UserInterfaceSystem>();
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

        var os = entMan.GetComponent<NinjaOsComponent>(suit);
        await server.WaitPost(() =>
        {
            for (var tier = 1; tier <= 5; tier++)
            {
                os.SelectedModules[tier] = NinjaPath.Ghost;
                os.LockedTiers.Add(tier);
            }

            ui.OpenUi(suit, NinjaOsUiKey.Key, human);
            ui.RaiseUiMessage(suit, NinjaOsUiKey.Key, new NinjaOsSetSuitPowerMessage(true) { Actor = human });
        });

        await server.WaitRunTicks(2);
        Assert.That(os.SuitActivated, Is.True, "The suit did not activate for the ghost abilities test");

        return (human, suit);
    }
}