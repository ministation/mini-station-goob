// SPDX-FileCopyrightText: 2026 Space Station 14 Contributors
//
// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Content.Server.GameTicking;
using Content.Server.Maps;
using Content.Shared._Mini.MiniCCVars;
using Content.Shared.CCVar;
using Content.Shared.Maps;
using Robust.Shared;
using Robust.Shared.Asynchronous;
using Robust.Shared.Configuration;
using Robust.Shared.Console;
using Robust.Server.Player;
using Robust.Shared.Network;

namespace Content.Server._Mini.SiteBridge;

/// <summary>
/// Мини: мост «игровой сервер - сайт» по контракту site-bridge-contract.md.
/// Игра сама регистрируется на сайте (hello), шлёт heartbeat, забирает команды
/// из админки и возвращает результат. Все соединения исходящие.
/// </summary>
public sealed class SiteBridgeSystem : EntitySystem
{
    private const string BridgePath = "api/game/bridge/";
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(10);
    private const int HelloStatusWaitSec = 5;
    private const int HeartbeatStatusWaitSec = 2;
    private static readonly TimeSpan MinRetryDelay = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan MaxRetryDelay = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan WarnInterval = TimeSpan.FromMinutes(1);
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly ILogManager _logManager = default!;
    [Dependency] private readonly IHttpClientHolder _http = default!;
    [Dependency] private readonly IConsoleHost _console = default!;
    [Dependency] private readonly ITaskManager _taskManager = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IGameMapManager _mapManager = default!;

    private ISawmill _sawmill = default!;
    private readonly SemaphoreSlim _shellSlots = new(2);

    private CancellationTokenSource? _cts;
    private Uri? _baseUrl;
    private string _secret = string.Empty;
    private string _serverName = string.Empty;
    private string _version = string.Empty;
    private string _hostname = string.Empty;
    private string _address = string.Empty;
    private int _port;
    private int _maxPlayers;
    private bool _allowHostCommands;

    private volatile string _token = string.Empty;
    private TimeSpan _pollInterval = TimeSpan.FromSeconds(3);
    private TimeSpan _heartbeatInterval = TimeSpan.FromSeconds(30);
    private DateTimeOffset _startedAt;
    private DateTimeOffset _lastWarning = DateTimeOffset.MinValue;

    public override void Initialize()
    {
        base.Initialize();
        _sawmill = _logManager.GetSawmill("mini.site-bridge");

        if (!_cfg.GetCVar(SiteBridgeCCVars.Enabled))
            return;

        _secret = _cfg.GetCVar(SiteBridgeCCVars.Secret);
        if (string.IsNullOrWhiteSpace(_secret))
        {
            _sawmill.Warning("site_bridge.secret пуст, мост с сайтом не запущен");
            return;
        }

        if (!SiteBridgeHelpers.TryNormalizeBaseUrl(_cfg.GetCVar(SiteBridgeCCVars.Url), out var baseUrl))
        {
            _sawmill.Warning("site_bridge.url некорректен, мост с сайтом не запущен");
            return;
        }

        _baseUrl = baseUrl;
        _allowHostCommands = _cfg.GetCVar(SiteBridgeCCVars.AllowHostCommands);
        _serverName = _cfg.GetCVar(CVars.GameHostName);
        _port = _cfg.GetCVar(CVars.NetPort);
        _maxPlayers = _cfg.GetCVar(CCVars.SoftMaxPlayers);
        _version = _cfg.GetCVar(CVars.BuildVersion);
        if (string.IsNullOrWhiteSpace(_version))
            _version = typeof(SiteBridgeSystem).Assembly.GetName().Version?.ToString() ?? string.Empty;
        _hostname = SafeHostname();
        _address = SiteBridgeHelpers.ExtractAddress(_cfg.GetCVar(CVars.StatusConnectAddress));
        if (_address.Length == 0)
            _address = SiteBridgeHelpers.ExtractAddress(_cfg.GetCVar(CVars.HubServerUrl));
        _startedAt = DateTimeOffset.UtcNow;

        _cts = new CancellationTokenSource();
        _ = Task.Run(() => RunAsync(_cts.Token));
        _sawmill.Info($"мост сайта включён: {baseUrl.GetLeftPart(UriPartial.Path)}");
    }

    public override void Shutdown()
    {
        base.Shutdown();
        // Мост останавливаем только отменой: фоновый цикл сам завершится.
        _cts?.Cancel();
    }

    private StatusSnapshot BuildStatus()
    {
        var ticker = EntitySystem.Get<GameTicker>();
        var preset = ticker.CurrentPreset ?? ticker.Preset;
        return new StatusSnapshot(
            _playerManager.PlayerCount,
            _cfg.GetCVar(CCVars.SoftMaxPlayers),
            ticker.RoundId,
            _mapManager.GetSelectedMap()?.MapName ?? string.Empty,
            preset == null ? string.Empty : Loc.GetString(preset.ModeTitle),
            ticker.RunLevel.ToString());
    }

