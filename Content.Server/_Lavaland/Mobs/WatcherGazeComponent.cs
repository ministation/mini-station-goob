// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.Server._Lavaland.Mobs;

/// <summary>
/// TG parity: which gaze variant a watcher has.
/// </summary>
public enum WatcherGazeType : byte
{
    /// <summary>Default watcher: flash and confusion.</summary>
    Normal,
    /// <summary>Magmawing: paralyze and set on fire.</summary>
    Fire,
    /// <summary>Icewing: freeze and throw away from the watcher.</summary>
    Ice,
}

/// <summary>
/// Marker for watchers that can use <see cref="WatcherGazeActionEvent"/> with the given effect variant.
/// </summary>
[RegisterComponent]
public sealed partial class WatcherGazeComponent : Component
{
    [DataField]
    public WatcherGazeType Type = WatcherGazeType.Normal;
}
