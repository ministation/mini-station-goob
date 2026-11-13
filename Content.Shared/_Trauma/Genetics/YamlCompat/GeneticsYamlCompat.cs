// SPDX-FileCopyrightText: 2026 Space Station 14 Contributors
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Actions;
using Content.Shared.Body.Components;
using Content.Shared.Body.Organ;
using Content.Shared.Body.Part;
using Content.Shared.Body.Prototypes;
using Content.Shared.Body.Systems;
using Content.Shared.EntityConditions;
using Content.Shared.EntityEffects;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Standing;
using Content.Shared.Stunnable;
using Content.Shared.Timing;
using Content.Shared.Whitelist;
using Content.Shared._Trauma.Genetics.Mutations;
using Robust.Shared.Containers;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Shared._Trauma.Genetics.YamlCompat;

[RegisterComponent]
public sealed partial class DnaServerComponent : Component;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ExtraReachComponent : Component
{
    [DataField, AutoNetworkedField]
    public float Bonus;
}

[RegisterComponent]
public sealed partial class TelekinesisComponent : Component;

[RegisterComponent]
public sealed partial class SimpleAccentComponent : Component
{
    [DataField]
    public string Accent = string.Empty;
}

[RegisterComponent]
public sealed partial class BlockReadingComponent : Component;

public sealed partial class TelekinesisActionEvent : EntityTargetActionEvent;

[RegisterComponent]
public sealed partial class StatusEffectEffectsApplyComponent : Component
{
    [DataField]
    public EntityEffect[]? EffectsOnRemoval;
}

public sealed partial class StatusEffectEffectsApplySystem : EntitySystem
{
    [Dependency] private SharedEntityEffectsSystem _effects = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<StatusEffectEffectsApplyComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnShutdown(Entity<StatusEffectEffectsApplyComponent> ent, ref ComponentShutdown args)
    {
        if (ent.Comp.EffectsOnRemoval is { } effects)
            _effects.ApplyEffects(ent, effects);
    }
}

public sealed partial class DropItems : EntityEffectBase<DropItems>
{
    public override string? EntityEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
        => Loc.GetString("reagent-effect-guidebook-drop-items", ("chance", Probability));
}

public sealed partial class DropItemsEffectSystem : EntityEffectSystem<HandsComponent, DropItems>
{
    protected override void Effect(Entity<HandsComponent> ent, ref EntityEffectEvent<DropItems> args)
    {
        var ev = new DropHandItemsEvent();
        RaiseLocalEvent(ent.Owner, ref ev);
    }
}

public sealed partial class Knockdown : EntityEffectBase<Knockdown>
{
    [DataField]
    public TimeSpan Time = TimeSpan.FromSeconds(2);

    public override string? EntityEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
        => Loc.GetString("entity-effect-guidebook-modify-knockdown", ("chance", Probability), ("time", Time));
}

public sealed partial class KnockdownEffectSystem : EntityEffectSystem<StandingStateComponent, Knockdown>
{
    [Dependency] private SharedStunSystem _stun = default!;

    protected override void Effect(Entity<StandingStateComponent> ent, ref EntityEffectEvent<Knockdown> args)
    {
        _stun.TryKnockdown(ent.Owner, args.Effect.Time, drop: true);
    }
}

public sealed partial class Gib : EntityEffectBase<Gib>
{
    public override string? EntityEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
        => Loc.GetString("entity-effect-guidebook-delete-entity", ("chance", Probability));
}

public sealed partial class GibEffectSystem : EntityEffectSystem<MetaDataComponent, Gib>
{
    protected override void Effect(Entity<MetaDataComponent> ent, ref EntityEffectEvent<Gib> args)
    {
        PredictedQueueDel(ent.Owner);
    }
}

public sealed partial class RelayBodyParts : EntityEffectBase<RelayBodyParts>
{
    [DataField]
    public BodyPartType PartType;

    [DataField]
    public EntityEffect[] Effects = [];

    [DataField]
    public LocId? GuidebookText;

    public override string? EntityEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
        => GuidebookText is { } loc ? Loc.GetString(loc, ("chance", Probability)) : null;
}

public sealed partial class RelayBodyPartsEffectSystem : EntityEffectSystem<MetaDataComponent, RelayBodyParts>
{
    [Dependency] private SharedBodySystem _body = default!;
    [Dependency] private SharedEntityEffectsSystem _effects = default!;

