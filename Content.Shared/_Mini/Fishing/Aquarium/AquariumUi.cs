// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from tgstation/tgstation (code/modules/fishing, AGPL-3.0-or-later).

using Robust.Shared.Serialization;

namespace Content.Shared._Mini.Fishing.Aquarium;

[Serializable, NetSerializable]
public enum AquariumUiKey : byte
{
    Key,
}

/// <summary>
/// One fish as shown in the aquarium window.
/// </summary>
[Serializable, NetSerializable]
public sealed class AquariumFishData(
    NetEntity entity,
    string name,
    string prototype,
    bool alive,
    float hunger,
    int happiness,
    int beauty,
    float size,
    float weight)
{
    public readonly NetEntity Entity = entity;

    public readonly string Name = name;

    /// <summary>
    /// Entity prototype id, used by the client to draw the fish icon.
    /// </summary>
    public readonly string Prototype = prototype;

    public readonly bool Alive = alive;

    /// <summary>
    /// Zero when fed, one when starving, above one when the fish has been starving for a while.
    /// </summary>
    public readonly float Hunger = hunger;

    public readonly int Happiness = happiness;

    public readonly int Beauty = beauty;

    public readonly float Size = size;

    public readonly float Weight = weight;
}

/// <summary>
/// One prop as shown in the aquarium window.
/// </summary>
[Serializable, NetSerializable]
public sealed class AquariumPropData(NetEntity entity, string name, string prototype, int beauty)
{
    public readonly NetEntity Entity = entity;

    public readonly string Name = name;

    public readonly string Prototype = prototype;

    public readonly int Beauty = beauty;
}

[Serializable, NetSerializable]
public sealed class AquariumBoundUserInterfaceState(
    AquariumFluid fluid,
    float temperature,
    float minTemperature,
    float maxTemperature,
    AquariumMode mode,
    int beauty,
    bool glassBroken,
    AquariumUpgradeFlags upgrades,
    int maxFish,
    AquariumFishData[] fish,
    AquariumPropData[] props)
    : BoundUserInterfaceState
{
    public readonly AquariumFluid Fluid = fluid;

    public readonly float Temperature = temperature;

    public readonly float MinTemperature = minTemperature;

    public readonly float MaxTemperature = maxTemperature;

    public readonly AquariumMode Mode = mode;

    public readonly int Beauty = beauty;

    public readonly bool GlassBroken = glassBroken;

    public readonly AquariumUpgradeFlags Upgrades = upgrades;

    public readonly int MaxFish = maxFish;

    public readonly AquariumFishData[] Fish = fish;

    public readonly AquariumPropData[] Props = props;
}

[Serializable, NetSerializable]
public sealed class AquariumSetTemperatureMessage(float temperature) : BoundUserInterfaceMessage
{
    public readonly float Temperature = temperature;
}

[Serializable, NetSerializable]
public sealed class AquariumSetFluidMessage(AquariumFluid fluid) : BoundUserInterfaceMessage
{
    public readonly AquariumFluid Fluid = fluid;
}

[Serializable, NetSerializable]
public sealed class AquariumSetModeMessage(AquariumMode mode) : BoundUserInterfaceMessage
{
    public readonly AquariumMode Mode = mode;
}

[Serializable, NetSerializable]
public sealed class AquariumRemoveMessage(NetEntity entity) : BoundUserInterfaceMessage
{
    public readonly NetEntity Entity = entity;
}

[Serializable, NetSerializable]
public sealed class AquariumFeedMessage : BoundUserInterfaceMessage;
