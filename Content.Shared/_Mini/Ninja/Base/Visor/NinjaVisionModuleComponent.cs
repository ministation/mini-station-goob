// SPDX-FileCopyrightText: 2026 Mini Station
// Мини-станция, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/ministation/mini-station-goob/master/LICENSE.TXT

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
