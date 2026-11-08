// SPDX-FileCopyrightText: 2026 Mini Station
// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

using Content.Shared.Actions;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Mini.Ninja.Base.Visor;

/// <summary>
/// Ninja visor module: cycles between flash protection, thermal vision and night vision.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class NinjaVisionModuleComponent : Component
{
    [DataField, AutoNetworkedField]
    public NinjaVisorMode Mode = NinjaVisorMode.Protective;

    [DataField]
    public EntProtoId CycleAction = "ActionCycleNinjaVisor";

    [DataField, AutoNetworkedField]
    public EntityUid? CycleActionEntity;

    [DataField]
    public SoundSpecifier? CycleSound = new SoundPathSpecifier("/Audio/_White/Items/Goggles/activate.ogg");
}

public enum NinjaVisorMode : byte
{
    Protective,
    Thermal,
    NightVision,
}

public sealed partial class NinjaCycleVisorEvent : InstantActionEvent;