    protected override void Effect(Entity<MetaDataComponent> ent, ref EntityEffectEvent<RelayBodyParts> args)
    {
        foreach (var (part, comp) in _body.GetBodyChildren(ent))
        {
            if (comp.PartType != args.Effect.PartType)
                continue;

            _effects.ApplyEffects(part, args.Effect.Effects, user: args.User);
        }
    }
}

public sealed partial class RelayOrgan : EntityEffectBase<RelayOrgan>
{
    [DataField]
    public string Category = string.Empty;

    [DataField]
    public EntityEffect[] Effects = [];

    [DataField]
    public LocId? GuidebookText;

    public override string? EntityEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
        => GuidebookText is { } loc ? Loc.GetString(loc, ("chance", Probability)) : null;
}

/// <summary>
/// Helpers shared by the body effect adapters below. The old body API keeps organ slots and
/// child body part slots in separate dictionaries, so lookups are unified by slot/category name.
/// </summary>
public abstract class BodyEffectSystem<T> : EntityEffectSystem<MetaDataComponent, T>
    where T : EntityEffectBase<T>
{
    [Dependency] protected readonly SharedBodySystem Body = default!;
    [Dependency] protected readonly SharedEntityEffectsSystem Effects = default!;
    [Dependency] protected readonly SharedContainerSystem Containers = default!;
    [Dependency] protected readonly IPrototypeManager Prototypes = default!;

    protected static string Normalize(string id) => id.ToLowerInvariant();

    protected bool TryGetBody(EntityUid uid, out EntityUid body, out BodyComponent bodyComp)
    {
        if (TryComp(uid, out BodyComponent? self))
        {
            body = uid;
            bodyComp = self;
            return true;
        }

        if (TryComp(uid, out BodyPartComponent? part)
            && part.Body is { } partBody
            && TryComp(partBody, out BodyComponent? partBodyComp))
        {
            body = partBody;
            bodyComp = partBodyComp;
            return true;
        }

        if (TryComp(uid, out OrganComponent? organ)
            && organ.Body is { } organBody
            && TryComp(organBody, out BodyComponent? organBodyComp))
        {
            body = organBody;
            bodyComp = organBodyComp;
            return true;
        }

        body = default;
        bodyComp = null!;
        return false;
    }

    protected bool TryGetPrototype(BodyComponent body, out BodyPrototype prototype)
    {
        prototype = null!;
        if (body.Prototype is not { } id || !Prototypes.TryIndex(id, out BodyPrototype? proto))
            return false;

        prototype = proto;
        return true;
    }

    protected bool TryGetPart(EntityUid body, string category, out EntityUid part, out BodyPartComponent partComp)
    {
        part = default;
        partComp = null!;

        if (category.Length == 0)
            return false;

        foreach (var (id, comp) in Body.GetBodyChildren(body))
        {
            if (!CategoryMatchesPart(comp, category))
                continue;

            part = id;
            partComp = comp;
            return true;
        }

        return false;
    }

    protected bool TryGetOrgan(EntityUid body, string category, out EntityUid organ, out OrganComponent organComp)
    {
        organ = default;
        organComp = null!;

        if (category.Length == 0)
            return false;

        foreach (var (id, comp) in Body.GetBodyOrgans(body))
        {
            if (!CategoryMatchesOrgan(id, comp, category))
                continue;

            organ = id;
            organComp = comp;
            return true;
        }

        return false;
    }

    protected bool CategoryMatchesOrgan(EntityUid organ, OrganComponent organComp, string category)
        => (organComp.SlotId.Length > 0 && organComp.SlotId.Equals(category, StringComparison.OrdinalIgnoreCase))
           || Prototype(organ)?.ID.Contains(category, StringComparison.OrdinalIgnoreCase) == true;

    protected static bool CategoryMatchesPart(BodyPartComponent part, string category)
        => (part.SlotId.Length > 0 && part.SlotId.Equals(category, StringComparison.OrdinalIgnoreCase))
           || (part.ParentSlot is { } parentSlot && parentSlot.Id.Equals(category, StringComparison.OrdinalIgnoreCase))
           || part.PartType.ToString().Equals(category, StringComparison.OrdinalIgnoreCase);

    protected bool HasOrganInSlot(EntityUid part, string slot)
        => Containers.TryGetContainer(part, SharedBodySystem.GetOrganContainerId(slot), out var container)
           && container.ContainedEntities.Count > 0;

    protected bool HasPartInSlot(EntityUid part, string slot)
        => Containers.TryGetContainer(part, SharedBodySystem.GetPartSlotContainerId(slot), out var container)
           && container.ContainedEntities.Count > 0;

    protected string? GetPrototypePartSlot(EntityUid body, EntityUid part, BodyPartComponent partComp, BodyPrototype prototype)
    {
        if (Body.IsPartRoot(body, part))
            return prototype.Root;

        var slotId = partComp.ParentSlot?.Id;
        return slotId is not null && prototype.Slots.ContainsKey(slotId) ? slotId : null;
    }

    protected bool TryGetPartSlotType(EntProtoId partProto, out BodyPartType partType, out BodyPartSymmetry symmetry)
    {
        partType = BodyPartType.Other;
        symmetry = BodyPartSymmetry.None;

        if (!Prototypes.TryIndex<EntityPrototype>(partProto.Id, out var entityProto)
            || !entityProto.Components.TryGetComponent("BodyPart", out var component)
            || component is not BodyPartComponent bodyPart)
            return false;

        partType = bodyPart.PartType;
        symmetry = bodyPart.Symmetry;
        return true;
    }

    /// <summary>
    /// Spawns an organ into an already existing free slot on the part.
    /// </summary>
    protected EntityUid? SpawnOrgan(EntityUid part, BodyPartComponent partComp, string slot, string protoId)
    {
        if (!Prototypes.HasIndex<EntityPrototype>(protoId))
            return null;

        var organ = SpawnAtPosition(protoId, Transform(part).Coordinates);
        if (!TryComp(organ, out OrganComponent? organComp))
        {
            QueueDel(organ);
            return null;
        }

        // the old body API only registers an inserted organ if its SlotId matches the slot container
        organComp.SlotId = slot;
        if (!Body.InsertOrgan(part, organ, slot, partComp, organComp))
        {
            QueueDel(organ);
            return null;
        }

        return organ;
    }

    /// <summary>
    /// Spawns the body part defined by a body prototype slot and attaches it to <paramref name="parent"/>.
    /// </summary>
    protected void SpawnBodyPart(
        EntityUid parent,
        BodyPartComponent parentComp,
        string slotId,
        BodyPrototypeSlot slot,
        BodyPrototype prototype,
        bool recursive)
    {
        if (slot.Part is not { } partProto)
            return;

        var child = SpawnAtPosition(partProto.Id, Transform(parent).Coordinates);
        if (!TryComp(child, out BodyPartComponent? childComp)
            || !Body.TryCreatePartSlot(parent, slotId, childComp.PartType, childComp.Symmetry, out _, parentComp)
            || !Body.AttachPart(parent, slotId, child, parentComp, childComp))
        {
            QueueDel(child);
            return;
        }

        foreach (var (organSlot, organProto) in slot.Organs)
        {
            Body.TryCreateOrganSlot(child, organSlot, out _, childComp);
            if (recursive)
                SpawnOrgan(child, childComp, organSlot, organProto);
        }

        if (!recursive)
            return;

        foreach (var connection in slot.Connections)
        {
            if (prototype.Slots.TryGetValue(connection, out var connectionSlot))
                SpawnBodyPart(child, childComp, connection, connectionSlot, prototype, true);
        }
    }

    /// <summary>
    /// Finds the slot an organ prototype belongs to. The old OrganComponent has a SlotId but many
    /// organ prototypes (ears, tails) leave it empty, so fall back to their prototype id.
    /// </summary>
    protected string? GetOrganSlot(BodyPartComponent part, string protoId, EntityPrototype entityProto)
    {
        if (entityProto.Components.TryGetComponent("Organ", out var component)
            && component is OrganComponent protoOrgan
            && protoOrgan.SlotId.Length > 0)
            return Normalize(protoOrgan.SlotId);

        foreach (var existing in part.Organs.Keys)
        {
            if (protoId.Contains(existing, StringComparison.OrdinalIgnoreCase))
                return existing;
        }

        return GuessSlotFromPrototype(protoId);
    }

    // shortcut: heuristic for protos without a slotId (OrganFelinidEars -> "ears"), add a datafield if upstream adds categories
    protected static string? GuessSlotFromPrototype(string protoId)
    {
        var start = -1;
        for (var i = 0; i < protoId.Length; i++)
        {
            if (char.IsUpper(protoId[i]))
                start = i;
        }

        return start > 0 ? protoId[start..].ToLowerInvariant() : null;
    }
}

