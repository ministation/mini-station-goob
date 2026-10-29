// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from tgstation/tgstation (code/modules/fishing, AGPL-3.0-or-later).

using System.Numerics;
using Content.Shared._Mini.Fishing;
using Content.Shared._Mini.Fishing.Components;
using Robust.Client.Player;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.GameObjects;

namespace Content.Client._Mini.Fishing.UI;

/// <summary>
/// Invisible full screen control that turns mouse wheel notches into reel input while a challenge is active.
/// Mouse buttons pass through, so the rest of the HUD keeps working while waiting for a bite.
/// </summary>
public sealed class FishingMinigameInputControl : Control
{
    private readonly IEntityManager _ent;
    private readonly IPlayerManager _player;
    private readonly EntityUid _rod;

    public event Action<int>? OnReel;

    public FishingMinigameInputControl(EntityUid rod)
    {
        _rod = rod;
        _ent = IoCManager.Resolve<IEntityManager>();
        _player = IoCManager.Resolve<IPlayerManager>();

        MouseFilter = MouseFilterMode.Pass;
        LayoutContainer.SetAnchorPreset(this, LayoutContainer.LayoutPreset.Wide);
    }

    protected override void MouseWheel(GUIMouseWheelEventArgs args)
    {
        base.MouseWheel(args);

        if (args.Delta.Y == 0f || !IsReelingActive())
            return;

        args.Handle();
        OnReel?.Invoke(args.Delta.Y > 0f ? 1 : -1);
    }

    private bool IsReelingActive()
    {
        if (_player.LocalEntity is not { } local ||
            !_ent.TryGetComponent<FishingChallengeComponent>(local, out var challenge) ||
            challenge.Rod != _rod)
            return false;

        return challenge.Phase is FishingPhase.Biting or FishingPhase.Minigame;
    }
}
