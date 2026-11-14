// SPDX-FileCopyrightText: 2026 Space Station 14 Contributors
//
// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace Content.Server._Mini.SiteBridge;

/// <summary>
/// Мини: чистая логика моста с сайтом (без движка), чтобы её можно было проверить тестом.
/// </summary>
public static class SiteBridgeHelpers
{
    public const int MaxOutputBytes = 64 * 1024;
    public const int MaxCommandLength = 4000;
    public const int DefaultTimeoutSec = 30;
    public const int MaxTimeoutSec = 300;

    public const string KindConsole = "console";
    public const string KindShell = "shell";
    /// <summary>Выключение сервера с сайта: пишет файл блокировки и останавливает движок.</summary>
    public const string KindShutdown = "shutdown";

    /// <summary>
    /// Привести базовый адрес сайта к виду с завершающим слэшем, чтобы относительные
    /// пути контракта склеивались правильно. Принимает только http/https.
    /// </summary>
    public static bool TryNormalizeBaseUrl(string? raw, [NotNullWhen(true)] out Uri? baseUrl)
    {
        baseUrl = null;
        if (string.IsNullOrWhiteSpace(raw))
            return false;

        var trimmed = raw.Trim().TrimEnd('/');
        if (!Uri.TryCreate(trimmed + "/", UriKind.Absolute, out var uri))
            return false;

        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            return false;

        baseUrl = uri;
        return true;
    }

    /// <summary>
    /// Вытащить адрес хоста из публичного адреса сервера: ss14s://host:port, host:port,
    /// https://host/... все дают host. Нужен для поля address в hello.
    /// </summary>
    public static string ExtractAddress(string? publicAddress)
    {
        if (string.IsNullOrWhiteSpace(publicAddress))
            return string.Empty;

        var value = publicAddress.Trim();
        var scheme = value.IndexOf("://", StringComparison.Ordinal);
        if (scheme >= 0)
            value = value[(scheme + 3)..];

        var slash = value.IndexOf('/');
        if (slash >= 0)
            value = value[..slash];

        if (value.StartsWith('['))
        {
            var close = value.IndexOf(']');
            return close > 0 ? value[1..close] : value;
        }

        var colon = value.LastIndexOf(':');
        if (colon > 0)
            value = value[..colon];

        return value;
    }

    /// <summary>
    /// Обрезка строки ровно по границе UTF-8 байт (контракт: output не больше 64 KiB).
    /// </summary>
    public static string TruncateUtf8(string? text, int maxBytes)
    {
        if (string.IsNullOrEmpty(text) || maxBytes <= 0)
            return string.Empty;

        var bytes = 0;
        var index = 0;
        for (; index < text.Length; index++)
        {
            var ch = text[index];
            int size;
            if (ch < 0x80)
                size = 1;
            else if (ch < 0x800)
                size = 2;
            else if (char.IsHighSurrogate(ch))
                size = 4;
            else
                size = 3;

            if (bytes + size > maxBytes)
                break;

            bytes += size;
            if (size == 4)
                index++;
        }

        return index >= text.Length ? text : text[..index];
    }
}

/// <summary>
/// Тело POST /api/game/bridge/hello.
/// </summary>
public sealed class HelloRequest
{
    [JsonPropertyName("name")] public string Name { get; init; } = string.Empty;
    [JsonPropertyName("hostname")] public string Hostname { get; init; } = string.Empty;
    [JsonPropertyName("address")] public string Address { get; init; } = string.Empty;
    [JsonPropertyName("port")] public int Port { get; init; }
    [JsonPropertyName("version")] public string Version { get; init; } = string.Empty;
    [JsonPropertyName("max_players")] public int MaxPlayers { get; init; }
    [JsonPropertyName("players")] public int Players { get; init; }
    [JsonPropertyName("uptime_sec")] public long UptimeSec { get; init; }
}

/// <summary>
/// Ответ POST /api/game/bridge/hello.
/// </summary>
public sealed class HelloResponse
{
    [JsonPropertyName("server_id")] public long ServerId { get; init; }
    [JsonPropertyName("token")] public string Token { get; init; } = string.Empty;
    [JsonPropertyName("poll_interval_sec")] public int PollIntervalSec { get; init; }
    [JsonPropertyName("heartbeat_interval_sec")] public int HeartbeatIntervalSec { get; init; }
}

/// <summary>
/// Тело POST /api/game/bridge/heartbeat.
/// </summary>
public sealed class HeartbeatRequest
{
    [JsonPropertyName("players")] public int Players { get; init; }
    [JsonPropertyName("max_players")] public int MaxPlayers { get; init; }
    [JsonPropertyName("round_id")] public int RoundId { get; init; }
    [JsonPropertyName("map")] public string Map { get; init; } = string.Empty;
    [JsonPropertyName("preset")] public string Preset { get; init; } = string.Empty;
    [JsonPropertyName("run_level")] public string RunLevel { get; init; } = string.Empty;
    [JsonPropertyName("uptime_sec")] public long UptimeSec { get; init; }
    [JsonPropertyName("version")] public string Version { get; init; } = string.Empty;
}

/// <summary>
/// Ответ POST /api/game/bridge/heartbeat.
/// </summary>
public sealed class HeartbeatResponse
{
    [JsonPropertyName("ok")] public bool Ok { get; init; }
    [JsonPropertyName("poll_interval_sec")] public int PollIntervalSec { get; init; }
}

/// <summary>
/// Ответ GET /api/game/bridge/commands.
/// </summary>
public sealed class CommandsResponse
{
    [JsonPropertyName("commands")] public List<BridgeCommand>? Commands { get; init; }
}

/// <summary>
/// Одна команда из админки сайта.
/// </summary>
public sealed class BridgeCommand
{
    [JsonPropertyName("id")] public long Id { get; init; }
    [JsonPropertyName("kind")] public string Kind { get; init; } = string.Empty;
    [JsonPropertyName("command")] public string Command { get; init; } = string.Empty;
    [JsonPropertyName("timeout_sec")] public int TimeoutSec { get; init; }
}

/// <summary>
/// Тело POST /api/game/bridge/result.
/// </summary>
public sealed class ResultRequest
{
    [JsonPropertyName("id")] public long Id { get; init; }
    [JsonPropertyName("ok")] public bool Ok { get; init; }
    [JsonPropertyName("output")] public string Output { get; init; } = string.Empty;
}
