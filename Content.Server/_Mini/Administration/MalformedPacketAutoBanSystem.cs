// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections.Concurrent;
using System.Net;
using System.Text.RegularExpressions;
using Content.Server.Administration.Managers;
using Content.Shared.Database;
using Robust.Shared.Log;
using Serilog.Events;

namespace Content.Server._Mini.Administration;

/// <summary>
///     Mini: банит IP, с которого пришёл мусорный пакет Lidgren: битый заголовок
///     ("Malformed packet from") или неизвестный числовой тип сообщения (модифицированный клиент).
///     Именованные library-сообщения обычных клиентов (Ping, Pong, Acknowledge) не трогаем,
///     иначе пришлось бы банить всех.
/// </summary>
public sealed class MalformedPacketAutoBanSystem : EntitySystem
{
    [Dependency] private readonly IBanManager _bans = default!;
    [Dependency] private readonly ILocalizationManager _loc = default!;
    [Dependency] private readonly ILogManager _logManager = default!;

    // "Received unhandled library message 200 from 1.2.3.4:5678"
    private static readonly Regex UnhandledNumericRx =
        new(@"Received unhandled library message \d+ from ", RegexOptions.Compiled);

    // "Malformed packet from 1.2.3.4:5678; stated payload length ..."
    private static readonly Regex EndPointRx =
        new(@"from (?:\[([0-9a-fA-F:.]+)\]|([0-9.]+)):\d+", RegexOptions.Compiled);

    private readonly ConcurrentQueue<IPAddress> _pending = new();
    private readonly HashSet<IPAddress> _banned = new();

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
            // Повторные пакеты с того же адреса не должны плодить записи в базе.
            if (!_banned.Add(address))
                continue;

            var info = new CreateServerBanInfo(_loc.GetString("ban-reason-malformed-packet"));
            info.AddAddress(address);
            info.WithSeverity(NoteSeverity.High);
            _bans.CreateServerBan(info);
        }
    }

    /// <summary>
    ///     Достаёт адрес из строк Lidgren. Мусорные пакеты понимает, именованные
    ///     library-сообщения (Ping и т.п.) пропускает.
    /// </summary>
    private static bool TryGetOffender(string message, out IPAddress address)
    {
        address = IPAddress.None;

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
            if (TryGetOffender(message.RenderMessage(), out var address))
                _system._pending.Enqueue(address);
        }
    }
}
