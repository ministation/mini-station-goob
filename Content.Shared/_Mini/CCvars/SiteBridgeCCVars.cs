// SPDX-FileCopyrightText: 2026 Space Station 14 Contributors
//
// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

using Robust.Shared.Configuration;

namespace Content.Shared._Mini.MiniCCVars;

/// <summary>
/// Мини: мост «игровой сервер - сайт». Сервер сам регистрируется на сайте, шлёт
/// heartbeat, забирает команды из админки и возвращает результат. Контракт:
/// /root/workspaces/site-bridge-contract.md (общий для игры и сайта).
/// </summary>
[CVarDefs]
public sealed class SiteBridgeCCVars
{
    /// <summary>
    /// Включает мост с сайтом целиком.
    /// </summary>
    public static readonly CVarDef<bool> Enabled =
        CVarDef.Create("site_bridge.enabled", false, CVar.SERVER);

    /// <summary>
    /// Базовый адрес сайта, куда игра шлёт запросы.
    /// </summary>
    public static readonly CVarDef<string> Url =
        CVarDef.Create("site_bridge.url", "https://ministation.ru", CVar.SERVER);

    /// <summary>
    /// Общий секрет с сайтом (GAME_BRIDGE_SECRET). Пустой секрет выключает мост.
    /// </summary>
    public static readonly CVarDef<string> Secret =
        CVarDef.Create("site_bridge.secret", "", CVar.SERVER | CVar.CONFIDENTIAL);

    /// <summary>
    /// Разрешить выполнение shell-команд на хосте из админки сайта.
    /// </summary>
    public static readonly CVarDef<bool> AllowHostCommands =
        CVarDef.Create("site_bridge.allow_host_commands", true, CVar.SERVER);
}
