// SPDX-FileCopyrightText: 2026 Casha
// SPDX-License-Identifier: LicenseRef-MiniStation-1.0
using System.Collections.Generic;
using Content.Server.Antag.Components;
using Robust.Shared.Player;

namespace Content.Server._Mini.AntagTokens;

public sealed class AntagSelectionGetForcedCandidatesEvent(
    AntagSelectionDefinition definition,
    IList<ICommonSession> playerPool) : EntityEventArgs
{
    public AntagSelectionDefinition Definition { get; private set; } = definition;
    public IList<ICommonSession> PlayerPool { get; private set; } = playerPool;
    public List<ICommonSession> ForcedSessions { get; private set; } = new();
}

public sealed class AntagSelectionBypassPreferenceCheckEvent(
    ICommonSession? session,
    AntagSelectionDefinition definition) : EntityEventArgs
{
    public ICommonSession? Session { get; private set; } = session;
    public AntagSelectionDefinition Definition { get; private set; } = definition;
    public bool Bypass { get; set; }
}

public sealed class AntagSelectionExcludeSessionEvent(
    ICommonSession session,
    AntagSelectionDefinition definition) : EntityEventArgs
{
    public ICommonSession Session { get; private set; } = session;
    public AntagSelectionDefinition Definition { get; private set; } = definition;
    public bool Excluded { get; set; }
}