public sealed partial class RelayOrganEffectSystem : BodyEffectSystem<RelayOrgan>
{
    protected override void Effect(Entity<MetaDataComponent> ent, ref EntityEffectEvent<RelayOrgan> args)
    {
        if (!TryGetBody(ent.Owner, out var body, out _))
            return;

        var category = args.Effect.Category;
        foreach (var (organ, organComp) in Body.GetBodyOrgans(body))
        {
            if (CategoryMatchesOrgan(organ, organComp, category))
                Effects.ApplyEffects(organ, args.Effect.Effects, args.Scale, args.User);
        }

        // felinid.yml targets body parts (Head, Chest) as well as organs
        foreach (var (part, partComp) in Body.GetBodyChildren(body))
        {
            if (CategoryMatchesPart(partComp, category))
                Effects.ApplyEffects(part, args.Effect.Effects, args.Scale, args.User);
        }
    }
}

public sealed partial class RelayRandomPart : EntityEffectBase<RelayRandomPart>
{
    [DataField]
    public List<BodyPartType> Types = new();

    [DataField]
    public EntityEffect Effect = default!;

    [DataField]
    public EntityEffect? FailEffect;

    public override string? EntityEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
        => Effect.EntityEffectGuidebookText(prototype, entSys);
}

public sealed partial class RelayRandomPartEffectSystem : EntityEffectSystem<MetaDataComponent, RelayRandomPart>
{
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SharedBodySystem _body = default!;
    [Dependency] private SharedEntityEffectsSystem _effects = default!;

