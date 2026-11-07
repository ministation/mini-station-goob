// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._Goobstation.Wizard.SpellCards;
using Content.Shared.Friction;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._Goobstation.Wizard;

/// <summary>
/// Reproduces the production line
/// "system.tile_friction_controller: Can't resolve "Content.Shared.Friction.TileFrictionModifierComponent"
/// on entity ... (MobCatFloppa, ...)!".
/// An admin ran "addcomp &lt;mob&gt; SpellCard" on a pet, and the "target lost" branch of
/// <see cref="SpellCardSystem"/> passed that mob to <see cref="TileFrictionController.SetModifier"/>
/// although a mob has no friction modifier, which logged an error every tick.
/// </summary>
[TestFixture]
[TestOf(typeof(SpellCardSystem))]
public sealed class SpellCardMissingFrictionTest
{
    [Test]
    public async Task SpellCardOnMobWithoutFrictionModifierDoesNotLogError()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var server = pair.Server;
        var entMan = server.EntMan;
        var map = await pair.CreateTestMap();

        await server.WaitPost(() =>
        {
            var floppa = entMan.SpawnEntity("MobCatFloppa", map.GridCoords);
            Assert.That(entMan.HasComponent<TileFrictionModifierComponent>(floppa), Is.False,
                "the reproducing entity must have no friction modifier");

            entMan.AddComponent<SpellCardComponent>(floppa);
        });

        // The card has no target, so the update takes the "lost target" branch that used
        // to call SetModifier on the mob without the component and log an error.
        await server.WaitRunTicks(5);

        await pair.CleanReturnAsync();
    }
}
