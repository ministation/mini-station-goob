// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using Content.Goobstation.Shared.Enchanting.Components;
using Content.Server.Antag.Components;
using Content.Server.Interaction;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests._Goobstation.Enchanting;

[TestFixture]
public sealed class EnchantAltarTest
{
    /// <summary>
    /// Token wizards used to be spawned without <see cref="CanEnchantComponent"/>,
    /// so the altar rejected them with "You are not worthy!".
    /// </summary>
    [Test]
    public async Task TokenWizardEnchantsItemOnAltar()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var sysMan = server.ResolveDependency<IEntitySystemManager>();
        var handSys = sysMan.GetEntitySystem<SharedHandsSystem>();
        var xformSys = sysMan.GetEntitySystem<SharedTransformSystem>();
        var interaction = sysMan.GetEntitySystem<InteractionSystem>();

        var map = await pair.CreateTestMap();
        var coords = map.MapCoords;

        EntityUid user = default;
        EntityUid grimoire = default;
        EntityUid altar = default;
        EntityUid core = default;
        EntityUid item = default;

        await server.WaitIdleAsync();
        await server.WaitPost(() =>
        {
            user = entMan.SpawnEntity("MobHuman", coords);
            var hands = entMan.EnsureComponent<HandsComponent>(user);
            entMan.EnsureComponent<ComplexInteractionComponent>(user);
            if (hands.ActiveHandId == null)
                handSys.AddHand(user, "hand", HandLocation.Left);

            // Apply exactly the components the token wizard game rule gives its antagonist.
            var rule = entMan.SpawnEntity("TokenWizard", coords);
            var definition = entMan.GetComponent<AntagSelectionComponent>(rule).Definitions.Single();
            entMan.AddComponents(user, definition.Components);
            entMan.QueueDeleteEntity(rule);

            grimoire = entMan.SpawnEntity("WizardsGrimoire", coords);
            Assert.That(handSys.TryPickup(user, grimoire, hands.ActiveHandId), Is.True);
            Assert.That(entMan.HasComponent<CanEnchantComponent>(user), Is.True,
                "the TokenWizard antag definition does not grant CanEnchant");

            altar = entMan.SpawnEntity("AltarNanotrasen", coords);
            core = entMan.SpawnEntity("AnomalyCoreRock", coords);
            item = entMan.SpawnEntity("ClothingOuterOblivionEnforcer", coords);
            xformSys.SetMapCoordinates((user, entMan.GetComponent<TransformComponent>(user)), coords);
        });
        await server.WaitRunTicks(5);

        await server.WaitPost(() =>
            interaction.UserInteraction(user, entMan.GetComponent<TransformComponent>(item).Coordinates, item));
        await server.WaitRunTicks(5);

        await server.WaitAssertion(() =>
        {
            Assert.That(entMan.HasComponent<EnchantedComponent>(item), Is.True, "the wizard failed to enchant the item");
            Assert.That(entMan.GetComponent<EnchantedComponent>(item).Enchants, Is.Not.Empty);
        });

        await server.WaitPost(() =>
        {
            entMan.QueueDeleteEntity(grimoire);
            entMan.QueueDeleteEntity(altar);
            entMan.QueueDeleteEntity(core);
            entMan.QueueDeleteEntity(item);
            entMan.QueueDeleteEntity(user);
        });
        await pair.CleanReturnAsync();
    }
}
