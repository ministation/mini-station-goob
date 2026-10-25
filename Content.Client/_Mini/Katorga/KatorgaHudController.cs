using Content.Client.Gameplay;
using Content.Client.UserInterface.Systems.Gameplay;
using Content.Shared._Mini.Katorga.Components;
using JetBrains.Annotations;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controllers;
using Robust.Shared.Timing;

namespace Content.Client._Mini.Katorga;

/// <summary>
///     Показывает и обновляет худ каторжанина, пока у локального игрока есть компонент заключённого.
/// </summary>
[UsedImplicitly]
public sealed class KatorgaHudController : UIController,
    IOnStateEntered<GameplayState>,
    IOnSystemChanged<KatorgaUiSystem>
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [UISystemDependency] private readonly KatorgaUiSystem? _katorga = default!;

    private Control? _screen;
    private KatorgaHudControl? _cachedHud;
    private KatorgaBoundComponent? _snapshot;
    private TimeSpan _receiveTime;

    public void OnStateEntered(GameplayState state)
    {
        _screen = null;
        _cachedHud = null;
        Refresh();
    }

    public void OnSystemLoaded(KatorgaUiSystem system)
    {
        system.InmateStateChanged += OnInmateStateChanged;
        Refresh();
    }

    public void OnSystemUnloaded(KatorgaUiSystem system)
    {
        system.InmateStateChanged -= OnInmateStateChanged;
        _snapshot = null;
        Refresh();
    }

    private void OnInmateStateChanged()
    {
        if (_katorga != null && _katorga.TryGetInmate(out var comp))
        {
            _snapshot = comp;
            _receiveTime = _timing.CurTime;
        }
        else
        {
            _snapshot = null;
        }
    }

    public override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        var hud = TryFindHud();
        if (hud == null)
            return;

        // Пока компонент у локального игрока: показываем и дотягиваем полосу до текущего момента.
        if (_snapshot != null && _katorga != null && _katorga.TryGetInmate(out var current) && current == _snapshot)
        {
            hud.ShowHud(_snapshot, _receiveTime, _timing.CurTime);
        }
        else
        {
            hud.HideHud();
        }
    }

    private void Refresh()
    {
        _ = TryFindHud();
    }

    private KatorgaHudControl? TryFindHud()
    {
        if (UIManager.ActiveScreen == null)
        {
            _screen = null;
            _cachedHud = null;
            return null;
        }

        if (_screen == UIManager.ActiveScreen && _cachedHud != null)
            return _cachedHud;

        _screen = UIManager.ActiveScreen;
        _cachedHud = UIManager.ActiveScreen.FindNameScope()?.Find("KatorgaHud") as KatorgaHudControl;
        return _cachedHud;
    }
}
