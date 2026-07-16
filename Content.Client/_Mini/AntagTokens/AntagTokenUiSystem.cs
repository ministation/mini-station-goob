// SPDX-FileCopyrightText: 2026 Casha
// Мини-станция/Freaky-station, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/ministation/mini-station-goob/master/LICENSE.TXT
using System;
using System.Collections.Generic;
using Content.Client.Players.PlayTimeTracking;
using Content.Client.UserInterface.Systems.Ghost.Controls.Roles;
using Content.Shared._Mini.AntagTokens;
using Content.Shared.Roles;
using Robust.Shared.Localization;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client._Mini.AntagTokens;

public sealed class AntagTokenUiSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly AntagTokenListingSystem _listings = default!;
    [Dependency] private readonly JobRequirementsManager _requirements = default!;

    private AntagTokenWindow? _window;
    private GhostRoleRulesWindow? _rulesConfirmWindow;
    private AntagTokenState? _cachedState;
    private bool _awaitingOpen;
    private readonly Dictionary<string, int> _purchaseCooldowns = new();
    private TimeSpan _lastStateSyncCurTime;
    private int _lastAppliedElapsedSeconds = -1;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<AntagTokenStateEvent>(OnState);
        _requirements.Updated += OnRequirementsUpdated;
    }

    public override void Shutdown()
    {
        _requirements.Updated -= OnRequirementsUpdated;
        _cachedState = null;
        CleanupWindow();
        _awaitingOpen = false;
        base.Shutdown();
    }

    private void OnRequirementsUpdated()
    {
        if (_cachedState != null && _window != null && !_window.Disposed)
            _window.UpdateState(_cachedState);
    }

    public void RequestOpen()
    {
        _awaitingOpen = true;
        EnsureWindow();

        if (_cachedState != null)
        {
            _window!.UpdateState(_cachedState);
            ApplyCooldownDisplayAfterStateUpdate();
        }
        else
        {
            _window!.SetLoading(true);
        }

        RaiseNetworkEvent(new AntagTokenOpenRequestEvent());
    }

    private void OnState(AntagTokenStateEvent ev)
    {
        _cachedState = ev.State;
        _lastStateSyncCurTime = _timing.CurTime;

        if (_window == null || _window.Disposed)
        {
            if (!_awaitingOpen)
                return;

            EnsureWindow();
        }

        _window?.UpdateState(ev.State);
        ApplyCooldownDisplayAfterStateUpdate();
    }

    private void RebuildPurchaseCooldownsFromElapsed()
    {
        _purchaseCooldowns.Clear();
        if (_cachedState == null)
            return;

        var elapsed = (int) Math.Max(0, (_timing.CurTime - _lastStateSyncCurTime).TotalSeconds);
        foreach (var r in _cachedState.Roles)
        {
            if (r.PurchaseCooldownSecondsRemaining <= 0)
                continue;

            var rem = Math.Max(0, r.PurchaseCooldownSecondsRemaining - elapsed);
            if (rem > 0)
                _purchaseCooldowns[r.RoleId] = rem;
        }
    }

    private void ApplyCooldownDisplayAfterStateUpdate()
    {
        RebuildPurchaseCooldownsFromElapsed();
        var elapsed = (int) Math.Max(0, (_timing.CurTime - _lastStateSyncCurTime).TotalSeconds);
        _lastAppliedElapsedSeconds = elapsed;

        _window?.RefreshPurchaseCooldowns(_purchaseCooldowns);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_window == null || _window.Disposed || _cachedState == null)
            return;

        var elapsed = (int) Math.Max(0, (_timing.CurTime - _lastStateSyncCurTime).TotalSeconds);
        if (elapsed == _lastAppliedElapsedSeconds)
            return;

        _lastAppliedElapsedSeconds = elapsed;

        RebuildPurchaseCooldownsFromElapsed();
        _window.RefreshPurchaseCooldowns(_purchaseCooldowns);
    }

    private void EnsureWindow()
    {
        if (_window != null && !_window.Disposed)
            return;

        CleanupWindow();

        _window = new AntagTokenWindow();
        _window.Title = Loc.GetString("antag-token-window-title");
        _window.OnPurchasePressed += OnPurchasePressed;
        _window.OnClearPressed += OnClearPressed;
        _window.OnClose += OnClosed;
        _window.OpenCentered();
    }

    private void OnPurchasePressed(string roleId)
    {
        var entryMode = _cachedState?.Roles.Find(r => r.RoleId == roleId)?.Mode;
        var treatAsGhost = entryMode == AntagPurchaseMode.GhostRule
            || (entryMode == null &&
                _listings.TryGetListing(roleId, out var catalogDef) &&
                catalogDef.Mode == AntagPurchaseMode.GhostRule);

        if (treatAsGhost && _listings.TryGetListing(roleId, out var def))
        {
            CloseRulesConfirmWindow();
            var rulesKey = def.GhostRulesLocKey ?? "ghost-role-information-antagonist-rules";
            var rulesText = Loc.GetString(rulesKey);

            GhostRoleRulesWindow? win = null;
            win = new GhostRoleRulesWindow(rulesText, _ =>
            {
                RaiseNetworkEvent(new AntagTokenPurchaseRequestEvent(roleId));
                win?.Close();
            });
            _rulesConfirmWindow = win;
            win.OnClose += () =>
            {
                if (_rulesConfirmWindow == win)
                    _rulesConfirmWindow = null;
            };
            win.OpenCentered();
            return;
        }

        RaiseNetworkEvent(new AntagTokenPurchaseRequestEvent(roleId));
    }

    private void CloseRulesConfirmWindow()
    {
        if (_rulesConfirmWindow == null || _rulesConfirmWindow.Disposed)
        {
            _rulesConfirmWindow = null;
            return;
        }

        _rulesConfirmWindow.Close();
        _rulesConfirmWindow = null;
    }

    private void OnClearPressed()
    {
        RaiseNetworkEvent(new AntagTokenClearRequestEvent());
    }

    private void OnClosed()
    {
        CleanupWindow();
        _awaitingOpen = false;
    }

    private void CleanupWindow()
    {
        CloseRulesConfirmWindow();

        if (_window == null)
            return;

        _purchaseCooldowns.Clear();
        _lastAppliedElapsedSeconds = -1;

        _window.OnPurchasePressed -= OnPurchasePressed;
        _window.OnClearPressed -= OnClearPressed;
        _window.OnClose -= OnClosed;

        if (!_window.Disposed)
            _window.Dispose();

        _window = null;
    }
}
