// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from tgstation/tgstation (code/modules/fishing, AGPL-3.0-or-later).

using Robust.Shared.Serialization;

namespace Content.Goobstation.Shared.Fishing;

[Serializable, NetSerializable]
public enum FishingRodEquipmentUiKey : byte
{
    Key,
}

/// <summary>
/// One equipment slot of a fishing rod, shown in the rod equipment window.
/// </summary>
[Serializable, NetSerializable]
public sealed class FishingRodEquipmentSlotState
{
    /// <summary>
    /// Item slot id: line, hook or bait.
    /// </summary>
    public string Slot = string.Empty;

    /// <summary>
    /// Localization key of the slot name.
    /// </summary>
    public string Name = string.Empty;

    /// <summary>
    /// Entity prototype of the slotted item, null when the slot is empty.
    /// </summary>
    public string? Prototype;
}

[Serializable, NetSerializable]
public sealed class FishingRodEquipmentBoundUserInterfaceState : BoundUserInterfaceState
{
    public readonly List<FishingRodEquipmentSlotState> Slots;

    public FishingRodEquipmentBoundUserInterfaceState(List<FishingRodEquipmentSlotState> slots)
    {
        Slots = slots;
    }
}

/// <summary>
/// Asks the server to take the item out of one of the rod equipment slots.
/// </summary>
[Serializable, NetSerializable]
public sealed class FishingRodEquipmentEjectMessage : BoundUserInterfaceMessage
{
    public readonly string Slot;

    public FishingRodEquipmentEjectMessage(string slot)
    {
        Slot = slot;
    }
}
