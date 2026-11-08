// SPDX-FileCopyrightText: 2026 Space Station 14 Contributors
//
// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

using Content.Server._Mini.Pinpointer.Systems;

namespace Content.Server._Mini.Pinpointer;

/// <summary>
/// Mini: пинпоинтер ОБР. Штатно ищет ядерную бомбу (whitelist прототипа), но умеет
/// переключаться на шаттл ОБР, чтобы отряд находил дорогу назад.
/// </summary>
[RegisterComponent, Access(typeof(ErtShuttlePinpointerSystem))]
public sealed partial class ErtShuttlePinpointerComponent : Component
{
    /// <summary>
    /// Маркер на шаттле ОБР (капсула криосна), на него ведёт переключение.
    /// </summary>
    [DataField]
    public EntityUid? ShuttleTarget;

    /// <summary>
    /// Штатная цель пинпоинтера до переключения на шаттл (например, вызвавший ОБР).
    /// null - штатный поиск по whitelist (диск/бомба).
    /// </summary>
    [DataField]
    public EntityUid? DefaultTarget;

    /// <summary>
    /// Текущий режим: true - цель шаттл ОБР, false - штатная цель (бомба).
    /// </summary>
    [DataField]
    public bool ShowingShuttle;
}
