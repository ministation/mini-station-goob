using Robust.Shared.GameStates;

namespace Content.Shared._Mini.TypanWar;

/// <summary>
/// Marks entities inside an active capture zone protection margin as indestructible during war.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class TypanWarCaptureZoneProtectedComponent : Component;
