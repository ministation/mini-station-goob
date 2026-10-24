// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Radio.Components;
using Content.Server.Silicons.Laws;
using Content.Shared._CorvaxNext.Silicons.Borgs;
using Content.Shared._CorvaxNext.Silicons.Borgs.Components;
using Content.Shared._Imp.Drone;
using Content.Shared.Actions;
using Content.Shared.Mind;
using Content.Shared.Silicons.Borgs.Components;
using Content.Shared.Silicons.Laws.Components;
using Content.Shared.Silicons.StationAi;
using Content.Shared.StationAi;
using Content.Shared.Verbs;
using Robust.Server.GameObjects;
using Robust.Shared.Player;

namespace Content.Server._CorvaxNext.Silicons.Borgs;

public sealed class AiRemoteControlSystem : SharedAiRemoteControlSystem
{
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly SiliconLawSystem _lawSystem = default!;
    [Dependency] private readonly SharedStationAiSystem _stationAiSystem = default!;
    [Dependency] private readonly SharedMindSystem _mind = default!;
    [Dependency] private readonly UserInterfaceSystem _userInterface = default!;
    [Dependency] private readonly SharedTransformSystem _xformSystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AiRemoteControllerComponent, ReturnMindIntoAiEvent>(OnReturnMindIntoAi);
        SubscribeLocalEvent<AiRemoteControllerComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<AiRemoteControllerComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<AiRemoteControllerComponent, GetVerbsEvent<AlternativeVerb>>(OnGetVerbs);
        SubscribeLocalEvent<StationAiHeldComponent, AiRemoteControllerComponent.RemoteDeviceActionMessage>(OnUiRemoteAction);
        SubscribeLocalEvent<StationAiHeldComponent, ToggleRemoteDevicesScreenEvent>(OnToggleRemoteDevicesScreen);

