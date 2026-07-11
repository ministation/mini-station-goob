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
        RefreshWindow();
        _window.PrepareForDisplay();

        // Wait one frame so layout assigns pixel size before the window becomes visible.
        Robust.Shared.Timing.Timer.Spawn(TimeSpan.Zero, () =>
        {
            if (_window == null)
                return;

            _window.OpenCentered();
        });
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
        _pendingOpen = false;

        if (_window == null)
            return;

        _window.OnClose -= CloseWindow;
        _window.Close();
        _window = null;
    }
}
