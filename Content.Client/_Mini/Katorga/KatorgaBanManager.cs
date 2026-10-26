// SPDX-FileCopyrightText: 2026 Space Station 14 Contributors
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._Mini.Katorga;
using Content.Shared.GameTicking;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Network;
using Robust.Shared.Timing;

namespace Content.Client._Mini.Katorga;

/// <summary>
///     Хранит остаток временного бана, присланный сервером, для лобби и окна позднего подключения.
///     Живёт в IoC, а не в EntitySystem: клиентские системы пересоздаются при каждом подключении,
///     а net-сообщение регистрируется в net-менеджере, который живёт весь процесс.
/// </summary>
public sealed class KatorgaBanManager : IEntityEventSubscriber
{
    [Dependency] private readonly IClientNetManager _net = default!;
    [Dependency] private readonly IEntityManager _entityManager = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    /// <summary>Сервер прислал новый срок бана (или сообщил, что бана нет).</summary>
    public event Action? BanStatusChanged;

    private bool _hasBan;
    private TimeSpan _banRemaining;
    private TimeSpan _banReceivedAt;

    public void Initialize()
    {
        IoCManager.InjectDependencies(this);

        _net.RegisterNetMessage<KatorgaBanStatusMessage>(OnBanStatus);
        _entityManager.EventBus.SubscribeEvent<RoundRestartCleanupEvent>(EventSource.Network, this, OnRoundRestart);
    }

    /// <summary>
    ///     Остаток временного бана для показа в лобби. Считается от момента получения
    ///     сообщения, поэтому не зависит от расхождения часов клиента и сервера.
    /// </summary>
    public bool TryGetActiveBan(out TimeSpan remaining)
    {
        remaining = _banRemaining - (_timing.CurTime - _banReceivedAt);
        if (!_hasBan || remaining <= TimeSpan.Zero)
        {
            remaining = TimeSpan.Zero;
            return false;
        }

        return true;
    }

    private void OnBanStatus(KatorgaBanStatusMessage msg)
    {
        _hasBan = msg.Remaining > TimeSpan.Zero;
        _banRemaining = _hasBan ? msg.Remaining : TimeSpan.Zero;
        _banReceivedAt = _timing.CurTime;
        BanStatusChanged?.Invoke();
    }

    private void OnRoundRestart(RoundRestartCleanupEvent ev)
    {
        _hasBan = false;
        _banRemaining = TimeSpan.Zero;
        BanStatusChanged?.Invoke();
    }
}