        // Mini: ИИ управляет и «голыми» кремниями: пустой корпус борга и дрон доступны без платы B.O.R.I.S.
        SubscribeLocalEvent<BorgChassisComponent, GetVerbsEvent<AlternativeVerb>>(OnBorgGetVerbs);
        SubscribeLocalEvent<DroneComponent, GetVerbsEvent<AlternativeVerb>>(OnDroneGetVerbs);
    }

    private void OnMapInit(Entity<AiRemoteControllerComponent> entity, ref MapInitEvent args)
    {
        SetupController(entity);
    }

    /// <summary>
    /// Mini: выдаёт дрону/боргу «зрение ИИ» и действие «Вернуться в ИИ».
    /// Нужен и для плат B.O.R.I.S.: компонент добавляется в рантайме, MapInit на корпус уже не придёт.
    /// </summary>
    private void SetupController(Entity<AiRemoteControllerComponent> entity)
    {
        EnsureComp<StationAiVisionComponent>(entity.Owner);

        if (entity.Comp.BackToAiActionEntity != null)
            return;

        EntityUid? actionEnt = null;

        _actions.AddAction(entity.Owner, ref actionEnt, entity.Comp.BackToAiAction);

        if (actionEnt != null)
            entity.Comp.BackToAiActionEntity = actionEnt.Value;
    }

    private void OnShutdown(Entity<AiRemoteControllerComponent> entity, ref ComponentShutdown args)
    {
        _actions.RemoveAction(entity.Owner, entity.Comp.BackToAiActionEntity);

        var backArgs = new ReturnMindIntoAiEvent();
        backArgs.Performer = entity;

        if (TryComp(entity, out IntrinsicRadioTransmitterComponent? transmitter)
            && entity.Comp.PreviouslyTransmitterChannels != null)
            transmitter.Channels = [.. entity.Comp.PreviouslyTransmitterChannels];

        if (TryComp(entity, out ActiveRadioComponent? activeRadio)
            && entity.Comp.PreviouslyActiveRadioChannels != null)
            activeRadio.Channels = [.. entity.Comp.PreviouslyActiveRadioChannels];

        ReturnMindIntoAi(entity);
    }

    private void OnGetVerbs(Entity<AiRemoteControllerComponent> entity, ref GetVerbsEvent<AlternativeVerb> args)
    {
        var user = args.User;

        if (!TryComp<StationAiHeldComponent>(user, out var stationAiHeldComp))
            return;

        var verb = new AlternativeVerb
        {
            Text = Loc.GetString("ai-remote-control"),
            Act = () => AiTakeControl(user, entity)
        };
        args.Verbs.Add(verb);
    }

    // Mini: глагол «Контролировать» на пустых корпусах боргов и дронах (плата B.O.R.I.S. не нужна).
    private void OnBorgGetVerbs(Entity<BorgChassisComponent> entity, ref GetVerbsEvent<AlternativeVerb> args) =>
        OnSiliconGetVerbs(entity.Owner, ref args);

    private void OnDroneGetVerbs(Entity<DroneComponent> entity, ref GetVerbsEvent<AlternativeVerb> args) =>
        OnSiliconGetVerbs(entity.Owner, ref args);

    private void OnSiliconGetVerbs(EntityUid target, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (HasComp<AiRemoteControllerComponent>(target))
            return; // уже есть обычный глагол у устройств с платой

        var user = args.User;

        if (!TryComp<StationAiHeldComponent>(user, out _))
            return;

        if (_mind.TryGetMind(target, out _, out _))
            return;

        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("ai-remote-control"),
            Act = () => AiTakeControl(user, target)
        });
    }

    private void OnReturnMindIntoAi(Entity<AiRemoteControllerComponent> entity, ref ReturnMindIntoAiEvent args) =>
        ReturnMindIntoAi(entity);

    public void AiTakeControl(EntityUid ai, EntityUid entity)
    {
        if (!_mind.TryGetMind(ai, out var mindId, out var mind))
            return;

        if (_mind.TryGetMind(entity, out _, out _)) // Don't take control of device with mind already. Would ghost them
            return;

        if (!TryComp<StationAiHeldComponent>(ai, out var stationAiHeldComp))
            return;

        var isBorg = TryComp<BorgChassisComponent>(entity, out _);
        var isDrone = HasComp<DroneComponent>(entity);

        // Mini: пустой корпус борга или дрон доступны ИИ и без платы B.O.R.I.S.
        if (!TryComp<AiRemoteControllerComponent>(entity, out var aiRemoteComp))
        {
            if (!isBorg && !isDrone)
                return;

            aiRemoteComp = EnsureComp<AiRemoteControllerComponent>(entity);
            SetupController((entity, aiRemoteComp));
        }

        if (TryComp(entity, out IntrinsicRadioTransmitterComponent? transmitter))
        {
            aiRemoteComp.PreviouslyTransmitterChannels = [.. transmitter.Channels];

            if (TryComp(ai, out IntrinsicRadioTransmitterComponent? stationAiTransmitter))
                transmitter.Channels = [.. stationAiTransmitter.Channels];
        }

        if (TryComp(entity, out ActiveRadioComponent? activeRadio))
        {
            aiRemoteComp.PreviouslyActiveRadioChannels = [.. activeRadio.Channels];

            if (TryComp(ai, out ActiveRadioComponent? stationAiActiveRadio))
                activeRadio.Channels = [.. stationAiActiveRadio.Channels];
        }

        _mind.ControlMob(ai, entity);
        aiRemoteComp.AiHolder = ai;
        aiRemoteComp.LinkedMind = mindId;

        stationAiHeldComp.CurrentConnectedEntity = entity;

        if (!_stationAiSystem.TryGetCore(ai, out var stationAiCore))
            return;

        _stationAiSystem.SwitchRemoteEntityMode(stationAiCore, false);

        // Дрон сохраняет свои законы и ограничения дрона, ИИ в корпусе борга живёт по своим законам.
        if (!isDrone)
            RewriteLaws(ai, entity);
    }

    private void OnToggleRemoteDevicesScreen(EntityUid uid, StationAiHeldComponent component, ToggleRemoteDevicesScreenEvent args)
    {
        if (args.Handled || !TryComp<ActorComponent>(uid, out var actor))
            return;
        args.Handled = true;

        _userInterface.TryToggleUi(uid, RemoteDeviceUiKey.Key, actor.PlayerSession);

        var remoteDevices = new List<RemoteDevicesData>();
        var seen = new HashSet<NetEntity>();

        var query = EntityManager.EntityQueryEnumerator<AiRemoteControllerComponent>();
        while (query.MoveNext(out var queryUid, out _))
        {
            AddRemoteDevice(remoteDevices, seen, queryUid);
        }

        // Mini: в списке устройств ИИ видны и свободные кремнии без платы B.O.R.I.S.:
        // пустые корпуса боргов (в роботике, на карте) и незанятые дроны.
        var borgQuery = EntityManager.EntityQueryEnumerator<BorgChassisComponent>();
        while (borgQuery.MoveNext(out var borgUid, out _))
        {
            if (!_mind.TryGetMind(borgUid, out _, out _))
                AddRemoteDevice(remoteDevices, seen, borgUid);
        }

        var droneQuery = EntityManager.EntityQueryEnumerator<DroneComponent>();
        while (droneQuery.MoveNext(out var droneUid, out _))
        {
            if (!_mind.TryGetMind(droneUid, out _, out _))
                AddRemoteDevice(remoteDevices, seen, droneUid);
        }

        var state = new RemoteDevicesBuiState(remoteDevices);
        _userInterface.SetUiState(uid, RemoteDeviceUiKey.Key, state);
    }

    private void AddRemoteDevice(List<RemoteDevicesData> devices, HashSet<NetEntity> seen, EntityUid uid)
    {
        if (Terminating(uid) || Deleted(uid))
            return;

        var netEntity = GetNetEntity(uid);
        if (!seen.Add(netEntity))
            return;

        devices.Add(new RemoteDevicesData
        {
            NetEntityUid = netEntity,
            DisplayName = Comp<MetaDataComponent>(uid).EntityName
        });
    }

    private void OnUiRemoteAction(EntityUid uid, StationAiHeldComponent component, AiRemoteControllerComponent.RemoteDeviceActionMessage msg)
    {
        if (msg.RemoteAction == null)
            return;

        var target = GetEntity(msg.RemoteAction?.Target);

        // Mini: кроме плат B.O.R.I.S. ИИ может взять под контроль пустой корпус борга или дрон.
        if (!HasComp<AiRemoteControllerComponent>(target)
            && !HasComp<BorgChassisComponent>(target)
            && !HasComp<DroneComponent>(target))
            return;

        switch (msg.RemoteAction?.ActionType)
        {
            case RemoteDeviceActionEvent.RemoteDeviceActionType.MoveToDevice:
                if (!_stationAiSystem.TryGetCore(uid, out var stationAiCore)
                    || stationAiCore.Comp?.RemoteEntity == null)
                    return;
                _xformSystem.SetCoordinates(stationAiCore.Comp.RemoteEntity.Value, Transform(target.Value).Coordinates);
                break;

            case RemoteDeviceActionEvent.RemoteDeviceActionType.TakeControl:
                AiTakeControl(uid, target.Value);
                break;
        }
    }

    private void RewriteLaws(EntityUid from, EntityUid to)
    {
        if (!TryComp<SiliconLawProviderComponent>(from, out var fromLawsComp))
            return;

        if (!TryComp<SiliconLawProviderComponent>(to, out var toLawsComp))
            return;

        if (fromLawsComp.Lawset == null)
            return;

        var fromLaws = _lawSystem.GetLaws(from);
        _lawSystem.SetLawsSilent(fromLaws.Laws, to);
    }
}