    /// <summary>
    /// Снимок статуса берём по требованию в главном потоке. От тика не зависим:
    /// на сервере с dummy ticker тика может не быть, а heartbeat должен идти.
    /// </summary>
    private async Task<StatusSnapshot> SnapshotAsync(int waitSec, CancellationToken cancel)
    {
        var tcs = new TaskCompletionSource<StatusSnapshot>(TaskCreationOptions.RunContinuationsAsynchronously);
        try
        {
            _taskManager.RunOnMainThread(() =>
            {
                try
                {
                    tcs.TrySetResult(BuildStatus());
                }
                catch (Exception e)
                {
                    tcs.TrySetException(e);
                }
            });
        }
        catch (Exception e)
        {
            tcs.TrySetException(e);
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancel);
        timeout.CancelAfter(TimeSpan.FromSeconds(waitSec));
        try
        {
            return await tcs.Task.WaitAsync(timeout.Token);
        }
        catch (OperationCanceledException) when (cancel.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return StatusSnapshot.Empty;
        }
    }

    private async Task RunAsync(CancellationToken cancel)
    {
        var retryDelay = MinRetryDelay;
        var lastHeartbeat = DateTimeOffset.MinValue;

        try
        {
            while (!cancel.IsCancellationRequested)
            {
                try
                {
                    if (_token.Length == 0)
                    {
                        if (!await HelloAsync(cancel))
                        {
                            await Task.Delay(retryDelay, cancel);
                            retryDelay = NextRetryDelay(retryDelay);
                            continue;
                        }

                        retryDelay = MinRetryDelay;
                        lastHeartbeat = DateTimeOffset.UtcNow;
                        continue;
                    }

                    if (DateTimeOffset.UtcNow - lastHeartbeat >= _heartbeatInterval)
                    {
                        if (!await HeartbeatAsync(cancel))
                        {
                            DropToken();
                            await Task.Delay(retryDelay, cancel);
                            retryDelay = NextRetryDelay(retryDelay);
                            continue;
                        }

                        lastHeartbeat = DateTimeOffset.UtcNow;
                        retryDelay = MinRetryDelay;
                    }

                    if (!await PollCommandsAsync(cancel))
                    {
                        DropToken();
                        await Task.Delay(retryDelay, cancel);
                        retryDelay = NextRetryDelay(retryDelay);
                        continue;
                    }

                    await Task.Delay(_pollInterval, cancel);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch (Exception e)
                {
                    DropToken();
                    WarnThrottled($"ошибка связи с сайтом: {e.Message}");
                    await Task.Delay(retryDelay, cancel);
                    retryDelay = NextRetryDelay(retryDelay);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // штатное завершение при выключении сервера
        }
        catch (Exception e)
        {
            _sawmill.Error($"мост сайта остановлен из-за ошибки: {e}");
        }
    }

    private static TimeSpan NextRetryDelay(TimeSpan current)
    {
        var next = TimeSpan.FromTicks(current.Ticks * 2);
        return next > MaxRetryDelay ? MaxRetryDelay : next;
    }

    private async Task<bool> HelloAsync(CancellationToken cancel)
    {
        if (_baseUrl == null)
            return false;

        var status = await SnapshotAsync(HelloStatusWaitSec, cancel);
        var payload = new HelloRequest
        {
            Name = _serverName,
            Hostname = _hostname,
            Address = _address,
            Port = _port,
            Version = _version,
            MaxPlayers = status.MaxPlayers > 0 ? status.MaxPlayers : _maxPlayers,
            Players = status.Players,
            UptimeSec = Uptime(),
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(_baseUrl, BridgePath + "hello"));
        request.Headers.TryAddWithoutValidation("X-Bridge-Secret", _secret);
        request.Content = JsonBody(payload);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancel);
        timeout.CancelAfter(RequestTimeout);

        using var response = await _http.Client.SendAsync(request, timeout.Token);
        if (!response.IsSuccessStatusCode)
        {
            WarnThrottled($"hello не принят сайтом: HTTP {(int) response.StatusCode}");
            return false;
        }

        var hello = await response.Content.ReadFromJsonAsync<HelloResponse>(JsonOptions, timeout.Token);
        if (hello == null || string.IsNullOrWhiteSpace(hello.Token))
        {
            WarnThrottled("hello: сайт вернул пустой токен");
            return false;
        }

        _pollInterval = TimeSpan.FromSeconds(Math.Clamp(hello.PollIntervalSec, 1, 60));
        _heartbeatInterval = TimeSpan.FromSeconds(Math.Clamp(hello.HeartbeatIntervalSec, 5, 300));
        _token = hello.Token;
        _sawmill.Info($"мост сайта: hello принят, server_id={hello.ServerId}");
        return true;
    }

    private async Task<bool> HeartbeatAsync(CancellationToken cancel)
    {
        if (_baseUrl == null)
            return false;

        var token = _token;
        if (token.Length == 0)
            return false;

        var status = await SnapshotAsync(HeartbeatStatusWaitSec, cancel);
        var payload = new HeartbeatRequest
        {
            Players = status.Players,
            MaxPlayers = status.MaxPlayers > 0 ? status.MaxPlayers : _maxPlayers,
            RoundId = status.RoundId,
            Map = status.Map,
            Preset = status.Preset,
            RunLevel = status.RunLevel,
            UptimeSec = Uptime(),
            Version = _version,
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(_baseUrl, BridgePath + "heartbeat"));
        request.Headers.TryAddWithoutValidation("Authorization", "Bearer " + token);
        request.Content = JsonBody(payload);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancel);
        timeout.CancelAfter(RequestTimeout);

        using var response = await _http.Client.SendAsync(request, timeout.Token);
        if (!response.IsSuccessStatusCode)
        {
            WarnThrottled($"heartbeat не принят сайтом: HTTP {(int) response.StatusCode}");
            return false;
        }

        var heartbeat = await response.Content.ReadFromJsonAsync<HeartbeatResponse>(JsonOptions, timeout.Token);
        if (heartbeat != null && heartbeat.PollIntervalSec > 0)
            _pollInterval = TimeSpan.FromSeconds(Math.Clamp(heartbeat.PollIntervalSec, 1, 60));

        return true;
    }

    private async Task<bool> PollCommandsAsync(CancellationToken cancel)
    {
        if (_baseUrl == null)
            return false;

        var token = _token;
        if (token.Length == 0)
            return false;

        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(_baseUrl, BridgePath + "commands"));
        request.Headers.TryAddWithoutValidation("Authorization", "Bearer " + token);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancel);
        timeout.CancelAfter(RequestTimeout);