    protected override void Effect(Entity<MetaDataComponent> ent, ref EntityEffectEvent<RelayRandomPart> args)
    {
        var partFilter = args.Effect.Types;
        var candidates = new List<EntityUid>();
        foreach (var (part, partComp) in _body.GetBodyChildren(ent))
        {
            if (partFilter.Count > 0 && !partFilter.Contains(partComp.PartType))
                continue;

            candidates.Add(part);
        }

        if (candidates.Count == 0)
        {
            if (args.Effect.FailEffect is { } fail)
                _effects.ApplyEffects(ent, [fail], user: args.User);
            return;
        }

        _effects.ApplyEffects(_random.Pick(candidates), [args.Effect.Effect], user: args.User);
    }
}

public sealed partial class MoveOrgan : EntityEffectBase<MoveOrgan>
{
    [DataField]
    public string Organ = string.Empty;

    [DataField]
    public string Dest = string.Empty;
}

public sealed partial class MoveOrganEffectSystem : BodyEffectSystem<MoveOrgan>
{
    protected override void Effect(Entity<MetaDataComponent> ent, ref EntityEffectEvent<MoveOrgan> args)
    {
        if (!TryGetBody(ent.Owner, out var body, out _)
            || !TryGetOrgan(body, args.Effect.Organ, out var organ, out var organComp)
            || !TryGetPart(body, args.Effect.Dest, out var dest, out var destComp))
            return;

        // already moved, e.g. the mutation was applied twice
        if (Body.GetParentPartOrNull(organ) == dest)
            return;

        var slot = organComp.SlotId.Length > 0 ? organComp.SlotId : Normalize(args.Effect.Organ);
        if (!Body.TryCreateOrganSlot(dest, slot, out _, destComp) || HasOrganInSlot(dest, slot))
            return;

        if (!Body.TryRemoveOrgan(organ, organComp))
            return;

        organComp.SlotId = slot;
        if (!Body.InsertOrgan(dest, organ, slot, destComp, organComp))
            Log.Error($"Failed to move organ {ToPrettyString(organ)} into slot {slot} of {ToPrettyString(dest)}");
    }
}

public sealed partial class AddOrganSlot : EntityEffectBase<AddOrganSlot>
{
    [DataField]
    public string Category = string.Empty;
}

