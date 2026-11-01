// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from tgstation/tgstation (code/modules/fishing, AGPL-3.0-or-later).

using Content.IntegrationTests.Tests.Interaction;
using Content.Server._Mini.Fishing;
using Content.Shared._Mini.Fishing.Components;
using Content.Shared.Damage.Components;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;

namespace Content.IntegrationTests.Tests._Mini.Fishing;

[TestFixture]
public sealed class FishSuicideTest : InteractionTest
{
    [Test]
    public async Task SwallowedFishKillsAndDisappears()
    {
        var user = SEntMan.GetEntity(await Spawn("MobHuman"));
        var fish = SEntMan.GetEntity(await Spawn("FishFryish"));

        await Server.WaitPost(() =>
        {
            var system = SEntMan.System<FishSuicideSystem>();
            Assert.That(system.TryFishSuicide((fish, SEntMan.GetComponent<FishComponent>(fish)), user), Is.True);
        });

        await Server.WaitRunTicks(2);

        await Server.WaitPost(() =>
        {
            var mobState = SEntMan.System<MobStateSystem>();
            var state = SEntMan.GetComponent<MobStateComponent>(user);
            var damageable = SEntMan.GetComponent<DamageableComponent>(user);
            Assert.That(mobState.IsDead(user), Is.True, $"Swallowing a fish should be lethal, state {state.CurrentState}, damage {damageable.Damage.GetTotal()}");
            Assert.That(SEntMan.EntityExists(fish), Is.False, "The swallowed fish is gone, like tg qdel(src)");
        });
    }

    [Test]
    public async Task ChainsawFishStaysAfterSuicide()
    {
        var user = SEntMan.GetEntity(await Spawn("MobHuman"));
        var fish = SEntMan.GetEntity(await Spawn("FishChainsawfish"));

        await Server.WaitPost(() =>
        {
            var system = SEntMan.System<FishSuicideSystem>();
            Assert.That(system.TryFishSuicide((fish, SEntMan.GetComponent<FishComponent>(fish)), user), Is.True);
        });

        await Server.WaitRunTicks(2);

        await Server.WaitPost(() =>
        {
            var mobState = SEntMan.System<MobStateSystem>();
            Assert.That(mobState.IsDead(user), Is.True);
            Assert.That(SEntMan.EntityExists(fish), Is.True, "The chainsaw fish is not consumed");
        });
    }
}
