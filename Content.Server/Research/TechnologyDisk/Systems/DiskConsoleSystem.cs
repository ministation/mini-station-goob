// SPDX-FileCopyrightText: 2023 Nemanja <98561806+EmoGarbage404@users.noreply.github.com>
// SPDX-FileCopyrightText: 2023 Pieter-Jan Briers <pieterjan.briers@gmail.com>
// SPDX-FileCopyrightText: 2024 Ed <96445749+TheShuEd@users.noreply.github.com>
// SPDX-FileCopyrightText: 2024 metalgearsloth <31366439+metalgearsloth@users.noreply.github.com>
// SPDX-FileCopyrightText: 2024 metalgearsloth <comedian_vs_clown@hotmail.com>
// SPDX-FileCopyrightText: 2025 Aiden <28298836+Aidenkrz@users.noreply.github.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using System;
using System.Collections.Generic;
using Content.Server.Power.EntitySystems;
using Content.Server.Research.Systems;
using Content.Server.Research.TechnologyDisk.Components;
using Content.Server.Stack;
using Content.Shared._Mini.Converter;
using Content.Shared.Power;
using Content.Shared.Research;
using Content.Shared.Research.Components;
using Content.Shared.Research.TechnologyDisk.Components;
using Content.Shared.UserInterface;
using Robust.Server.Audio;
using Robust.Server.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server.Research.TechnologyDisk.Systems;

