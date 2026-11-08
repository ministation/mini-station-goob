// SPDX-FileCopyrightText: 2026 Egorik1
// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

using Content.Shared._Mini.TypanWar;
using Robust.Shared.Serialization;

namespace Content.Shared._Mini.TypanWar;

/// <summary>
/// Client asks the server to re-send the current war HUD state.
/// </summary>
[Serializable, NetSerializable]
public sealed class TypanWarStatusRequestEvent : EntityEventArgs;
