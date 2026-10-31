// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from tgstation/tgstation (code/modules/fishing, AGPL-3.0-or-later).

using System.Numerics;
using Content.Client._Mini.Fishing.Overlays;
using Content.Client._Mini.Fishing.UI;
using Content.Shared._Mini.Fishing;
using Content.Shared._Mini.Fishing.Components;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.GameObjects;
using Robust.Shared.GameStates;
using Robust.Shared.Map;
using Robust.Shared.Player;

namespace Content.Client._Mini.Fishing;

public sealed partial class FishingSystem : EntitySystem
{
    [Dependency] private IOverlayManager _overlay = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IUserInterfaceManager _ui = default!;
    [Dependency] private IEyeManager _eye = default!;
    [Dependency] private SharedTransformSystem _xform = default!;

    // The minigame panel is drawn next to the float: the bar plus the wheel hint is about
    // 1.9 m wide and 3.8 m tall, and its center sits a bit to the right of the float.
    private static readonly Vector2 PanelSize = new(1.9f, 3.8f);
    private static readonly Vector2 PanelOffset = new(1.05f, 0f);

    // The bite marker is a small sprite, but the hook click is forgiving on purpose.
    private static readonly Vector2 BiteSize = new(5f, 5f);

    private FishingMinigameInputControl? _input;

    public override void Initialize()
    {
        base.Initialize();

        _overlay.AddOverlay(new FishingOverlay());

        SubscribeLocalEvent<FishingChallengeComponent, AfterAutoHandleStateEvent>(OnChallengeState);
        SubscribeLocalEvent<FishingChallengeComponent, ComponentShutdown>(OnChallengeShutdown);
        SubscribeLocalEvent<LocalPlayerDetachedEvent>(OnLocalPlayerDetached);
    }

    public override void Shutdown()
    {
        base.Shutdown();

        _overlay.RemoveOverlay<FishingOverlay>();
        RemoveInput();
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_input == null ||
            _player.LocalEntity is not { } local ||
            !TryComp<FishingChallengeComponent>(local, out var challenge))
            return;

        if (challenge.Phase is not (FishingPhase.Biting or FishingPhase.Minigame))
            return;

        var anchor = GetAnchor(challenge, local);
        if (_eye.CurrentEye.Position.MapId != anchor.MapId)
            return;

        // Both ends go through the same screen projection, so camera zoom cancels out.
        var anchorPx = _eye.WorldToScreen(anchor.Position);
        var meterPx = (_eye.WorldToScreen(anchor.Position + Vector2.UnitX) - anchorPx).Length();

        var scale = _ui.RootControl.UIScale;
        if (scale <= 0f || meterPx <= 0f)
            return;

        var anchorUi = anchorPx / scale;
        var meterUi = meterPx / scale;

        var isBite = challenge.Phase == FishingPhase.Biting;
        var size = (isBite ? BiteSize : PanelSize) * meterUi;
        var center = anchorUi + (isBite ? Vector2.Zero : PanelOffset) * meterUi;

        _input.SetRect(center - size * 0.5f, size);
    }

    private void OnChallengeState(Entity<FishingChallengeComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        if (_player.LocalEntity != ent.Owner)
            return;

        // Only the bite window and the fight need the wheel, and the catcher is small, so the
        // HUD and open windows stay usable the whole time.
        if (ent.Comp.Phase is FishingPhase.Biting or FishingPhase.Minigame)
            EnsureInput();
        else
            RemoveInput();
    }

    private void OnChallengeShutdown(Entity<FishingChallengeComponent> ent, ref ComponentShutdown args)
    {
        if (_player.LocalEntity == ent.Owner)
            RemoveInput();
    }

    private void OnLocalPlayerDetached(LocalPlayerDetachedEvent args)
    {
        RemoveInput();
    }

    private void EnsureInput()
    {
        if (_input == null)
        {
            _input = new FishingMinigameInputControl();
            _input.OnReel += SendReel;
            _input.OnHook += TryHook;
        }

        // The catcher has to sit above the viewport to be the control under the cursor,
        // so it goes on the active screen as the last child.
        var parent = (Control?) _ui.ActiveScreen ?? _ui.StateRoot;
        if (_input.Parent != parent)
        {
            _input.Orphan();
            parent.AddChild(_input);
        }
    }

    private MapCoordinates GetAnchor(FishingChallengeComponent challenge, EntityUid local)
    {
        if (challenge.Lure is { } lure && !TerminatingOrDeleted(lure))
            return _xform.GetMapCoordinates(lure);

        return _xform.GetMapCoordinates(local);
    }

    private void TryHook()
    {
        if (_player.LocalEntity is not { } local ||
            !TryComp<FishingChallengeComponent>(local, out var challenge) ||
            challenge.Phase != FishingPhase.Biting)
            return;

        SendReel(1);
    }

    private void SendReel(int direction)
    {
        RaiseNetworkEvent(new FishingReelInputEvent(direction));
    }

    private void RemoveInput()
    {
        if (_input == null)
            return;

        if (_input.Parent != null)
            _input.Orphan();

        _input.Dispose();
        _input = null;
    }
}