public sealed partial class AddOrganSlotEffectSystem : BodyEffectSystem<AddOrganSlot>
{
    protected override void Effect(Entity<MetaDataComponent> ent, ref EntityEffectEvent<AddOrganSlot> args)
    {
        if (!TryComp(ent.Owner, out BodyPartComponent? part))
            return;

        var slot = Normalize(args.Effect.Category);
        if (slot.Length == 0)
            return;

        // slots that exist in the body prototype (Head, Groin, ...) are body part slots, the rest are organ slots
        if (TryGetBody(ent.Owner, out var body, out var bodyComp)
            && TryGetPrototype(bodyComp, out var prototype)
            && prototype.Slots.TryGetValue(slot, out var protoSlot)
            && protoSlot.Part is { } partProto
            && TryGetPartSlotType(partProto, out var partType, out var symmetry))
        {
            Body.TryCreatePartSlot(ent.Owner, slot, partType, symmetry, out _, part);
            return;
        }

        Body.TryCreateOrganSlot(ent.Owner, slot, out _, part);
    }
}

public sealed partial class RemoveOrganSlot : EntityEffectBase<RemoveOrganSlot>
{
    [DataField]
    public string Slot = string.Empty;
}

public sealed partial class RemoveOrganSlotEffectSystem : BodyEffectSystem<RemoveOrganSlot>
{
    protected override void Effect(Entity<MetaDataComponent> ent, ref EntityEffectEvent<RemoveOrganSlot> args)
    {
        if (!TryComp(ent.Owner, out BodyPartComponent? part))
            return;

        var slot = Normalize(args.Effect.Slot);
        if (slot.Length == 0)
            return;

        if (part.Organs.ContainsKey(slot))
        {
            // move the organ out first, mutations do that before removing their temporary slots
            if (HasOrganInSlot(ent.Owner, slot))
                return;

            part.Organs.Remove(slot);
            Dirty(ent.Owner, part);
            return;
        }

        if (part.Children.ContainsKey(slot) && !HasPartInSlot(ent.Owner, slot))
        {
            part.Children.Remove(slot);
            Dirty(ent.Owner, part);
        }
    }
}

public sealed partial class DetachOrgan : EntityEffectBase<DetachOrgan>;

public sealed partial class DetachOrganEffectSystem : EntityEffectSystem<MetaDataComponent, DetachOrgan>
{
    [Dependency] private SharedBodySystem _body = default!;

    protected override void Effect(Entity<MetaDataComponent> ent, ref EntityEffectEvent<DetachOrgan> args)
    {
        if (HasComp<BodyPartComponent>(ent))
        {
            _body.TryDetachPart(ent);
            return;
        }

        if (TryComp<OrganComponent>(ent, out var organ))
            _body.RemoveOrgan(ent, organ);
    }
}

public sealed partial class RegenerateOrgan : EntityEffectBase<RegenerateOrgan>
{
    [DataField]
    public string Slot = string.Empty;

    [DataField]
    public bool Recursive = true;
}

public sealed partial class RegenerateOrganEffectSystem : BodyEffectSystem<RegenerateOrgan>
{
    protected override void Effect(Entity<MetaDataComponent> ent, ref EntityEffectEvent<RegenerateOrgan> args)
    {
        if (!TryComp(ent.Owner, out BodyPartComponent? part)
            || !TryGetBody(ent.Owner, out var body, out var bodyComp)
            || !TryGetPrototype(bodyComp, out var prototype))
            return;

        var partSlot = GetPrototypePartSlot(body, ent.Owner, part, prototype);
        if (partSlot is null || !prototype.Slots.TryGetValue(partSlot, out var protoSlot))
            return;

        var slot = Normalize(args.Effect.Slot);
        foreach (var connection in protoSlot.Connections)
        {
            if (!connection.Equals(slot, StringComparison.OrdinalIgnoreCase))
                continue;

            if (!HasPartInSlot(ent.Owner, connection) && prototype.Slots.TryGetValue(connection, out var connectionSlot))
                SpawnBodyPart(ent.Owner, part, connection, connectionSlot, prototype, args.Effect.Recursive);
            return;
        }

        foreach (var (organSlot, organProto) in protoSlot.Organs)
        {
            if (!organSlot.Equals(slot, StringComparison.OrdinalIgnoreCase))
                continue;

            if (!Body.TryCreateOrganSlot(ent.Owner, organSlot, out _, part) || HasOrganInSlot(ent.Owner, organSlot))
                return;

            SpawnOrgan(ent.Owner, part, organSlot, organProto);
            return;
        }
    }
}

