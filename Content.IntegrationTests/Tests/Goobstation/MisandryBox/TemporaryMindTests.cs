using System.Linq;
using Content.Goobstation.Server.MisandryBox.Mind;
using Content.Goobstation.Shared.MisandryBox.Mind;
using Content.IntegrationTests.Pair;
using Content.Server.GameTicking;
using Content.Server.Ghost;
using Content.Shared.GameTicking;
using Content.Shared.Ghost;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Robust.Server.Player;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Player;

namespace Content.IntegrationTests.Tests.Goobstation.MisandryBox;

[TestFixture]
public sealed class TemporaryMindTests
{
    [Test]
    public async Task SwapAndRestoreAsGhost()
    {
        await using var pair = await SetupPair();
        var ctx = await SetupContext(pair);

        await pair.Server.WaitPost(() =>
        {
            Assert.That(ctx.TempMindSys.TrySwapTempMind(ctx.Player, ctx.NewBody), Is.True);
        });
        await pair.RunTicksSync(5);

        EntityUid disposableMind = default;

        await pair.Server.WaitAssertion(() =>
        {
            Assert.That(ctx.Player.AttachedEntity, Is.EqualTo(ctx.NewBody));

            var temp = ctx.EntMan.GetComponent<TemporaryMindComponent>(ctx.NewBody);
            Assert.That(temp.OriginalMind, Is.EqualTo(ctx.OrigMindId));
            disposableMind = temp.DisposableMind;

            var origMind = ctx.EntMan.GetComponent<MindComponent>(ctx.OrigMindId);
            Assert.That(origMind.UserId, Is.Null, "Original mind userId should be null during swap");

            var dispMind = ctx.EntMan.GetComponent<MindComponent>(disposableMind);
            Assert.That(dispMind.UserId, Is.EqualTo(ctx.Player.UserId));
            Assert.That(ctx.MindSys.GetMind(ctx.Player.UserId), Is.EqualTo(disposableMind));
        });

        EntityUid? ghost = null;
        await pair.Server.WaitPost(() =>
        {
            ghost = ctx.TempMindSys.TryRestoreAsGhost(ctx.NewBody);
        });
        await pair.RunTicksSync(5);

        await pair.Server.WaitAssertion(() =>
        {
            Assert.That(ghost, Is.Not.Null, "TryRestoreAsGhost should return a ghost");
            Assert.That(ctx.EntMan.HasComponent<GhostComponent>(ghost!.Value));
            Assert.That(ctx.Player.AttachedEntity, Is.EqualTo(ghost!.Value));

            var origMind = ctx.EntMan.GetComponent<MindComponent>(ctx.OrigMindId);
            Assert.That(origMind.UserId, Is.EqualTo(ctx.Player.UserId));
            Assert.That(origMind.VisitingEntity, Is.EqualTo(ghost!.Value));
            Assert.That(origMind.OwnedEntity, Is.EqualTo(ctx.OriginalBody));

            Assert.That(ctx.MindSys.GetMind(ctx.Player.UserId), Is.EqualTo(ctx.OrigMindId));
            Assert.That(ctx.EntMan.Deleted(disposableMind) || ctx.EntMan.IsQueuedForDeletion(disposableMind));
            Assert.That(!ctx.EntMan.HasComponent<TemporaryMindComponent>(ctx.NewBody));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task SwapAndRestoreToOriginalBody()
    {
        await using var pair = await SetupPair();
        var ctx = await SetupContext(pair);

        await pair.Server.WaitPost(() =>
        {
            Assert.That(ctx.TempMindSys.TrySwapTempMind(ctx.Player, ctx.NewBody), Is.True);
        });
        await pair.RunTicksSync(5);

        await pair.Server.WaitPost(() =>
        {
            Assert.That(ctx.TempMindSys.TryRestoreToOriginalBody(ctx.NewBody), Is.True);
        });
        await pair.RunTicksSync(5);

        await pair.Server.WaitAssertion(() =>
        {
            Assert.That(ctx.Player.AttachedEntity, Is.EqualTo(ctx.OriginalBody));

            var origMind = ctx.EntMan.GetComponent<MindComponent>(ctx.OrigMindId);
            Assert.That(origMind.UserId, Is.EqualTo(ctx.Player.UserId));
            Assert.That(origMind.OwnedEntity, Is.EqualTo(ctx.OriginalBody));

            Assert.That(ctx.MindSys.GetMind(ctx.Player.UserId), Is.EqualTo(ctx.OrigMindId));
            Assert.That(!ctx.EntMan.HasComponent<TemporaryMindComponent>(ctx.NewBody));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task OriginalBodyDeletedFallsBackToGhost()
    {
        await using var pair = await SetupPair();
        var ctx = await SetupContext(pair);

        await pair.Server.WaitPost(() =>
        {
            Assert.That(ctx.TempMindSys.TrySwapTempMind(ctx.Player, ctx.NewBody), Is.True);
        });
        await pair.RunTicksSync(5);

        await pair.Server.WaitPost(() =>
        {
            ctx.EntMan.DeleteEntity(ctx.OriginalBody);
        });
        await pair.RunTicksSync(5);

        await pair.Server.WaitAssertion(() =>
        {
            Assert.That(ctx.TempMindSys.TryRestoreToOriginalBody(ctx.NewBody), Is.False,
                "TryRestoreToOriginalBody should fail when original body is deleted");
        });

        EntityUid? ghost = null;
        await pair.Server.WaitPost(() =>
        {
            ghost = ctx.TempMindSys.TryRestoreAsGhost(ctx.NewBody);
        });
        await pair.RunTicksSync(5);

        await pair.Server.WaitAssertion(() =>
        {
            Assert.That(ghost, Is.Not.Null, "TryRestoreAsGhost should still succeed");
            Assert.That(ctx.EntMan.HasComponent<GhostComponent>(ghost!.Value));
            Assert.That(ctx.Player.AttachedEntity, Is.EqualTo(ghost!.Value));

            var origMind = ctx.EntMan.GetComponent<MindComponent>(ctx.OrigMindId);
            Assert.That(origMind.UserId, Is.EqualTo(ctx.Player.UserId));
            Assert.That(ctx.MindSys.GetMind(ctx.Player.UserId), Is.EqualTo(ctx.OrigMindId));
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// Arena/Thunderdome regression: the player joins the arena while a ghost whose mind OWNS the
    /// ghost entity (living player used the ghost command, or a lobby observer). After the swap the
    /// session must control the new body and keep controlling it after the old ghost is deleted.
    /// </summary>
    [Test]
    public async Task SwapFromOwnedGhostAttachesSessionToNewBody()
    {
        await using var pair = await SetupPair();
        var ctx = await SetupGhostContext(pair, killFirst: false);

        await pair.Server.WaitPost(() =>
        {
            Assert.That(ctx.TempMindSys.TrySwapTempMind(ctx.Player, ctx.NewBody), Is.True);
        });
        // The old ghost is queue-deleted during the swap; let its deletion and any cascades run.
        await pair.RunTicksSync(10);

        await pair.Server.WaitAssertion(() =>
        {
            Assert.That(ctx.Player.AttachedEntity, Is.EqualTo(ctx.NewBody),
                "session must be attached to the new body after swapping from a ghost");
        });

        await pair.RunTicksSync(30);

        await pair.Server.WaitAssertion(() =>
        {
            Assert.That(ctx.Player.AttachedEntity, Is.EqualTo(ctx.NewBody),
                "session must stay attached to the new body after the old ghost is cleaned up");
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// Same regression for a dead player's ghost: the mind owns the corpse and only VISITS the
    /// ghost. UnVisit re-attaches the session to the corpse mid-swap; the swap must still end with
    /// the session controlling the new body.
    /// </summary>
    [Test]
    public async Task SwapFromVisitingGhostAttachesSessionToNewBody()
    {
        await using var pair = await SetupPair();
        var ctx = await SetupGhostContext(pair, killFirst: true);

        await pair.Server.WaitPost(() =>
        {
            Assert.That(ctx.TempMindSys.TrySwapTempMind(ctx.Player, ctx.NewBody), Is.True);
        });
        await pair.RunTicksSync(10);

        await pair.Server.WaitAssertion(() =>
        {
            Assert.That(ctx.Player.AttachedEntity, Is.EqualTo(ctx.NewBody),
                "session must be attached to the new body after swapping from a visiting ghost");
        });

        await pair.RunTicksSync(30);

        await pair.Server.WaitAssertion(() =>
        {
            Assert.That(ctx.Player.AttachedEntity, Is.EqualTo(ctx.NewBody),
                "session must stay attached to the new body after the old ghost is cleaned up");
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// Ghosts the player like the "ghost" command does and leaves the session attached to the
    /// ghost, then prepares a fresh body for the swap.
    /// </summary>
    private static async Task<TestContext> SetupGhostContext(TestPair pair, bool killFirst)
    {
        var server = pair.Server;
        var entMan = server.EntMan;
        var mindSys = server.System<SharedMindSystem>();
        var tempMindSys = server.System<TemporaryMindSystem>();
        var ghostSys = server.System<GhostSystem>();
        var mobState = server.System<MobStateSystem>();
        var playerMan = server.ResolveDependency<IPlayerManager>();
        var player = playerMan.Sessions.Single();

        if (killFirst)
        {
            await server.WaitPost(() =>
            {
                var body = player.AttachedEntity!.Value;
                mobState.ChangeMobState(body, MobState.Dead);
            });
            await pair.RunTicksSync(5);
        }

        await server.WaitPost(() =>
        {
            var body = player.AttachedEntity!.Value;
            Assert.That(mindSys.TryGetMind(body, out var mindId, out _), Is.True,
                "player should have a mind");
            Assert.That(ghostSys.OnGhostAttempt(mindId, true), Is.True, "ghost attempt should succeed");
        });
        await pair.RunTicksSync(10);

        var ctx = new TestContext
        {
            EntMan = entMan,
            MindSys = mindSys,
            TempMindSys = tempMindSys,
            Player = player,
        };

        await server.WaitAssertion(() =>
        {
            Assert.That(player.AttachedEntity, Is.Not.Null, "player should be attached to a ghost");
            var ghost = player.AttachedEntity!.Value;
            Assert.That(entMan.HasComponent<GhostComponent>(ghost), "player should be a ghost");
            Assert.That(mindSys.TryGetMind(ghost, out var origMindId, out _), Is.True,
                "ghost should resolve to the player's mind");
            ctx.OrigMindId = origMindId;
            ctx.OriginalBody = default; // original body may be deleted/dead, not needed here
        });

        await server.WaitPost(() =>
        {
            ctx.NewBody = entMan.SpawnEntity(null, MapCoordinates.Nullspace);
            entMan.EnsureComponent<MindContainerComponent>(ctx.NewBody);
        });

        await pair.RunTicksSync(5);
        return ctx;
    }

    private static async Task<TestPair> SetupPair()
    {
        var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            DummyTicker = false,
            Connected = true,
            Dirty = true,
            InLobby = true,
        });

        var server = pair.Server;
        var ticker = server.System<GameTicker>();
        await server.WaitPost(() =>
        {
            ticker.SetGamePreset("Sandbox");
            ticker.ClearGameRules();
            ticker.ToggleReadyAll(true);
            ticker.StartRound(force: true);
        });

        await PoolManager.WaitUntil(server,
            () => ticker.RunLevel == GameRunLevel.InRound && pair.Player?.AttachedEntity != null,
            maxTicks: 600);

        await pair.RunTicksSync(10);

        return pair;
    }

    private static async Task<TestContext> SetupContext(TestPair pair)
    {
        var server = pair.Server;
        var entMan = server.EntMan;
        var mindSys = server.System<SharedMindSystem>();
        var tempMindSys = server.System<TemporaryMindSystem>();
        var playerMan = server.ResolveDependency<IPlayerManager>();
        var player = playerMan.Sessions.Single();

        var ctx = new TestContext
        {
            EntMan = entMan,
            MindSys = mindSys,
            TempMindSys = tempMindSys,
            Player = player,
        };

        await server.WaitPost(() =>
        {
            Assert.That(mindSys.TryGetMind(player, out var origMindId, out _), Is.True,
                "Player should have a mind after round start");
            Assert.That(origMindId, Is.Not.EqualTo(EntityUid.Invalid));
            ctx.OrigMindId = origMindId;

            ctx.OriginalBody = entMan.SpawnEntity(null, MapCoordinates.Nullspace);
            entMan.EnsureComponent<MindContainerComponent>(ctx.OriginalBody);
            mindSys.SetGhostOnShutdown(ctx.OriginalBody, false);
            mindSys.TransferTo(ctx.OrigMindId, ctx.OriginalBody);
            playerMan.SetAttachedEntity(player, ctx.OriginalBody);

            ctx.NewBody = entMan.SpawnEntity(null, MapCoordinates.Nullspace);
            entMan.EnsureComponent<MindContainerComponent>(ctx.NewBody);
        });

        await pair.RunTicksSync(5);
        return ctx;
    }

    private sealed class TestContext
    {
        public IEntityManager EntMan = default!;
        public SharedMindSystem MindSys = default!;
        public TemporaryMindSystem TempMindSys = default!;
        public ICommonSession Player = default!;
        public EntityUid OriginalBody;
        public EntityUid NewBody;
        public EntityUid OrigMindId;
    }
}
