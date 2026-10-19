// SPDX-FileCopyrightText: 2026 Casha
// Мини-станция/Freaky-station, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/ministation/mini-station-goob/master/LICENSE.TXT

using System.Linq;
using Content.Client._Mini.DailyRewards;
using Content.Server._Mini.AntagTokens;
using Content.Server._Mini.DailyRewards;
using Content.Shared._Mini.DailyRewards;
using Content.Shared.Input;
using NUnit.Framework;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Server.Player;
using Robust.Shared.GameObjects;
using Robust.Shared.Input;
using Robust.Shared.Map;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.Tests._Mini;

/// <summary>
/// End-to-end check that the "Забрать" button in the daily rewards menu actually claims the reward.
/// Goes through the real client UI window, the network event and the server system.
/// </summary>
[TestFixture]
public sealed class DailyRewardClaimTest
{
    [Test]
    public async Task ClaimButtonGrantsReward()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Connected = true,
            DummyTicker = true,
        });

        var server = pair.Server;
        var client = pair.Client;

        await server.WaitIdleAsync();
        await client.WaitIdleAsync();

        var sEntMan = server.ResolveDependency<IEntityManager>();
        var playerMan = server.ResolveDependency<IPlayerManager>();
        var daily = sEntMan.System<DailyRewardSystem>();
        var tokens = sEntMan.System<AntagTokenSystem>();

        var session = playerMan.Sessions.Single();

        // Force the daily reward into "ready to claim" state.
        await server.WaitPost(() =>
        {
            Assert.That(daily.TryOpenForSession(session), Is.True, "Failed to open daily rewards for session");
            Assert.That(daily.MakeReadyToClaim(session.UserId), Is.True, "Failed to make reward ready");
            daily.RefreshUi(session.UserId);
        });

        await pair.RunTicksSync(10);

        // Open the menu on the client the same way the lobby "Награды" button does.
        await client.WaitPost(() =>
        {
            var uiSystem = client.ResolveDependency<IEntityManager>().System<DailyRewardUiSystem>();
            uiSystem.RequestOpen();
        });

        await pair.RunTicksSync(10);

        DailyRewardWindow? window = null;
        Button? claimButton = null;
        await client.WaitPost(() =>
        {
            var ui = client.ResolveDependency<IUserInterfaceManager>();
            window = ui.WindowRoot.Children.OfType<DailyRewardWindow>().FirstOrDefault(w => w.IsOpen);
            Assert.That(window, Is.Not.Null, "Daily reward window did not open");

            var field = typeof(DailyRewardWindow).GetField("_claimButton",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            claimButton = field?.GetValue(window) as Button;
            Assert.That(claimButton, Is.Not.Null, "Claim button field was not found");
        });

        Assert.That(claimButton!.Disabled, Is.False, "Claim button is disabled even though the reward is ready");

        // Verify the button can actually be reached by a mouse click: something may cover it.
        var center = new ScreenCoordinates(
            claimButton.GlobalPixelPosition + claimButton.PixelSize / 2,
            claimButton.Window?.Id ?? default);

        Control? hit = null;
        await client.WaitPost(() =>
        {
            var ui = client.ResolveDependency<IUserInterfaceManager>();
            hit = ui.MouseGetControl(center);
        });

        var reachedByClick = false;
        for (var c = hit; c != null; c = c.Parent)
        {
            if (c == claimButton)
            {
                reachedByClick = true;
                break;
            }
        }

        TestContext.Out.WriteLine($"window rect: {window!.GlobalPixelPosition} size {window.PixelSize}");
        TestContext.Out.WriteLine($"claim button rect: {claimButton.GlobalPixelPosition} size {claimButton.PixelSize}");
        TestContext.Out.WriteLine($"hit control at button center: {hit?.GetType().Name ?? "<none>"}");
        Assert.That(reachedByClick, Is.True, "The claim button is covered by another control and cannot be clicked");

        var balanceBefore = tokens.GetBalance(session.UserId);

        // Click the button (same sequence as InteractionTest.ClickControl).
        var screenCoords = new ScreenCoordinates(
            claimButton.GlobalPixelPosition + claimButton.PixelSize / 2,
            claimButton.Window?.Id ?? default);

        var relativePos = screenCoords.Position / claimButton.UIScale - claimButton.GlobalPosition;
        var relativePixelPos = screenCoords.Position - claimButton.GlobalPixelPosition;

        await client.DoGuiEvent(claimButton, new GUIBoundKeyEventArgs(
            EngineKeyFunctions.UIClick,
            BoundKeyState.Down,
            screenCoords,
            default,
            relativePos,
            relativePixelPos));

        await pair.RunTicksSync(2);

        await client.DoGuiEvent(claimButton, new GUIBoundKeyEventArgs(
            EngineKeyFunctions.UIClick,
            BoundKeyState.Up,
            screenCoords,
            default,
            relativePos,
            relativePixelPos));

        await pair.RunTicksSync(15);

        await server.WaitPost(() =>
        {
            Assert.That(daily.TryGetDebugState(session.UserId, out var progress), Is.True);
            Assert.That(progress!.LastClaimTime, Is.Not.Null, "Claim was not registered on the server");
            Assert.That(progress.CurrentStreak, Is.EqualTo(1), "Streak did not advance after claim");
        });

        var balanceAfter = tokens.GetBalance(session.UserId);
        Assert.That(balanceAfter, Is.GreaterThan(balanceBefore), "Coin balance did not increase after claim");

        // The player must see feedback in the window, otherwise the button looks dead.
        Button? buttonAfter = null;
        Label? streakAfter = null;
        Label? statusAfter = null;
        await client.WaitPost(() =>
        {
            var buttonField = typeof(DailyRewardWindow).GetField("_claimButton",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            buttonAfter = buttonField?.GetValue(window) as Button;

            var streakField = typeof(DailyRewardWindow).GetField("_streakValueLabel",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            streakAfter = streakField?.GetValue(window) as Label;

            var statusField = typeof(DailyRewardWindow).GetField("_claimStatusLabel",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            statusAfter = statusField?.GetValue(window) as Label;
        });

        TestContext.Out.WriteLine($"after claim: button text='{buttonAfter?.Text}' disabled={buttonAfter?.Disabled}");
        TestContext.Out.WriteLine($"after claim: streak label='{streakAfter?.Text}'");
        TestContext.Out.WriteLine($"after claim: status label='{statusAfter?.Text}' visible={statusAfter?.Visible}");

        Assert.That(buttonAfter!.Disabled, Is.True, "Claim button stayed enabled after the claim");
        Assert.That(streakAfter!.Text, Does.Contain("1"), "Streak label did not refresh after the claim");
        Assert.That(statusAfter!.Visible, Is.True, "No claim result was shown in the window");
        Assert.That(statusAfter.Text, Is.Not.Null.And.Not.Empty, "Claim result text is empty");

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task RejectedClaimShowsReason()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Connected = true,
            DummyTicker = true,
        });

        var server = pair.Server;
        var client = pair.Client;

        await server.WaitIdleAsync();
        await client.WaitIdleAsync();

        var sEntMan = server.ResolveDependency<IEntityManager>();
        var playerMan = server.ResolveDependency<IPlayerManager>();
        var daily = sEntMan.System<DailyRewardSystem>();
        var session = playerMan.Sessions.Single();

        // Fresh progress: no playtime yet, so the claim must be rejected.
        await server.WaitPost(() =>
        {
            Assert.That(daily.TryOpenForSession(session), Is.True);
            Assert.That(daily.ResetProgress(session.UserId), Is.True);
            daily.RefreshUi(session.UserId);
        });

        await pair.RunTicksSync(10);

        await client.WaitPost(() =>
            client.ResolveDependency<IEntityManager>().System<DailyRewardUiSystem>().RequestOpen());
        await pair.RunTicksSync(10);

        DailyRewardWindow? window = null;
        await client.WaitPost(() =>
        {
            var ui = client.ResolveDependency<IUserInterfaceManager>();
            window = ui.WindowRoot.Children.OfType<DailyRewardWindow>().FirstOrDefault(w => w.IsOpen);
            Assert.That(window, Is.Not.Null, "Daily reward window did not open");
        });

        var net = client.ResolveDependency<IEntityNetworkManager>();
        DailyRewardClaimResultEvent? result = null;
        net.ReceivedSystemMessage += (_, message) =>
        {
            if (message is DailyRewardClaimResultEvent claimResult)
                result = claimResult;
        };

        // Send the request directly: the button is correctly disabled when the reward is locked,
        // but the server must still explain why a claim attempt was refused.
        await client.WaitPost(() => net.SendSystemNetworkMessage(new DailyRewardClaimRequestEvent()));
        await pair.RunTicksSync(15);

        DailyRewardClaimResultEvent? captured = null;
        Label? statusAfter = null;
        await client.WaitPost(() =>
        {
            captured = result;

            var statusField = typeof(DailyRewardWindow).GetField("_claimStatusLabel",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            statusAfter = statusField?.GetValue(window!) as Label;
        });

        TestContext.Out.WriteLine($"rejected claim: result={captured?.Granted} reason={captured?.ReasonLocKey}");
        TestContext.Out.WriteLine($"rejected claim: status label='{statusAfter?.Text}' visible={statusAfter?.Visible}");

        Assert.That(captured, Is.Not.Null, "Server did not answer the claim request");
        Assert.That(captured!.Granted, Is.False, "Locked reward was granted");
        Assert.That(captured.ReasonLocKey, Is.Not.Null.And.Not.Empty, "Rejection has no reason");
        Assert.That(statusAfter!.Visible, Is.True, "Rejection reason is not shown in the window");
        Assert.That(statusAfter.Text, Is.Not.Null.And.Not.Empty, "Rejection reason text is empty");

        await server.WaitPost(() =>
        {
            Assert.That(daily.TryGetDebugState(session.UserId, out var progress), Is.True);
            Assert.That(progress!.LastClaimTime, Is.Null, "Rejected claim must not register a claim");
            Assert.That(progress.CurrentStreak, Is.EqualTo(0), "Rejected claim must not advance the streak");
        });

        await pair.CleanReturnAsync();
    }
}
