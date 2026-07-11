// SPDX-FileCopyrightText: 2026 Egorik1
// Мини-станция, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/ministation/mini-station-goob/master/LICENSE.TXT

using Robust.Shared.GameStates;

namespace Content.Shared._Mini.TypanWar;

/// <summary>
/// Marks entities inside an active capture zone protection margin as indestructible during war.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class TypanWarCaptureZoneProtectedComponent : Component;
