// SPDX-FileCopyrightText: 2026 Space Station 14 Contributors
//
// SPDX-License-Identifier: AGPL-3.0-or-later

// SPDX-FileCopyrightText: 2026 Egorik1
// Мини-станция, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/ministation/mini-station-goob/master/LICENSE.TXT

using Robust.Shared.Network;
using Robust.Shared.Serialization;

namespace Content.Shared._Mini.NeuroAHelp;

/// <summary>
/// Состояние ИИ-помощника в конкретном ахелп-тиките. Показывается админам кольцом-индикатором
/// в окне ахелпа; игроку статус не рассылается, чтобы не светить, что тикет разбирает машина.
/// </summary>
public enum NeuroAHelpState : byte
{
    /// <summary>Статуса нет, индикатор скрыт.</summary>
    Idle = 0,

    /// <summary>Агент работает над тикетом (кольцо крутится).</summary>
    Working,

    /// <summary>Агент ответил игроку и закончил работу.</summary>
    Done,

    /// <summary>ИИ молчит: тикет взял живой администратор.</summary>
    Silent,

    /// <summary>ИИ не справился и передал тикет живым администраторам.</summary>
    Failed,

    /// <summary>ИИ выключен в этом тикете командой /ai off.</summary>
    Muted,
}

/// <summary>
/// Рассылается живым админам (флаг Adminhelp) при смене состояния нейропомощника в тикете.
/// Name - имя ассистента с сервера (neuroahelp.name), Detail - короткая человеческая строка
/// для подписи и тултипа кольца («читаю логи раунда»).
/// </summary>
[Serializable, NetSerializable]
public sealed class NeuroAHelpStatusMessage : EntityEventArgs
{
    public NetUserId Channel { get; }
    public NeuroAHelpState State { get; }
    public string Name { get; }
    public string Detail { get; }

    public NeuroAHelpStatusMessage(NetUserId channel, NeuroAHelpState state, string name, string detail)
    {
        Channel = channel;
        State = state;
        Name = name;
        Detail = detail;
    }
}
