using Content.Shared.Inventory;

namespace Content.Shared._Trauma.Genetics.Events;

[ByRefEvent]
public record struct ModifyViewconeAngleEvent() : IInventoryRelayEvent
{
    public SlotFlags TargetSlots { get; } = SlotFlags.EYES | SlotFlags.HEAD | SlotFlags.MASK;
    public float AngleModifier = 1f;
}