        using var response = await _http.Client.SendAsync(request, timeout.Token);
        if (!response.IsSuccessStatusCode)
        {
            WarnThrottled($"опрос команд не удался: HTTP {(int) response.StatusCode}");
            return false;
        }

        var commands = await response.Content.ReadFromJsonAsync<CommandsResponse>(JsonOptions, timeout.Token);
        if (commands?.Commands == null)
            return true;

        foreach (var command in commands.Commands)
            RunCommand(command);

        return true;
    }

    /// <summary>
    /// Команда выполняется вне цикла опроса: shell может идти минутами, а heartbeat
    /// и опрос должны продолжаться.
    /// </summary>
    private void RunCommand(BridgeCommand command)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                await ExecuteCommandAsync(command);
            }
            catch (Exception e)
            {
                _sawmill.Warning($"мост сайта: команда #{command.Id} упала: {e.Message}");
            }
        });
    }

    private async Task ExecuteCommandAsync(BridgeCommand command)
    {
        if (command.Id <= 0 || string.IsNullOrEmpty(command.Command))
        {
            _sawmill.Warning("мост сайта: сайт прислал некорректную команду, пропускаю");
            return;
        }

        if (command.Command.Length > SiteBridgeHelpers.MaxCommandLength)
        {
            await SendResultAsync(command.Id, false, "command too long");
            return;
        }

        if (command.Kind == SiteBridgeHelpers.KindConsole)
        {
            var text = command.Command;
            _taskManager.RunOnMainThread(() =>
            {
                try
                {
                    _console.ExecuteCommand(text);
                }
                catch (Exception e)
                {
                    _sawmill.Warning($"мост сайта: консольная команда упала: {e.Message}");
                }
            });
            // Контракт: ok значит команда принята диспетчером, вывода консоли нет.
            await SendResultAsync(command.Id, true, string.Empty);
            return;
        }

        if (command.Kind != SiteBridgeHelpers.KindShell)
        {
            await SendResultAsync(command.Id, false, "unsupported kind");
            return;
        }

        if (!_allowHostCommands)
        {
            await SendResultAsync(command.Id, false, "host commands disabled");
            return;
        }

        var timeoutSec = command.TimeoutSec <= 0
            ? SiteBridgeHelpers.DefaultTimeoutSec
            : Math.Min(command.TimeoutSec, SiteBridgeHelpers.MaxTimeoutSec);

        var (ok, output) = await RunShellAsync(command.Command, timeoutSec);
        await SendResultAsync(command.Id, ok, output);
    }

    private async Task<(bool Ok, string Output)> RunShellAsync(string commandLine, int timeoutSec)
    {
        await _shellSlots.WaitAsync();
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = OperatingSystem.IsWindows() ? "cmd.exe" : "/bin/sh",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
            };
            startInfo.ArgumentList.Add(OperatingSystem.IsWindows() ? "/c" : "-c");
            startInfo.ArgumentList.Add(commandLine);

            using var process = new Process { StartInfo = startInfo };
            process.Start();

            // Читаем параллельно, иначе процесс встанет на заполненном пайпе.
            var stdout = ReadLimitedAsync(process.StandardOutput);
            var stderr = ReadLimitedAsync(process.StandardError);

            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSec));
            try
            {
                await process.WaitForExitAsync(timeout.Token);
                var output = (await stdout) + (await stderr);
                return (process.ExitCode == 0, SiteBridgeHelpers.TruncateUtf8(output, SiteBridgeHelpers.MaxOutputBytes));
            }
            catch (OperationCanceledException)
            {
                KillProcess(process);
                // Дочитываем то, что успело прийти, не блокируясь на убитом процессе.
                DrainQuietly(stdout);
                DrainQuietly(stderr);
                return (false, $"timeout: команда не уложилась в {timeoutSec} с");
            }
        }
        finally
        {
            _shellSlots.Release();
        }
    }

    /// <summary>
    /// Чтение до лимита: лишнее вычитывается и выбрасывается, чтобы процесс не завис
    /// на полном пайпе, но в память не попадало.
    /// </summary>
    private static async Task<string> ReadLimitedAsync(StreamReader reader)
    {
        var buffer = new char[4096];
        var builder = new StringBuilder();
        while (true)
        {
            var read = await reader.ReadAsync(buffer, 0, buffer.Length);
            if (read <= 0)
                break;

            var room = SiteBridgeHelpers.MaxOutputBytes - builder.Length;
            if (room <= 0)
                continue;

            builder.Append(buffer, 0, Math.Min(read, room));
        }

        return builder.ToString();
    }

    private static void DrainQuietly(Task<string> read)
    {
        _ = read.ContinueWith(static t => _ = t.Exception, TaskContinuationOptions.OnlyOnFaulted);
    }

    /// <summary>
    /// Тело собираем в UTF-8 байты и шлём с явным Content-Length: HttpClient иначе
    /// отправляет поток с Transfer-Encoding: chunked, что понимают не все прокси и моки.
    /// </summary>
    private static ByteArrayContent JsonBody<T>(T payload)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(payload, JsonOptions);
        var content = new ByteArrayContent(bytes);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/json") { CharSet = "utf-8" };
        content.Headers.ContentLength = bytes.Length;
        return content;
    }

    private static void KillProcess(Process process)
    {
        try
        {
            process.Kill(entireProcessTree: true);
        }
        catch
        {
            // процесс уже мог завершиться сам
        }
    }

    private async Task SendResultAsync(long id, bool ok, string output)
    {
        if (_baseUrl == null)
            return;

        var token = _token;
        if (token.Length == 0)
        {
            WarnThrottled($"мост сайта: результат команды #{id} не отправлен, нет токена");
            return;
        }

        try
        {
            var payload = new ResultRequest
            {
                Id = id,
                Ok = ok,
                Output = SiteBridgeHelpers.TruncateUtf8(output, SiteBridgeHelpers.MaxOutputBytes),
            };

            using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(_baseUrl, BridgePath + "result"));
            request.Headers.TryAddWithoutValidation("Authorization", "Bearer " + token);
            request.Content = JsonBody(payload);

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken.None);
            timeout.CancelAfter(RequestTimeout);

            using var response = await _http.Client.SendAsync(request, timeout.Token);
            if (!response.IsSuccessStatusCode)
                WarnThrottled($"мост сайта: результат команды #{id} не принят: HTTP {(int) response.StatusCode}");
        }
        catch (Exception e)
        {
            WarnThrottled($"мост сайта: результат команды #{id} не отправлен: {e.Message}");
        }
    }

    private void DropToken()
    {
        _token = string.Empty;
    }

    private void WarnThrottled(string message)
    {
        var now = DateTimeOffset.UtcNow;
        if (now - _lastWarning < WarnInterval)
            return;

        _lastWarning = now;
        _sawmill.Warning(message);
    }

    private long Uptime()
    {
        var seconds = (long) (DateTimeOffset.UtcNow - _startedAt).TotalSeconds;
        return seconds < 0 ? 0 : seconds;
    }

    private static string SafeHostname()
    {
        try
        {
            return System.Net.Dns.GetHostName();
        }
        catch
        {
            return string.Empty;
        }
    }

    private readonly record struct StatusSnapshot(
        int Players,
        int MaxPlayers,
        int RoundId,
        string Map,
        string Preset,
        string RunLevel)
    {
        public static StatusSnapshot Empty { get; } =
            new(0, 0, 0, string.Empty, string.Empty, string.Empty);
    }
}