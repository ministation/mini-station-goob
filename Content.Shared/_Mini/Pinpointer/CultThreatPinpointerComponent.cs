// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._Mini.Pinpointer;

/// <summary>
/// ERT chaplain pinpointer: cycle threat modes or auto-pick by priority
/// (cosmic monument → Ratvar portal → Nar'Sie rending → heretic/devil).
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CultThreatPinpointerComponent : Component
{
    [DataField, AutoNetworkedField]
    public CultThreatPinpointerMode Mode = CultThreatPinpointerMode.Auto;
}

[Serializable, NetSerializable]
public enum CultThreatPinpointerMode : byte
{
    Auto = 0,
    CosmicMonument = 1,
    RatvarPortal = 2,
    NarSieRending = 3,
    Heretic = 4,
    Devil = 5,
}
