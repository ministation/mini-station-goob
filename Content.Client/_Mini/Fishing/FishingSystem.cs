// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from tgstation/tgstation (code/modules/fishing, AGPL-3.0-or-later).

using Content.Client._Mini.Fishing.Overlays;
using Content.Client._Mini.Fishing.UI;
using Content.Shared._Mini.Fishing;
using Content.Shared._Mini.Fishing.Components;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Client.UserInterface;
using Robust.Shared.GameObjects;
using Robust.Shared.GameStates;
using Robust.Shared.Player;

namespace Content.Client._Mini.Fishing;

public sealed partial class FishingSystem : EntitySystem
{
    [Dependency] private IOverlayManager _overlay = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IUserInterfaceManager _ui = default!;
    [Dependency] private SharedUserInterfaceSystem _uiSystem = default!;

    private FishingMinigameInputControl? _input;
    private EntityUid? _inputRod;

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
            EnsureInput(ent.Comp.Rod);
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

    private void EnsureInput(EntityUid? rod)
    {
        if (rod is not { } rodUid)
        {
            RemoveInput();
            return;
        }

        if (_input == null)
        {
            _input = new FishingMinigameInputControl();
            _input.OnReel += SendReel;
            _ui.StateRoot.AddChild(_input);
        }

        _inputRod = rodUid;
    }

    private void SendReel(int direction)
    {
        if (_inputRod is not { } rod || !TryComp<UserInterfaceComponent>(rod, out var uiComp))
            return;

        _uiSystem.ClientSendUiMessage((rod, uiComp), FishingMinigameUiKey.Key, new FishingMinigameReelMessage(direction));
    }

    private void RemoveInput()
    {
        _inputRod = null;

        if (_input == null)
            return;

        if (_input.Parent != null)
            _input.Orphan();

        _input.Dispose();
        _input = null;
    }
}
