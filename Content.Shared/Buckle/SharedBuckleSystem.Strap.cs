// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using Content.Shared.Buckle.Components;
using Content.Shared.Construction;
using Content.Shared.Destructible;
using Content.Shared.Foldable;
using Content.Shared.Storage;
using Robust.Shared.Containers;
using Robust.Shared.Map.Events;

namespace Content.Shared.Buckle;

public abstract partial class SharedBuckleSystem
{
    private void InitializeStrap()
    {
        SubscribeLocalEvent<StrapComponent, ComponentStartup>(OnStrapStartup);
        SubscribeLocalEvent<StrapComponent, ComponentShutdown>(OnStrapShutdown);
        SubscribeLocalEvent<StrapComponent, EntityTerminatingEvent>(OnStrapTerminating);
        SubscribeLocalEvent<StrapComponent, ComponentRemove>((e, c, _) => StrapRemoveAll(e, c));

        SubscribeLocalEvent<StrapComponent, ContainerGettingInsertedAttemptEvent>(OnStrapContainerGettingInsertedAttempt);
        SubscribeLocalEvent<StrapComponent, DestructionEventArgs>((e, c, _) => StrapRemoveAll(e, c));
        SubscribeLocalEvent<StrapComponent, BreakageEventArgs>((e, c, _) => StrapRemoveAll(e, c));

        SubscribeLocalEvent<StrapComponent, FoldAttemptEvent>(OnAttemptFold);
        SubscribeLocalEvent<StrapComponent, MachineDeconstructedEvent>((e, c, _) => StrapRemoveAll(e, c));

        SubscribeLocalEvent<BeforeSerializationEvent>(OnBeforeSerialization);
    }

    private void OnStrapStartup(EntityUid uid, StrapComponent component, ComponentStartup args)
    {
        Appearance.SetData(uid, StrapVisuals.State, component.BuckledEntities.Count != 0);
    }

    private void OnStrapShutdown(EntityUid uid, StrapComponent component, ComponentShutdown args)
    {
        if (!TerminatingOrDeleted(uid))
            StrapRemoveAll(uid, component);
    }

    private void OnStrapTerminating(Entity<StrapComponent> entity, ref EntityTerminatingEvent args)
    {
        StrapRemoveAll(entity, entity.Comp);
    }

    private void OnStrapContainerGettingInsertedAttempt(EntityUid uid, StrapComponent component, ContainerGettingInsertedAttemptEvent args)
    {
        // If someone is attempting to put this item inside of a backpack, ensure that it has no entities strapped to it.
        if (args.Container.ID == StorageComponent.ContainerId && component.BuckledEntities.Count != 0)
            args.Cancel();
    }

    private void OnAttemptFold(EntityUid uid, StrapComponent component, ref FoldAttemptEvent args)
    {
        if (args.Cancelled)
            return;

        args.Cancelled = component.BuckledEntities.Count != 0;
    }

    /// <summary>
    /// Remove everything attached to the strap
    /// </summary>
    private void StrapRemoveAll(EntityUid uid, StrapComponent strapComp)
    {
        foreach (var entity in strapComp.BuckledEntities.ToArray())
        {
            Unbuckle(entity, entity);
        }
    }

    /// <summary>
    /// Unbuckles entities that will not be part of the save before the strap references are serialized.
    /// Non-savable entities (player mobs use "save: false") and dangling references would otherwise be
    /// written as invalid and make entity_serializer log "Encountered a reference to a missing entity".
    /// FollowerSystem drops non-savable followers on save the same way.
    /// </summary>
    private void OnBeforeSerialization(BeforeSerializationEvent ev)
    {
        var query = AllEntityQuery<StrapComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var strap, out _))
        {
            if (strap.BuckledEntities.Count == 0 || !IsSaved(uid, ev.Entities))
                continue;

            foreach (var buckled in strap.BuckledEntities.ToArray())
            {
                if (IsSaved(buckled, ev.Entities) && TryComp(buckled, out MetaDataComponent? meta) && meta.EntityPrototype?.MapSavable != false)
                    continue;

                if (!TerminatingOrDeleted(buckled))
                    Unbuckle(buckled, null);

                if (strap.BuckledEntities.Remove(buckled))
                    Dirty(uid, strap);
            }
        }
    }

    /// <summary>
    /// Whether the entity is one of the roots of the save or one of their transform children.
    /// </summary>
    private bool IsSaved(EntityUid uid, HashSet<EntityUid> roots)
    {
        while (uid.IsValid())
        {
            if (roots.Contains(uid))
                return true;

            if (!TryComp(uid, out TransformComponent? xform))
                return false;

            uid = xform.ParentUid;
        }

        return false;
    }

    private bool StrapHasSpace(EntityUid strapUid, BuckleComponent buckleComp, StrapComponent? strapComp = null)
    {
        if (!Resolve(strapUid, ref strapComp, false))
            return false;

        var avail = strapComp.Size;
        foreach (var buckle in strapComp.BuckledEntities)
        {
            avail -= CompOrNull<BuckleComponent>(buckle)?.Size ?? 0;
        }

        return avail >= buckleComp.Size;
    }

    /// <summary>
    /// Sets the enabled field in the strap component to a value
    /// </summary>
    public void StrapSetEnabled(EntityUid strapUid, bool enabled, StrapComponent? strapComp = null)
    {
        if (!Resolve(strapUid, ref strapComp, false) ||
            strapComp.Enabled == enabled)
            return;

        strapComp.Enabled = enabled;
        Dirty(strapUid, strapComp);

        if (!enabled)
            StrapRemoveAll(strapUid, strapComp);
    }
}