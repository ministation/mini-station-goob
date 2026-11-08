// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

using System.Collections.Concurrent;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using Content.Server.Administration.Managers;
using Content.Shared.Database;
using Content.Shared._Mini.MiniCCVars;
using Robust.Server.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Log;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Timing;
using Serilog.Events;

namespace Content.Server._Mini.Administration;

/// <summary>
///     Mini: банит IP, с которого сыплется мусор Lidgren: битый заголовок
///     ("Malformed packet from") или неизвестный числовой тип сообщения (модифицированный клиент).
///     Named library-сообщения обычных клиентов (Ping, Pong, Acknowledge) не трогаем,
///     иначе пришлось бы банить всех.
///
///     Хардненинг против ложных банов: источник UDP можно подделать, а мусор на
///     игровой порт шлют и обычные клиенты (например, HTTP/3-запросы к тому же
///     порту), поэтому бан выдаётся только за астрономический флуд: порог ударов
///     за окно времени держится выше того, что даёт всплеск от обычного клиента,
///     а сам бан временный, чтобы ошибочное срабатывание сходило само и его мог
///     снять админ. В лог пишутся подключённые с адреса игроки.
/// </summary>
public sealed class MalformedPacketAutoBanSystem : EntitySystem
{
    [Dependency] private readonly IBanManager _bans = default!;
    [Dependency] private readonly ILocalizationManager _loc = default!;
    [Dependency] private readonly ILogManager _logManager = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IPlayerManager _players = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    // "Received unhandled library message 200 from 1.2.3.4:5678"
    private static readonly Regex UnhandledNumericRx =
        new(@"Received unhandled library message \d+ from ", RegexOptions.Compiled);

    // "Malformed packet from 1.2.3.4:5678; stated payload length ..."
    private static readonly Regex EndPointRx =
        new(@"from (?:\[([0-9a-fA-F:.]+)\]|([0-9.]+)):\d+", RegexOptions.Compiled);

    // Временный бан: ложное срабатывание не должно оставлять игрока в пермаче.
    private const int BanMinutes = 30;

    private readonly ConcurrentQueue<IPAddress> _pending = new();
    private readonly Dictionary<IPAddress, long> _bannedUntil = new();

    private readonly object _strikesLock = new();
    private readonly Dictionary<IPAddress, List<long>> _strikes = new();

    private TimeSpan _lastPrune;

    public override void Initialize()
    {
        base.Initialize();
        _logManager.GetSawmill("net").AddHandler(new NetLogHandler(this));
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        while (_pending.TryDequeue(out var address))
        {
            RegisterStrike(address);
        }

        // Периодически выкидываем устаревшие счётчики, чтобы спуфинг с тысячи
        // адресов не раздувал словарь.
        if (_timing.RealTime - _lastPrune < TimeSpan.FromSeconds(30))
            return;

        _lastPrune = _timing.RealTime;
        var window = _cfg.GetCVar(MiniCCVars.NetMalformedAutobanWindow) * 1000L;
        var now = Environment.TickCount64;
        lock (_strikesLock)
        {
            foreach (var (address, stamps) in _strikes)
            {
                stamps.RemoveAll(t => t < now - window);
                if (stamps.Count == 0)
                    _strikes.Remove(address);
            }

            foreach (var (address, until) in _bannedUntil)
            {
                if (until <= now)
                    _bannedUntil.Remove(address);
            }
        }
    }

    private void RegisterStrike(IPAddress address)
    {
        var now = Environment.TickCount64;
        var window = _cfg.GetCVar(MiniCCVars.NetMalformedAutobanWindow) * 1000L;
        var threshold = _cfg.GetCVar(MiniCCVars.NetMalformedAutobanThreshold);
        if (threshold < 1)
            return;

        lock (_strikesLock)
        {
            if (!_strikes.TryGetValue(address, out var stamps))
            {
                stamps = new List<long>();
                _strikes[address] = stamps;
            }

            stamps.RemoveAll(t => t < now - window);
            stamps.Add(now);
            if (stamps.Count > threshold * 4)
                stamps.RemoveAt(0);

            if (stamps.Count < threshold)
                return;

            // Порог набран: сбрасываем счётчик — до бана и выгрузки из _banned
            // новые пакеты с адреса не должны повторно дойти до создания бана.
            _strikes.Remove(address);
        }

        Ban(address, threshold);
    }

    private void Ban(IPAddress address, int threshold)
    {
        // Повторные пакеты с того же адреса не должны плодить записи в базе.
        var now = Environment.TickCount64;
        lock (_strikesLock)
        {
            if (_bannedUntil.TryGetValue(address, out var until) && until > now)
                return;

            _bannedUntil[address] = now + BanMinutes * 60_000L;
        }

        // Прибинтим к бану всех, кто подключён с этого адреса: админ видит,
        // кого задело, и может снять бан при ложном срабатывании.
        var connected = new List<(NetUserId Id, string Name)>();
        foreach (var session in _players.Sessions)
        {
            if (session.Channel.RemoteEndPoint.Address.Equals(address))
                connected.Add((session.UserId, session.Name));
        }

        var sawmill = _logManager.GetSawmill("net.malformed_ban");
        if (connected.Count > 0)
        {
            var names = string.Join(", ", connected.Select(c => c.Name));
            sawmill.Log(LogLevel.Warning,
                $"Автобан IP {address} за мусорные пакеты: порог {threshold} превышен. С этого адреса подключены: {names}.");
        }
        else
        {
            sawmill.Log(LogLevel.Warning,
                $"Автобан IP {address} за мусорные пакеты: порог {threshold} превышен, активных сессий с адреса нет.");
        }

        var info = new CreateServerBanInfo(_loc.GetString("ban-reason-malformed-packet"));
        info.AddAddress(address);
        foreach (var (id, name) in connected)
            info.AddUser(id, name);
        info.WithSeverity(NoteSeverity.High);
        info.WithMinutes(BanMinutes);
        _bans.CreateServerBan(info);
    }

    /// <summary>
    ///     Достаёт адрес из строк Lidgren. Мусорные пакеты понимает, именованные
    ///     library-сообщения (Ping и т.п.) пропускает.
    /// </summary>
    private bool TryGetOffender(string message, out IPAddress address)
    {
        address = IPAddress.None;

        if (_cfg.GetCVar(MiniCCVars.NetMalformedAutoban) == false)
            return false;

        if (!message.Contains("Malformed packet from") && !UnhandledNumericRx.IsMatch(message))
            return false;

        var match = EndPointRx.Match(message);
        if (!match.Success)
            return false;

        var raw = match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value;
        if (!IPAddress.TryParse(raw, out var parsed) || IPAddress.IsLoopback(parsed))
            return false;

        address = parsed!;
        return true;
    }

    private sealed class NetLogHandler : ILogHandler
    {
        private readonly MalformedPacketAutoBanSystem _system;

        public NetLogHandler(MalformedPacketAutoBanSystem system)
        {
            _system = system;
        }

        public void Log(string sawmillName, LogEvent message)
        {
            if (_system.TryGetOffender(message.RenderMessage(), out var address))
                _system._pending.Enqueue(address);
        }
    }
}
