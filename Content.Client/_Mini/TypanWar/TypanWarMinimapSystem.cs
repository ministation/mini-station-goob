// SPDX-FileCopyrightText: 2026 Egorik1
// Мини-станция, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/ministation/mini-station-goob/master/LICENSE.TXT

using Content.Shared._Mini.TypanWar;
using Content.Shared.GameTicking;
using Robust.Client.Player;
using Robust.Shared.Timing;

namespace Content.Client._Mini.TypanWar;

public sealed class TypanWarMinimapSystem : EntitySystem
{
    [Dependency] private readonly IPlayerManager _players = default!;
    [Dependency] private readonly TypanWarUiSystem _war = default!;

    private TypanWarMinimapWindow? _window;
    private bool _pendingOpen;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<TypanWarMinimapActionEvent>(OnMinimapAction);
        _war.StatusUpdated += OnStatusUpdated;
        SubscribeNetworkEvent<RoundRestartCleanupEvent>(OnRoundRestart);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _war.StatusUpdated -= OnStatusUpdated;
        CloseWindow();
    }

    private void OnRoundRestart(RoundRestartCleanupEvent ev) => CloseWindow();

    private void OnMinimapAction(TypanWarMinimapActionEvent ev)
    {
        if (_players.LocalEntity == null)
            return;

        if (_window != null || _pendingOpen)
        {
            _pendingOpen = false;
            CloseWindow();
            return;
        }

        if (_war.MinimapGrids.Length == 0)
        {
            _pendingOpen = true;
            _war.RequestStatus();
            return;
        }

        OpenMinimapWindow();
    }

    private void OnStatusUpdated()
    {
        if (_pendingOpen)
        {
            if (_war.Phase != TypanWarPhase.Active || _war.MinimapGrids.Length == 0)
                return;

            OpenMinimapWindow();
            return;
        }

        if (_window == null)
            return;

        if (_war.Phase != TypanWarPhase.Active)
        {
            CloseWindow();
            return;
        }

        RefreshWindow();
    }

    private void OpenMinimapWindow()
    {
        _pendingOpen = false;
        _window = new TypanWarMinimapWindow();
        _window.OnClose += CloseWindow;
        _window.PrepareForDisplay();
        _window.Refresh(
            _war.MinimapGrids,
            _war.CaptureZones,
            _war.AllyBlips,
            _war.NtCapturePoints,
            _war.TypanCapturePoints,
            _war.CapturePointsToWin);
        _window.OpenCentered();
    }

    private void RefreshWindow()
    {
        _window?.Refresh(
            _war.MinimapGrids,
            _war.CaptureZones,
            _war.AllyBlips,
            _war.NtCapturePoints,
            _war.TypanCapturePoints,
            _war.CapturePointsToWin);
    }

    private void CloseWindow()
    {
        _pendingOpen = false;

        if (_window == null)
            return;

        _window.OnClose -= CloseWindow;
        _window.Close();
        _window = null;
    }
}
