// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.Server._Lavaland.Ashwalker;

/// <summary>
/// TG head pike: a bone spear you can put a severed head on.
/// </summary>
[RegisterComponent]
public sealed partial class HeadPikeComponent : Component
{
    /// <summary>
    /// Container holding the impaled head.
    /// </summary>
    [DataField]
    public string ContainerId = "head";

    [ViewVariables]
    public EntityUid? Head;
}
