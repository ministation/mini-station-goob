// SPDX-FileCopyrightText: 2026 Space Station 14 Contributors
//
// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Content.Server._Mini.SiteBridge;

/// <summary>
/// Мини: проверка подписанной активации релизной сборки (контракт v2, раздел 2).
/// Класс без #if MINI_RELEASE_BUILD: его тестирует Content.Tests, а сама проверка
/// при старте живёт под #if в Content.Goobstation.Server, поэтому локальные
/// сборки ничего не проверяют и не ограничивают.
/// </summary>
public static class MiniActivation
{
    public const string FileName = "mini_activation.json";
    public const string ArgPath = "--mini-activation";
    public const string ArgDataDir = "--data-dir";
    public const string ArgConfigFile = "--config-file";
    public const string EnvPath = "MINI_ACTIVATION";
    /// <summary>Файл блокировки: сервер выключен с сайта Mini Station и стартует урезанно.</summary>
    public const string BlockFileName = "mini_blocked";
    public const int PayloadVersion = 1;
    public const string PayloadKind = "mini-release";

    /// <summary>
    /// Ищет первый существующий файл активации в порядке из контракта:
    /// аргумент, переменная окружения, data-dir, текущая папка, папка конфига.
    /// </summary>
    public static string? FindFile(IEnumerable<string> args, string? envPath, string cwd)
    {
        var explicitPath = GetOption(args, ArgPath);
        if (!string.IsNullOrWhiteSpace(explicitPath) && File.Exists(explicitPath))
            return explicitPath;

        if (!string.IsNullOrWhiteSpace(envPath) && File.Exists(envPath))
            return envPath;

        // data-dir из аргумента или штатный движковый: папка data рядом с исполняемым файлом.
        var dataDir = GetOption(args, ArgDataDir);
        if (string.IsNullOrWhiteSpace(dataDir))
            dataDir = Path.Combine(AppContext.BaseDirectory, "data");

        var dataCandidate = Path.Combine(dataDir, FileName);
        if (File.Exists(dataCandidate))
            return dataCandidate;

        var local = Path.Combine(cwd, FileName);
        if (File.Exists(local))
            return local;

        var configFile = GetOption(args, ArgConfigFile);
        if (!string.IsNullOrWhiteSpace(configFile))
        {
            var configDir = Path.GetDirectoryName(Path.GetFullPath(configFile));
            if (configDir != null)
            {
                var candidate = Path.Combine(configDir, FileName);
                if (File.Exists(candidate))
                    return candidate;
            }
        }

        return null;
    }

    /// <summary>
    /// Читает файл активации и проверяет подпись вшитым публичным ключом.
    /// </summary>
    public static bool TryVerifyFile(string path, string publicKeyBase64, out string error)
    {
        if (!File.Exists(path))
        {
            error = $"файл активации не найден: {path}";
            return false;
        }

        string text;
        try
        {
            text = File.ReadAllText(path, Encoding.UTF8);
        }
        catch (Exception e)
        {
            error = $"файл активации не читается: {e.Message}";
            return false;
        }

        string? payload;
        string? signature;
        try
        {
            using var doc = JsonDocument.Parse(text);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                error = "файл активации: ожидался JSON-объект";
                return false;
            }

            payload = ReadString(doc.RootElement, "payload");
            signature = ReadString(doc.RootElement, "signature");
        }
        catch (JsonException e)
        {
            error = $"файл активации: битый JSON: {e.Message}";
            return false;
        }

        if (string.IsNullOrEmpty(payload) || string.IsNullOrEmpty(signature))
        {
            error = "файл активации: нет payload или signature";
            return false;
        }

        if (!Verify(payload, signature, publicKeyBase64, out error))
            return false;

        error = string.Empty;
        return true;
    }

    /// <summary>
    /// Проверяет подпись payload и его версию с kind. Публичный ключ передаётся
    /// параметром, чтобы тесты могли использовать свою пару.
    /// </summary>
    public static bool Verify(string payload, string signatureBase64, string publicKeyBase64, out string error)
    {
        if (!VerifyPayload(payload, out error))
            return false;

        byte[] signature;
        try
        {
            signature = Convert.FromBase64String(signatureBase64);
        }
        catch (FormatException)
        {
            error = "подпись активации: не base64";
            return false;
        }

        byte[] publicKey;
        try
        {
            publicKey = Convert.FromBase64String(publicKeyBase64);
        }
        catch (FormatException)
        {
            error = "публичный ключ активации: не base64";
            return false;
        }

        try
        {
            using var ecdsa = ECDsa.Create();
            ecdsa.ImportSubjectPublicKeyInfo(publicKey, out _);
            // Сайт подписывает через cryptography/OpenSSL, это DER-последовательность.
            if (!ecdsa.VerifyData(Encoding.UTF8.GetBytes(payload), signature, HashAlgorithmName.SHA256,
                    DSASignatureFormat.Rfc3279DerSequence))
            {
                error = "подпись активации неверна";
                return false;
            }
        }
        catch (CryptographicException e)
        {
            error = $"публичный ключ активации: {e.Message}";
            return false;
        }

        error = string.Empty;
        return true;
    }

    /// <summary>
    /// Проверяет версию и kind внутри payload.
    /// </summary>
    public static bool VerifyPayload(string payload, out string error)
    {
        try
        {
            using var doc = JsonDocument.Parse(payload);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                error = "payload активации: ожидался JSON-объект";
                return false;
            }

            if (!doc.RootElement.TryGetProperty("v", out var version) || version.ValueKind != JsonValueKind.Number
                || !version.TryGetInt32(out var v) || v != PayloadVersion)
            {
                error = $"payload активации: версия не {PayloadVersion}";
                return false;
            }

            var kind = ReadString(doc.RootElement, "kind");
            if (kind != PayloadKind)
            {
                error = $"payload активации: kind не {PayloadKind}";
                return false;
            }
        }
        catch (JsonException e)
        {
            error = $"payload активации: битый JSON: {e.Message}";
            return false;
        }

        error = string.Empty;
        return true;
    }

    /// <summary>
    /// Путь файла блокировки: рядом с найденным файлом активации, иначе в cwd.
    /// Тот же путь проверяет ActivationGate при старте и пишет мост при
    /// выключении с сайта, поэтому блокировка переживает перезапуск сервера.
    /// </summary>
    public static string BlockPath(string? activationPath, string cwd)
    {
        if (!string.IsNullOrWhiteSpace(activationPath))
        {
            var dir = Path.GetDirectoryName(Path.GetFullPath(activationPath));
            if (!string.IsNullOrWhiteSpace(dir))
                return Path.Combine(dir, BlockFileName);
        }

        return Path.Combine(cwd, BlockFileName);
    }

    public static bool IsBlocked(string path) => File.Exists(path);

    /// <summary>
    /// Путь блокировки, вычисленный при старте (ActivationGate.Prepare). Мост
    /// пишет файл сюда, чтобы путь не разъезжался с проверкой при старте.
    /// </summary>
    public static string? RuntimeBlockPath { get; set; }

    /// <summary>
    /// Значение аргумента вида "--name value" или "--name=value".
    /// </summary>
    public static string? GetOption(IEnumerable<string> args, string name)
    {
        var expectValue = false;
        foreach (var arg in args)
        {
            if (expectValue)
                return arg;

            if (arg == name)
            {
                expectValue = true;
                continue;
            }

            if (arg.StartsWith(name + "=", StringComparison.Ordinal))
                return arg[(name.Length + 1)..];
        }

        return null;
    }

    private static string? ReadString(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.String)
            return null;

        return value.GetString();
    }
}