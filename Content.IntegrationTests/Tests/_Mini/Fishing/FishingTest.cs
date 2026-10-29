// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using System.Numerics;
using System.Reflection;
using Content.Client._Mini.Fishing.Overlays;
using Content.Client._Mini.Fishing.UI;
using Content.Shared._Mini.Fishing;
using Content.Shared._Mini.Fishing.Components;
using Content.IntegrationTests.Tests.Interaction;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.GameObjects;
using Robust.Shared.Input;

namespace Content.IntegrationTests.Tests._Mini.Fishing;

[TestFixture]
public sealed class FishingTest : InteractionTest
{
    /// <summary>
    /// Casting at water starts a challenge, the bite phase rolls loot, and a finished minigame gives a fish.
    /// </summary>
    [Test]
    public async Task CastFightCatchTest()
    {
        await SpawnTarget("FloorWaterEntity");
        await PlaceInHands("FishingRod");
        await Interact();
        await RunTicks(5);

        var player = ToServer(Player);
        Assert.That(SEntMan.TryGetComponent(player, out FishingChallengeComponent? challenge),
            "Casting a rod at water did not start a fishing challenge.");

        await Server.WaitPost(() => challenge!.NextPhaseTime = STiming.CurTime - TimeSpan.FromSeconds(1));
        await RunTicks(10);

        Assert.That(challenge!.Phase, Is.EqualTo(FishingPhase.Biting),
            "Fishing challenge did not enter the biting phase.");

        // Make the roll deterministic: water may roll a dud.
        await Server.WaitPost(() =>
        {
            challenge.Reward = "FishGoldfish";
            challenge.RewardEntity = null;
        });

        // Skip the click window and go straight to the minigame with the bait on the fish.
        await Server.WaitPost(() =>
        {
            challenge.Phase = FishingPhase.Minigame;
            challenge.Completion = 99f;
            challenge.CompletionGain = 100f;
            challenge.BaitHeight = 300f;
            challenge.FishHeight = 50f;
            challenge.FishPosition = 500f;
            challenge.BaitPosition = 500f;
            challenge.BaitVelocity = 0f;
            challenge.ReelDirection = 0;
        });

        await RunTicks(20);

        var fishCount = 0;
        await Server.WaitPost(() =>
        {
            var query = SEntMan.EntityQueryEnumerator<FishComponent>();
            while (query.MoveNext(out _))
                fishCount++;
        });

        Assert.That(fishCount, Is.GreaterThan(0), "Finishing the minigame did not spawn any fish.");
    }

    /// <summary>
    /// The client must receive the challenge, the lure and the lure's rod, otherwise the line and
    /// the minigame indicator next to the float cannot be drawn.
    /// </summary>
    [Test]
    public async Task ClientSeesFishingLineStateTest()
    {
        await SpawnTarget("FloorWaterEntity");
        await PlaceInHands("FishingRod");
        await Interact();
        await RunTicks(5);

        await Client.WaitAssertion(() =>
        {
            Assert.That(CEntMan.TryGetComponent<FishingChallengeComponent>(CPlayer, out var challenge), Is.True,
                "The client player has no fishing challenge.");

            Assert.That(challenge!.Rod, Is.Not.Null, "The challenge rod was not synced to the client.");
            Assert.That(challenge.Lure, Is.Not.Null, "The challenge lure was not synced to the client.");

            var lure = challenge.Lure!.Value;
            Assert.That(CEntMan.TryGetComponent<FishingLureComponent>(lure, out var lureComp), Is.True,
                "The client has no FishingLureComponent on the lure entity.");
            Assert.That(lureComp!.Rod, Is.Not.Null, "The lure's rod was not synced to the client.");

            var overlay = Client.Resolve<IOverlayManager>();
            Assert.That(overlay.HasOverlay<FishingOverlay>(), Is.True,
                "The fishing overlay is not registered on the client.");
        });
    }

