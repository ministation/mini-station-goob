// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

using Content.Server._Mini.Pinpointer.Systems;

namespace Content.Server._Mini.Pinpointer;

/// <summary>
/// Pinpointer that can track targets from antagonist objectives
/// </summary>
[RegisterComponent, Access(typeof(ObjectivePinpointerSystem))]
public sealed partial class ObjectivePinpointerComponent : Component
{
    /// <summary>
    /// Mind entity that owns this pinpointer, used to access objectives
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public EntityUid? OwnerMind;
}
