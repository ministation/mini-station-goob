using Content.Shared.Actions;
using Content.Shared.Body.Organ;
using Content.Shared.Body.Part;
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
using Robust.Shared.Prototypes;

namespace Content.Shared._Trauma.Genetics.YamlCompat;

[RegisterComponent]
public sealed partial class DnaServerComponent : Component;

[RegisterComponent]
public sealed partial class ExtraReachComponent : Component
{
    [DataField]
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

public sealed partial class RelayOrganEffectSystem : EntityEffectSystem<MetaDataComponent, RelayOrgan>
{
    [Dependency] private SharedBodySystem _body = default!;
    [Dependency] private SharedEntityEffectsSystem _effects = default!;

    protected override void Effect(Entity<MetaDataComponent> ent, ref EntityEffectEvent<RelayOrgan> args)
    {
        var category = args.Effect.Category;
        foreach (var (organ, organComp) in _body.GetBodyOrgans(ent))
        {
            if (!organComp.SlotId.Equals(category, StringComparison.OrdinalIgnoreCase) &&
                Prototype(organ)?.ID.Contains(category, StringComparison.OrdinalIgnoreCase) != true)
                continue;

            _effects.ApplyEffects(organ, args.Effect.Effects, user: args.User);
        }
    }
}

public sealed partial class RelayRandomPart : EntityEffectBase<RelayRandomPart>
{
    [DataField]
    public EntityEffect Effect = default!;

    [DataField]
    public EntityEffect? FailEffect;

    public override string? EntityEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
        => Effect.EntityEffectGuidebookText(prototype, entSys);
}

public sealed partial class RelayRandomPartEffectSystem : EntityEffectSystem<MetaDataComponent, RelayRandomPart>
{
    protected override void Effect(Entity<MetaDataComponent> ent, ref EntityEffectEvent<RelayRandomPart> args)
    {
    }
}

public sealed partial class MoveOrgan : EntityEffectBase<MoveOrgan>
{
    [DataField]
    public string Organ = string.Empty;

    [DataField]
    public string Dest = string.Empty;
}

public sealed partial class MoveOrganEffectSystem : EntityEffectSystem<MetaDataComponent, MoveOrgan>
{
    protected override void Effect(Entity<MetaDataComponent> ent, ref EntityEffectEvent<MoveOrgan> args)
    {
    }
}

public sealed partial class AddOrganSlot : EntityEffectBase<AddOrganSlot>
{
    [DataField]
    public string Category = string.Empty;
}

public sealed partial class AddOrganSlotEffectSystem : EntityEffectSystem<MetaDataComponent, AddOrganSlot>
{
    protected override void Effect(Entity<MetaDataComponent> ent, ref EntityEffectEvent<AddOrganSlot> args)
    {
    }
}

public sealed partial class RemoveOrganSlot : EntityEffectBase<RemoveOrganSlot>
{
    [DataField]
    public string Slot = string.Empty;
}

public sealed partial class RemoveOrganSlotEffectSystem : EntityEffectSystem<MetaDataComponent, RemoveOrganSlot>
{
    protected override void Effect(Entity<MetaDataComponent> ent, ref EntityEffectEvent<RemoveOrganSlot> args)
    {
    }
}

public sealed partial class DetachOrgan : EntityEffectBase<DetachOrgan>;

public sealed partial class DetachOrganEffectSystem : EntityEffectSystem<OrganComponent, DetachOrgan>
{
    [Dependency] private SharedBodySystem _body = default!;

    protected override void Effect(Entity<OrganComponent> ent, ref EntityEffectEvent<DetachOrgan> args)
    {
        _body.RemoveOrgan(ent);
    }
}

public sealed partial class RegenerateOrgan : EntityEffectBase<RegenerateOrgan>
{
    [DataField]
    public string Slot = string.Empty;

    [DataField]
    public bool Recursive = true;
}

public sealed partial class RegenerateOrganEffectSystem : EntityEffectSystem<MetaDataComponent, RegenerateOrgan>
{
    protected override void Effect(Entity<MetaDataComponent> ent, ref EntityEffectEvent<RegenerateOrgan> args)
    {
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

public sealed partial class InsertNewOrganEffectSystem : EntityEffectSystem<MetaDataComponent, InsertNewOrgan>
{
    protected override void Effect(Entity<MetaDataComponent> ent, ref EntityEffectEvent<InsertNewOrgan> args)
    {
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

public sealed partial class RelayOrgansEffectSystem : EntityEffectSystem<MetaDataComponent, RelayOrgans>
{
    protected override void Effect(Entity<MetaDataComponent> ent, ref EntityEffectEvent<RelayOrgans> args)
    {
    }
}

public sealed partial class RemoveMetabolizerType : EntityEffectBase<RemoveMetabolizerType>
{
    [DataField]
    public string Type = string.Empty;

    public override string? EntityEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
        => null;
}

public sealed partial class RemoveMetabolizerTypeEffectSystem : EntityEffectSystem<MetaDataComponent, RemoveMetabolizerType>
{
    protected override void Effect(Entity<MetaDataComponent> ent, ref EntityEffectEvent<RemoveMetabolizerType> args)
    {
    }
}

public sealed partial class AddMetabolizerType : EntityEffectBase<AddMetabolizerType>
{
    [DataField]
    public string Type = string.Empty;

    public override string? EntityEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
        => null;
}

public sealed partial class AddMetabolizerTypeEffectSystem : EntityEffectSystem<MetaDataComponent, AddMetabolizerType>
{
    protected override void Effect(Entity<MetaDataComponent> ent, ref EntityEffectEvent<AddMetabolizerType> args)
    {
    }
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
