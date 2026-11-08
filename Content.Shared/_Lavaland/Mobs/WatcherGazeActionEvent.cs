// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Actions;

namespace Content.Shared._Lavaland.Mobs;

/// <summary>
/// TG parity: the watcher's gaze action. The actual effect lives in the server-side WatcherGazeSystem,
/// the event has to be shared because the action prototype references it in YAML.
/// </summary>
public sealed partial class WatcherGazeActionEvent : EntityTargetActionEvent;