    /// <summary>
    /// The bite window must install the wheel input control, and one wheel notch has to start the fight.
    /// </summary>
    [Test]
    public async Task ClientWheelStartsMinigameTest()
    {
        await SpawnTarget("FloorWaterEntity");
        await PlaceInHands("FishingRod");
        await Interact();
        await RunTicks(5);

        var player = ToServer(Player);
        var challenge = SEntMan.GetComponent<FishingChallengeComponent>(player);

        await Client.WaitAssertion(() =>
        {
            var ui = Client.Resolve<IUserInterfaceManager>();
            Assert.That(ui.StateRoot.Children.OfType<FishingMinigameInputControl>().Any(), Is.False,
                "The wheel input control must not exist while waiting for a bite, the HUD has to stay usable.");
        });

        await Server.WaitPost(() => challenge.NextPhaseTime = STiming.CurTime - TimeSpan.FromSeconds(1));
        await RunTicks(5);

        await Client.WaitAssertion(() =>
        {
            Assert.That(CEntMan.TryGetComponent<FishingChallengeComponent>(CPlayer, out var clientChallenge), Is.True,
                "The client player has no fishing challenge.");
            Assert.That(clientChallenge!.Phase, Is.EqualTo(FishingPhase.Biting),
                "The client did not see the bite window.");

            var ui = Client.Resolve<IUserInterfaceManager>();
            var input = ui.StateRoot.Children.OfType<FishingMinigameInputControl>().FirstOrDefault();
            Assert.That(input, Is.Not.Null, "The input control was not created during the bite window.");
            Assert.That(input!.Size.X, Is.GreaterThan(0f), "The input control has zero width.");
            Assert.That(input.Size.Y, Is.GreaterThan(0f), "The input control has zero height.");

            ClickLmb(input);
        });

        await RunTicks(5);

        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.GetComponent<FishingChallengeComponent>(player).Phase, Is.EqualTo(FishingPhase.Minigame),
                "Scrolling the mouse wheel did not start the minigame.");
        });
    }

    private static void ClickLmb(Control control)
    {
        var method = typeof(Control).GetMethod("KeyBindDown", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null, "Control.KeyBindDown was not found.");

        var args = new GUIBoundKeyEventArgs(EngineKeyFunctions.UIClick, BoundKeyState.Down, default, true, Vector2.Zero, Vector2.Zero);
        method!.Invoke(control, new object[] { args });
    }

    private static void ScrollWheel(Control control, float y)
    {
        var method = typeof(Control).GetMethod("MouseWheel", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null, "Control.MouseWheel was not found.");

        var args = new GUIMouseWheelEventArgs(new Vector2(0f, y), control, Vector2.Zero, default, Vector2.Zero, Vector2.Zero);
        method!.Invoke(control, new object[] { args });
    }

    /// <summary>
    /// Walking away from the float must snap the line and stop the challenge.
    /// </summary>
    [Test]
    public async Task LineSnapsWhenWalkingAwayTest()
    {
        await SpawnTarget("FloorWaterEntity");
        await PlaceInHands("FishingRod");
        await Interact();
        await RunTicks(5);

        var player = ToServer(Player);
        Assert.That(SEntMan.TryGetComponent(player, out FishingChallengeComponent? challenge),
            "Casting a rod at water did not start a fishing challenge.");

        var lure = challenge!.Lure;
        Assert.That(lure, Is.Not.Null, "Casting a rod did not spawn a lure.");

        await Server.WaitPost(() =>
        {
            var mapCoords = Transform.GetMapCoordinates(player);
            Transform.SetWorldPosition(player, mapCoords.Position + new Vector2(30f, 0f));
        });

        await RunTicks(5);

        Assert.That(SEntMan.HasComponent<FishingChallengeComponent>(player), Is.False,
            "Walking far away from the float did not stop the fishing challenge.");

        await Server.WaitPost(() =>
        {
            var rod = SEntMan.EntityQueryEnumerator<FishingRodComponent>();
            while (rod.MoveNext(out _, out var rodComp))
                Assert.That(rodComp.FishingLure, Is.Null, "The rod still references the lure after the line snapped.");

            var lures = SEntMan.EntityQueryEnumerator<FishingLureComponent>();
            Assert.That(lures.MoveNext(out _, out _), Is.False, "The lure was not deleted after the line snapped.");
        });
    }

    /// <summary>
    /// Vending machines are dynamic fishing sources: the catch must come from the machine inventory.
    /// </summary>
    [Test]
    public async Task VendingDynamicSourceTest()
    {
        await SpawnTarget("VendingMachineCola");
        await PlaceInHands("FishingRod");
        await Interact();
        await RunTicks(5);

        var player = ToServer(Player);
        Assert.That(SEntMan.TryGetComponent(player, out FishingChallengeComponent? challenge),
            "Casting a rod at a vending machine did not start a fishing challenge.");

        await Server.WaitPost(() => challenge!.NextPhaseTime = STiming.CurTime - TimeSpan.FromSeconds(1));
        await RunTicks(10);

        Assert.That(challenge!.Phase, Is.EqualTo(FishingPhase.Biting),
            "Vending machine fishing challenge did not enter the biting phase.");
        Assert.That(challenge.Reward, Is.Not.Null,
            "Dynamic vending source did not roll any product.");

        var rewardId = challenge.Reward?.Id;
        Assert.That(rewardId, Is.Not.Null, "Dynamic vending source did not roll a concrete product id.");

        await Server.WaitPost(() =>
        {
            challenge.Phase = FishingPhase.Minigame;
            challenge.Completion = 99f;
            challenge.CompletionGain = 100f;
            challenge.BaitHeight = 300f;
            challenge.FishHeight = 50f;
            challenge.FishPosition = 500f;
            challenge.BaitPosition = 500f;
            challenge.BaitVelocity = 0f;
            challenge.ReelDirection = 0;
        });

        await RunTicks(20);

        var productCount = 0;
        await Server.WaitPost(() =>
        {
            var q = SEntMan.EntityQueryEnumerator<MetaDataComponent>();
            while (q.MoveNext(out _, out var meta))
            {
                if (meta.EntityPrototype?.ID == rewardId)
                    productCount++;
            }
        });

        Assert.That(productCount, Is.GreaterThan(0),
            "Finishing the minigame on a vending machine did not spawn the rolled product.");
    }
}