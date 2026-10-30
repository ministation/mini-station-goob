// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from tgstation/tgstation (code/modules/fishing, AGPL-3.0-or-later).

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
using Robust.Shared.Player;

namespace Content.Client._Mini.Fishing;

public sealed partial class FishingSystem : EntitySystem
{
    [Dependency] private IOverlayManager _overlay = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IUserInterfaceManager _ui = default!;

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

    private void OnChallengeState(Entity<FishingChallengeComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        if (_player.LocalEntity != ent.Owner)
            return;

        // Only grab the mouse wheel for the short bite window and the fight itself.
        // Holding the rod otherwise must not affect the HUD.
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

        // The game viewport is a full screen control and it sits above StateRoot, so a catcher
        // parented there would never be the control under the cursor and would never get the wheel.
        // The active screen is above the viewport, and being its last child puts the catcher on top
        // of the HUD too, which is fine for the short bite window and the fight.
        var parent = (Control?) _ui.ActiveScreen ?? _ui.StateRoot;
        if (_input.Parent != parent)
        {
            _input.Orphan();
            parent.AddChild(_input);
        }
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
