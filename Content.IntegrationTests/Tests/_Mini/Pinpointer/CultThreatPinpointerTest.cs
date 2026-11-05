// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Heretic;
using Content.Shared.Interaction;
using Content.Shared.Pinpointer;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._Mini.Pinpointer;

/// <summary>
/// Activating the ERT chaplain pinpointer must apply its current mode targets even though
/// <see cref="Content.Server.Pinpointer.PinpointerSystem"/> handles the activation event first.
/// </summary>
[TestFixture]
public sealed class CultThreatPinpointerTest
{
    [Test]
    public async Task ActivationAppliesModeTargets()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Connected = true,
            DummyTicker = false,
            Dirty = true
        });
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var mapData = await pair.CreateTestMap();

        EntityUid pinpointer = default;
        EntityUid threat = default;
        var handled = false;

        await server.WaitPost(() =>
        {
            threat = entMan.SpawnEntity("MobHuman", mapData.GridCoords);
            entMan.AddComponent<HereticComponent>(threat);
            pinpointer = entMan.SpawnEntity("PinpointerCult", mapData.GridCoords);

            var ev = new ActivateInWorldEvent(threat, pinpointer, true);
            entMan.EventBus.RaiseLocalEvent(pinpointer, ev);
            handled = ev.Handled;
        });

        await server.WaitAssertion(() =>
        {
            Assert.That(handled, Is.True);

            var comp = entMan.GetComponent<PinpointerComponent>(pinpointer);
            Assert.That(comp.IsActive, Is.True, "the pinpointer should activate on use");
            Assert.That(comp.Targets, Does.Contain(threat), "activation should apply the auto mode targets");
        });

        await pair.CleanReturnAsync();
    }
}
