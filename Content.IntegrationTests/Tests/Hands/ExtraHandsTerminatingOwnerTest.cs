// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Inventory;
using Content.Shared.Inventory.VirtualItem;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests.Hands;

/// <summary>
/// Round-end cleanup deletes the whole entity tree, so everything inside a mob is already terminating by the
/// time the mob's H.A.R.M.P.A.C.K. is unequipped. <see cref="ExtraHandsEquipmentSystem"/> then used to remove
/// the pack's extra hands from the terminating mob; <see cref="SharedHandsSystem.RemoveHand"/> tried to empty
/// their container and delete the still contained virtual item, which the engine reports as
/// "system.container: Attempting to remove an entity from a container while it is terminating" and
/// "Called Delete on an entity already being deleted".
/// </summary>
[TestFixture]
[TestOf(typeof(ExtraHandsEquipmentSystem))]
public sealed class ExtraHandsTerminatingOwnerTest
{
    [Test]
    public async Task DeletingMobWithExtraHandsDoesNotTouchTerminatingHands()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.EntMan;
        var map = await pair.CreateTestMap();

        EntityUid mob = default;
        EntityUid virtualItem = default;

        await server.WaitPost(() =>
        {
            var inventory = server.System<InventorySystem>();
            var containers = server.System<SharedContainerSystem>();

            mob = entMan.SpawnEntity("MobHuman", map.GridCoords);

            var harmpack = entMan.SpawnEntity("ClothingBackpackHarmpack", map.GridCoords);
            Assert.That(inventory.TryEquip(mob, harmpack, "back"), Is.True);

            // Extra hands are named "<net entity id>-<hand id>" by the equipment system.
            var hands = entMan.GetComponent<HandsComponent>(mob);
            var handId = hands.Hands.Keys.Single(id => id.EndsWith("-extra_hand_1"));
            var hand = containers.GetContainer(mob, handId);

            // A virtual item (e.g. from pulling someone) is still inside the extra hand when the mob is deleted.
            virtualItem = entMan.SpawnEntity("VirtualItem", map.GridCoords);
            entMan.GetComponent<VirtualItemComponent>(virtualItem).BlockingEntity = mob;
            Assert.That(containers.Insert(virtualItem, hand), Is.True);
        });

        // Deleting the mob first flags all of its contents, including the virtual item, as terminating, and
        // only then unequips the pack. Without the guard that removal handler emptied the extra hand of the
        // already terminating mob, which logged the engine errors above.
        await server.WaitPost(() => entMan.DeleteEntity(mob));

        await server.WaitAssertion(() =>
        {
            Assert.That(entMan.Deleted(mob), Is.True);
            Assert.That(entMan.Deleted(virtualItem), Is.True);
        });

        // The engine errors are also collected in the pooled server log and fail the test at this point.
        await pair.CleanReturnAsync();
    }
}
