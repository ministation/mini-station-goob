// SPDX-FileCopyrightText: 2026 Space Station 14 Contributors
//
// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

#if MINI_RELEASE_BUILD
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Content.Server._Mini.SiteBridge;
using Robust.Shared;
using Robust.Shared.Configuration;
using Robust.Shared.Console;
using Robust.Shared.IoC;
using Robust.Shared.Log;
using Robust.Shared.Network;

namespace Content.Goobstation.Server.Activation;

/// <summary>
/// Мини: активация релизной сборки (контракт v2, раздел 2). Файл компилируется
/// только при MINI_RELEASE_BUILD, локальные сборки проверку не делают и не
/// ограничивают игроков.
/// </summary>
internal static class ActivationGate
{
    // Публичный ключ боевой активации из спеки, ECDSA P-256, SubjectPublicKeyInfo DER.
    private const string ActivationPublicKey =
        "MFkwEwYHKoZIzj0CAQYIKoZIzj0DAQcDQgAEoTYs2T07zVMJOBTmGQkbi4HajOwnS2k29xf/WayVsWdhgnb28bhHBALt0Wcg6y/p0KSDkxYHAOsMJbPWzOOu2Q==";

    private static string _problem = string.Empty;
    private static bool _blocked;
    private static string _blockPath = string.Empty;

    public static bool Valid { get; private set; }

    /// <summary>
    /// Проверка файла до ContentStart: только поиск и подпись, без ограничений.
    /// Возвращает аргументы без --mini-activation: движок не знает этот флаг
    /// и упал бы на разборе командной строки.
    /// </summary>
    public static string[] Prepare(string[] args)
    {
        var cwd = Directory.GetCurrentDirectory();
        var envPath = Environment.GetEnvironmentVariable(MiniActivation.EnvPath);
        var path = MiniActivation.FindFile(args, envPath, cwd);

        // Блокировка с сайта важнее подписи: выключенный сервер не должен
        // снова включаться по тому же файлу активации после перезапуска.
        _blockPath = MiniActivation.BlockPath(path, cwd);
        MiniActivation.RuntimeBlockPath = _blockPath;

        if (MiniActivation.IsBlocked(_blockPath))
        {
            _blocked = true;
            _problem = "сервер выключен с сайта Mini Station";
        }
        else if (path == null)
            _problem = "файл активации не найден";
        else if (!MiniActivation.TryVerifyFile(path, ActivationPublicKey, out var error))
            _problem = error;
        else
            Valid = true;

        return RemoveCustomArgs(args);
    }

    private static string[] RemoveCustomArgs(string[] args)
    {
        var result = new List<string>(args.Length);
        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            if (arg == MiniActivation.ArgPath)
            {
                i++;
                continue;
            }

            if (arg.StartsWith(MiniActivation.ArgPath + "=", StringComparison.Ordinal))
                continue;

            result.Add(arg);
        }

        return result.ToArray();
    }

    /// <summary>
    /// Вызывается из EntryPoint.PostInit, когда IoC и консоль уже готовы.
    /// </summary>
    public static void Install()
    {
        // Релизный пакет стартует из Robust.Server: Program.Main контента (единственный
        // вызов Prepare) не выполняется, поэтому проверяем активацию здесь.
        if (!Valid && !_blocked && _problem.Length == 0)
            Prepare(Environment.GetCommandLineArgs());

        var sawmill = IoCManager.Resolve<ILogManager>().GetSawmill("mini.activation");
        if (Valid)
        {
            sawmill.Info("мини-активация: релиз активирован");
            return;
        }

        var cfg = IoCManager.Resolve<IConfigurationManager>();
        var advertiseBefore = cfg.GetCVar(CVars.HubAdvertise);
        cfg.SetCVar(CVars.HubAdvertise, false);

        if (_blocked)
        {
            sawmill.Warning($"мини-активация: сервер выключен с сайта Mini Station (файл блокировки {_blockPath}). Урезанный режим: hub.advertise {advertiseBefore} -> false, новые подключения игроков отклоняются.");
            sawmill.Warning($"мини-активация: включить сервер обратно может только владелец машины: удалите файл {MiniActivation.BlockFileName} и перезапустите сервер.");
            IoCManager.Resolve<IConsoleHost>().LocalShell.WriteLine("Сервер выключен с сайта Mini Station. Включить обратно: удалить файл " + MiniActivation.BlockFileName + " и перезапустить сервер.");
        }
        else
        {
            sawmill.Warning($"мини-активация: релиз не активирован ({_problem}). Урезанный режим: hub.advertise {advertiseBefore} -> false, новые подключения игроков отклоняются.");
            sawmill.Warning("мини-активация: скачайте mini_activation.json в админке https://ministation.ru (раздел «Серверы»), положите рядом с конфигом или задайте MINI_ACTIVATION и перезапустите сервер.");
            IoCManager.Resolve<IConsoleHost>().LocalShell.WriteLine("ВНИМАНИЕ: сервер не активирован, игроки не смогут подключиться. Скачайте mini_activation.json в разделе Серверы на https://ministation.ru и перезапустите сервер.");
        }

        var denyReason = _blocked ? "Сервер выключен владельцем" : "Сервер не активирован, обратитесь к владельцу";
        IoCManager.Resolve<INetManager>().Connecting += connecting =>
        {
            connecting.Deny(denyReason);
            return Task.CompletedTask;
        };
    }
}
#endif