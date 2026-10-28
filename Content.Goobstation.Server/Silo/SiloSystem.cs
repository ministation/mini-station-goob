// SPDX-FileCopyrightText: 2024 Aviu00 <93730715+Aviu00@users.noreply.github.com>
// SPDX-FileCopyrightText: 2024 Piras314 <p1r4s@proton.me>
// SPDX-FileCopyrightText: 2025 Aiden <28298836+Aidenkrz@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 Misandry <mary@thughunt.ing>
// SPDX-FileCopyrightText: 2025 gus <august.eymann@gmail.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using Content.Goobstation.Common.Silo;
using Content.Server._Mini.Networking;
using Content.Server.Lathe;
using Content.Server.Station.Components;
using Content.Shared._Goobstation.Silo;
using Content.Shared.DeviceLinking;
using Content.Shared.Lathe;
using Content.Shared.Materials;
using Robust.Server.GameStates;
using Robust.Server.Player;
using Timer = Robust.Shared.Timing.Timer;

namespace Content.Goobstation.Server.Silo;

public sealed partial class SiloSystem : SharedSiloSystem
{
    [Dependency] private LatheSystem _lathe = default!;
    [Dependency] private PvsOverrideSystem _pvs = default!;
    [Dependency] private PvsSessionOverrideSystem _pvsSession = default!;
    [Dependency] private IPlayerManager _player = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BecomesStationComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<SiloComponent, MapInitEvent>(OnSiloInit);
        SubscribeLocalEvent<SiloUtilizerComponent, MapInitEvent>(OnUtilizerInit);
        SubscribeLocalEvent<SiloComponent, MaterialAmountChangedEvent>(OnMaterialAmountChanged);
        SubscribeLocalEvent<SiloComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<SiloComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnStartup(Entity<SiloComponent> ent, ref ComponentStartup args)
    {
        _pvsSession.RefreshSiloOverrides();
    }

    private void OnShutdown(Entity<SiloComponent> ent, ref ComponentShutdown args)
    {
        foreach (var session in _player.Sessions)
        {
            _pvs.RemoveSessionOverride(ent, session);
        }

        // Сило разобрали или взорвали: ссылки лат на него становятся мусором, из-за чего
        // фабы перестают видеть общие материалы, а каждый пакет состояния сыпет ошибкой
        // "Can't resolve MetaDataComponent". Чистим ссылки, пока сущность ещё жива.
        ClearUtilizerLinks(ent.Owner);
    }

    /// <summary>
    /// Свежепостроенное (или перенесённое) сило подключает к себе латы на своём гриде,
    /// у которых ссылки ещё нет. Иначе после разборки старого сило новый никто не видит.
    /// </summary>
    private void OnSiloInit(Entity<SiloComponent> ent, ref MapInitEvent args)
    {
        if (!TryComp(ent, out TransformComponent? xform) || xform.GridUid is not { } grid)
            return;

        var utilizers = AllEntityQuery<SiloUtilizerComponent, TransformComponent>();
        while (utilizers.MoveNext(out var utilizerUid, out var utilizer, out var utilizerXform))
        {
            if (utilizerXform.GridUid != grid)
                continue;

            if (utilizer.Silo != null && !TerminatingOrDeleted(utilizer.Silo.Value))
                continue;

            utilizer.Silo = ent.Owner;
            Dirty(utilizerUid, utilizer);
        }
    }

    /// <summary>
    /// Новый фаб на станции сразу подхватывает сило с того же грида.
    /// </summary>
    private void OnUtilizerInit(Entity<SiloUtilizerComponent> ent, ref MapInitEvent args)
    {
        if (ent.Comp.Silo != null && !TerminatingOrDeleted(ent.Comp.Silo.Value))
            return;

        if (!TryComp(ent, out TransformComponent? xform) || xform.GridUid is not { } grid)
            return;

        var silos = AllEntityQuery<SiloComponent, TransformComponent>();
        while (silos.MoveNext(out var siloUid, out _, out var siloXform))
        {
            if (siloXform.GridUid != grid)
                continue;

            ent.Comp.Silo = siloUid;
            Dirty(ent.Owner, ent.Comp);
            return;
        }
    }

    private void ClearUtilizerLinks(EntityUid silo)
    {
        var utilizers = AllEntityQuery<SiloUtilizerComponent>();
        while (utilizers.MoveNext(out var utilizerUid, out var utilizer))
        {
            if (utilizer.Silo != silo)
                continue;

            utilizer.Silo = null;
            Dirty(utilizerUid, utilizer);
        }
    }

    private void OnMaterialAmountChanged(Entity<SiloComponent> ent, ref MaterialAmountChangedEvent args)
    {
        // Spawning a timer because SetUiState in UpdateUserInterfaceState is being networked before
        // silo's MaterialStorageComponent state gets handled.
        // That causes lathe ui recipe list to not update properly.
        Timer.Spawn(20,
            () =>
            {
                if (!TryComp(ent, out DeviceLinkSourceComponent? source))
                    return;

                foreach (var utilizerSet in source.Outputs.Where(x => x.Key == SourcePort).Select(x => x.Value))
                {
                    foreach (var utilizer in utilizerSet)
                    {
                        if (TryComp(utilizer, out LatheComponent? lathe))
                            _lathe.UpdateUserInterfaceState(utilizer, lathe);
                    }
                }
            });
    }

    private void OnMapInit(Entity<BecomesStationComponent> ent, ref MapInitEvent args)
    {
        var siloQuery =
            AllEntityQuery<SiloComponent, MaterialStorageComponent, TransformComponent, DeviceLinkSourceComponent>();

        Entity<DeviceLinkSourceComponent>? silo = null;

        while (siloQuery.MoveNext(out var siloEnt, out _, out _, out var siloXform, out var source))
        {
            if (siloXform.GridUid != ent)
                continue;

            silo = (siloEnt, source);
            break;
        }

        if (silo == null)
            return;

        var utilizerQuery = AllEntityQuery<SiloUtilizerComponent, MaterialStorageComponent, TransformComponent,
            DeviceLinkSinkComponent>();
        while (utilizerQuery.MoveNext(out var utilizer, out _, out var storage, out var utilizerXform, out var sink))
        {
            if (!storage.ConnectToSilo)
                continue;

            if (utilizerXform.GridUid != ent)
                continue;

            DeviceLink.LinkDefaults(null, silo.Value, utilizer, silo.Value.Comp, sink);
        }
    }
}
