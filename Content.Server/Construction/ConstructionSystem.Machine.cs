// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.Construction.Components;
using Content.Shared._Mini.Construction.Components;
using Content.Shared._Mini.Construction.Prototypes;
using Content.Shared.Construction.Components;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;

namespace Content.Server.Construction;

public sealed partial class ConstructionSystem
{
    private void InitializeMachines()
    {
        SubscribeLocalEvent<MachineComponent, ComponentInit>(OnMachineInit);
        SubscribeLocalEvent<MachineComponent, ComponentStartup>(OnMachineStartup);
        SubscribeLocalEvent<MachineComponent, MapInitEvent>(OnMachineMapInit);
        InitializeMachineUpgrades();
    }

    private void OnMachineInit(EntityUid uid, MachineComponent component, ComponentInit args)
    {
        component.BoardContainer = _container.EnsureContainer<Container>(uid, MachineFrameComponent.BoardContainerName);
        component.PartContainer = _container.EnsureContainer<Container>(uid, MachineFrameComponent.PartContainerName);
    }

    private void OnMachineStartup(EntityUid uid, MachineComponent component, ComponentStartup args)
    {
        if (component.BoardContainer.ContainedEntities.Count == 0)
            return;

        RefreshParts(uid, component);
    }

    private void OnMachineMapInit(EntityUid uid, MachineComponent component, MapInitEvent args)
    {
        CreateBoardAndStockParts(uid, component);
        RefreshParts(uid, component);
    }

    private void CreateBoardAndStockParts(EntityUid uid, MachineComponent component)
    {
        var boardContainer = _container.EnsureContainer<Container>(uid, MachineFrameComponent.BoardContainerName);
        var partContainer = _container.EnsureContainer<Container>(uid, MachineFrameComponent.PartContainerName);

        var spawnedBoard = boardContainer.ContainedEntities.Count == 0;
        EntityUid boardUid;

        if (spawnedBoard)
        {
            if (component.Board is not { } boardProto)
                return;

            if (!TrySpawnInContainer(boardProto, uid, MachineFrameComponent.BoardContainerName, out var spawnedBoardUid))
                throw new Exception($"Couldn't insert board with prototype {boardProto} to machine with prototype {Prototype(uid)?.ID ?? "N/A"}!");

            boardUid = spawnedBoardUid.Value;
        }
        else
        {
            boardUid = boardContainer.ContainedEntities[0];
        }

        if (!TryComp(boardUid, out MachineBoardComponent? machineBoard))
        {
            if (spawnedBoard)
                throw new Exception($"Entity with prototype {component.Board} doesn't have a {nameof(MachineBoardComponent)}!");

            return;
        }

        if (spawnedBoard)
        {
            var xform = Transform(uid);
            foreach (var (stackType, amount) in machineBoard.StackRequirements)
            {
                var stack = _stackSystem.SpawnAtPosition(amount, stackType, xform.Coordinates);
                if (!_container.Insert(stack, partContainer))
                    throw new Exception($"Couldn't insert machine material of type {stackType} to machine with prototype {Prototype(uid)?.ID ?? "N/A"}");
            }

            foreach (var (compName, info) in machineBoard.ComponentRequirements)
            {
                for (var i = 0; i < info.Amount; i++)
                {
                    if (!TrySpawnInContainer(info.DefaultPrototype, uid, MachineFrameComponent.PartContainerName, out _))
                        throw new Exception($"Couldn't insert machine component part with default prototype '{compName}' to machine with prototype {Prototype(uid)?.ID ?? "N/A"}");
                }
            }

            foreach (var (tagName, info) in machineBoard.TagRequirements)
            {
                for (var i = 0; i < info.Amount; i++)
                {
                    if (!TrySpawnInContainer(info.DefaultPrototype, uid, MachineFrameComponent.PartContainerName, out _))
                        throw new Exception($"Couldn't insert machine component part with default prototype '{tagName}' to machine with prototype {Prototype(uid)?.ID ?? "N/A"}");
                }
            }
        }

        var installedParts = new Dictionary<ProtoId<MachinePartPrototype>, int>();
        foreach (var partUid in partContainer.ContainedEntities)
        {
            if (!TryComp<MachinePartComponent>(partUid, out var installedPart))
                continue;

            installedParts[installedPart.Part] = installedParts.GetValueOrDefault(installedPart.Part) + 1;
        }

        var partRequirements = new Dictionary<ProtoId<MachinePartPrototype>, int>(machineBoard.PartRequirements);
        if (component.Board is { } boardProtoId
            && PrototypeManager.TryIndex<EntityPrototype>(boardProtoId.Id, out var boardEntityProto)
            && boardEntityProto.TryGetComponent(out MachineBoardComponent? protoBoard, EntityManager.ComponentFactory))
        {
            foreach (var (partType, amount) in protoBoard.PartRequirements)
            {
                if (!partRequirements.TryGetValue(partType, out var current) || amount > current)
                    partRequirements[partType] = amount;
            }
        }

        foreach (var (partType, amount) in partRequirements)
        {
            if (!PrototypeManager.TryIndex(partType, out var machinePart))
                throw new Exception($"Unknown machine part requirement {partType} for machine with prototype {Prototype(uid)?.ID ?? "N/A"}");

            var missing = amount - installedParts.GetValueOrDefault(partType);
            for (var i = 0; i < missing; i++)
            {
                if (!TrySpawnInContainer(machinePart.StockPartPrototype, uid, MachineFrameComponent.PartContainerName, out _))
                    throw new Exception($"Couldn't insert machine part requirement {partType} to machine with prototype {Prototype(uid)?.ID ?? "N/A"}");
            }
        }
    }
}
