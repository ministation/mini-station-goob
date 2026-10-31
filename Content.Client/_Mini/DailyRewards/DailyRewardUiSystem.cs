// SPDX-FileCopyrightText: 2026 Space Station 14 Contributors
//
// SPDX-License-Identifier: AGPL-3.0-or-later

// SPDX-FileCopyrightText: 2026 Casha
// Мини-станция/Freaky-station, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/ministation/mini-station-goob/master/LICENSE.TXT
using Content.Shared._Mini.DailyRewards;
using Robust.Shared.Localization;
using Robust.Shared.Log;

namespace Content.Client._Mini.DailyRewards;

public sealed class DailyRewardUiSystem : EntitySystem
{
    // Release-клиент глотает исключения в UI-обработчиках (EXCEPTION_TOLERANCE): без
    // своих try/catch сломанное состояние выглядело бы как "меню то открывается, то нет".
    private readonly ISawmill _sawmill = Logger.GetSawmill("daily_rewards");

    private DailyRewardWindow? _window;
    private DailyRewardUpdateMessage? _cachedState;
    private bool _awaitingOpen;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<DailyRewardStateEvent>(OnState);
        SubscribeNetworkEvent<DailyRewardClaimResultEvent>(OnClaimResult);
        SubscribeNetworkEvent<DailyQuestReplaceDeniedEvent>(OnReplaceDenied);
    }

    public void RequestOpen()
    {
        _awaitingOpen = true;
        EnsureWindow();

        if (_window == null || _window.Disposed)
            return;

        // Кэшированное состояние может быть любым: окно обязано открыться,
        // свежие данные придут с сервера следом.
        if (_cachedState != null)
        {
            try
            {
                _window.UpdateState(_cachedState);
            }
            catch (Exception e)
            {
                _sawmill.Error($"Failed to apply the cached daily rewards state: {e}");
            }
        }

        _window.OpenCentered();
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
            try
            {
                _window.UpdateState(ev.State);
            }
            catch (Exception e)
            {
                _sawmill.Error($"Failed to refresh the daily rewards window: {e}");
            }

            return;
        }

        if (!_awaitingOpen)
            return;

        EnsureWindow();
        if (_window is { Disposed: false })
        {
            try
            {
                _window.UpdateState(ev.State);
            }
            catch (Exception e)
            {
                _sawmill.Error($"Failed to refresh the daily rewards window: {e}");
            }

            _window.OpenCentered();
        }
    }

    private void EnsureWindow()
    {
        if (_window != null && !_window.Disposed)
            return;

        DailyRewardWindow window;
        try
        {
            window = new DailyRewardWindow();
        }
        catch (Exception e)
        {
            // Сломанная постройка окна повторится и на следующий раз: пишем в лог,
            // чтобы причина была видна в клиентском логе сразу.
            _sawmill.Error($"Failed to create the daily rewards window: {e}");
            return;
        }

        // The window hosts weekly quests and achievements, so it is the generic rewards menu.
        window.Title = Loc.GetString("daily-reward-window-title-menu");
        AttachWindow(window);
    }

    private void OnClaimPressed()
    {
        RaiseNetworkEvent(new DailyRewardClaimRequestEvent());
    }

    private void OnClaimResult(DailyRewardClaimResultEvent ev)
    {
        // Show the outcome in the window itself. In the lobby there is no attached entity,
        // so server popups are not visible and a silent reaction looks like a dead button.
        if (_window is { Disposed: false })
            _window.ShowClaimResult(ev);
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
