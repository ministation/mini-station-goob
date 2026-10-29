// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from tgstation/tgstation (code/modules/fishing, AGPL-3.0-or-later).

using Content.Shared.Atmos;
using Robust.Shared.GameStates;

namespace Content.Goobstation.Shared.Fishing.Aquarium;

/// <summary>
/// An aquarium. Fish and props are kept inside plain containers so they never get physics,
/// while the aquarium itself keeps the fluid, the temperature and the tank modes.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class AquariumComponent : Component
{
    public const string FishContainerId = "fish";
    public const string PropContainerId = "props";

    /// <summary>
    /// Fluid currently filling the tank. Fish whose required fluid does not match slowly die.
    /// </summary>
    [DataField, AutoNetworkedField]
    public AquariumFluid Fluid = AquariumFluid.Freshwater;

    /// <summary>
    /// Fluid temperature, in kelvin.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float Temperature = Atmospherics.T0C + 24f;

    [DataField, AutoNetworkedField]
    public AquariumMode Mode = AquariumMode.Assisted;

    /// <summary>
    /// Beauty of the aquarium with the current fish and props, recalculated by the server.
    /// </summary>
    [DataField, AutoNetworkedField]
    public int Beauty;

    [DataField, AutoNetworkedField]
    public bool GlassBroken;

    [DataField, AutoNetworkedField]
    public AquariumUpgradeFlags Upgrades;

    /// <summary>
    /// Beauty of the empty tank, before fish and props.
    /// </summary>
    [DataField]
    public int DefaultBeauty = 150;

    /// <summary>
    /// How many fish fit in the tank. The bluespace upgrade raises it.
    /// </summary>
    [DataField]
    public int MaxFish = 20;

    [DataField]
    public float MinTemperature = Atmospherics.T0C;

    [DataField]
    public float MaxTemperature = Atmospherics.T0C + 100f;

    /// <summary>
    /// Temperature the tank drifts back to when it is not held by the player in manual mode.
    /// </summary>
    [DataField]
    public float RoomTemperature = Atmospherics.T0C + 20f;

    /// <summary>
    /// How fast the temperature drifts towards the target, in kelvin per second.
    /// </summary>
    [DataField]
    public float TemperatureDrift = 0.1f;

    /// <summary>
    /// Accumulated damage at which the glass shatters and everything inside spills out.
    /// </summary>
    [DataField]
    public float GlassBreakDamage = 60f;
}
