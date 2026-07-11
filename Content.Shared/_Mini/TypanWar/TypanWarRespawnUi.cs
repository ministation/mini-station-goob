using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Serialization;

namespace Content.Shared._Mini.TypanWar;

[Serializable, NetSerializable]
public enum TypanWarRespawnUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public sealed class TypanWarRespawnOption
{
    public string Label = "";
    public string Description = "";
    public int Index;
}

[Serializable, NetSerializable]
public sealed class TypanWarRespawnBoundUserInterfaceState : BoundUserInterfaceState
{
    public float SecondsRemaining;
    public bool CanRespawn;
    public TypanWarRespawnOption[] Options = [];

    public TypanWarRespawnBoundUserInterfaceState(float secondsRemaining, bool canRespawn, TypanWarRespawnOption[] options)
    {
        SecondsRemaining = secondsRemaining;
        CanRespawn = canRespawn;
        Options = options;
    }
}

[Serializable, NetSerializable]
public sealed class TypanWarRespawnRequestMessage : BoundUserInterfaceMessage
{
    public int OptionIndex;

    public TypanWarRespawnRequestMessage(int optionIndex)
    {
        OptionIndex = optionIndex;
    }
}
