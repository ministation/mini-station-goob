// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

namespace Content.Shared._Lavaland.Chasm;

[RegisterComponent]
public sealed partial class PreventChasmFallingComponent : Component
{
    [DataField]
    public bool DeleteOnUse = true;
}