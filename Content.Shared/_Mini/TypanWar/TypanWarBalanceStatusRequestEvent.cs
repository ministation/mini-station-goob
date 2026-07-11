// SPDX-FileCopyrightText: 2026 Egorik1
// Мини-станция, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/ministation/mini-station-goob/master/LICENSE.TXT

using Robust.Shared.Serialization;

namespace Content.Shared._Mini.TypanWar;

/// <summary>
/// Client requests current faction balance status for late join UI.
/// </summary>
[Serializable, NetSerializable]
public sealed class TypanWarBalanceStatusRequestEvent : EntityEventArgs;
