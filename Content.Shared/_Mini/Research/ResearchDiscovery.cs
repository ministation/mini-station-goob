// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

using Robust.Shared.Serialization;

namespace Content.Shared._Mini.Research;

[Serializable, NetSerializable]
public enum ResearchDiscoveryEventType : byte
{
    ScanEntity,
    MachineInsertion,
    DeconstructEntity,
    ServerTrigger,
}

[Serializable, NetSerializable]
public enum ResearchTechnologyVisibilityState : byte
{
    Hidden,
    RevealedLocked,
    Available,
    Researched,
}
