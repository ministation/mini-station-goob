// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

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
