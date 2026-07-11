using Content.Shared._Mini.TypanWar;
using Content.Shared.GameTicking;
using Robust.Client.Player;

namespace Content.Client._Mini.TypanWar;

public sealed class TypanWarMinimapSystem : EntitySystem
{
    [Dependency] private readonly IPlayerManager _players = default!;
    [Dependency] private readonly TypanWarUiSystem _war = default!;

    private TypanWarMinimapWindow? _window;

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

        if (_window != null)
        {
            CloseWindow();
            return;
        }

        _window = new TypanWarMinimapWindow();
        _window.OnClose += CloseWindow;
        _window.OpenCentered();
        _war.RequestStatus();
        RefreshWindow();
    }

    private void OnStatusUpdated()
    {
        if (_window == null)
            return;

        if (_war.Phase != TypanWarPhase.Active)
        {
            CloseWindow();
            return;
        }

        RefreshWindow();
    }

    private void RefreshWindow()
    {
        _window?.Update(
            _war.MinimapGrids,
            _war.CaptureZones,
            _war.AllyBlips,
            _war.NtCapturePoints,
            _war.TypanCapturePoints,
            _war.CapturePointsToWin);
    }

    private void CloseWindow()
    {
        if (_window == null)
            return;

        _window.OnClose -= CloseWindow;
        _window.Close();
        _window = null;
    }
}
