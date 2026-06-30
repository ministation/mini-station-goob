using Robust.Shared.GameStates;

namespace Content.Goobstation.Shared.Xenobiology.Components;

[RegisterComponent, NetworkedComponent]
public sealed partial class SlimeXenoVacuumReleasedComponent : Component
{
    public TimeSpan BlockLatchUntil = TimeSpan.Zero;
}
