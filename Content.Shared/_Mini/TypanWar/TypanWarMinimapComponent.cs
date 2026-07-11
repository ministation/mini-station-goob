using Robust.Shared.GameStates;

namespace Content.Shared._Mini.TypanWar;

[RegisterComponent, NetworkedComponent]
public sealed partial class TypanWarMinimapComponent : Component
{
    public EntityUid? ActionEntity;
}
