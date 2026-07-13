// SPDX-FileCopyrightText: 2026 Egorik1
// Мини-станция, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/ministation/mini-station-goob/master/LICENSE.TXT

using Content.Shared._Mini.TypanWar;
using Content.Shared.GameTicking;
using Robust.Client.Player;

namespace Content.Client._Mini.TypanWar;

public sealed class TypanWarMinimapSystem : EntitySystem
{
    private const float OpenPollIntervalSeconds = 1.5f;

    [Dependency] private readonly IPlayerManager _players = default!;
    [Dependency] private readonly TypanWarUiSystem _war = default!;

    private TypanWarMinimapWindow? _window;
    private bool _pendingOpen;
    private float _openPollAccumulator;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<TypanWarMinimapActionEvent>(OnMinimapAction);
        _war.StatusUpdated += OnStatusUpdated;
        SubscribeNetworkEvent<RoundRestartCleanupEvent>(OnRoundRestart);
    }

    // FrameUpdate runs once per render frame — unlike Update, it is not re-run during prediction.
    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        if (!IsWindowOpen())
        {
            _openPollAccumulator = 0f;
            return;
        }

        _openPollAccumulator += frameTime;
        if (_openPollAccumulator < OpenPollIntervalSeconds)
            return;

        _openPollAccumulator = 0f;
        _war.RequestStatus();
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _war.StatusUpdated -= OnStatusUpdated;
        DisposeWindow();
    }

    private void OnRoundRestart(RoundRestartCleanupEvent ev) => DisposeWindow();

    private void OnMinimapAction(TypanWarMinimapActionEvent ev)
    {
        if (_players.LocalEntity == null)
            return;

        ev.Handled = true;

        if (IsWindowOpen() || _pendingOpen)
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

        if (!IsWindowOpen())
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
        _window ??= new TypanWarMinimapWindow();

        _window.Refresh(
            _war.MinimapGrids,
            _war.CaptureZones,
            _war.AllyBlips,
            _war.NtAlive,
            _war.TypanAlive);
        _window.OpenPrepared();
    }

    private void RefreshWindow()
    {
        if (!IsWindowOpen())
            return;

        _window!.Refresh(
            _war.MinimapGrids,
            _war.CaptureZones,
            _war.AllyBlips,
            _war.NtAlive,
            _war.TypanAlive);
    }

    private bool IsWindowOpen() => _window is { IsOpen: true };

    private void CloseWindow()
    {
        _pendingOpen = false;
        _window?.Close();
    }

    private void DisposeWindow()
    {
        _pendingOpen = false;

        if (_window == null)
        {
            TypanWarMinimapControl.ClearShapeCache();
            return;
        }

        _window.Close();
        _window.Dispose();
        _window = null;
        TypanWarMinimapControl.ClearShapeCache();
    }
}
