// SPDX-FileCopyrightText: 2026 Space Station 14 Contributors
//
// SPDX-License-Identifier: AGPL-3.0-or-later

// SPDX-FileCopyrightText: 2026 Egorik1
// Мини-станция, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/ministation/mini-station-goob/master/LICENSE.TXT

using Content.Client.Administration.UI.Bwoink;
using Content.Client.UserInterface.Systems.Bwoink;
using Content.Shared._Mini.NeuroAHelp;
using Content.Shared.GameTicking;
using Robust.Client.UserInterface;
using Robust.Shared.Network;
using Robust.Shared.Timing;

namespace Content.Client._Mini.NeuroAHelp;

/// <summary>
/// Клиентский приемник статусов нейропомощника в ахелпах. Хранит последнее состояние каждого
/// тикета, обновляет живые панели окна ахелпа и список каналов; кольцо-индикатор рисует BwoinkPanel.
/// </summary>
public sealed class NeuroAHelpIndicatorSystem : EntitySystem
{
    [Dependency] private readonly IUserInterfaceManager _ui = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    /// <summary>Последний известный статус по каждому тикету (переживает открытие окна ахелпа).</summary>
    public IReadOnlyDictionary<NetUserId, NeuroAHelpTicketStatus> Statuses => _statuses;

    private readonly Dictionary<NetUserId, NeuroAHelpTicketStatus> _statuses = new();

    /// <summary>Дергается на каждое обновление, чтобы список каналов перерисовался.</summary>
    public event Action<NeuroAHelpTicketStatus>? OnStatusUpdated;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<NeuroAHelpStatusMessage>(OnStatus);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);
    }

    public bool TryGetStatus(NetUserId channel, out NeuroAHelpTicketStatus status)
    {
        return _statuses.TryGetValue(channel, out status!);
    }

    private void OnStatus(NeuroAHelpStatusMessage msg, EntitySessionEventArgs args)
    {
        var status = new NeuroAHelpTicketStatus(msg.Channel, msg.State, msg.Name, msg.Detail, _timing.RealTime);
        _statuses[msg.Channel] = status;

        var controller = _ui.GetUIController<AHelpUIController>();
        if (controller.UIHelper is AdminAHelpUIHandler handler)
        {
            if (handler.TryGetChannel(msg.Channel, out var panel))
                panel.SetNeuroStatus(msg.State, msg.Name, msg.Detail);

            if (handler.Control is { Disposed: false } control)
                control.ChannelSelector.PlayerListContainer.DirtyList();
        }

        OnStatusUpdated?.Invoke(status);
    }

    private void OnRoundRestart(RoundRestartCleanupEvent ev)
    {
        _statuses.Clear();
    }
}

/// <summary>Снимок статуса нейропомощника в тикете для UI.</summary>
public sealed record NeuroAHelpTicketStatus(
    NetUserId Channel,
    NeuroAHelpState State,
    string Name,
    string Detail,
    TimeSpan At);
