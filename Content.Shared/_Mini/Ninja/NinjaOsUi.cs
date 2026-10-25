// SPDX-FileCopyrightText: 2026 Mini Station
// Мини-станция, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/ministation/mini-station-goob/master/LICENSE.TXT

using Content.Shared.Actions;
using Robust.Shared.Serialization;

namespace Content.Shared._Mini.Ninja;

[Serializable, NetSerializable]
public enum NinjaOsUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public sealed class NinjaOsBoundUserInterfaceState : BoundUserInterfaceState
{
    public string Os = "NinjaOs";
    public Dictionary<int, NinjaPath> SelectedModules = new();
    public HashSet<int> LockedTiers = new();
    public HashSet<int> ActivatedTiers = new();
    public bool SuitActivated;
    public string ActivationBoot = "NinjaOsActivationBoot";
    public string DeactivationBoot = "NinjaOsDeactivationBoot";

    public NinjaOsBoundUserInterfaceState()
    {
    }

    public NinjaOsBoundUserInterfaceState(
        string os,
        Dictionary<int, NinjaPath> selectedModules,
        HashSet<int> lockedTiers,
        HashSet<int> activatedTiers,
        bool suitActivated,
        string activationBoot,
        string deactivationBoot)
    {
        Os = os;
        SelectedModules = selectedModules;
        LockedTiers = lockedTiers;
        ActivatedTiers = activatedTiers;
        SuitActivated = suitActivated;
        ActivationBoot = activationBoot;
        DeactivationBoot = deactivationBoot;
    }
}

[Serializable, NetSerializable]
public sealed class NinjaOsSelectModuleMessage : BoundUserInterfaceMessage
{
    public int Tier;
    public NinjaPath Path;

    public NinjaOsSelectModuleMessage()
    {
    }

    public NinjaOsSelectModuleMessage(int tier, NinjaPath path)
    {
        Tier = tier;
        Path = path;
    }
}

[Serializable, NetSerializable]
public sealed class NinjaOsSetSuitPowerMessage : BoundUserInterfaceMessage
{
    public bool Activated;

    public NinjaOsSetSuitPowerMessage()
    {
    }

    public NinjaOsSetSuitPowerMessage(bool activated)
    {
        Activated = activated;
    }
}

[Serializable, NetSerializable]
public sealed class NinjaOsResultMessage : BoundUserInterfaceMessage
{
    public bool Success;
    public string FailReason = string.Empty;

    public NinjaOsResultMessage()
    {
    }

    public NinjaOsResultMessage(bool success, string failReason = "")
    {
        Success = success;
        FailReason = failReason;
    }
}

public sealed partial class OpenNinjaOsEvent : InstantActionEvent;