public sealed partial class DnaUnstableCondition : EntityConditionBase<DnaUnstableCondition>
{
    public override string EntityConditionGuidebookText(IPrototypeManager prototype)
        => string.Empty;
}

public sealed partial class DnaUnstableConditionSystem : EntityConditionSystem<MutatableComponent, DnaUnstableCondition>
{
    protected override void Condition(Entity<MutatableComponent> ent, ref EntityConditionEvent<DnaUnstableCondition> args)
    {
        args.Result = ent.Comp.TotalInstability >= ent.Comp.MaxInstability;
    }
}

public sealed partial class HoldingItemCondition : EntityConditionBase<HoldingItemCondition>
{
    public override string EntityConditionGuidebookText(IPrototypeManager prototype)
        => string.Empty;
}

public sealed partial class HoldingItemConditionSystem : EntityConditionSystem<HandsComponent, HoldingItemCondition>
{
    [Dependency] private SharedHandsSystem _hands = default!;

    protected override void Condition(Entity<HandsComponent> ent, ref EntityConditionEvent<HoldingItemCondition> args)
    {
        foreach (var _ in _hands.EnumerateHeld(ent.AsNullable()))
        {
            args.Result = true;
            return;
        }
    }
}

public sealed partial class InContainerCondition : EntityConditionBase<InContainerCondition>
{
    public override string EntityConditionGuidebookText(IPrototypeManager prototype)
        => string.Empty;
}

public sealed partial class InContainerConditionSystem : EntityConditionSystem<TransformComponent, InContainerCondition>
{
    [Dependency] private SharedContainerSystem _container = default!;

    protected override void Condition(Entity<TransformComponent> ent, ref EntityConditionEvent<InContainerCondition> args)
    {
        args.Result = _container.IsEntityInContainer(ent);
    }
}

public sealed partial class UseDelayCondition : EntityConditionBase<UseDelayCondition>
{
    [DataField]
    public string DelayId = UseDelaySystem.DefaultId;

    public override string EntityConditionGuidebookText(IPrototypeManager prototype)
        => string.Empty;
}

public sealed partial class UseDelayConditionSystem : EntityConditionSystem<UseDelayComponent, UseDelayCondition>
{
    [Dependency] private UseDelaySystem _useDelay = default!;

    protected override void Condition(Entity<UseDelayComponent> ent, ref EntityConditionEvent<UseDelayCondition> args)
    {
        args.Result = !_useDelay.IsDelayed((ent, ent.Comp), args.Condition.DelayId);
    }
}

public sealed partial class StandingCondition : EntityConditionBase<StandingCondition>
{
    public override string EntityConditionGuidebookText(IPrototypeManager prototype)
        => string.Empty;
}

public sealed partial class StandingConditionSystem : EntityConditionSystem<StandingStateComponent, StandingCondition>
{
    protected override void Condition(Entity<StandingStateComponent> ent, ref EntityConditionEvent<StandingCondition> args)
    {
        args.Result = ent.Comp.Standing;
    }
}

public sealed partial class HasOrganSlot : EntityConditionBase<HasOrganSlot>
{
    [DataField]
    public string Organ = string.Empty;

    [DataField]
    public string Slot = string.Empty;

    [DataField]
    public BodyPartType? PartType;

    public override string EntityConditionGuidebookText(IPrototypeManager prototype)
        => string.Empty;
}

public sealed partial class HasOrganSlotConditionSystem : EntityConditionSystem<MetaDataComponent, HasOrganSlot>
{
    [Dependency] private SharedBodySystem _body = default!;

