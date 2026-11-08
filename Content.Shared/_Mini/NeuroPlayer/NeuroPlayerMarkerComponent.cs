// SPDX-FileCopyrightText: 2026 Egorik1
// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

namespace Content.Shared._Mini.NeuroPlayer;

/// <summary>
/// Marks a body puppeteered by a neuro player: a mind without a user id on purpose, because an
/// LLM answers instead of a client. Shared so the mind status can tell such a body apart from one
/// whose player never attached - otherwise a live bot is examined as catatonic and a dead one as
/// beyond recovery.
/// </summary>
[RegisterComponent]
public sealed partial class NeuroPlayerMarkerComponent : Component;
