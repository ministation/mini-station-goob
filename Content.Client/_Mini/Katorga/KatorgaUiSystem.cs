// SPDX-FileCopyrightText: 2026 Space Station 14 Contributors
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._Mini.Katorga;
using Content.Shared._Mini.Katorga.Components;
using Content.Shared.GameTicking;
using Robust.Client.Player;
using Robust.Shared.GameObjects;
using Robust.Shared.Timing;

namespace Content.Client._Mini.Katorga;

/// <summary>
///     Клиентская система: следит за состоянием каторжанина у локального игрока
///     и хранит состояние выбора фракции из окна позднего подключения.
/// </summary>
public sealed class KatorgaUiSystem : EntitySystem
{
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    /// <summary>Данные компонента локального игрока изменились (стейт, старт, удаление).</summary>
    public event Action? InmateStateChanged;

    /// <summary>Пришло новое состояние выбора фракции (варианты, отсчёт или выбранная фракция).</summary>
    public event Action? FactionStateChanged;

    private KatorgaFactionStateEvent? _factionState;
    private TimeSpan _factionStateReceived;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<KatorgaBoundComponent, ComponentStartup>(OnStartup);
        // ComponentHandleState занят авто-стейтом (AutoGenerateComponentState),
        // поэтому подписываемся на событие после его обработки.
        SubscribeLocalEvent<KatorgaBoundComponent, AfterAutoHandleStateEvent>(OnHandleState);
        SubscribeLocalEvent<KatorgaBoundComponent, ComponentRemove>(OnRemove);
        SubscribeNetworkEvent<KatorgaFactionStateEvent>(OnFactionState);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);
    }

    private void OnRoundRestart(RoundRestartCleanupEvent ev)
    {
        // Состояние прошлого раунда не должно мелькать в late join нового.
        _factionState = null;
        FactionStateChanged?.Invoke();
    }

    /// <summary>Последнее состояние выбора фракции, если сервер его присылал.</summary>
    public KatorgaFactionStateEvent? FactionState => _factionState;

    /// <summary>Остаток секунд на выбор фракции: клиент считает его своими часами.</summary>
    public int FactionSecondsRemaining
    {
        get
        {
            if (_factionState == null || _factionState.Selected != null)
                return 0;

            var elapsed = (_timing.CurTime - _factionStateReceived).TotalSeconds;
            return Math.Max(0, (int) Math.Ceiling(_factionState.SecondsRemaining - elapsed));
        }
    }

    /// <summary>Окно late join открылось: просим у сервера варианты и остаток времени.</summary>
    public void RequestFactionState()
    {
        if (!_timing.IsFirstTimePredicted)
            return;

        RaiseNetworkEvent(new KatorgaFactionStateRequestEvent());
    }

    /// <summary>Игрок нажал фракцию в окне late join.</summary>
    public void RequestFactionSelect(string factionId)
    {
        if (!_timing.IsFirstTimePredicted)
            return;

        RaiseNetworkEvent(new KatorgaFactionSelectEvent(factionId));
    }

    private void OnFactionState(KatorgaFactionStateEvent ev)
    {
        _factionState = ev;
        _factionStateReceived = _timing.CurTime;
        FactionStateChanged?.Invoke();
    }

    private void OnStartup(EntityUid uid, KatorgaBoundComponent component, ComponentStartup args)
    {
        Notify(uid);
    }

    private void OnHandleState(Entity<KatorgaBoundComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        Notify(ent);
    }

    private void OnRemove(EntityUid uid, KatorgaBoundComponent component, ComponentRemove args)
    {
        Notify(uid);
    }

    private void Notify(EntityUid uid)
    {
        if (_player.LocalEntity == uid)
            InmateStateChanged?.Invoke();
    }

    /// <summary>Есть ли у локального игрока компонент заключённого.</summary>
    public bool TryGetInmate(out KatorgaBoundComponent? inmate)
    {
        inmate = null;
        return _player.LocalEntity is { } local && TryComp(local, out inmate);
    }
}
