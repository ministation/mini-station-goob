// SPDX-FileCopyrightText: 2026 Space Station 14 Contributors
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Numerics;
using Content.Shared.Guidebook;
using Robust.Shared.Prototypes;

namespace Content.Client.Guidebook;

internal readonly record struct GuidebookPageState(
    Vector2 Scroll,
    string Search,
    HashSet<string> ExpandedCollapsibles,
    HashSet<string> RevealedEntryAnchors);

internal static class GuidebookPageStateStorage
{
    // Parallel integration tests can run several clients in one process, so writes can race.
    internal static readonly ConcurrentDictionary<ProtoId<GuideEntryPrototype>, GuidebookPageState> States = new();

    internal static readonly GuidebookPageState Empty = new(default, string.Empty, [], []);
}
