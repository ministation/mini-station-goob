// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._CorvaxNext.Silicons.Borgs.Components;
using Content.Shared.Actions;
using Content.Shared.Mind;
using Content.Shared.Silicons.Borgs.Components;
using Content.Shared.Silicons.StationAi;
using Content.Shared.StationAi;
using Robust.Shared.GameObjects;
using Robust.Shared.Serialization;

namespace Content.Shared._CorvaxNext.Silicons.Borgs;

public abstract class SharedAiRemoteControlSystem : EntitySystem
{
    [Dependency] private readonly SharedStationAiSystem _stationAiSystem = default!;
    [Dependency] private readonly SharedTransformSystem _xformSystem = default!;
    [Dependency] private readonly SharedMindSystem _mind = default!;

    public override void Initialize()
    {
        base.Initialize();
    }

    public void ReturnMindIntoAi(EntityUid entity)
    {
        if (!TryComp<AiRemoteControllerComponent>(entity, out var remoteComp))
            return;

        if (remoteComp?.AiHolder == null
            || !_stationAiSystem.TryGetCore(remoteComp.AiHolder.Value, out var stationAiCore)
            || stationAiCore.Comp?.RemoteEntity == null)
            return;

        if (remoteComp.LinkedMind == null)
            return;

        if (!TryComp<StationAiHeldComponent>(remoteComp.AiHolder.Value, out var stationAiHeldComp))
            return;

        stationAiHeldComp.CurrentConnectedEntity = null;

        _mind.TransferTo(remoteComp.LinkedMind.Value, remoteComp.AiHolder);

        _stationAiSystem.SwitchRemoteEntityMode(stationAiCore, true);
        remoteComp.AiHolder = null;
        remoteComp.LinkedMind = null;

        _xformSystem.SetCoordinates(stationAiCore.Comp.RemoteEntity.Value, Transform(entity).Coordinates);

        // Mini: если ИИ вошёл без платы B.O.R.I.S., убираем временную связь при выходе:
        // корпус снова обычный борг (можно эмагнуть, он не висит в списке устройств).
        RemoveTemporaryController(entity);
    }

    private void RemoveTemporaryController(EntityUid entity)
    {
        if (!TryComp<AiRemoteControllerComponent>(entity, out var remoteComp))
            return;

        // Убираем только «живой» компонент: если он уже выключается (снос корпуса), не трогаем.
        if (remoteComp.LifeStage != ComponentLifeStage.Running)
            return;

        if (remoteComp.AiHolder != null)
            return; // ИИ всё ещё внутри

        // Настоящая плата B.O.R.I.S. внутри корпуса: постоянная связь остаётся.
        if (TryComp<BorgChassisComponent>(entity, out var chassis)
            && chassis.BrainEntity is { } brain
            && HasComp<AiRemoteBrainComponent>(brain))
        {
            return;
        }

        RemComp<AiRemoteControllerComponent>(entity);
        RemComp<StationAiVisionComponent>(entity);
    }
}

public sealed partial class ReturnMindIntoAiEvent : InstantActionEvent
{
}

public sealed partial class ToggleRemoteDevicesScreenEvent : InstantActionEvent
{
}

[Serializable, NetSerializable]
public enum RemoteDeviceUiKey : byte
{
    Key
}
