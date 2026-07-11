// SPDX-FileCopyrightText: 2026 Egorik1
// Мини-станция, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/ministation/mini-station-goob/master/LICENSE.TXT

using Content.Client.Gameplay;
using Content.Client.UserInterface.Systems.Gameplay;
using Content.Shared._Mini.TypanWar;
using JetBrains.Annotations;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controllers;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Timing;

namespace Content.Client._Mini.TypanWar;

[UsedImplicitly]
public sealed class TypanWarHudController : UIController,
    IOnStateEntered<GameplayState>,
    IOnSystemChanged<TypanWarUiSystem>
{
    private const float EndedFlashDuration = 3f;

    [Dependency] private readonly IGameTiming _timing = default!;
    [UISystemDependency] private readonly TypanWarUiSystem _war = default!;

    private TimeSpan? _endedFlashUntil;

    public override void Initialize()
    {
        base.Initialize();

        var gameplayStateLoad = UIManager.GetUIController<GameplayStateLoadController>();
        gameplayStateLoad.OnScreenLoad += OnScreenLoad;
    }

    public override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        if (_endedFlashUntil != null && _timing.CurTime >= _endedFlashUntil)
        {
            _endedFlashUntil = null;
            Refresh();
        }
    }

    private void OnScreenLoad()
    {
        _war.RequestStatus();
        Refresh();
    }

    public void OnStateEntered(GameplayState state)
    {
        _war.RequestStatus();
        Refresh();
    }

    public void OnSystemLoaded(TypanWarUiSystem system)
    {
        system.StatusUpdated += OnStatusUpdated;
        Refresh();
    }

    public void OnSystemUnloaded(TypanWarUiSystem system)
    {
        system.StatusUpdated -= OnStatusUpdated;
    }

    private void OnStatusUpdated()
    {
        if (_war.Phase == TypanWarPhase.Ended && _war.Winner != TypanWarWinner.None)
            _endedFlashUntil = _timing.CurTime + TimeSpan.FromSeconds(EndedFlashDuration);

        Refresh();
    }

    private void Refresh()
    {
        if (UIManager.ActiveScreen == null)
            return;

        var hud = TryFindHud(UIManager.ActiveScreen);
        if (hud == null)
            return;

        var showEnded = _war.Phase == TypanWarPhase.Ended && _endedFlashUntil != null;

        hud.Update(
            showEnded ? TypanWarPhase.Ended : _war.Phase,
            _war.Winner,
            _war.NtCapturePoints,
            _war.TypanCapturePoints,
            _war.CapturePointsToWin,
            _war.TimeRemainingSeconds);
    }

    private static TypanWarHudControl? TryFindHud(Control screen)
    {
        var nameScope = screen.FindNameScope();
        return nameScope?.Find("TypanWarHud") as TypanWarHudControl;
    }
}