public sealed class DiskConsoleSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly AudioSystem _audio = default!;
    [Dependency] private readonly ResearchSystem _research = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly StackSystem _stack = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;

    /// <inheritdoc/>
    public override void Initialize()
    {
        SubscribeLocalEvent<DiskConsoleComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<DiskConsoleComponent, DiskConsolePrintDiskMessage>(OnPrintDisk);
        SubscribeLocalEvent<DiskConsoleComponent, DiskConsoleSetAutoPrintMessage>(OnSetAutoPrint);
        SubscribeLocalEvent<DiskConsoleComponent, DiskConsoleSetAutoFeedAdjacentConverterMessage>(OnSetAutoFeedAdjacentConverter);
        SubscribeLocalEvent<DiskConsoleComponent, ResearchServerPointsChangedEvent>(OnPointsChanged);
        SubscribeLocalEvent<DiskConsoleComponent, ResearchRegistrationChangedEvent>(OnRegistrationChanged);
        SubscribeLocalEvent<DiskConsoleComponent, PowerChangedEvent>(OnPowerChanged);
        SubscribeLocalEvent<DiskConsoleComponent, BeforeActivatableUIOpenEvent>(OnBeforeUiOpen);

        SubscribeLocalEvent<DiskConsolePrintingComponent, ComponentShutdown>(OnShutdown);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var restartPrint = new List<EntityUid>();

        var query = EntityQueryEnumerator<DiskConsolePrintingComponent, DiskConsoleComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var printing, out var console, out var xform))
        {
            if (printing.FinishTime > _timing.CurTime || !this.IsPowered(uid, EntityManager))
                continue;

            RemComp(uid, printing);

            if (!TryFeedAdjacentConverter(uid, console, xform))
                Spawn(console.DiskPrototype, xform.Coordinates);

            if (console.AutoPrint)
                restartPrint.Add(uid);
        }

        foreach (var uid in restartPrint)
        {
            if (!TryComp<DiskConsoleComponent>(uid, out var console))
                continue;

            TryStartPrinting(uid, console);
            UpdateUserInterface(uid, console);
        }
    }

    private void OnStartup(EntityUid uid, DiskConsoleComponent component, ComponentStartup args)
    {
        TryStartAutoPrint(uid, component);
        UpdateUserInterface(uid, component);
    }

    private void OnPrintDisk(EntityUid uid, DiskConsoleComponent component, DiskConsolePrintDiskMessage args)
    {
        if (!TryStartPrinting(uid, component))
            return;

        UpdateUserInterface(uid, component);
    }

    private void OnSetAutoPrint(EntityUid uid, DiskConsoleComponent component, DiskConsoleSetAutoPrintMessage args)
    {
        if (component.AutoPrint == args.AutoPrint)
            return;

        component.AutoPrint = args.AutoPrint;
        TryStartAutoPrint(uid, component);
        UpdateUserInterface(uid, component);
    }

    private void OnSetAutoFeedAdjacentConverter(EntityUid uid, DiskConsoleComponent component, DiskConsoleSetAutoFeedAdjacentConverterMessage args)
    {
        if (component.AutoFeedAdjacentConverter == args.AutoFeedAdjacentConverter)
            return;

        component.AutoFeedAdjacentConverter = args.AutoFeedAdjacentConverter;
        UpdateUserInterface(uid, component);
    }

    private bool TryStartPrinting(EntityUid uid, DiskConsoleComponent component)
    {
        if (HasComp<DiskConsolePrintingComponent>(uid))
            return false;

        if (!this.IsPowered(uid, EntityManager))
            return false;

        if (!_research.TryGetClientServer(uid, out var server, out var serverComp))
            return false;

        if (serverComp.Points < component.PricePerDisk)
            return false;

        var printing = EnsureComp<DiskConsolePrintingComponent>(uid);
        printing.FinishTime = _timing.CurTime + component.PrintDuration;

        _research.ModifyServerPoints(server.Value, -component.PricePerDisk, serverComp);
        _audio.PlayPvs(component.PrintSound, uid);
        return true;
    }

    private void TryStartAutoPrint(EntityUid uid, DiskConsoleComponent component)
    {
        if (!component.AutoPrint)
            return;

        TryStartPrinting(uid, component);
    }

    private bool TryFeedAdjacentConverter(EntityUid uid, DiskConsoleComponent console, TransformComponent xform)
    {
        if (!console.AutoFeedAdjacentConverter)
            return false;

        if (!_prototype.TryIndex<EntityPrototype>(console.DiskPrototype, out var proto))
            return false;

        if (!proto.TryGetComponent<TechnologyDiskComponent>(out var diskComp, EntityManager.ComponentFactory))
            return false;

        if (!TryFindNearestConverter(xform, console.AdjacentConverterRange, out var converterUid, out var converter))
            return false;

        if (converter.PointsPerTelecrystal <= 0)
            return false;

        if (!this.IsPowered(converterUid, EntityManager))
            return false;

        var value = diskComp.TierWeightPrototype == "RareTechDiskTierWeights"
            ? converter.RareTechnologyDiskPoints
            : converter.TechnologyDiskPoints;

        converter.StoredPoints += Math.Max(value, 0);

        var payout = 0;
        if (converter.PointsPerTelecrystal > 0)
        {
            payout = converter.StoredPoints / converter.PointsPerTelecrystal;
            converter.StoredPoints %= converter.PointsPerTelecrystal;
        }

        if (payout <= 0)
            return true;

        var coords = Transform(converterUid).Coordinates;
        var telecrystalStack = Spawn("Telecrystal1", coords);
        _stack.SetCount(telecrystalStack, payout);
        _stack.TryMergeToContacts(telecrystalStack);

        return true;
    }

    private bool TryFindNearestConverter(TransformComponent sourceXform, float range, out EntityUid converterUid, out ConverterComponent converter)
    {
        converterUid = default;
        converter = default!;

        var maxDistanceSquared = range * range;
        var bestDistanceSquared = float.MaxValue;

        var query = EntityQueryEnumerator<ConverterComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var converterComp, out var converterXform))
        {
            if (converterXform.ParentUid != sourceXform.ParentUid)
                continue;

            var distanceSquared = (converterXform.Coordinates.Position - sourceXform.Coordinates.Position).LengthSquared();
            if (distanceSquared > maxDistanceSquared || distanceSquared >= bestDistanceSquared)
                continue;

            bestDistanceSquared = distanceSquared;
            converterUid = uid;
            converter = converterComp;
        }

        return bestDistanceSquared < float.MaxValue;
    }

    private void OnPointsChanged(EntityUid uid, DiskConsoleComponent component, ref ResearchServerPointsChangedEvent args)
    {
        TryStartAutoPrint(uid, component);
        UpdateUserInterface(uid, component);
    }

    private void OnRegistrationChanged(EntityUid uid, DiskConsoleComponent component, ref ResearchRegistrationChangedEvent args)
    {
        TryStartAutoPrint(uid, component);
        UpdateUserInterface(uid, component);
    }

    private void OnPowerChanged(EntityUid uid, DiskConsoleComponent component, ref PowerChangedEvent args)
    {
        if (args.Powered)
            TryStartAutoPrint(uid, component);

        UpdateUserInterface(uid, component);
    }

    private void OnBeforeUiOpen(EntityUid uid, DiskConsoleComponent component, BeforeActivatableUIOpenEvent args)
    {
        UpdateUserInterface(uid, component);
    }

    public void UpdateUserInterface(EntityUid uid, DiskConsoleComponent? component = null)
    {
        if (!Resolve(uid, ref component, false))
            return;

        var totalPoints = 0;
        if (_research.TryGetClientServer(uid, out _, out var server))
        {
            totalPoints = server.Points;
        }

        var powered = this.IsPowered(uid, EntityManager);
        var canPrint = !(TryComp<DiskConsolePrintingComponent>(uid, out var printing) && printing.FinishTime >= _timing.CurTime) &&
                       powered &&
                       totalPoints >= component.PricePerDisk;

        var state = new DiskConsoleBoundUserInterfaceState(
            totalPoints,
            component.PricePerDisk,
            canPrint,
            component.AutoPrint,
            component.AutoFeedAdjacentConverter);
        _ui.SetUiState(uid, DiskConsoleUiKey.Key, state);
    }

    private void OnShutdown(EntityUid uid, DiskConsolePrintingComponent component, ComponentShutdown args)
    {
        UpdateUserInterface(uid);
    }
}
