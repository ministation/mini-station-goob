// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Content.Shared.Ghost;
using Content.Shared.Prayer;
using Content.Shared.Verbs;
using Robust.Server.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.Localization;
using Robust.Shared.Map;
using Robust.Shared.Player;

namespace Content.IntegrationTests.Tests.Prayer;

[TestFixture]
public sealed class PrayerGhostTest
{
    /// <summary>
    /// Regression test for ghosts being able to use the red phone's prayer verb.
    /// The red phone is also an instrument with ActivatableUI, and ghosts are allowed to open UIs,
    /// which previously made CanInteract return true for them and exposed the prayer verb.
    /// </summary>
    [Test]
    public async Task GhostCannotCallRedPhone()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            DummyTicker = false,
            Connected = true,
            Dirty = true,
        });

        var server = pair.Server;
        var entMan = server.ResolveDependency<IServerEntityManager>();
        var playerMan = server.ResolveDependency<ISharedPlayerManager>();
        var verbSystem = entMan.System<SharedVerbSystem>();
        var ghostSystem = entMan.System<SharedGhostSystem>();
        var loc = server.ResolveDependency<ILocalizationManager>();

        Assert.That(pair.Client.Session, Is.Not.Null);
        var session = playerMan.GetSessionById(pair.Client.Session!.UserId);

        await pair.CreateTestMap();
        var coordinates = new MapCoordinates(new Vector2(0.5f, 0.5f), pair.TestMap!.MapId);

        EntityUid ghost = default;
        EntityUid phone = default;
        await server.WaitPost(() =>
        {
            ghost = entMan.SpawnEntity("MobObserver", coordinates);
            phone = entMan.SpawnEntity("PhoneInstrument", coordinates);
            playerMan.SetAttachedEntity(session, ghost);
        });

        await pair.RunTicksSync(5);

        var callVerbText = loc.GetString("prayer-verbs-call");
        var ghostVerbs = new List<string>();
        await server.WaitPost(() =>
        {
            var verbs = verbSystem.GetLocalVerbs(phone, ghost, new List<Type> { typeof(ActivationVerb) }, out _);
            ghostVerbs = verbs.Select(verb => verb.Text).ToList();
        });

        Assert.Multiple(() =>
        {
            Assert.That(entMan.HasComponent<GhostComponent>(ghost), "Test entity is not a ghost.");
            Assert.That(entMan.HasComponent<PrayableComponent>(phone), "Test entity is not prayable.");
            Assert.That(ghostVerbs, Does.Not.Contain(callVerbText), "A ghost got the prayer/call verb.");
        });

        // Admin ghosts are allowed to interact and must still be able to call.
        await server.WaitPost(() =>
        {
            ghostSystem.SetCanGhostInteract(ghost, true);
        });

        var adminGhostVerbs = new List<string>();
        await server.WaitPost(() =>
        {
            var verbs = verbSystem.GetLocalVerbs(phone, ghost, new List<Type> { typeof(ActivationVerb) }, out _);
            adminGhostVerbs = verbs.Select(verb => verb.Text).ToList();
        });

        Assert.That(adminGhostVerbs, Does.Contain(callVerbText),
            "An admin ghost should still get the prayer/call verb.");

        await pair.CleanReturnAsync();
    }
}
