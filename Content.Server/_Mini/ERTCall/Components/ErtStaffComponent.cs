// SPDX-License-Identifier: LicenseRef-MiniStation-1.0


namespace Content.Server._Mini.ERT.Components;

/// <summary>
/// Компонент для бойцов ERT отряда.
/// При добавлении mind им будет назначен objective с целью вызова.
/// </summary>
[RegisterComponent]
public sealed partial class ErtStaffComponent : Component
{
    /// <summary>
    /// Цель вызова ERT отряда.
    /// </summary>
    [DataField]
    public string? CallReason;
}
