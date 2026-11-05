// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.StatusEffectNew;
using Content.Shared.Stunnable;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;

namespace Content.IntegrationTests.Tests.StatusEffects;

/// <summary>
/// Status effect entities are spawned into the target's status effect container. Applying an effect while
/// the target is already terminating used to spawn the effect and hit the engine's insert guard, logging
/// "Attempted to insert an entity ... into an entity that is terminating" for every shutdown handler that
/// stuns its owner (mind control, inner body anomaly) during round cleanup.
/// </summary>
[TestFixture]
[TestOf(typeof(StatusEffectsSystem))]
public sealed class StatusEffectTerminatingTest
{
    // Discovered on both sides like any content system. The client instance never has a Target and does nothing.
    public sealed class TerminatingStatusEffectTestSystem : EntitySystem
    {
        [Dependency] private readonly StatusEffectsSystem _status = default!;

        public EntityUid Target;
        public bool? AddResult;

        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<EntityTerminatingEvent>(OnTerminating);
        }

        private void OnTerminating(ref EntityTerminatingEvent args)
        {
            if (args.Entity.Owner != Target)
                return;

            AddResult = _status.TryAddStatusEffectDuration(
                args.Entity.Owner,
                SharedStunSystem.StunId,
                TimeSpan.FromSeconds(1));
        }
    }

    [Test]
    public async Task AddingStatusEffectWhileTerminatingIsIgnored()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        EntityUid mob = default;
        await server.WaitPost(() =>
        {
            mob = server.EntMan.SpawnEntity("MobHuman", map.GridCoords);

            // Status effects live in a container on the target. Make sure it exists while the mob is alive,
            // so that the attempted insertion below reaches the engine's terminating-entity check.
            Assert.That(server.System<StatusEffectsSystem>().TryAddStatusEffectDuration(
                mob, "StatusEffectStutter", TimeSpan.FromSeconds(1)), Is.True);

            server.System<TerminatingStatusEffectTestSystem>().Target = mob;
        });

        var testSystem = server.System<TerminatingStatusEffectTestSystem>();

        await server.WaitPost(() => server.EntMan.QueueDeleteEntity(mob));

        // Deletion runs on the next tick. Without the guard this logs an error, which fails the test.
        await server.WaitRunTicks(2);

        await server.WaitAssertion(() => Assert.That(testSystem.AddResult, Is.False));

        await pair.CleanReturnAsync();
    }
}
