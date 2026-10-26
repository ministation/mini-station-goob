// SPDX-FileCopyrightText: 2026 Space Station 14 Contributors
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Roles;
using Robust.Shared.Prototypes;

namespace Content.Shared._Mini.Katorga;

/// <summary>
///     Должности каторги. Заключённый - единственная должность, которую видит
///     временно забаненный игрок в окне позднего подключения.
/// </summary>
public static class KatorgaJobs
{
    public static readonly ProtoId<JobPrototype> Prisoner = "Prisoner";
}
