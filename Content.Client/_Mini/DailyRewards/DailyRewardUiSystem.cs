// SPDX-FileCopyrightText: 2026 Casha
// Мини-станция/Freaky-station, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/ministation/mini-station-goob/master/LICENSE.TXT
using Content.Shared._Mini.DailyRewards;
using Robust.Shared.Localization;

namespace Content.Client._Mini.DailyRewards;

public sealed class DailyRewardUiSystem : EntitySystem
{
    private DailyRewardWindow? _window;
    private DailyRewardUpdateMessage? _cachedState;
    private bool _awaitingOpen;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<DailyRewardStateEvent>(OnState);
        SubscribeNetworkEvent<DailyQuestReplaceDeniedEvent>(OnReplaceDenied);
    }

    public void RequestOpen()
    {
        _awaitingOpen = true;
        EnsureWindow();

        if (_cachedState != null)
            _window!.UpdateState(_cachedState);

        _window!.OpenCentered();
        RaiseNetworkEvent(new DailyRewardOpenRequestEvent());
    }

    public void SendReplaceRequest(string questId, int slotIndex)
    {
        if (string.IsNullOrWhiteSpace(questId))
            return;

        RaiseNetworkEvent(new DailyQuestReplaceRequestEvent(questId, slotIndex));
    }

    public void AttachWindow(DailyRewardWindow window)
    {
        if (_window != null && _window != window && !_window.Disposed)
            DetachWindow(_window);

        _window = window;

        _window.OnClaimPressed -= OnClaimPressed;
        _window.OnClaimPressed += OnClaimPressed;

        _window.OnClose -= OnWindowClosed;
        _window.OnClose += OnWindowClosed;
    }

    public void DetachWindow(DailyRewardWindow window)
    {
        if (_window != window)
            return;

        window.OnClaimPressed -= OnClaimPressed;
        window.OnClose -= OnWindowClosed;
        _window = null;
    }

    private void OnState(DailyRewardStateEvent ev)
    {
        _cachedState = ev.State;

        if (_window != null && !_window.Disposed)
        {
            _window.UpdateState(ev.State);
            return;
        }

        if (!_awaitingOpen)
            return;

        EnsureWindow();
        _window?.UpdateState(ev.State);
        _window?.OpenCentered();
    }

    private void EnsureWindow()
    {
        if (_window != null && !_window.Disposed)
            return;

        var window = new DailyRewardWindow();
        window.Title = Loc.GetString("daily-reward-window-title");
        AttachWindow(window);
    }

    private void OnClaimPressed()
    {
        RaiseNetworkEvent(new DailyRewardClaimRequestEvent());
    }

    private void OnReplaceDenied(DailyQuestReplaceDeniedEvent ev)
    {
        if (_window == null || _window.Disposed)
            EnsureWindow();

        _window?.ClearQuestReplacePending();
        _window?.ShowQuestReplaceError(ev.Reason);
    }

    private void OnWindowClosed()
    {
        if (_window != null)
            DetachWindow(_window);

        _awaitingOpen = false;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_window == null || _window.Disposed || !_window.IsOpen)
            return;

        _window.AdvanceTimers();
    }

    public override void Shutdown()
    {
        base.Shutdown();

        if (_window != null)
            DetachWindow(_window);

        _cachedState = null;
        _awaitingOpen = false;
    }
}