    protected override void Condition(Entity<MetaDataComponent> ent, ref EntityConditionEvent<HasOrganSlot> args)
    {
        var slot = args.Condition.Slot;
        var organName = args.Condition.Organ;
        foreach (var (organ, organComp) in _body.GetBodyOrgans(ent))
        {
            if (organName != string.Empty &&
                !organComp.SlotId.Equals(organName, StringComparison.OrdinalIgnoreCase) &&
                Prototype(organ)?.ID.Contains(organName, StringComparison.OrdinalIgnoreCase) != true)
                continue;

            if (slot != string.Empty &&
                !organComp.SlotId.Equals(slot, StringComparison.OrdinalIgnoreCase))
                continue;

            // HasOrganSlot conditions like "brain in the head" also filter by the part type
            if (args.Condition.PartType is { } partType
                && (_body.GetParentPartOrNull(organ) is not { } parentPart
                    || !TryComp(parentPart, out BodyPartComponent? parentPartComp)
                    || parentPartComp.PartType != partType))
                continue;

            args.Result = true;
            return;
        }
    }
}

public sealed partial class WhitelistCondition : EntityConditionBase<WhitelistCondition>
{
    [DataField]
    public EntityWhitelist? Whitelist;

    [DataField]
    public EntityWhitelist? Blacklist;

    public override string EntityConditionGuidebookText(IPrototypeManager prototype)
        => string.Empty;
}

public sealed partial class WhitelistConditionSystem : EntityConditionSystem<MetaDataComponent, WhitelistCondition>
{
    [Dependency] private EntityWhitelistSystem _whitelist = default!;

    protected override void Condition(Entity<MetaDataComponent> ent, ref EntityConditionEvent<WhitelistCondition> args)
    {
        args.Result = _whitelist.CheckBoth(ent, blacklist: args.Condition.Blacklist, whitelist: args.Condition.Whitelist);
    }
}

public sealed partial class InsertNewOrgan : EntityEffectBase<InsertNewOrgan>
{
    [DataField]
    public string Organ = string.Empty;

    public override string? EntityEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
        => null;
}

public sealed partial class InsertNewOrganEffectSystem : BodyEffectSystem<InsertNewOrgan>
{
    protected override void Effect(Entity<MetaDataComponent> ent, ref EntityEffectEvent<InsertNewOrgan> args)
    {
        if (!TryComp(ent.Owner, out BodyPartComponent? part))
            return;

        var organProto = args.Effect.Organ;
        if (!Prototypes.TryIndex<EntityPrototype>(organProto, out var entityProto))
            return;

        var slot = GetOrganSlot(part, organProto, entityProto);
        if (slot is null
            || !Body.TryCreateOrganSlot(ent.Owner, slot, out _, part)
            || HasOrganInSlot(ent.Owner, slot))
            return;

        SpawnOrgan(ent.Owner, part, slot, organProto);
    }
}

public sealed partial class RelayOrgans : EntityEffectBase<RelayOrgans>
{
    [DataField]
    public EntityWhitelist? Whitelist;

    [DataField]
    public EntityEffect[] Effects = [];

    public override string? EntityEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
        => null;
}

public sealed partial class RelayOrgansEffectSystem : BodyEffectSystem<RelayOrgans>
{
    [Dependency] private EntityWhitelistSystem _whitelist = default!;

    protected override void Effect(Entity<MetaDataComponent> ent, ref EntityEffectEvent<RelayOrgans> args)
    {
        if (!TryGetBody(ent.Owner, out var body, out _))
            return;

        foreach (var (organ, _) in Body.GetBodyOrgans(body))
        {
            if (!_whitelist.CheckBoth(organ, whitelist: args.Effect.Whitelist))
                continue;

            Effects.ApplyEffects(organ, args.Effect.Effects, args.Scale, args.User);
        }
    }
}

public sealed partial class RemoveMetabolizerType : EntityEffectBase<RemoveMetabolizerType>
{
    [DataField]
    public string Type = string.Empty;

    public override string? EntityEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
        => null;
}

public sealed partial class AddMetabolizerType : EntityEffectBase<AddMetabolizerType>
{
    [DataField]
    public string Type = string.Empty;

    public override string? EntityEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
        => null;
}

public sealed partial class NoEffect : EntityEffectBase<NoEffect>
{
    public override string? EntityEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
        => null;
}

public sealed partial class NoEffectSystem : EntityEffectSystem<MetaDataComponent, NoEffect>
{
    protected override void Effect(Entity<MetaDataComponent> ent, ref EntityEffectEvent<NoEffect> args)
    {
    }
}

[RegisterComponent]
public sealed partial class FragileComponent : Component;
